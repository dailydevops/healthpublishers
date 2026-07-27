namespace NetEvolve.HealthPublishers.Prometheus.PushGateway;

/// <summary>
/// Represents configuration options for the Prometheus Pushgateway health check publisher.
/// </summary>
public sealed record PrometheusPushGatewayOptions
{
    /// <summary>
    /// Gets or sets the base address of the Prometheus Pushgateway instance, e.g. <c>https://pushgateway.example.com</c>.
    /// </summary>
    /// <remarks>
    /// Required. Must be a valid absolute URI.
    /// </remarks>
    public Uri? ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the job name the metrics are grouped under.
    /// </summary>
    /// <remarks>
    /// Required. Used as the <c>job</c> path segment of the Pushgateway API, e.g. <c>POST /metrics/job/&lt;Job&gt;</c>.
    /// </remarks>
    public string Job { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an instance label to distinguish the source of the pushed metrics.
    /// </summary>
    /// <remarks>
    /// Required. Used as the <c>instance</c> path segment of the Pushgateway API,
    /// e.g. <c>PUT /metrics/job/&lt;Job&gt;/instance/&lt;Instance&gt;</c>. Pushgateway groups pushed metrics by the
    /// full path, so without a distinct instance per publisher, publishers sharing a job would overwrite each
    /// other's same-named metrics.
    /// </remarks>
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as a label on every published metric,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
