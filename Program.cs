using System;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol.Protocol;
using SyntheticSearchMcp.Tools;

var apiKey = Environment.GetEnvironmentVariable("SYNTHETIC_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
  throw new InvalidOperationException(
    "SYNTHETIC_API_KEY environment variable is required. " +
    "Get your API key from https://synthetic.new");
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddEventSourceLogger();

var debugMode = bool.TryParse(Environment.GetEnvironmentVariable("DebugMode"), out var parsedDebugMode) && parsedDebugMode;
if (debugMode)
{
  builder.Logging.AddConsole(options =>
  {
    options.LogToStandardErrorThreshold = LogLevel.Debug;
    options.FormatterName = ConsoleFormatterNames.Simple;
  });

  builder.Logging.AddSimpleConsole(options =>
  {
    options.ColorBehavior = LoggerColorBehavior.Disabled;
    options.IncludeScopes = false;
    options.SingleLine = true;
    options.TimestampFormat = "[HH:mm:ss] ";
  });

  builder.Logging.AddFilter("Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider", LogLevel.Debug);
  builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

builder.Services.AddHttpClient("SyntheticSearch", client =>
{
  client.BaseAddress = new Uri("https://api.synthetic.new");
  client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
  client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
  client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services
  .AddMcpServer(options =>
  {
    options.ProtocolVersion = "2024-11-05";
    options.ServerInfo = new Implementation
    {
      Name = "synthetic-search-mcp",
      Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
    };
  })
  .WithStdioServerTransport()
  .WithTools<SyntheticSearchTool>();

await builder.Build().RunAsync();
