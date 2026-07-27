namespace NetEvolve.HealthPublishers.Tests.Integration.AWS.CloudWatch;

using System;
using System.Threading.Tasks;
using Testcontainers.LocalStack;
using TUnit.Core.Interfaces;

/// <summary>
/// Wraps the official <c>Testcontainers.LocalStack</c> module, pinned to a specific LocalStack release for
/// reproducible builds, exposing the CloudWatch-compatible endpoint used by the integration tests.
/// </summary>
public sealed class CloudWatchLocalStackContainer : IAsyncInitializer, IAsyncDisposable
{
    private const string Image = "localstack/localstack:4.9.1";

    /// <summary>
    /// Dummy access key id accepted by LocalStack; no real AWS account is involved.
    /// </summary>
    public const string AccessKeyId = "test";

    /// <summary>
    /// Dummy secret access key accepted by LocalStack; no real AWS account is involved.
    /// </summary>
    public const string SecretAccessKey = "test";

    /// <summary>
    /// The AWS region system name the LocalStack container is configured for.
    /// </summary>
    public const string Region = "eu-central-1";

    private readonly LocalStackContainer _container = new LocalStackBuilder(Image).Build();

    /// <summary>
    /// Gets the base address of the LocalStack edge endpoint, which multiplexes all emulated AWS services,
    /// including CloudWatch.
    /// </summary>
    public Uri ServiceUrl => new(_container.GetConnectionString());

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);
}
