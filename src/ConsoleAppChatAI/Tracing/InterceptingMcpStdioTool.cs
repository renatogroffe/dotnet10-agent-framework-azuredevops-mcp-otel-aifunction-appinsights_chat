using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.Json;

namespace ConsoleAppChatAI.Tracing;

public sealed class InterceptingMcpStdioTool : AIFunction
{
    private readonly AIFunction _inner;

    public InterceptingMcpStdioTool(AIFunction inner)
    {
        _inner = inner;
    }

    public override string Name => _inner.Name;
    public override string Description => _inner.Description;
    public override JsonElement JsonSchema => _inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => _inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // Convenções OpenTelemetry: https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp/#spans
        using var activityInvokeMcpTool = OpenTelemetryExtensions.ActivitySource!
            .StartActivity($"invoke mcpTool {Name}", ActivityKind.Client)!;
        activityInvokeMcpTool.SetTag("mcp.method.name", Name);
        activityInvokeMcpTool.SetTag("network.transport", "pipe");

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _inner.InvokeAsync(arguments, cancellationToken);
            activityInvokeMcpTool.SetTag("invoke.status", "success");
            return result;
        }
        catch (Exception ex)
        {
            activityInvokeMcpTool.SetTag("invoke.status", "error");
            activityInvokeMcpTool.SetTag("error.type", ex.GetType().FullName);
            throw;
        }
    }
}