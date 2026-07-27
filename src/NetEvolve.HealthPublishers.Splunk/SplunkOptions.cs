namespace NetEvolve.HealthPublishers.Splunk;

/// <summary>
/// Represents configuration options for the Splunk health check publisher.
/// </summary>
public sealed record SplunkOptions
{
    /// <summary>
    /// Gets or sets the base address of the Splunk HTTP Event Collector (HEC), e.g. <c>https://splunk.example.com:8088</c>.
    /// </summary>
    public Uri? ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the HEC token used to authenticate against the Splunk HTTP Event Collector.
    /// </summary>
    /// <remarks>
    /// Required. Sent as the <c>Authorization: Splunk &lt;token&gt;</c> header.
    /// </remarks>
    public string HecToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Splunk sourcetype assigned to the published event.
    /// </summary>
    /// <remarks>
    /// Optional. When not set, Splunk assigns the sourcetype configured for the HEC token.
    /// </remarks>
    public string? SourceType { get; set; }

    /// <summary>
    /// Gets or sets the Splunk source assigned to the published event.
    /// </summary>
    /// <remarks>
    /// Optional. When not set, Splunk assigns the source configured for the HEC token.
    /// </remarks>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the Splunk index the published event is written to.
    /// </summary>
    /// <remarks>
    /// Optional. When not set, Splunk writes to the index configured for the HEC token.
    /// </remarks>
    public string? Index { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published event,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
