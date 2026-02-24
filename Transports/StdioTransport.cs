using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SyntheticSearchMcp.Transports;

/// <summary>
/// Standard input/output transport for CLI tools using JSON-RPC protocol.
/// </summary>
public sealed class StdioTransport : IMcpTransport
{
  private readonly SyntheticSearchMcpServer _server;
  private readonly ILogger<StdioTransport> _logger;
  private CancellationTokenSource? _cancellationTokenSource;

  /// <summary>
  /// Initializes a new instance of the <see cref="StdioTransport"/> class.
  /// </summary>
  /// <param name="server">The MCP server instance.</param>
  /// <param name="logger">The logger instance.</param>
  public StdioTransport(SyntheticSearchMcpServer server, ILogger<StdioTransport> logger)
  {
    _server = server ?? throw new ArgumentNullException(nameof(server));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <inheritdoc/>
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting STDIO transport");
    _cancellationTokenSource?.Dispose();
    _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    return Task.Run(
      async () =>
      {
        await ProcessStdioAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
      },
      _cancellationTokenSource.Token);
  }

  /// <inheritdoc/>
  public Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Stopping STDIO transport");
    _cancellationTokenSource?.Cancel();
    return Task.CompletedTask;
  }

  /// <inheritdoc/>
  public ValueTask DisposeAsync()
  {
    _cancellationTokenSource?.Dispose();
    return ValueTask.CompletedTask;
  }

  private async Task ProcessStdioAsync(CancellationToken cancellationToken)
  {
    using var reader = new StreamReader(Console.OpenStandardInput());
    using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

    while (!cancellationToken.IsCancellationRequested)
    {
      var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
      if (line is null)
      {
        break;
      }

      try
      {
        var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
        if (request is null)
        {
          continue;
        }

        var response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
        var responseJson = JsonSerializer.Serialize(response);
        await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing STDIO request");
        var errorResponse = new JsonRpcResponse
        {
          Id = null,
          Error = new JsonRpcError
          {
            Code = -32603,
            Message = "Internal error",
          },
        };
        var errorJson = JsonSerializer.Serialize(errorResponse);
        await writer.WriteLineAsync(errorJson).ConfigureAwait(false);
      }
    }
  }

  private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
  {
    // Handle initialization
    if (string.Equals(request.Method, "initialize", StringComparison.Ordinal))
    {
      return new JsonRpcResponse
      {
        Id = request.Id,
        Result = new
        {
          protocolVersion = "2024-11-05",
          capabilities = new { },
          serverInfo = new
          {
            name = "synthetic-search-mcp",
            version = "1.0.0",
          },
        },
      };
    }

    // Handle tools/list
    if (string.Equals(request.Method, "tools/list", StringComparison.Ordinal))
    {
      var tools = _server.Registry.GetAll();
      var toolDescriptions = tools.Select(t => new
      {
        name = t.Name,
        description = t.Description,
        inputSchema = t.InputSchema,
      }).ToList();

      return new JsonRpcResponse
      {
        Id = request.Id,
        Result = new { tools = toolDescriptions },
      };
    }

    // Handle tools/call
    if (string.Equals(request.Method, "tools/call", StringComparison.Ordinal))
    {
      var toolName = request.Params?.GetProperty("name").GetString();
      var arguments = request.Params?.GetProperty("arguments") ?? default;

      if (string.IsNullOrWhiteSpace(toolName))
      {
        return new JsonRpcResponse
        {
          Id = request.Id,
          Error = new JsonRpcError
          {
            Code = -32602,
            Message = "Invalid params: tool name is required",
          },
        };
      }

      try
      {
        var result = await _server.ExecuteToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
        return new JsonRpcResponse
        {
          Id = request.Id,
          Result = result,
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
        return new JsonRpcResponse
        {
          Id = request.Id,
          Error = new JsonRpcError
          {
            Code = -32603,
            Message = ex.Message,
          },
        };
      }
    }

    return new JsonRpcResponse
    {
      Id = request.Id,
      Error = new JsonRpcError
      {
        Code = -32601,
        Message = $"Method not found: {request.Method}",
      },
    };
  }

  private sealed class JsonRpcRequest
  {
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
  }

  private sealed class JsonRpcResponse
  {
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
  }

  private sealed class JsonRpcError
  {
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
  }
}
