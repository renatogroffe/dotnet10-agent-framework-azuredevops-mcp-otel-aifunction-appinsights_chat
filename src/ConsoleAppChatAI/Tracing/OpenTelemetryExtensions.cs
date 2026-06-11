using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace ConsoleAppChatAI.Tracing;

public static class OpenTelemetryExtensions
{
    public static string? ServiceName { get; private set; }
    public static string? ServiceVersion { get; private set; }
    public static string? ResourceAttributes { get; private set; }
    public static ActivitySource? ActivitySource { get; private set; }

    public static void Initialize(IConfiguration configuration)
    {
        ServiceName = configuration["OTEL_SERVICE_NAME"];
        ServiceVersion = typeof(OpenTelemetryExtensions).Assembly.GetName().Version!.ToString();
        ResourceAttributes = configuration["OTEL_RESOURCE_ATTRIBUTES"]!.Replace("#OTEL_SERVICE_NAME#", ServiceName!);
        ActivitySource = new ActivitySource(ServiceName!, ServiceVersion);
    }
}