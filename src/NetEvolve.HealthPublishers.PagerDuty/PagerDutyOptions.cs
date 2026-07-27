namespace NetEvolve.HealthPublishers.PagerDuty;

/// <summary>
/// Represents configuration options for the PagerDuty health check publisher.
/// </summary>
public sealed record PagerDutyOptions
{
    /// <summary>
    /// Gets or sets the base address of the PagerDuty Events API, e.g. <c>https://events.pagerduty.com</c>.
    /// </summary>
    /// <remarks>
    /// Optional. Defaults to <c>https://events.pagerduty.com</c> when not set.
    /// </remarks>
    public Uri? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the PagerDuty Events API v2 integration/routing key used to route the event to the
    /// correct service.
    /// </summary>
    /// <remarks>
    /// Required. Sent as <c>routing_key</c> in the request body.
    /// </remarks>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Used to derive a stable <c>dedup_key</c>, so that a triggered incident can later be
    /// resolved by a subsequent healthy report from the same system, and sent alongside
    /// <see cref="Environment.MachineName"/> as the event's <c>source</c>.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
