namespace NetEvolve.HealthPublishers.Tests.Integration.Prometheus.Metrics;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using global::Prometheus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Prometheus.Metrics;

[TestGroup($"{nameof(Prometheus)}.{nameof(Metrics)}")]
public sealed class PrometheusMetricsHealthCheckPublisherTests
{
    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, registry) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");
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
        await VerifyRegistry(registry, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, registry) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");
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
        await VerifyRegistry(registry, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, registry) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");
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
        await VerifyRegistry(registry, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, registry) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");
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
        await VerifyRegistry(registry, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Prometheus:Metrics:Default:SystemIdentifier", "integration-tests" },
        };
        var (publisher, registry) = CreatePublisher(configureConfiguration: config =>
            config.AddInMemoryCollection(values)
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyRegistry(registry, cancellationToken);
    }

    [Test]
    public void AddPrometheusMetricsPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddPrometheusMetricsPublisher(name, options => options.SystemIdentifier = "integration-tests")
                .AddPrometheusMetricsPublisher(name, options => options.SystemIdentifier = "integration-tests");

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddPrometheusMetricsPublisher_WhenRegisteredWithDifferentNames_KeepsRegistriesIsolated(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddPrometheusMetricsPublisher("Internal", options => options.SystemIdentifier = "internal-system");
        _ = builder.AddPrometheusMetricsPublisher("External", options => options.SystemIdentifier = "external-system");

        var provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>().ToArray();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, cancellationToken);
        }

        // Assert
        var internalRegistry = provider.GetRequiredKeyedService<CollectorRegistry>("Internal");
        var externalRegistry = provider.GetRequiredKeyedService<CollectorRegistry>("External");

        var internalText = await ExportAsTextAsync(internalRegistry, cancellationToken);
        var externalText = await ExportAsTextAsync(externalRegistry, cancellationToken);

        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalText).Contains("system_identifier=\"internal-system\"");
            _ = await Assert.That(internalText).DoesNotContain("external-system");
            _ = await Assert.That(externalText).Contains("system_identifier=\"external-system\"");
            _ = await Assert.That(externalText).DoesNotContain("internal-system");
        }
    }

    [Test]
    public async Task AddPrometheusMetricsPublisher_WhenRegisteredViaHealthChecksPipeline_RecordsRealHealthReport(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddPrometheusMetricsPublisher(options => options.SystemIdentifier = "integration-tests");

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IHealthCheckPublisher>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var registry = provider.GetRequiredKeyedService<CollectorRegistry>(DependencyInjectionExtensions.DefaultName);
        var text = await ExportAsTextAsync(registry, cancellationToken);

        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            _ = await Assert.That(text).Contains("check=\"self\"");
            _ = await Assert.That(text).Contains("healthcheck_report_status");
        }
    }

    [Test]
    public async Task PublishAsync_WhenEntriesChurnAcrossManyPublishes_OnlyLatestReportEntriesRemainInRegistry(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, registry) = CreatePublisher(options => options.SystemIdentifier = "integration-tests");

        // Each wave overlaps partially with the previous one, so some checks persist, some disappear, some
        // reappear later, and some show up for the first time, stressing RemoveStaleEntries across many publishes.
        string[][] waves =
        [
            ["database", "cache", "queue"],
            ["database", "queue", "search"],
            ["search"],
            ["database", "cache"],
            ["queue", "search", "cache", "gateway"],
            ["gateway"],
        ];

        // Act
        foreach (var wave in waves)
        {
            var entries = new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal);
            foreach (var check in wave)
            {
                entries[check] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(1L),
                    null,
                    null
                );
            }

            var report = new HealthReport(entries, TimeSpan.FromMilliseconds(1L));
            await publisher.PublishAsync(report, cancellationToken);
        }

        // Assert
        var text = await ExportAsTextAsync(registry, cancellationToken);
        var lastWave = waves[^1];
        var staleChecks = waves.SelectMany(wave => wave).Distinct(StringComparer.Ordinal).Except(lastWave);

        using (Assert.Multiple())
        {
            foreach (var check in lastWave)
            {
                _ = await Assert.That(text).Contains($"check=\"{check}\"");
            }

            foreach (var check in staleChecks)
            {
                _ = await Assert.That(text).DoesNotContain($"check=\"{check}\"");
            }
        }
    }

    private static readonly Regex LastPublishTimestampLine = new(
        @"^healthcheck_last_publish_timestamp_seconds\{[^}]*\} \d+$",
        RegexOptions.Multiline | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1L)
    );

    private static async Task<string> ExportAsTextAsync(
        CollectorRegistry registry,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream, cancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task VerifyRegistry(CollectorRegistry registry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = await ExportAsTextAsync(registry, cancellationToken);

        using (Assert.Multiple())
        {
            _ = await Assert.That(text).Contains($"machine_name=\"{Environment.MachineName}\"");
            _ = await Assert.That(LastPublishTimestampLine.IsMatch(text)).IsTrue();
        }

        // machine_name is excluded: it varies per environment and would break the snapshot elsewhere.
        var normalized = text.Replace(
            $"machine_name=\"{Environment.MachineName}\"",
            "machine_name=\"placeholder\"",
            StringComparison.Ordinal
        );

        // The last-publish timestamp is excluded too: it is the current unix time and changes on every run.
        normalized = LastPublishTimestampLine.Replace(
            normalized,
            match => match.Value[..match.Value.LastIndexOf(' ')] + " <timestamp>"
        );

        _ = await Verify(normalized).IgnoreParametersForVerified();
    }

    private static (IHealthCheckPublisher Publisher, CollectorRegistry Registry) CreatePublisher(
        Action<PrometheusMetricsOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddPrometheusMetricsPublisher(options);

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredKeyedService<CollectorRegistry>(DependencyInjectionExtensions.DefaultName);

        return (provider.GetRequiredService<IHealthCheckPublisher>(), registry);
    }
}
