namespace NetEvolve.HealthPublishers.OpenTelemetry;

/// <summary>
/// Represents configuration options for the OpenTelemetry health check publisher.
/// </summary>
public sealed record OpenTelemetryOptions
{
    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as a tag on every recorded metric,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
