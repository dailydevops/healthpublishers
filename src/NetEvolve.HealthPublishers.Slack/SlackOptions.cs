namespace NetEvolve.HealthPublishers.Slack;

/// <summary>
/// Represents configuration options for the Slack health check publisher.
/// </summary>
public sealed record SlackOptions
{
    /// <summary>
    /// Gets or sets the Slack incoming webhook URL, e.g. <c>https://hooks.slack.com/services/T000/B000/XXX</c>.
    /// </summary>
    /// <remarks>
    /// Required. The publisher posts a message payload directly to this address.
    /// </remarks>
    public Uri? WebhookUrl { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published message,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
