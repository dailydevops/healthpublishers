namespace NetEvolve.HealthPublishers.Tests.Integration.Email;

using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TUnit.Core.Interfaces;

/// <summary>
/// A bespoke <see cref="IContainer"/> hosting the <c>axllent/mailpit</c> image, since no official Testcontainers
/// module exists for Mailpit (or any other SMTP test server) yet.
/// </summary>
/// <remarks>
/// The container accepts any username/password combination for SMTP AUTH (<c>MP_SMTP_AUTH_ACCEPT_ANY</c> /
/// <c>MP_SMTP_AUTH_ALLOW_INSECURE</c>), so the same instance can be used both for unauthenticated and
/// authenticated integration test scenarios.
/// </remarks>
public sealed class MailpitContainer : IAsyncInitializer, IAsyncDisposable
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.22")
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(HttpPort, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/livez"))
        )
        .Build();

    /// <summary>
    /// Gets the hostname of the SMTP server exposed by the container.
    /// </summary>
    public string SmtpHost => _container.Hostname;

    /// <summary>
    /// Gets the mapped public port of the SMTP server exposed by the container.
    /// </summary>
    public int SmtpPortMapped => _container.GetMappedPublicPort(SmtpPort);

    /// <summary>
    /// Gets the base address of the Mailpit HTTP API/UI exposed by the container.
    /// </summary>
#pragma warning disable S5332 // Using http protocol is insecure - intentional for the test-only Mailpit API endpoint.
    public Uri ApiBaseAddress => new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}");
#pragma warning restore S5332

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);
}
