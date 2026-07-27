namespace NetEvolve.HealthPublishers.AWS.CloudWatch;

/// <summary>
/// Represents configuration options for the AWS CloudWatch health check publisher.
/// </summary>
public sealed record CloudWatchOptions
{
    /// <summary>
    /// Gets or sets the AWS region system name the metrics are published to, e.g. <c>eu-central-1</c>.
    /// </summary>
    /// <remarks>
    /// Required.
    /// </remarks>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the CloudWatch metric namespace the published metrics are grouped under.
    /// </summary>
    /// <remarks>
    /// Required. Must be 1-255 characters, contain only valid CloudWatch namespace characters
    /// (ASCII alphanumerics and <c>. - _ / # :</c>), and must not start with the reserved <c>AWS/</c> prefix.
    /// </remarks>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a custom CloudWatch service endpoint, used instead of the regional AWS endpoint.
    /// </summary>
    /// <remarks>
    /// Optional. Useful to target a VPC endpoint or a CloudWatch-compatible service, e.g. LocalStack, during testing.
    /// </remarks>
    public Uri? ServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the AWS access key id used to authenticate against CloudWatch.
    /// </summary>
    /// <remarks>
    /// Optional. Must be used together with <see cref="SecretAccessKey"/>. When not set, the default AWS
    /// credential resolution chain is used instead.
    /// </remarks>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// Gets or sets the AWS secret access key used to authenticate against CloudWatch.
    /// </summary>
    /// <remarks>
    /// Optional. Must be used together with <see cref="AccessKeyId"/>.
    /// </remarks>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="Environment.MachineName"/> as a CloudWatch dimension, useful to
    /// distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;
}
