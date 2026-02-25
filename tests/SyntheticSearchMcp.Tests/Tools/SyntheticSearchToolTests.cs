using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using SyntheticSearchMcp.Tools;
using Xunit;

namespace SyntheticSearchMcp.Tests.Tools;

public sealed class SyntheticSearchToolTests
{
  [Fact]
  public async Task SearchAsync_ReturnsMappedResult_WhenApiRequestSucceeds()
  {
    var responseBody = """
      {
        "query": "Model Context Protocol",
        "results": [
          {
            "title": "MCP Spec",
            "url": "https://modelcontextprotocol.io/specification",
            "snippet": "Protocol reference"
          }
        ]
      }
      """;

    var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
    {
      Assert.Equal(HttpMethod.Post, request.Method);
      Assert.Equal("https://api.synthetic.new/v2/search", request.RequestUri?.ToString());

      var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
      Assert.Contains("\"query\":\"Model Context Protocol\"", requestBody, StringComparison.Ordinal);

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
      };
    });

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.synthetic.new"),
    };

    var tool = new SyntheticSearchTool(new StubHttpClientFactory(httpClient), NullLogger<SyntheticSearchTool>.Instance);

    var result = await tool.SearchAsync("Model Context Protocol", CancellationToken.None);

    Assert.Equal("Model Context Protocol", result.Query);
    Assert.Equal(1, result.ResultCount);
    Assert.Single(result.Results);
    Assert.Equal("MCP Spec", result.Results[0].Title);
  }

  [Fact]
  public async Task SearchAsync_UsesInputQuery_WhenUpstreamQueryIsEmpty()
  {
    var responseBody = """
      {
        "query": "",
        "results": []
      }
      """;

    var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
    }));

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.synthetic.new"),
    };

    var tool = new SyntheticSearchTool(new StubHttpClientFactory(httpClient), NullLogger<SyntheticSearchTool>.Instance);

    var result = await tool.SearchAsync("Fallback Query", CancellationToken.None);

    Assert.Equal("Fallback Query", result.Query);
    Assert.Equal(0, result.ResultCount);
    Assert.Empty(result.Results);
  }

  [Fact]
  public async Task SearchAsync_ThrowsMcpException_WhenQueryIsMissing()
  {
    var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.synthetic.new"),
    };

    var tool = new SyntheticSearchTool(new StubHttpClientFactory(httpClient), NullLogger<SyntheticSearchTool>.Instance);

    var exception = await Assert.ThrowsAsync<McpException>(() => tool.SearchAsync(string.Empty, CancellationToken.None));
    Assert.Contains("Query is required", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SearchAsync_ThrowsMcpException_WhenUpstreamReturnsNonSuccessStatusCode()
  {
    var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
    {
      Content = new StringContent("bad request", Encoding.UTF8, "text/plain"),
    }));

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.synthetic.new"),
    };

    var tool = new SyntheticSearchTool(new StubHttpClientFactory(httpClient), NullLogger<SyntheticSearchTool>.Instance);

    var exception = await Assert.ThrowsAsync<McpException>(() => tool.SearchAsync("invalid", CancellationToken.None));
    Assert.Contains("Synthetic API request failed", exception.Message, StringComparison.Ordinal);
  }

  private sealed class StubHttpClientFactory : IHttpClientFactory
  {
    private readonly HttpClient _client;

    public StubHttpClientFactory(HttpClient client)
    {
      _client = client;
    }

    public HttpClient CreateClient(string name)
    {
      return _client;
    }
  }

  private sealed class StubHttpMessageHandler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
      _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      return _handler(request, cancellationToken);
    }
  }
}
