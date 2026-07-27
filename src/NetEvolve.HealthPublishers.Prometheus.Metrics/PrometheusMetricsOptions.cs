namespace NetEvolve.HealthPublishers.Prometheus.Metrics;

/// <summary>
/// Represents configuration options for the Prometheus Metrics health check publisher.
/// </summary>
public sealed record PrometheusMetricsOptions
{
    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as a label on every updated gauge, useful
    /// to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
