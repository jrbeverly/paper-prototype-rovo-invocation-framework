using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using ConfluenceClient;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace InvocationHandler;

public sealed record SubmitRequest(string Agent, JsonElement Input);
public sealed record SubmitResponse(string RequestPageId, string Title);

public class Function
{
    public async Task<SubmitResponse> FunctionHandler(SubmitRequest request, ILambdaContext context)
    {
        var title = $"rovo-{request.Agent}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var payload = JsonSerializer.Serialize(request.Input);

        context.Logger.Log($"Submitting invocation: agent={request.Agent} title={title}");

        var config = ConfluenceTransportConfig.FromEnvironment();
        using var client = new ConfluenceTransportClient(config);
        var page = await client.CreateRequestPageAsync(title, request.Agent, payload);

        context.Logger.Log($"Created request page: id={page.Id}");
        return new SubmitResponse(page.Id, page.Title);
    }
}
