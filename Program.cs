using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyntheticSearchMcp;
using SyntheticSearchMcp.Tools;
using SyntheticSearchMcp.Transports;

// Build host with configuration
var host = Host.CreateDefaultBuilder(args)
  .ConfigureLogging((context, logging) =>
  {
    // Configure logging to stderr to avoid interfering with JSON-RPC over stdout
    logging.AddSimpleConsole(options =>
    {
      options.SingleLine = true;
    });
    logging.SetMinimumLevel(LogLevel.Information);
  })
  .ConfigureServices((context, services) =>
  {
    // Get API key from environment variable
    var apiKey = Environment.GetEnvironmentVariable("SYNTHETIC_API_KEY")
      ?? throw new InvalidOperationException(
        "SYNTHETIC_API_KEY environment variable is required. " +
        "Get your API key from https://synthetic.new");

    // Configure HttpClient with default headers
    services.AddHttpClient("SyntheticSearch", client =>
    {
      client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", apiKey);
      client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
      client.Timeout = TimeSpan.FromSeconds(60);
    });

    // Register dependencies
    services.AddSingleton<IToolRegistry, ToolRegistry>();

    // Register the search tool with HttpClient
    services.AddSingleton<SyntheticSearchTool>(sp =>
    {
      var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
      var httpClient = httpClientFactory.CreateClient("SyntheticSearch");
      var logger = sp.GetRequiredService<ILogger<SyntheticSearchTool>>();
      return new SyntheticSearchTool(httpClient, logger);
    });

    // Register MCP server
    services.AddSingleton<SyntheticSearchMcpServer>();

    // Register transport
    services.AddSingleton<StdioTransport>(sp =>
    {
      var server = sp.GetRequiredService<SyntheticSearchMcpServer>();
      var logger = sp.GetRequiredService<ILogger<StdioTransport>>();
      return new StdioTransport(server, logger);
    });
  })
  .Build();

// Start the server
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Synthetic Search MCP Server starting...");

var transport = host.Services.GetRequiredService<StdioTransport>();
var cts = new CancellationTokenSource();

// Handle graceful shutdown
Console.CancelKeyPress += (sender, e) =>
{
  e.Cancel = true;
  logger.LogInformation("Shutdown requested via Ctrl+C");
  cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
{
  logger.LogInformation("Process exiting...");
  cts.Cancel();
};

try
{
  // Start the transport - it runs until EOF on stdin or cancellation
  await transport.StartAsync(cts.Token).ConfigureAwait(false);

  // Wait for transport to complete or cancellation requested
  var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  await using (cts.Token.Register(() => tcs.TrySetResult()))
  {
    await tcs.Task.ConfigureAwait(false);
  }
}
catch (OperationCanceledException)
{
  // Expected when shutting down
  logger.LogInformation("Cancellation requested, shutting down...");
}
finally
{
  logger.LogInformation("Synthetic Search MCP Server shutting down...");

  await transport.StopAsync(CancellationToken.None).ConfigureAwait(false);
  await transport.DisposeAsync().ConfigureAwait(false);
}
