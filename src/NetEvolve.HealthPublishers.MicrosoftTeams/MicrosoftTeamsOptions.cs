namespace NetEvolve.HealthPublishers.MicrosoftTeams;

/// <summary>
/// Represents configuration options for the Microsoft Teams health check publisher.
/// </summary>
public sealed record MicrosoftTeamsOptions
{
    /// <summary>
    /// Gets or sets the incoming webhook, or workflow connector, URL health reports are posted to.
    /// </summary>
    /// <remarks>
    /// Required. Must be an absolute URI, e.g. a Microsoft Teams incoming webhook URL created via a channel's
    /// connector configuration, or a Power Automate workflow trigger URL.
    /// </remarks>
    public Uri? WebhookUrl { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published card,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
