namespace LocalTechAgent.Tools;

public enum NotionBlockType
{
    Paragraph,
    Code,
    Image
}

public sealed record NotionContentBlock(NotionBlockType Type, string Content);