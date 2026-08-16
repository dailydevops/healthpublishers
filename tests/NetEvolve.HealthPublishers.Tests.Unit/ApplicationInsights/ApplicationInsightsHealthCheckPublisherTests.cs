namespace NetEvolve.HealthPublishers.Tests.Unit.ApplicationInsights;

using System.Globalization;
using System.Threading;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.ApplicationInsights;

[TestGroup(nameof(ApplicationInsights))]
public sealed class ApplicationInsightsHealthCheckPublisherTests
{
    private const string TestName = "Test";
    private const string TestConnectionString = "InstrumentationKey=11111111-1111-1111-1111-111111111111";

    [Test]
    [Arguments(HealthStatus.Healthy, true)]
    [Arguments(HealthStatus.Degraded, false)]
    [Arguments(HealthStatus.Unhealthy, false)]
    public async Task PublishAsync_WhenReportHasStatus_SetsSuccessBasedOnStatus(
        HealthStatus status,
        bool expectedSuccess,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(status, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        using (Assert.Multiple())
        {
            _ = await Assert.That(telemetry.GetAvailabilityAttribute("success")).IsEqualTo(expectedSuccess.ToString());
            _ = await Assert.That(telemetry.GetAvailabilityAttribute("message")).IsEqualTo(status.ToString());
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SetsMachineNameAndSystemIdentifierProperties(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        using (Assert.Multiple())
        {
            _ = await Assert.That(telemetry.GetAttribute("MachineName")).IsEqualTo(Environment.MachineName);
            _ = await Assert.That(telemetry.GetAttribute("SystemIdentifier")).IsEqualTo("checkout-service");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            timeProvider
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        var timestamp = DateTimeOffset.Parse(
            telemetry.GetAvailabilityAttribute("testTimestamp")!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
        _ = await Assert.That(timestamp).IsEqualTo(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsDurationFromReportTotalDuration(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(123)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        var duration = TimeSpan.Parse(telemetry.GetAvailabilityAttribute("duration")!, CultureInfo.InvariantCulture);
        _ = await Assert.That(duration).IsEqualTo(TimeSpan.FromMilliseconds(123));
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsRunLocationToMachineName(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        _ = await Assert.That(telemetry.GetAvailabilityAttribute("runLocation")).IsEqualTo(Environment.MachineName);
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsNameToHealthReport(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        _ = await Assert.That(telemetry.GetAvailabilityAttribute("name")).IsEqualTo("HealthReport");
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SerializesEntriesIntoPropertiesJson(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    "all good",
                    TimeSpan.FromMilliseconds(5),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var telemetry = channel.LogRecords.Single();
        var entries = telemetry.GetAttribute("Entries");
        using (Assert.Multiple())
        {
            _ = await Assert.That(entries).Contains("\"self\"");
            _ = await Assert.That(entries).Contains("\"Status\":\"Healthy\"");
            _ = await Assert.That(entries).Contains("\"Description\":\"all good\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_FlushesTelemetryClientAsynchronously(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        // The in-memory exporter only receives log records once flushed; without the
        // publisher awaiting FlushAsync, this collection would still be empty.
        _ = await Assert.That(channel.LogRecords.Count).IsEqualTo(1);
    }

    [Test]
    public async Task PublishAsync_WhenCalledMultipleTimes_GeneratesUniqueIds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var channel = new TestTelemetryChannel();
        using var configuration = CreateTelemetryConfiguration(channel);
        var client = new TelemetryClient(configuration);
        var optionsMonitor = CreateOptionsMonitor(_ => { });
        var publisher = new ApplicationInsightsHealthCheckPublisher(
            TestName,
            () => client,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var ids = channel.LogRecords.Select(record => record.GetAvailabilityAttribute("id")).ToList();
        _ = await Assert.That(ids[0]).IsNotEqualTo(ids[1]);
    }

    private static TelemetryConfiguration CreateTelemetryConfiguration(TestTelemetryChannel channel)
    {
        var configuration = new TelemetryConfiguration { ConnectionString = TestConnectionString };
        channel.Configure(configuration);
        return configuration;
    }

    private static IOptionsMonitor<ApplicationInsightsOptions> CreateOptionsMonitor(
        Action<ApplicationInsightsOptions> configure
    )
    {
        var services = new ServiceCollection();
        _ = services.Configure<ApplicationInsightsOptions>(
            TestName,
            options =>
            {
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<ApplicationInsightsOptions>>();
    }
}
