namespace NetEvolve.HealthPublishers.Tests.Integration.ApplicationInsights;

using System.Globalization;
using System.Threading;
using global::OpenTelemetry.Logs;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.ApplicationInsights;

[TestGroup(nameof(ApplicationInsights))]
public sealed class ApplicationInsightsHealthCheckPublisherTests
{
    private const string TestConnectionString = "InstrumentationKey=11111111-1111-1111-1111-111111111111";

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, channel) = CreatePublisher(options =>
        {
            options.ConnectionString = TestConnectionString;
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedTelemetry(channel);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, channel) = CreatePublisher(options =>
        {
            options.ConnectionString = TestConnectionString;
            options.SystemIdentifier = "integration-tests";
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedTelemetry(channel);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, channel) = CreatePublisher(options =>
        {
            options.ConnectionString = TestConnectionString;
            options.SystemIdentifier = "integration-tests";
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedTelemetry(channel);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, channel) = CreatePublisher(options =>
        {
            options.ConnectionString = TestConnectionString;
            options.SystemIdentifier = "integration-tests";
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedTelemetry(channel);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:ApplicationInsights:Default:ConnectionString", TestConnectionString },
            { "HealthPublishers:ApplicationInsights:Default:SystemIdentifier", "integration-tests" },
        };
        var (publisher, channel) = CreatePublisher(configureConfiguration: config =>
            config.AddInMemoryCollection(values)
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedTelemetry(channel);
    }

    [Test]
    public void AddApplicationInsightsPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddApplicationInsightsPublisher(
                    name,
                    options =>
                    {
                        options.ConnectionString = TestConnectionString;
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddApplicationInsightsPublisher(
                    name,
                    options =>
                    {
                        options.ConnectionString = TestConnectionString;
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddApplicationInsightsPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddApplicationInsightsPublisher(
            "Internal",
            options =>
            {
                options.ConnectionString = TestConnectionString;
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddApplicationInsightsPublisher(
            "External",
            options =>
            {
                options.ConnectionString = TestConnectionString;
                options.SystemIdentifier = "external-system";
            }
        );

        var internalChannel = new TestTelemetryChannel();
        var externalChannel = new TestTelemetryChannel();
#pragma warning disable CA2000 // Disposed together with the DI-owned TelemetryConfiguration
        _ = services.AddKeyedSingleton(
            "Internal",
            (_, _) =>
            {
                var configuration = new TelemetryConfiguration { ConnectionString = TestConnectionString };
                internalChannel.Configure(configuration);
                return configuration;
            }
        );
        _ = services.AddKeyedSingleton(
            "External",
            (_, _) =>
            {
                var configuration = new TelemetryConfiguration { ConnectionString = TestConnectionString };
                externalChannel.Configure(configuration);
                return configuration;
            }
        );
#pragma warning restore CA2000

        var provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>().ToArray();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, cancellationToken);
        }

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert
                .That(internalChannel.LogRecords.Single().GetAttribute("SystemIdentifier"))
                .IsEqualTo("internal-system");
            _ = await Assert
                .That(externalChannel.LogRecords.Single().GetAttribute("SystemIdentifier"))
                .IsEqualTo("external-system");
        }
    }

    [Test]
    public async Task AddApplicationInsightsPublisher_WhenRegisteredViaHealthChecksPipeline_PublishesRealHealthReport(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddApplicationInsightsPublisher(options =>
            {
                options.ConnectionString = TestConnectionString;
                options.SystemIdentifier = "integration-tests";
            });

        var channel = new TestTelemetryChannel();
#pragma warning disable CA2000 // Disposed together with the DI-owned TelemetryConfiguration
        _ = services.AddKeyedSingleton(
            DependencyInjectionExtensions.DefaultName,
            (_, _) =>
            {
                var configuration = new TelemetryConfiguration { ConnectionString = TestConnectionString };
                channel.Configure(configuration);
                return configuration;
            }
        );
#pragma warning restore CA2000

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IHealthCheckPublisher>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            var telemetry = channel.LogRecords.Single();
            _ = await Assert.That(telemetry.GetAvailabilityAttribute("success")).IsEqualTo(bool.TrueString);
        }
    }

    private static async Task VerifyCapturedTelemetry(TestTelemetryChannel channel)
    {
        var telemetry = channel.LogRecords.Single();

        using (Assert.Multiple())
        {
            _ = await Assert.That(telemetry.GetAvailabilityAttribute("runLocation")).IsEqualTo(Environment.MachineName);
            _ = await Assert.That(telemetry.GetAttribute("MachineName")).IsEqualTo(Environment.MachineName);
        }

        _ = await Verify(Normalize(telemetry)).IgnoreParametersForVerified();
    }

    private static object Normalize(LogRecord telemetry) =>
        new
        {
            Duration = TimeSpan.Parse(telemetry.GetAvailabilityAttribute("duration")!, CultureInfo.InvariantCulture),
            // MachineName is excluded: it varies per environment and would break the snapshot elsewhere.
            Entries = telemetry.GetAttribute("Entries"),
            Message = telemetry.GetAvailabilityAttribute("message"),
            Name = telemetry.GetAvailabilityAttribute("name"),
            Success = bool.Parse(telemetry.GetAvailabilityAttribute("success")!),
            SystemIdentifier = telemetry.GetAttribute("SystemIdentifier"),
        };

    private static (IHealthCheckPublisher Publisher, TestTelemetryChannel Channel) CreatePublisher(
        Action<ApplicationInsightsOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddApplicationInsightsPublisher(options);

        var channel = new TestTelemetryChannel();
#pragma warning disable CA2000 // Disposed together with the DI-owned TelemetryConfiguration
        _ = services.AddKeyedSingleton(
            DependencyInjectionExtensions.DefaultName,
            (_, _) =>
            {
                var telemetryConfiguration = new TelemetryConfiguration { ConnectionString = TestConnectionString };
                channel.Configure(telemetryConfiguration);
                return telemetryConfiguration;
            }
        );
#pragma warning restore CA2000

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<IHealthCheckPublisher>(), channel);
    }
}
