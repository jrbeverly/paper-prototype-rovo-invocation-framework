using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ConfluenceClient;

public sealed class ConfluenceTransportConfig
{
    public required string BaseUrl { get; init; }
    public required string UserEmail { get; init; }
    public required string ApiToken { get; init; }
    public required string SpaceKey { get; init; }
    public required string NamespacePageId { get; init; }

    public static ConfluenceTransportConfig FromEnvironment() => new()
    {
        BaseUrl = Required("CONFLUENCE_BASE_URL"),
        UserEmail = Required("CONFLUENCE_USER_EMAIL"),
        ApiToken = Required("CONFLUENCE_API_TOKEN"),
        SpaceKey = Required("CONFLUENCE_SPACE_KEY"),
        NamespacePageId = Required("CONFLUENCE_NAMESPACE_PAGE_ID"),
    };

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}

public sealed record PageSummary(string Id, string Title);

public sealed class ConfluenceTransportClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly ConfluenceTransportConfig _config;

    public ConfluenceTransportClient(ConfluenceTransportConfig config)
    {
        _config = config;
        _http = new HttpClient { BaseAddress = new Uri(config.BaseUrl) };
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.UserEmail}:{config.ApiToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PageSummary> CreateRequestPageAsync(
        string title, string agentName, string payload, CancellationToken ct = default)
    {
        var pageBody = BuildRequestBody(agentName, payload);
        var requestDoc = new
        {
            type = "page",
            title,
            space = new { key = _config.SpaceKey },
            ancestors = new[] { new { id = _config.NamespacePageId } },
            body = new
            {
                storage = new { value = pageBody, representation = "storage" }
            }
        };

        var createResponse = await _http.PostAsJsonAsync("/wiki/rest/api/content", requestDoc, JsonOptions, ct);
        createResponse.EnsureSuccessStatusCode();

        string pageId;
        using (var stream = await createResponse.Content.ReadAsStreamAsync(ct))
        using (var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct))
        {
            pageId = doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Confluence did not return a page id.");
        }

        var labels = new[]
        {
            new { prefix = "global", name = "rovo-request" },
            new { prefix = "global", name = $"rovo-agent-{agentName}" },
        };
        var labelResponse = await _http.PostAsJsonAsync(
            $"/wiki/rest/api/content/{pageId}/label", labels, JsonOptions, ct);
        labelResponse.EnsureSuccessStatusCode();

        return new PageSummary(pageId, title);
    }

    public async Task<IReadOnlyList<PageSummary>> GetChildPagesAsync(string pageId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/wiki/rest/api/content/{pageId}/child/page", ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.GetProperty("results")
            .EnumerateArray()
            .Select(e => new PageSummary(
                e.GetProperty("id").GetString()!,
                e.GetProperty("title").GetString()!))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetPageLabelsAsync(string pageId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/wiki/rest/api/content/{pageId}/label", ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.GetProperty("results")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToList();
    }

    public async Task<string> GetPageBodyAsync(string pageId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"/wiki/rest/api/content/{pageId}?expand=body.storage", ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement
            .GetProperty("body")
            .GetProperty("storage")
            .GetProperty("value")
            .GetString() ?? string.Empty;
    }

    // Encodes agent name and JSON payload in Confluence storage format.
    // See docs/confluence-transport-conventions.md for the full convention.
    private static string BuildRequestBody(string agentName, string payload) => $"""
        <p><strong>Agent:</strong> {agentName}</p>
        <ac:structured-macro ac:name="code" ac:schema-version="1">
          <ac:parameter ac:name="language">json</ac:parameter>
          <ac:plain-text-body><![CDATA[{payload}]]></ac:plain-text-body>
        </ac:structured-macro>
        """;

    public void Dispose() => _http.Dispose();
}
