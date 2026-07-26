namespace NetEvolve.HealthPublishers.Tests.Integration.OpenTelemetry;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.OpenTelemetry;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(OpenTelemetry))]
public sealed class OpenTelemetryHealthCheckPublisherTests
{
    [Test]
    [Arguments(HealthStatus.Healthy)]
    [Arguments(HealthStatus.Degraded)]
    [Arguments(HealthStatus.Unhealthy)]
    public async Task PublishAsync_UseOptions_RecordsReportDurationWithStatusTag(HealthStatus status)
    {
        // Arrange
        var (publisher, recorder) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(status, "details", TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var measurement = recorder.Measurements.Single(m => m.InstrumentName == "healthchecks.report.duration");
        using (Assert.Multiple())
        {
            _ = await Assert.That(measurement.Value).IsEqualTo(42d);
            _ = await Assert.That(measurement.Tags["healthchecks.status"]).IsEqualTo(status.ToString());
            _ = await Assert.That(measurement.Tags["healthchecks.system.identifier"]).IsEqualTo("integration-tests");
            _ = await Assert.That(measurement.Tags["healthchecks.machine.name"]).IsEqualTo(Environment.MachineName);
        }
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_RecordsEntryDurationForEachEntry()
    {
        // Arrange
        var (publisher, recorder) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");
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
        var entryMeasurements = recorder
            .Measurements.Where(m => m.InstrumentName == "healthchecks.entry.duration")
            .ToDictionary(m => m.Tags["healthchecks.entry.name"]!, m => m, StringComparer.Ordinal);

        using (Assert.Multiple())
        {
            _ = await Assert.That(entryMeasurements.Count).IsEqualTo(2);
            _ = await Assert.That(entryMeasurements["database"].Value).IsEqualTo(3d);
            _ = await Assert.That(entryMeasurements["database"].Tags["healthchecks.status"]).IsEqualTo("Healthy");
            _ = await Assert.That(entryMeasurements["cache"].Value).IsEqualTo(120d);
            _ = await Assert.That(entryMeasurements["cache"].Tags["healthchecks.status"]).IsEqualTo("Degraded");
        }
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_RecordsMetricsFromConfigurationBoundOptions()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:OpenTelemetry:Default:SystemIdentifier", "integration-tests" },
        };
        var (publisher, recorder) = CreatePublisher(configureConfiguration: config =>
            config.AddInMemoryCollection(values)
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var measurement = recorder.Measurements.Single(m => m.InstrumentName == "healthchecks.report.duration");
        _ = await Assert.That(measurement.Tags["healthchecks.system.identifier"]).IsEqualTo("integration-tests");
    }

    [Test]
    public void AddOpenTelemetryPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddOpenTelemetryPublisher(name, options => options.SystemIdentifier = "integration-tests")
                .AddOpenTelemetryPublisher(name, options => options.SystemIdentifier = "integration-tests");

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddOpenTelemetryPublisher_WhenRegisteredWithDifferentNames_TagsMeasurementsWithRespectivePublisherName()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddOpenTelemetryPublisher("Internal", options => options.SystemIdentifier = "internal-system");
        _ = builder.AddOpenTelemetryPublisher("External", options => options.SystemIdentifier = "external-system");

        var provider = services.BuildServiceProvider();
        using var recorder = new MetricsRecorder(provider.GetRequiredService<Meter>());
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
        var reportMeasurements = recorder
            .Measurements.Where(m => m.InstrumentName == "healthchecks.report.duration")
            .ToDictionary(m => m.Tags["healthchecks.publisher.name"]!, m => m, StringComparer.Ordinal);

        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(reportMeasurements.Count).IsEqualTo(2);
            _ = await Assert
                .That(reportMeasurements["Internal"].Tags["healthchecks.system.identifier"])
                .IsEqualTo("internal-system");
            _ = await Assert
                .That(reportMeasurements["External"].Tags["healthchecks.system.identifier"])
                .IsEqualTo("external-system");
        }
    }

    [Test]
    public async Task AddOpenTelemetryPublisher_WhenRegisteredViaHealthChecksPipeline_RecordsRealHealthReport()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddOpenTelemetryPublisher(options => options.SystemIdentifier = "integration-tests");

        var provider = services.BuildServiceProvider();
        using var recorder = new MetricsRecorder(provider.GetRequiredService<Meter>());
        var publisher = provider.GetRequiredService<IHealthCheckPublisher>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(CancellationToken.None);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            _ = await Assert
                .That(recorder.Measurements.Any(m => m.InstrumentName == "healthchecks.report.duration"))
                .IsTrue();
            _ = await Assert
                .That(
                    recorder.Measurements.Single(m => m.InstrumentName == "healthchecks.entry.duration").Tags[
                        "healthchecks.entry.name"
                    ]
                )
                .IsEqualTo("self");
        }
    }

    private static (IHealthCheckPublisher Publisher, MetricsRecorder Recorder) CreatePublisher(
        Action<OpenTelemetryOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddOpenTelemetryPublisher(options);

        var provider = services.BuildServiceProvider();
        var recorder = new MetricsRecorder(provider.GetRequiredService<Meter>());

        return (provider.GetRequiredService<IHealthCheckPublisher>(), recorder);
    }
}
