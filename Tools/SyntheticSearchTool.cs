using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace SyntheticSearchMcp.Tools;

/// <summary>
/// MCP tool for searching the web using the Synthetic.new API.
/// </summary>
[McpServerToolType]
public sealed class SyntheticSearchTool
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger<SyntheticSearchTool> _logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="SyntheticSearchTool"/> class.
  /// </summary>
  /// <param name="httpClientFactory">The HTTP client factory.</param>
  /// <param name="logger">The logger instance.</param>
  public SyntheticSearchTool(IHttpClientFactory httpClientFactory, ILogger<SyntheticSearchTool> logger)
  {
    _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <summary>
  /// Searches the web via Synthetic API.
  /// </summary>
  /// <param name="query">The search query to execute.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The structured search result.</returns>
  [McpServerTool(Name = "synthetic_search")]
  [Description("Search the web using Synthetic.new API - zero data retention web search for coding agents")]
  [McpMeta("destructive", false)]
  [McpMeta("readOnlyHint", true)]
  [McpMeta("category", "search")]
  public async Task<SearchToolResult> SearchAsync(
    [Description("The search query to execute")] string query,
    CancellationToken cancellationToken)
  {
    var stopwatch = Stopwatch.StartNew();

    try
    {
      if (string.IsNullOrWhiteSpace(query))
      {
        throw new McpException("Query is required");
      }

      var httpClient = _httpClientFactory.CreateClient("SyntheticSearch");

      _logger.LogInformation("Executing synthetic search for query: {Query}", query);

      var request = new SearchRequest { Query = query };
      var json = JsonSerializer.Serialize(request, SearchJsonContext.Default.SearchRequest);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      using var response = await httpClient.PostAsync(
        "/v2/search",
        content,
        cancellationToken).ConfigureAwait(false);

      if (!response.IsSuccessStatusCode)
      {
        var statusCode = (int)response.StatusCode;
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new McpException($"Synthetic API request failed ({statusCode}): {responseText}");
      }

      var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      var searchResponse = JsonSerializer.Deserialize(responseBody, SearchJsonContext.Default.SearchResponse);

      if (searchResponse is null)
      {
        throw new McpException("Failed to deserialize search response");
      }

      stopwatch.Stop();
      _logger.LogInformation(
        "Synthetic search completed in {DurationMs}ms. Found {ResultCount} results",
        stopwatch.ElapsedMilliseconds,
        searchResponse.Results?.Count ?? 0);

      return FormatSearchResponse(searchResponse, query);
    }
    catch (HttpRequestException ex)
    {
      stopwatch.Stop();
      _logger.LogError(ex, "HTTP error calling Synthetic.new API after {DurationMs}ms", stopwatch.ElapsedMilliseconds);
      throw new McpException($"Search API request failed: {ex.Message}");
    }
    catch (JsonException ex)
    {
      stopwatch.Stop();
      _logger.LogError(ex, "JSON parsing error after {DurationMs}ms", stopwatch.ElapsedMilliseconds);
      throw new McpException($"Failed to parse response: {ex.Message}");
    }
    catch (McpException)
    {
      throw;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      _logger.LogError(ex, "Error executing synthetic search after {DurationMs}ms", stopwatch.ElapsedMilliseconds);
      throw new McpException($"Search failed: {ex.Message}");
    }
  }

  private static SearchToolResult FormatSearchResponse(SearchResponse response, string query)
  {
    return new SearchToolResult
    {
      Query = string.IsNullOrWhiteSpace(response.Query) ? query : response.Query,
      Results = response.Results?.Select(r => new SearchToolResultItem
      {
        Title = r.Title,
        Url = r.Url,
        Snippet = r.Snippet,
      }).ToArray() ?? [],
      ResultCount = response.Results?.Count ?? 0,
    };
  }
}

/// <summary>
/// Structured tool result payload for synthetic search.
/// </summary>
public sealed class SearchToolResult
{
  [JsonPropertyName("query")]
  public string Query { get; set; } = string.Empty;

  [JsonPropertyName("results")]
  public SearchToolResultItem[] Results { get; set; } = [];

  [JsonPropertyName("resultCount")]
  public int ResultCount { get; set; }
}

/// <summary>
/// Individual item in the synthetic search result.
/// </summary>
public sealed class SearchToolResultItem
{
  [JsonPropertyName("title")]
  public string Title { get; set; } = string.Empty;

  [JsonPropertyName("url")]
  public string Url { get; set; } = string.Empty;

  [JsonPropertyName("snippet")]
  public string Snippet { get; set; } = string.Empty;
}

/// <summary>
/// Search request payload.
/// </summary>
public sealed class SearchRequest
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
public sealed class SearchResponse
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
public sealed class SearchResult
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
