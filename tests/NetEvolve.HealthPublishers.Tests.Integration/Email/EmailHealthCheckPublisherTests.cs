namespace NetEvolve.HealthPublishers.Tests.Integration.Email;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Email;

[TestGroup(nameof(Email))]
[ClassDataSource<MailpitContainer>(Shared = SharedType.PerClass)]
public sealed class EmailHealthCheckPublisherTests : IDisposable
{
    private readonly MailpitContainer _container;
    private readonly MailpitApiClient _api;

    public EmailHealthCheckPublisherTests(MailpitContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        _container = container;
        _api = new MailpitApiClient(container.ApiBaseAddress);
    }

    public void Dispose() => _api.Dispose();

    [Test]
    public async Task PublishAsync_UseOptions_FreshPublisherHealthyReport_DoesNotSend(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange - a fresh publisher's baseline is Healthy, so a first Healthy report is a no-op, not a send.
        var systemIdentifier = CreateSystemIdentifier();
        var publisher = CreatePublisher(options =>
        {
            options.Host = _container.SmtpHost;
            options.Port = _container.SmtpPortMapped;
            options.From = "health-checks@example.com";
            options.To = ["ops-team@example.com"];
            options.SystemIdentifier = systemIdentifier;
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5L), null, null),
            },
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var count = await _api.CountMessagesAsync(systemIdentifier, cancellationToken);
        _ = await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var systemIdentifier = CreateSystemIdentifier();
        var publisher = CreatePublisher(options =>
        {
            options.Host = _container.SmtpHost;
            options.Port = _container.SmtpPortMapped;
            options.From = "health-checks@example.com";
            options.To = ["ops-team@example.com"];
            options.SystemIdentifier = systemIdentifier;
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow",
                    TimeSpan.FromMilliseconds(5L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifySentMessage(systemIdentifier, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var systemIdentifier = CreateSystemIdentifier();
        var publisher = CreatePublisher(options =>
        {
            options.Host = _container.SmtpHost;
            options.Port = _container.SmtpPortMapped;
            options.From = "health-checks@example.com";
            options.To = ["ops-team@example.com"];
            options.SystemIdentifier = systemIdentifier;
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "boom",
                    TimeSpan.FromMilliseconds(5L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifySentMessage(systemIdentifier, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var systemIdentifier = CreateSystemIdentifier();
        var publisher = CreatePublisher(options =>
        {
            options.Host = _container.SmtpHost;
            options.Port = _container.SmtpPortMapped;
            options.From = "health-checks@example.com";
            options.To = ["ops-team@example.com"];
            options.SystemIdentifier = systemIdentifier;
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null,
                    tags: ["db", "sql"]
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120L),
                    null,
                    null,
                    tags: ["cache"]
                ),
            },
            TimeSpan.FromMilliseconds(123L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifySentMessage(systemIdentifier, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_UnhealthyReport_Succeeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var systemIdentifier = CreateSystemIdentifier();
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Email:Default:Host", _container.SmtpHost },
            {
                "HealthPublishers:Email:Default:Port",
                _container.SmtpPortMapped.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            { "HealthPublishers:Email:Default:From", "health-checks@example.com" },
            { "HealthPublishers:Email:Default:To:0", "ops-team@example.com" },
            { "HealthPublishers:Email:Default:SystemIdentifier", systemIdentifier },
        };
        var publisher = CreatePublisher(configureConfiguration: config => config.AddInMemoryCollection(values));
        // A fresh publisher's baseline is Healthy, so the report must be a worsening to send immediately.
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifySentMessage(systemIdentifier, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_WhenCredentialsConfigured_AuthenticatesAndSucceeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var systemIdentifier = CreateSystemIdentifier();
        var publisher = CreatePublisher(options =>
        {
            options.Host = _container.SmtpHost;
            options.Port = _container.SmtpPortMapped;
            options.From = "health-checks@example.com";
            options.To = ["ops-team@example.com"];
            options.SystemIdentifier = systemIdentifier;
            options.Username = "integration-tests";
            options.Password = "integration-tests";
        });
        // A fresh publisher's baseline is Healthy, so the report must be a worsening to send immediately.
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifySentMessage(systemIdentifier, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_WhenStatusImprovesAfterWorsening_WaitsForRecoveryConfirmationDelayBeforeSending(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var systemIdentifier = CreateSystemIdentifier();
        var timeProvider = new FakeTimeProvider();
        var delay = TimeSpan.FromMinutes(5L);
        var publisher = CreatePublisher(
            options =>
            {
                options.Host = _container.SmtpHost;
                options.Port = _container.SmtpPortMapped;
                options.From = "health-checks@example.com";
                options.To = ["ops-team@example.com"];
                options.SystemIdentifier = systemIdentifier;
                options.RecoveryConfirmationDelay = delay;
            },
            timeProvider: timeProvider
        );
        var unhealthyReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );
        var healthyReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Healthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act & Assert - the worsening sends immediately.
        await publisher.PublishAsync(unhealthyReport, cancellationToken);
        _ = await Assert.That(await _api.CountMessagesAsync(systemIdentifier, cancellationToken)).IsEqualTo(1);

        // The subsequent improvement does not send right away - it only starts the recovery-confirmation timer.
        await publisher.PublishAsync(healthyReport, cancellationToken);
        _ = await Assert.That(await _api.CountMessagesAsync(systemIdentifier, cancellationToken)).IsEqualTo(1);

        // Once the configured delay has elapsed, the still-improved status is finally reported.
        timeProvider.Advance(delay);
        await publisher.PublishAsync(healthyReport, cancellationToken);
        var count = await _api.CountMessagesAsync(systemIdentifier, cancellationToken);
        _ = await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public void AddEmailPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddEmailPublisher(
                    name,
                    options =>
                    {
                        options.Host = _container.SmtpHost;
                        options.Port = _container.SmtpPortMapped;
                        options.From = "health-checks@example.com";
                        options.To = ["ops-team@example.com"];
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddEmailPublisher(
                    name,
                    options =>
                    {
                        options.Host = _container.SmtpHost;
                        options.Port = _container.SmtpPortMapped;
                        options.From = "health-checks@example.com";
                        options.To = ["ops-team@example.com"];
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddEmailPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var internalIdentifier = CreateSystemIdentifier();
        var externalIdentifier = CreateSystemIdentifier();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddEmailPublisher(
            "Internal",
            options =>
            {
                options.Host = _container.SmtpHost;
                options.Port = _container.SmtpPortMapped;
                options.From = "health-checks@example.com";
                options.To = ["internal-ops@example.com"];
                options.SystemIdentifier = internalIdentifier;
            }
        );
        _ = builder.AddEmailPublisher(
            "External",
            options =>
            {
                options.Host = _container.SmtpHost;
                options.Port = _container.SmtpPortMapped;
                options.From = "health-checks@example.com";
                options.To = ["external-ops@example.com"];
                options.SystemIdentifier = externalIdentifier;
            }
        );

        var provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>().ToArray();

        // A fresh publisher's baseline is Healthy, so the report must be a worsening to send immediately.
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, cancellationToken);
        }

        // Assert
        var internalMessage = await _api.FindSingleMessageAsync(internalIdentifier, cancellationToken);
        var externalMessage = await _api.FindSingleMessageAsync(externalIdentifier, cancellationToken);
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalMessage.To.Single().Address).IsEqualTo("internal-ops@example.com");
            _ = await Assert.That(externalMessage.To.Single().Address).IsEqualTo("external-ops@example.com");
        }
    }

    private static string CreateSystemIdentifier() => $"integration-tests-{Guid.NewGuid():N}";

    private async Task VerifySentMessage(string systemIdentifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = await _api.FindSingleMessageAsync(systemIdentifier, cancellationToken);

        using (Assert.Multiple())
        {
            _ = await Assert.That(message.From!.Address).IsEqualTo("health-checks@example.com");
            _ = await Assert.That(message.To.Any(to => to.Address == "ops-team@example.com")).IsTrue();
            _ = await Assert.That(message.Text).Contains($"Machine: {Environment.MachineName}");
            _ = await Assert.That(message.Text).Contains($"System: {systemIdentifier}");
        }

        _ = await Verify(Normalize(message, systemIdentifier)).IgnoreParametersForVerified();
    }

    private static object Normalize(MailpitMessage message, string systemIdentifier) =>
        new
        {
            Subject = message.Subject.Replace(systemIdentifier, "<system-identifier>", StringComparison.Ordinal),
            Text = Regex
                .Replace(
                    message.Text,
                    "Timestamp: .*",
                    "Timestamp: <timestamp>",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1L)
                )
                .Replace(systemIdentifier, "<system-identifier>", StringComparison.Ordinal)
                .Replace(Environment.MachineName, "<machine-name>", StringComparison.Ordinal),
        };

    private static IHealthCheckPublisher CreatePublisher(
        Action<EmailOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null,
        TimeProvider? timeProvider = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // AddEmailPublisher only registers TimeProvider.System via TryAddSingleton, so registering one upfront lets
        // tests control time (e.g. to assert the RecoveryConfirmationDelay behavior without a real wait).
        if (timeProvider is not null)
        {
            _ = services.AddSingleton(timeProvider);
        }

        _ = builder.AddEmailPublisher(options);

        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IHealthCheckPublisher>();
    }
}
