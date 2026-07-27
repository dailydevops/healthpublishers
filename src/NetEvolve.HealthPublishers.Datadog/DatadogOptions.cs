namespace NetEvolve.HealthPublishers.Datadog;

/// <summary>
/// Represents configuration options for the Datadog health check publisher.
/// </summary>
public sealed record DatadogOptions
{
    /// <summary>
    /// Gets or sets the base address of the Datadog events intake API, e.g. <c>https://api.datadoghq.com</c>.
    /// </summary>
    /// <remarks>
    /// Optional. Defaults to <c>https://api.datadoghq.com</c> when not set. Use a regional site, e.g.
    /// <c>https://api.datadoghq.eu</c>, when the Datadog organization is hosted outside the US1 site.
    /// </remarks>
    public Uri? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the API key used to authenticate against the Datadog events intake API.
    /// </summary>
    /// <remarks>
    /// Required. Sent as the <c>DD-API-KEY</c> header.
    /// </remarks>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published event's tags,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
