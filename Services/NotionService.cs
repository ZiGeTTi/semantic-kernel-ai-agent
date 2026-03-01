using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LocalTechAgent.Configuration;

namespace LocalTechAgent.Services;

public sealed class NotionService
{
    private readonly HttpClient _httpClient;
    private readonly NotionOptions _options;

    public NotionService(HttpClient httpClient, NotionOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> SearchAsync(string query, int pageSize = 5, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            query,
            page_size = pageSize
        };

        var request = CreateRequest(HttpMethod.Post, "/search");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> GetPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/pages/{pageId}");
        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> CreatePageAsync(object payload, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Post, "/pages");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );
        Console.WriteLine($"Notion CreatePage Payload: {JsonSerializer.Serialize(payload)}");
        return await SendAsync(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var relative = path.StartsWith('/') ? path : $"/{path}";
        var request = new HttpRequestMessage(method, $"{baseUrl}{relative}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("Notion-Version", _options.Version);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Notion request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {body}"
            );
        }

        return body;
    }
}
