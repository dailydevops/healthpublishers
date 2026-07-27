namespace NetEvolve.HealthPublishers.Opsgenie;

/// <summary>
/// Represents configuration options for the Opsgenie health check publisher.
/// </summary>
public sealed record OpsgenieOptions
{
    /// <summary>
    /// Gets or sets the base address of the Opsgenie Alert API, e.g. <c>https://api.opsgenie.com</c>.
    /// </summary>
    /// <remarks>
    /// Optional. Defaults to <c>https://api.opsgenie.com</c> when not set. Use the EU instance,
    /// <c>https://api.eu.opsgenie.com</c>, when the Opsgenie account is hosted in the European Union.
    /// </remarks>
    public Uri? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the API key used to authenticate against the Opsgenie Alert API.
    /// </summary>
    /// <remarks>
    /// Required. Sent as the <c>Authorization: GenieKey &lt;api-key&gt;</c> header.
    /// </remarks>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Used to derive a stable alert alias, so that repeated unhealthy reports update the same
    /// Opsgenie alert instead of creating duplicates, and healthy reports close that same alert. Also sent
    /// alongside <see cref="Environment.MachineName"/> as part of the created alert's tags and details.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
