using Amazon.Lambda.Core;
using ConfluenceClient;

namespace InvocationHandler;

public sealed record ExtractResultRequest(string ResponsePageId);
public sealed record ExtractionResult(string Content, string Format);

public class ExtractResultFunction
{
    public async Task<ExtractionResult> FunctionHandler(
        ExtractResultRequest request, ILambdaContext context)
    {
        var config = ConfluenceTransportConfig.FromEnvironment();
        using var client = new ConfluenceTransportClient(config);

        var storageBody = await client.GetPageBodyAsync(request.ResponsePageId);
        var (content, format) = ExtractContent(storageBody);

        context.Logger.Log($"Extracted result: format={format} contentLength={content.Length}");
        return new ExtractionResult(content, format);
    }

    // Best-effort extraction from Confluence storage XHTML.
    // Pulls CDATA from a code macro when present (JSON/YAML/code); falls back to raw storage body.
    private static (string content, string format) ExtractContent(string storageXhtml)
    {
        const string cdataOpen = "<![CDATA[";
        const string cdataClose = "]]>";

        var openIdx = storageXhtml.IndexOf(cdataOpen, StringComparison.Ordinal);
        if (openIdx >= 0)
        {
            var contentStart = openIdx + cdataOpen.Length;
            var closeIdx = storageXhtml.IndexOf(cdataClose, contentStart, StringComparison.Ordinal);
            if (closeIdx >= 0)
            {
                var content = storageXhtml[contentStart..closeIdx].Trim();
                var trimmed = content.TrimStart();
                var format = (trimmed.StartsWith('{') || trimmed.StartsWith('[')) ? "json"
                    : trimmed.StartsWith("---") ? "yaml"
                    : "code";
                return (content, format);
            }
        }

        return (storageXhtml, "text");
    }
}
