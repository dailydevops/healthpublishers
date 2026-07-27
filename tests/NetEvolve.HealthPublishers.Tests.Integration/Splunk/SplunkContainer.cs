namespace NetEvolve.HealthPublishers.Tests.Integration.Splunk;

using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TUnit.Core.Interfaces;

/// <summary>
/// A bespoke <see cref="IContainer"/> hosting the official <c>splunk/splunk</c> image with the HTTP Event
/// Collector (HEC) enabled, since no dedicated Testcontainers module exists for Splunk.
/// </summary>
public sealed class SplunkContainer : IAsyncInitializer, IAsyncDisposable
{
    /// <summary>
    /// The HEC token configured on the container, used by tests to authenticate against it.
    /// </summary>
    public const string HecToken = "00000000-0000-0000-0000-000000000000";

    private const int HecPort = 8088;

    private readonly IContainer _container = new ContainerBuilder("splunk/splunk:9.3")
        .WithEnvironment("SPLUNK_START_ARGS", "--accept-license")
        .WithEnvironment("SPLUNK_PASSWORD", "Integration-Tests-Passw0rd!")
        .WithEnvironment("SPLUNK_HEC_TOKEN", HecToken)
        .WithEnvironment("SPLUNK_HEC_SSL", "False")
        .WithPortBinding(HecPort, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(HecPort).ForPath("/services/collector/health"))
        )
        .Build();

    /// <summary>
    /// Gets the base address of the HEC endpoint exposed by the container.
    /// </summary>
    /// <remarks>
    /// The container is started with <c>SPLUNK_HEC_SSL=False</c>, so HEC is reached over plain HTTP; this is
    /// only used for local/CI integration testing, never in production.
    /// </remarks>
#pragma warning disable S5332 // Using http protocol is insecure - intentional for the test-only HEC endpoint.
    public Uri ServerUrl => new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HecPort)}");
#pragma warning restore S5332

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);
}
