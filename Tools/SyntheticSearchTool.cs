using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SyntheticSearchMcp.Tools;

/// <summary>
/// MCP tool for searching the web using the Synthetic.new API.
/// </summary>
public sealed class SyntheticSearchTool : IMcpTool
{
  private readonly HttpClient _httpClient;
  private readonly ILogger<SyntheticSearchTool> _logger;
  private readonly JsonDocument _inputSchema;

  /// <summary>
  /// Initializes a new instance of the <see cref="SyntheticSearchTool"/> class.
  /// </summary>
  /// <param name="httpClient">The HTTP client for making API requests.</param>
  /// <param name="logger">The logger instance.</param>
  public SyntheticSearchTool(HttpClient httpClient, ILogger<SyntheticSearchTool> logger)
  {
    _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Define JSON schema for the tool's input parameters
    var schemaJson = """
      {
          "type": "object",
          "properties": {
              "query": {
                  "type": "string",
                  "description": "The search query to execute"
              }
          },
          "required": ["query"]
      }
      """;

    _inputSchema = JsonDocument.Parse(schemaJson);
  }

  /// <inheritdoc/>
  public string Name => "synthetic_search";

  /// <inheritdoc/>
  public string Description => "Search the web using Synthetic.new API - zero data retention web search for coding agents";

  /// <inheritdoc/>
  public JsonDocument InputSchema => _inputSchema;

  /// <inheritdoc/>
  public async Task<object> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
  {
    var query = arguments.GetProperty("query").GetString()
      ?? throw new ArgumentException("Query is required", nameof(arguments));

    _logger.LogInformation("Executing synthetic search for query: {Query}", query);

    try
    {
      var request = new SearchRequest { Query = query };
      var json = JsonSerializer.Serialize(request);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      var response = await _httpClient.PostAsync(
        "https://api.synthetic.new/v2/search",
        content,
        cancellationToken).ConfigureAwait(false);

      response.EnsureSuccessStatusCode();

      var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      var searchResponse = JsonSerializer.Deserialize<SearchResponse>(responseBody);

      if (searchResponse is null)
      {
        throw new InvalidOperationException("Failed to deserialize search response");
      }

      _logger.LogInformation(
        "Synthetic search completed. Found {ResultCount} results",
        searchResponse.Results?.Count ?? 0);

      return FormatSearchResponse(searchResponse);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HTTP error calling Synthetic.new API");
      throw new InvalidOperationException($"Search API request failed: {ex.Message}", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error executing synthetic search");
      throw new InvalidOperationException($"Search failed: {ex.Message}", ex);
    }
  }

  private static object FormatSearchResponse(SearchResponse response)
  {
    return new
    {
      query = response.Query,
      results = response.Results?.Select(r => new
      {
        title = r.Title,
        url = r.Url,
        snippet = r.Snippet,
      }).ToArray(),
      resultCount = response.Results?.Count ?? 0,
    };
  }
}

/// <summary>
/// Search request payload.
/// </summary>
public class SearchRequest
{
  /// <summary>
  /// The search query.
  /// </summary>
  [JsonPropertyName("query")]
  public string Query { get; set; } = string.Empty;
}

/// <summary>
/// Search response from the API.
/// </summary>
public class SearchResponse
{
  /// <summary>
  /// The original query.
  /// </summary>
  [JsonPropertyName("query")]
  public string Query { get; set; } = string.Empty;

  /// <summary>
  /// The search results.
  /// </summary>
  [JsonPropertyName("results")]
  public List<SearchResult>? Results { get; set; }
}

/// <summary>
/// Individual search result.
/// </summary>
public class SearchResult
{
  /// <summary>
  /// The result title.
  /// </summary>
  [JsonPropertyName("title")]
  public string Title { get; set; } = string.Empty;

  /// <summary>
  /// The result URL.
  /// </summary>
  [JsonPropertyName("url")]
  public string Url { get; set; } = string.Empty;

  /// <summary>
  /// The result snippet.
  /// </summary>
  [JsonPropertyName("snippet")]
  public string Snippet { get; set; } = string.Empty;
}
