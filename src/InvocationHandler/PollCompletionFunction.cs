using Amazon.Lambda.Core;
using ConfluenceClient;

namespace InvocationHandler;

public sealed record PollCompletionRequest(string RequestPageId);
public sealed record PollCompletionResponse(bool IsComplete, string? ResponsePageId);

public class PollCompletionFunction
{
    public async Task<PollCompletionResponse> FunctionHandler(
        PollCompletionRequest request, ILambdaContext context)
    {
        var config = ConfluenceTransportConfig.FromEnvironment();
        using var client = new ConfluenceTransportClient(config);

        var children = await client.GetChildPagesAsync(request.RequestPageId);
        foreach (var child in children)
        {
            var labels = await client.GetPageLabelsAsync(child.Id);
            if (labels.Contains("rovo-response", StringComparer.Ordinal))
            {
                context.Logger.Log($"Response page found: id={child.Id}");
                return new PollCompletionResponse(true, child.Id);
            }
        }

        context.Logger.Log($"No response yet for request page {request.RequestPageId}");
        return new PollCompletionResponse(false, null);
    }
}
