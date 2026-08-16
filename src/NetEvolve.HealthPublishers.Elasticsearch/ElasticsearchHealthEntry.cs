namespace NetEvolve.HealthPublishers.Elasticsearch;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal sealed record ElasticsearchHealthEntry
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("elapsed_ms")]
    public required double ElapsedMilliseconds { get; init; }

    [JsonPropertyName("tags")]
    public required IReadOnlyCollection<string> Tags { get; init; }
}
