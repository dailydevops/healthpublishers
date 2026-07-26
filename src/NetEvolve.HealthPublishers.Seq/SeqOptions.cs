namespace NetEvolve.HealthPublishers.Seq;

/// <summary>
/// Represents configuration options for the Seq health check publisher.
/// </summary>
public sealed record SeqOptions
{
    /// <summary>
    /// Gets or sets the base address of the Seq server, e.g. <c>https://seq.example.com</c>.
    /// </summary>
    public Uri? ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the API key used to authenticate against the Seq server.
    /// </summary>
    /// <remarks>
    /// When null or empty, the request is sent without the <c>X-Seq-ApiKey</c> header.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as part of the published event,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
