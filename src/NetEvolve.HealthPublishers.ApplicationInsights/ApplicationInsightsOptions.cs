namespace NetEvolve.HealthPublishers.ApplicationInsights;

/// <summary>
/// Represents configuration options for the Application Insights health check publisher.
/// </summary>
public sealed record ApplicationInsightsOptions
{
    /// <summary>
    /// Gets or sets the Application Insights connection string, e.g. <c>InstrumentationKey=...;IngestionEndpoint=...</c>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published telemetry,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
