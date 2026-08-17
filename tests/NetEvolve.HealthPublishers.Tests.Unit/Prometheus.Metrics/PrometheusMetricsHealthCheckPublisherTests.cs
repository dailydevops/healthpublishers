namespace NetEvolve.HealthPublishers.Tests.Unit.Prometheus.Metrics;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Prometheus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Prometheus.Metrics;

[TestGroup($"{nameof(Prometheus)}.{nameof(Metrics)}")]
public sealed class PrometheusMetricsHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy, 2)]
    [Arguments(HealthStatus.Degraded, 1)]
    [Arguments(HealthStatus.Unhealthy, 0)]
    public async Task PublishAsync_WhenReportHasStatus_SetsReportStatusGauge(
        HealthStatus status,
        int expected,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, instruments, _) = CreatePublisher(options => options.SystemIdentifier = "checkout-service");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(status, null, TimeSpan.FromMilliseconds(5L), null, null),
            },
            TimeSpan.FromMilliseconds(42L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var gauge = instruments.ReportStatus.WithLabels("checkout-service", Environment.MachineName);
        _ = await Assert.That(gauge.Value).IsEqualTo(expected);
    }

    [Test]
    public async Task PublishAsync_WhenReportHasDuration_SetsReportDurationGaugeInSeconds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, instruments, _) = CreatePublisher(options => options.SystemIdentifier = "checkout-service");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(500L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var gauge = instruments.ReportDuration.WithLabels("checkout-service", Environment.MachineName);
        _ = await Assert.That(gauge.Value).IsEqualTo(0.5d);
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsLastPublishTimestampFromTimeProvider(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var (publisher, instruments, _) = CreatePublisher(
            options => options.SystemIdentifier = "checkout-service",
            timeProvider
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var gauge = instruments.LastPublishTimestamp.WithLabels("checkout-service", Environment.MachineName);
        _ = await Assert.That(gauge.Value).IsEqualTo(timeProvider.GetUtcNow().ToUnixTimeSeconds());
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_SetsEntryStatusAndDurationGauges(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, instruments, _) = CreatePublisher(options => options.SystemIdentifier = "checkout-service");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(123L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(
                    instruments
                        .EntryStatus.WithLabels("database", string.Empty, "checkout-service", Environment.MachineName)
                        .Value
                )
                .IsEqualTo(2D);
            _ = await Assert
                .That(
                    instruments
                        .EntryDuration.WithLabels("database", string.Empty, "checkout-service", Environment.MachineName)
                        .Value
                )
                .IsEqualTo(0.003d);
            _ = await Assert
                .That(
                    instruments
                        .EntryStatus.WithLabels("cache", "slow response", "checkout-service", Environment.MachineName)
                        .Value
                )
                .IsEqualTo(1D);
            _ = await Assert
                .That(
                    instruments
                        .EntryDuration.WithLabels("cache", "slow response", "checkout-service", Environment.MachineName)
                        .Value
                )
                .IsEqualTo(0.12d);
        }
    }

    [Test]
    public async Task PublishAsync_WhenEntryDisappearsFromLaterReport_RemovesStaleEntryGaugeSeries(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, _, registry) = CreatePublisher(options => options.SystemIdentifier = "checkout-service");
        var firstReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(3L)
        );
        var secondReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.Zero
        );

        // Act
        await publisher.PublishAsync(firstReport, cancellationToken);
        await publisher.PublishAsync(secondReport, cancellationToken);

        // Assert
        var text = await ExportAsTextAsync(registry, cancellationToken);
        _ = await Assert.That(text).DoesNotContain("check=\"database\"");
    }

    [Test]
    public async Task PublishAsync_WhenEntryDescriptionChangesBetweenReports_RemovesStalePreviousDescriptionSeries(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, _, registry) = CreatePublisher(options => options.SystemIdentifier = "checkout-service");
        var firstReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "boom",
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(3L)
        );
        var secondReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(3L)
        );

        // Act
        await publisher.PublishAsync(firstReport, cancellationToken);
        await publisher.PublishAsync(secondReport, cancellationToken);

        // Assert
        var text = await ExportAsTextAsync(registry, cancellationToken);
        using (Assert.Multiple())
        {
            _ = await Assert.That(text).DoesNotContain("description=\"boom\"");
            _ = await Assert.That(text).Contains("description=\"\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenSomeEntriesPersist_RemovesOnlyStaleEntryAndKeepsCurrentOnes(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, instruments, registry) = CreatePublisher(options =>
            options.SystemIdentifier = "checkout-service"
        );
        var firstReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(123L)
        );
        var secondReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(3L)
        );

        // Act
        await publisher.PublishAsync(firstReport, cancellationToken);
        await publisher.PublishAsync(secondReport, cancellationToken);

        // Assert
        var text = await ExportAsTextAsync(registry, cancellationToken);
        using (Assert.Multiple())
        {
            _ = await Assert.That(text).DoesNotContain("check=\"cache\"");
            _ = await Assert.That(text).Contains("check=\"database\"");
            _ = await Assert
                .That(
                    instruments
                        .EntryStatus.WithLabels("database", string.Empty, "checkout-service", Environment.MachineName)
                        .Value
                )
                .IsEqualTo(2D);
        }
    }

    [Test]
    public async Task PublishAsync_WhenSameEntriesRepeatAcrossPublishes_DoesNotRemoveThem(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, _, registry) = CreatePublisher(options => options.SystemIdentifier = "checkout-service");
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(3L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);
        await publisher.PublishAsync(report, cancellationToken);
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var text = await ExportAsTextAsync(registry, cancellationToken);
        _ = await Assert.That(text).Contains("check=\"database\"");
    }

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

    private static (
        PrometheusMetricsHealthCheckPublisher Publisher,
        PrometheusMetricsInstruments Instruments,
        CollectorRegistry Registry
    ) CreatePublisher(Action<PrometheusMetricsOptions> configure, TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        _ = services.Configure(TestName, configure);
        var provider = services.BuildServiceProvider();

        var registry = Metrics.NewCustomRegistry();
        var instruments = new PrometheusMetricsInstruments(Metrics.WithCustomRegistry(registry));
        var publisher = new PrometheusMetricsHealthCheckPublisher(
            TestName,
            provider.GetRequiredService<IOptionsMonitor<PrometheusMetricsOptions>>(),
            instruments,
            timeProvider ?? TimeProvider.System
        );

        return (publisher, instruments, registry);
    }
}
