namespace NetEvolve.HealthPublishers.Tests.Integration.AWS.CloudWatch;

using System;
using System.Threading.Tasks;
using Testcontainers.Floci;
using TUnit.Core.Interfaces;

/// <summary>
/// Wraps the official <c>Testcontainers.Floci</c> module, pinned to a specific Floci release for
/// reproducible builds, exposing the CloudWatch-compatible endpoint used by the integration tests.
/// </summary>
public sealed class CloudWatchFlociContainer : IAsyncInitializer, IAsyncDisposable
{
    private const string Image = /*dockerimage*/
        "floci/floci:1.7.0";

    /// <summary>
    /// Dummy access key id accepted by Floci; no real AWS account is involved.
    /// </summary>
    public const string AccessKeyId = "test";

    /// <summary>
    /// Dummy secret access key accepted by Floci; no real AWS account is involved.
    /// </summary>
    public const string SecretAccessKey = "test";

    /// <summary>
    /// The AWS region system name the Floci container is configured for.
    /// </summary>
    public const string Region = "eu-central-1";

    private readonly FlociContainer _container = new FlociBuilder(Image).Build();

    /// <summary>
    /// Gets the base address of the Floci edge endpoint, which multiplexes all emulated AWS services,
    /// including CloudWatch.
    /// </summary>
    public Uri ServiceUrl => new(_container.GetConnectionString());

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);
}
