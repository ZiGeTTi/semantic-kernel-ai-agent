using LocalTechAgent.Tools;
using Microsoft.SemanticKernel;

namespace LocalTechAgent.Agents;

public sealed class NotionAgent
{
    private readonly Kernel _kernel;
    private readonly NotionTool _notionTool;

    public NotionAgent(Kernel kernel, NotionTool notionTool)
    {
        _kernel = kernel;
        _notionTool = notionTool;
    }

    public async Task<string> ProcessArticleAsync(
        string title,
        string bodyText,
        IReadOnlyList<NotionContentBlock> contentBlocks)
    {
        var result = await _kernel.InvokePromptAsync($"""
You are a senior software architect assistant.
Summarize the following article clearly for Notion:

Title: {title}

Content:
{bodyText}
""");

        var summary = result.GetValue<string>() ?? string.Empty;

    return await _notionTool.PushToNotionAsync(title, summary, bodyText, contentBlocks);
    }
}
