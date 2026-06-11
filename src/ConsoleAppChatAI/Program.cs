using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using ConsoleAppChatAI.Tracing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.ClientModel;


Console.WriteLine("***** Testes com Agent Framework + Microsoft Foundry + MCP Azure DevOps *****");
Console.WriteLine();

var oldForegroundColor = Console.ForegroundColor;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

OpenTelemetryExtensions.Initialize(configuration);
var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(OpenTelemetryExtensions.ServiceName!);

var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(OpenTelemetryExtensions.ServiceName!)
    .AddHttpClientInstrumentation()
    .AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = configuration.GetConnectionString("AppInsights");
    })
    .Build();

var mcpName = configuration["MCP:Name"]!;
var envVariables = new Dictionary<string, string?>
{
    ["AZURE_DEVOPS_PAT"] = configuration["AzureDevOps:PAT"]!
};
await using var mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new()
{
    Name = mcpName,
    Command = "npx",
    Arguments = ["-y", "@azure-devops/mcp", configuration["AzureDevOps:Organization"]!],
    EnvironmentVariables = envVariables
}));
Console.WriteLine($"Ferramentas do MCP:");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"***** {mcpName} *****");
var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
Console.WriteLine($"Quantidade de ferramentas disponiveis = {mcpTools.Count}");
Console.WriteLine();
foreach (var tool in mcpTools)
{
    Console.WriteLine($"* {tool.Name}: {tool.Description}");
}
Console.ForegroundColor = oldForegroundColor;
Console.WriteLine();
var interceptedMcpTools = mcpTools.Select(tool => new InterceptingMcpStdioTool(tool)).ToArray();

var agent = new AzureOpenAIClient(endpoint: new Uri(configuration["MicrosoftFoundry:Endpoint"]!),
        credential: new ApiKeyCredential(configuration["MicrosoftFoundry:ApiKey"]!))
    .GetChatClient(configuration["MicrosoftFoundry:DeploymentName"]!)
    .AsAIAgent(
        instructions: "Você é um assistente de IA que ajuda o usuario a interagir com projetos, repositorios e " +
            "pipelines em uma Organization do Azure DevOps.",
        tools: [.. interceptedMcpTools])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: OpenTelemetryExtensions.ServiceName)
    .Build();
while (true)
{
    Console.WriteLine("Sua pergunta:");
    var userPrompt = Console.ReadLine();

    using var activity1 = OpenTelemetryExtensions.ActivitySource!
        .StartActivity("PerguntaChatIA")!;

    var result = await agent.RunAsync(userPrompt!);

    Console.WriteLine();
    Console.WriteLine("Resposta da IA:");
    Console.WriteLine();

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(result.AsChatResponse().Messages.Last().Text);
    Console.ForegroundColor = oldForegroundColor;

    Console.WriteLine();
    Console.WriteLine();

    activity1.Stop();
}