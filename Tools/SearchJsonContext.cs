using System.Text.Json.Serialization;

namespace SyntheticSearchMcp.Tools;

[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchToolResult))]
[JsonSerializable(typeof(SearchToolResultItem))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
internal sealed partial class SearchJsonContext : JsonSerializerContext
{
}
