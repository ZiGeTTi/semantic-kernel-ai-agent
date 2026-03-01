namespace LocalTechAgent.Configuration;

public sealed class NotionOptions
{
    public string? ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://api.notion.com/v1";
    public string Version { get; init; } = "2022-06-28";
    public string? DatabaseId { get; init; }
    public string TitlePropertyName { get; init; } = "Name";
    public string BodyPropertyName { get; init; } = "Body";
    public string ExtrasPropertyName { get; init; } = "Summary";
}
