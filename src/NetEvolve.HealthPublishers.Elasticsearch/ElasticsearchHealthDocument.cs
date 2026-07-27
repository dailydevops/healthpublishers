namespace NetEvolve.HealthPublishers.Elasticsearch;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal sealed record ElasticsearchHealthDocument
{
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("elapsed_ms")]
    public required double ElapsedMilliseconds { get; init; }

    [JsonPropertyName("system_identifier")]
    public required string SystemIdentifier { get; init; }

    [JsonPropertyName("machine_name")]
    public required string MachineName { get; init; }

    [JsonPropertyName("entries")]
    public required IReadOnlyDictionary<string, ElasticsearchHealthEntry> Entries { get; init; }
}

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
