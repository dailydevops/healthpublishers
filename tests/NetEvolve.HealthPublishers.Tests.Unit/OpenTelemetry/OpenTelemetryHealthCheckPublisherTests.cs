namespace NetEvolve.HealthPublishers.Tests.Unit.OpenTelemetry;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.OpenTelemetry;

[TestGroup(nameof(OpenTelemetry))]
public sealed class OpenTelemetryHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy)]
    [Arguments(HealthStatus.Degraded)]
    [Arguments(HealthStatus.Unhealthy)]
    public async Task PublishAsync_WhenReportHasStatus_RecordsReportDurationWithStatusTag(
        HealthStatus status,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var meter = new Meter(DependencyInjectionExtensions.MeterName);
        var instruments = new OpenTelemetryInstruments(meter);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpenTelemetryHealthCheckPublisher(
            TestName,
            optionsMonitor,
            instruments,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(status, null, TimeSpan.FromMilliseconds(5L), null, null),
            },
            TimeSpan.FromMilliseconds(42L)
        );
        using var measurements = new MeasurementRecorder(meter, "healthchecks.report.duration");

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(measurements.Values.Count).IsEqualTo(1);
            _ = await Assert.That(measurements.Values[0]).IsEqualTo(42d);
            _ = await Assert.That(measurements.Tags[0]["healthchecks.status"]).IsEqualTo(status.ToString());
            _ = await Assert.That(measurements.Tags[0]["healthchecks.publisher.name"]).IsEqualTo(TestName);
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_TagsMachineNameAndSystemIdentifier(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var meter = new Meter(DependencyInjectionExtensions.MeterName);
        var instruments = new OpenTelemetryInstruments(meter);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpenTelemetryHealthCheckPublisher(
            TestName,
            optionsMonitor,
            instruments,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);
        using var measurements = new MeasurementRecorder(meter, "healthchecks.report.duration");

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(measurements.Tags[0]["healthchecks.system.identifier"]).IsEqualTo("checkout-service");
            _ = await Assert.That(measurements.Tags[0]["healthchecks.machine.name"]).IsEqualTo(Environment.MachineName);
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_RecordsEntryDurationPerEntry(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var meter = new Meter(DependencyInjectionExtensions.MeterName);
        var instruments = new OpenTelemetryInstruments(meter);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpenTelemetryHealthCheckPublisher(
            TestName,
            optionsMonitor,
            instruments,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(7L), null, null),
            },
            TimeSpan.Zero
        );
        using var measurements = new MeasurementRecorder(meter, "healthchecks.entry.duration");

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(measurements.Values.Count).IsEqualTo(1);
            _ = await Assert.That(measurements.Values[0]).IsEqualTo(7d);
            _ = await Assert.That(measurements.Tags[0]["healthchecks.entry.name"]).IsEqualTo("self");
            _ = await Assert.That(measurements.Tags[0]["healthchecks.status"]).IsEqualTo("Healthy");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var meter = new Meter(DependencyInjectionExtensions.MeterName);
        var instruments = new OpenTelemetryInstruments(meter);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new OpenTelemetryHealthCheckPublisher(TestName, optionsMonitor, instruments, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);
        using var measurements = new MeasurementRecorder(meter, "healthchecks.report.duration");

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        _ = await Assert
            .That(measurements.Tags[0]["healthchecks.timestamp"])
            .IsEqualTo("2026-01-02T03:04:05.0000000+00:00");
    }

    private static IOptionsMonitor<OpenTelemetryOptions> CreateOptionsMonitor(Action<OpenTelemetryOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<OpenTelemetryOptions>(
            TestName,
            options =>
            {
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<OpenTelemetryOptions>>();
    }

    private sealed class MeasurementRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();

        public MeasurementRecorder(Meter meter, string instrumentName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == meter && instrument.Name == instrumentName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<double>(
                (_, measurement, tags, _) =>
                {
                    Values.Add(measurement);
                    var tagDictionary = new Dictionary<string, string?>(StringComparer.Ordinal);
                    foreach (var tag in tags)
                    {
                        tagDictionary[tag.Key] = tag.Value?.ToString();
                    }
                    Tags.Add(tagDictionary);
                }
            );
            _listener.Start();
        }

        public List<double> Values { get; } = [];

        public List<Dictionary<string, string?>> Tags { get; } = [];

        public void Dispose() => _listener.Dispose();
    }
}
