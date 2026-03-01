using LocalTechAgent.Configuration;
using LocalTechAgent.Services;

namespace LocalTechAgent.Tools;

public sealed class NotionTool
{
    private readonly NotionService _notionService;
    private readonly NotionOptions _options;

    public NotionTool(NotionService notionService, NotionOptions options)
    {
        _notionService = notionService;
        _options = options;
    }

    public Task<string> PushToNotionAsync(
        string title,
        string summary,
        string bodyText,
        IReadOnlyList<NotionContentBlock> contentBlocks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DatabaseId))
        {
            throw new InvalidOperationException(
                "Missing Notion DatabaseId. Set Notion:DatabaseId in appsettings or NOTION_DATABASE_ID env var."
            );
        }

        var blocks = BuildContentBlocks(contentBlocks);

        var titlePropertyName = string.IsNullOrWhiteSpace(_options.TitlePropertyName)
            ? "Title"
            : _options.TitlePropertyName;
        var bodyPropertyName = string.IsNullOrWhiteSpace(_options.BodyPropertyName)
            ? "Body"
            : _options.BodyPropertyName;
        var extrasPropertyName = string.IsNullOrWhiteSpace(_options.ExtrasPropertyName)
            ? "Extras"
            : _options.ExtrasPropertyName;

        var extrasText = BuildExtrasText(contentBlocks);

        var properties = new Dictionary<string, object>
        {
            {
                titlePropertyName,
                new
                {
                    title = new[]
                    {
                        new
                        {
                            text = new { content = title }
                        }
                    }
                }
            },
            {
                bodyPropertyName,
                new
                {
                    rich_text = BuildRichTextChunks(bodyText)
                }
            },
            {
                extrasPropertyName,
                new
                {
                    rich_text = BuildRichTextChunks(extrasText)
                }
            }
        };

        var payload = new
        {
            parent = new { database_id = _options.DatabaseId },
            properties = properties,
            children = blocks
        };

        return _notionService.CreatePageAsync(payload, cancellationToken);
    }

    private static string BuildExtrasText(IReadOnlyList<NotionContentBlock> contentBlocks)
    {
        var imageCount = contentBlocks.Count(block => block.Type == NotionBlockType.Image);
        var codeCount = contentBlocks.Count(block => block.Type == NotionBlockType.Code);

        return imageCount == 0 && codeCount == 0
            ? string.Empty
            : $"Images: {imageCount}, Code blocks: {codeCount}";
    }

    private static List<object> BuildContentBlocks(IReadOnlyList<NotionContentBlock> contentBlocks)
    {
        var blocks = new List<object>();

        foreach (var block in contentBlocks)
        {
            switch (block.Type)
            {
                case NotionBlockType.Paragraph:
                    foreach (var chunk in SplitToChunks(block.Content, 2000))
                    {
                        blocks.Add(new
                        {
                            @object = "block",
                            type = "paragraph",
                            paragraph = new
                            {
                                rich_text = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = new { content = chunk }
                                    }
                                }
                            }
                        });
                    }
                    break;
                case NotionBlockType.Code:
                    foreach (var chunk in SplitToChunks(block.Content, 2000))
                    {
                        blocks.Add(new
                        {
                            @object = "block",
                            type = "code",
                            code = new
                            {
                                language = "plain text",
                                rich_text = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = new { content = chunk }
                                    }
                                }
                            }
                        });
                    }
                    break;
                case NotionBlockType.Image:
                    if (!string.IsNullOrWhiteSpace(block.Content))
                    {
                        blocks.Add(new
                        {
                            @object = "block",
                            type = "image",
                            image = new
                            {
                                type = "external",
                                external = new { url = block.Content }
                            }
                        });
                    }
                    break;
            }
        }

        return blocks;
    }

    private static object[] BuildRichTextChunks(string text)
    {
        return SplitToChunks(text, 2000)
            .Select(chunk => new { text = new { content = chunk } })
            .Cast<object>()
            .ToArray();
    }

    private static List<string> SplitToChunks(string text, int maxLength)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return chunks;
        }

        var index = 0;
        while (index < text.Length)
        {
            var length = Math.Min(maxLength, text.Length - index);
            chunks.Add(text.Substring(index, length));
            index += length;
        }

        return chunks;
    }
}
