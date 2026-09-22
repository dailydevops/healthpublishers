namespace NetEvolve.HealthPublishers.Webhook;

using System.Collections.Generic;

/// <summary>
/// Represents configuration options for the webhook health check publisher.
/// </summary>
public sealed record WebhookOptions
{
    /// <summary>
    /// Gets or sets the absolute address of the webhook endpoint the health report is posted to.
    /// </summary>
    /// <remarks>
    /// Required. Must be an absolute URI, e.g. <c>https://example.com/webhooks/health</c>.
    /// </remarks>
    public Uri? Uri { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published payload,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets the custom HTTP headers sent with every request, e.g. for authentication.
    /// </summary>
    /// <remarks>
    /// Optional. Empty by default. Populate via configuration or code, e.g. <c>options.Headers["Authorization"] = "Bearer &lt;token&gt;"</c>.
    /// </remarks>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
