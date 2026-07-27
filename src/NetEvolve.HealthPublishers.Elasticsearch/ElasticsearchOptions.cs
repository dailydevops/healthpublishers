namespace NetEvolve.HealthPublishers.Elasticsearch;

/// <summary>
/// Represents configuration options for the Elasticsearch health check publisher.
/// </summary>
public sealed record ElasticsearchOptions
{
    /// <summary>
    /// Gets or sets the base address of the Elasticsearch cluster, e.g. <c>https://elasticsearch.example.com:9200</c>.
    /// </summary>
    public Uri? ServerUri { get; set; }

    /// <summary>
    /// Gets or sets the name of the Elasticsearch index the published document is written to.
    /// </summary>
    /// <remarks>
    /// Required.
    /// </remarks>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key used to authenticate against the Elasticsearch cluster.
    /// </summary>
    /// <remarks>
    /// Optional. When set, takes precedence over <see cref="Username"/> and <see cref="Password"/>.
    /// Sent as an <c>Authorization: ApiKey &lt;key&gt;</c> header.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the username used for basic authentication against the Elasticsearch cluster.
    /// </summary>
    /// <remarks>
    /// Optional. Ignored when <see cref="ApiKey"/> is set. Must be used together with <see cref="Password"/>.
    /// </remarks>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password used for basic authentication against the Elasticsearch cluster.
    /// </summary>
    /// <remarks>
    /// Optional. Ignored when <see cref="ApiKey"/> is set. Must be used together with <see cref="Username"/>.
    /// </remarks>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the indexed document,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
