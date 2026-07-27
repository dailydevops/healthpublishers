namespace NetEvolve.HealthPublishers.Tests.Integration.Email;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
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
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifySentMessage(systemIdentifier);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds()
    {
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
                    TimeSpan.FromMilliseconds(5),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifySentMessage(systemIdentifier);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds()
    {
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
                    TimeSpan.FromMilliseconds(5),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifySentMessage(systemIdentifier);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds()
    {
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
                    TimeSpan.FromMilliseconds(3),
                    null,
                    null,
                    tags: ["db", "sql"]
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null,
                    tags: ["cache"]
                ),
            },
            TimeSpan.FromMilliseconds(123)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifySentMessage(systemIdentifier);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds()
    {
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
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifySentMessage(systemIdentifier);
    }

    [Test]
    public async Task PublishAsync_WhenCredentialsConfigured_AuthenticatesAndSucceeds()
    {
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
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifySentMessage(systemIdentifier);
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
    public async Task AddEmailPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
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

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }

        // Assert
        var internalMessage = await _api.FindSingleMessageAsync(internalIdentifier, CancellationToken.None);
        var externalMessage = await _api.FindSingleMessageAsync(externalIdentifier, CancellationToken.None);
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalMessage.To.Single().Address).IsEqualTo("internal-ops@example.com");
            _ = await Assert.That(externalMessage.To.Single().Address).IsEqualTo("external-ops@example.com");
        }
    }

    private static string CreateSystemIdentifier() => $"integration-tests-{Guid.NewGuid():N}";

    private async Task VerifySentMessage(string systemIdentifier)
    {
        var message = await _api.FindSingleMessageAsync(systemIdentifier, CancellationToken.None);

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
            Text = message
                .Text.Replace(systemIdentifier, "<system-identifier>", StringComparison.Ordinal)
                .Replace(Environment.MachineName, "<machine-name>", StringComparison.Ordinal),
        };

    private static IHealthCheckPublisher CreatePublisher(
        Action<EmailOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddEmailPublisher(options);

        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IHealthCheckPublisher>();
    }
}
