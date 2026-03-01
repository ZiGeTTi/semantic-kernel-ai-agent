using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using LocalTechAgent.Agents;
using LocalTechAgent.Configuration;
using LocalTechAgent.Services;
using LocalTechAgent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.Net;
using System.Text.RegularExpressions;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "Missing OpenAI API key. Set OpenAI:ApiKey in appsettings.json or OPENAI_API_KEY env var."
    );
}

var notionOptions = new NotionOptions
{
    ApiKey = configuration["Notion:ApiKey"] ?? configuration["NOTION_API_KEY"],
    BaseUrl = configuration["Notion:BaseUrl"] ?? "https://api.notion.com/v1",
    Version = configuration["Notion:Version"] ?? "2022-06-28",
    DatabaseId = configuration["Notion:DatabaseId"] ?? configuration["NOTION_DATABASE_ID"],
    TitlePropertyName = configuration["Notion:TitlePropertyName"] ?? "Name",
    BodyPropertyName = configuration["Notion:BodyPropertyName"] ?? "Body",
    ExtrasPropertyName = configuration["Notion:ExtrasPropertyName"] ?? "Summary"
};

var builder = Kernel.CreateBuilder();

builder.AddOpenAIChatCompletion(
    modelId: "gpt-4o-mini",  // ucuz ve yeterli
    apiKey: apiKey
);

var kernel = builder.Build();

NotionAgent? notionAgent = null;
if (!string.IsNullOrWhiteSpace(notionOptions.ApiKey))
{
    var notionHttp = new HttpClient { BaseAddress = new Uri(notionOptions.BaseUrl) };
    var notionService = new NotionService(notionHttp, notionOptions);
    var notionTool = new NotionTool(notionService, notionOptions);
    if (!string.IsNullOrWhiteSpace(notionOptions.DatabaseId))
    {
        notionAgent = new NotionAgent(kernel, notionTool);
    }
}

Console.WriteLine("Enter a URL to summarize, or press Enter to provide title and content:");
var input = Console.ReadLine()?.Trim();

string title;
string content;
var contentBlocks = new List<NotionContentBlock>();

if (!string.IsNullOrWhiteSpace(input) && IsHttpUrl(input))
{
    using var webHttp = new HttpClient();
    var html = await webHttp.GetStringAsync(input);
    var extracted = ExtractFromHtml(html, input, input);
    title = extracted.Title;
    content = extracted.Content;
    contentBlocks = extracted.Blocks;
}
else
{
    Console.Write("Title: ");
    title = Console.ReadLine() ?? string.Empty;
    Console.WriteLine("Content (finish with an empty line):");
    content = ReadMultiline();
    contentBlocks = string.IsNullOrWhiteSpace(content)
        ? new List<NotionContentBlock>()
        : new List<NotionContentBlock> { new(NotionBlockType.Paragraph, content) };
}

if (string.IsNullOrWhiteSpace(content))
{
    Console.WriteLine("No content provided. Exiting.");
    return;
}

if (notionAgent is not null)
{
    var notionResponse = await notionAgent.ProcessArticleAsync(title, content, contentBlocks);
    Console.WriteLine(notionResponse);
}
else
{
    var summary = await SummarizeAsync(kernel, title, content);
    Console.WriteLine(summary);
}

static bool IsHttpUrl(string value)
{
    return Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

static (string Title, string Content, List<NotionContentBlock> Blocks) ExtractFromHtml(
    string html,
    string fallbackTitle,
    string baseUrl)
{
    var parser = new HtmlParser();
    var document = parser.ParseDocument(html);

    var title = string.IsNullOrWhiteSpace(document.Title) ? fallbackTitle : document.Title;

    RemoveNoise(document);

    var main = document.QuerySelector("article, main")
        ?? LargestTextElement(document.Body)
        ?? document.Body;

    var content = NormalizeWhitespace(WebUtility.HtmlDecode(main?.TextContent ?? string.Empty));
    var blocks = ExtractOrderedBlocks(main, baseUrl);

    return (NormalizeWhitespace(title), content, blocks);
}

static void RemoveNoise(IDocument document)
{
    foreach (var element in document.QuerySelectorAll("script, style, noscript, svg, nav, footer, header, form, aside"))
    {
        element.Remove();
    }

    foreach (var element in document.QuerySelectorAll("*[class], *[id]"))
    {
        var key = (element.GetAttribute("class") + " " + element.GetAttribute("id")).ToLowerInvariant();
        if (Regex.IsMatch(key, "cookie|consent|banner|subscribe|newsletter|promo|advert|ads|sidebar|breadcrumbs|breadcrumb|share|social|menu"))
        {
            element.Remove();
        }
    }
}

static IElement? LargestTextElement(IElement? root)
{
    if (root is null)
    {
        return null;
    }

    return root.QuerySelectorAll("article, main, section, div")
        .Select(element => new { element, length = (element.TextContent ?? string.Empty).Length })
        .OrderByDescending(item => item.length)
        .FirstOrDefault()
        ?.element;
}

static string NormalizeWhitespace(string? text)
{
    return Regex.Replace(text ?? string.Empty, "\\s+", " ").Trim();
}

static List<NotionContentBlock> ExtractOrderedBlocks(IElement? root, string baseUrl)
{
    if (root is null)
    {
        return new List<NotionContentBlock>();
    }

    var baseUri = new Uri(baseUrl);
    var blocks = new List<NotionContentBlock>();
    var elements = root.QuerySelectorAll("p, pre, img, h1, h2, h3, h4, h5, h6, li");

    foreach (var element in elements)
    {
        switch (element.TagName)
        {
            case "IMG":
                var src = element.GetAttribute("src")
                    ?? element.GetAttribute("data-src")
                    ?? element.GetAttribute("data-original");
                var url = string.IsNullOrWhiteSpace(src) ? null : ResolveUrl(baseUri, src);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    blocks.Add(new NotionContentBlock(NotionBlockType.Image, url));
                }
                break;
            case "PRE":
                var code = NormalizeCode(element.TextContent);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    blocks.Add(new NotionContentBlock(NotionBlockType.Code, code));
                }
                break;
            default:
                var text = NormalizeWhitespace(WebUtility.HtmlDecode(element.TextContent ?? string.Empty));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(new NotionContentBlock(NotionBlockType.Paragraph, text));
                }
                break;
        }
    }

    return blocks;
}

static string? ResolveUrl(Uri baseUri, string src)
{
    if (Uri.TryCreate(src, UriKind.Absolute, out var absolute))
    {
        if (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
        {
            return absolute.ToString();
        }

        return null;
    }

    if (Uri.TryCreate(baseUri, src, out var resolved))
    {
        return resolved.ToString();
    }

    return null;
}

static string NormalizeCode(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return string.Empty;
    }

    return text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
}

static string ReadMultiline()
{
    var lines = new List<string>();
    while (true)
    {
        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            break;
        }

        lines.Add(line);
    }

    return string.Join(Environment.NewLine, lines);
}

static async Task<string> SummarizeAsync(Kernel kernel, string title, string content)
{
    var result = await kernel.InvokePromptAsync($"""
You are a senior software architect and expert.
Summarize the following article clearly:

Title: {title}

Content:
{content}
""");

    return result.GetValue<string>() ?? string.Empty;
}

