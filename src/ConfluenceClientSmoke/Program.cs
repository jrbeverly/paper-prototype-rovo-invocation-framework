using System.Diagnostics;
using ConfluenceClient;

// End-to-end experiment runner: submit → poll → extract → print result.
// Usage: dotnet run --project src/ConfluenceClientSmoke -- <agent-name> [poll-interval-secs] [max-polls]
// Required env vars: see docs/confluence-transport-conventions.md

var agentName = args.Length > 0 ? args[0] : "test-agent";
var pollSecs  = args.Length > 1 && int.TryParse(args[1], out var pi) ? pi : 15;
var maxPolls  = args.Length > 2 && int.TryParse(args[2], out var mp) ? mp : 20;

var config = ConfluenceTransportConfig.FromEnvironment();
using var client = new ConfluenceTransportClient(config);

var title   = $"rovo-{agentName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
var payload = """{ "prompt": "Return a brief summary of your capabilities as a JSON object with keys: name (string), description (string)." }""";

var sw = Stopwatch.StartNew();

Log(sw, $"Submitting: agent={agentName} title={title}");
var page = await client.CreateRequestPageAsync(title, agentName, payload);
Log(sw, $"Request page created: id={page.Id}");

var initial = await client.GetPageLabelsAsync(page.Id);
Log(sw, $"Initial labels: {string.Join(", ", initial)}");

Log(sw, $"Polling every {pollSecs}s (max {maxPolls} attempts)...");

string? responsePageId = null;
for (var attempt = 1; attempt <= maxPolls; attempt++)
{
    await Task.Delay(TimeSpan.FromSeconds(pollSecs));

    var children = await client.GetChildPagesAsync(page.Id);
    foreach (var child in children)
    {
        var childLabels = await client.GetPageLabelsAsync(child.Id);
        if (childLabels.Contains("rovo-response", StringComparer.Ordinal))
        {
            responsePageId = child.Id;
            break;
        }
    }

    if (responsePageId is not null)
    {
        Log(sw, $"Completion detected after {attempt} poll(s). Response page id={responsePageId}");
        break;
    }

    var requestLabels = await client.GetPageLabelsAsync(page.Id);
    Log(sw, $"Poll {attempt}/{maxPolls}: no response yet. Request labels: {string.Join(", ", requestLabels)}");
}

if (responsePageId is null)
{
    Log(sw, $"Timed out after {maxPolls} polls ({maxPolls * pollSecs}s total wait).");
    return;
}

Log(sw, $"Extracting result from response page id={responsePageId}...");
var body = await client.GetPageBodyAsync(responsePageId);
var (content, format) = ExtractContent(body);

Log(sw, $"Format detected: {format}");
Console.WriteLine();
Console.WriteLine("--- Result ---");
Console.WriteLine(content);
Console.WriteLine("--- End ---");
Console.WriteLine();
Console.WriteLine($"Total elapsed: {sw.Elapsed.TotalSeconds:F1}s");

static void Log(Stopwatch sw, string message) =>
    Console.WriteLine($"[{sw.Elapsed.TotalSeconds,6:F1}s] {message}");

// Pulls CDATA from a Confluence storage XHTML code macro when present;
// falls back to the raw storage body. Sniffs json/yaml/code/text by leading chars.
static (string content, string format) ExtractContent(string storageXhtml)
{
    const string cdataOpen  = "<![CDATA[";
    const string cdataClose = "]]>";

    var openIdx = storageXhtml.IndexOf(cdataOpen, StringComparison.Ordinal);
    if (openIdx >= 0)
    {
        var start    = openIdx + cdataOpen.Length;
        var closeIdx = storageXhtml.IndexOf(cdataClose, start, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            var raw     = storageXhtml[start..closeIdx].Trim();
            var trimmed = raw.TrimStart();
            var fmt     = (trimmed.StartsWith('{') || trimmed.StartsWith('[')) ? "json"
                        : trimmed.StartsWith("---")                            ? "yaml"
                        : "code";
            return (raw, fmt);
        }
    }

    return (storageXhtml, "text");
}
