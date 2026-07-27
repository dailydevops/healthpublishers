namespace NetEvolve.HealthPublishers.Tests.Unit.ApplicationInsights;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
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
        bool expectedSuccess
    )
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        using (Assert.Multiple())
        {
            _ = await Assert.That(telemetry.Success).IsEqualTo(expectedSuccess);
            _ = await Assert.That(telemetry.Message).IsEqualTo(status.ToString());
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SetsMachineNameAndSystemIdentifierProperties()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        using (Assert.Multiple())
        {
            _ = await Assert.That(telemetry.Properties["MachineName"]).IsEqualTo(Environment.MachineName);
            _ = await Assert.That(telemetry.Properties["SystemIdentifier"]).IsEqualTo("checkout-service");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        _ = await Assert.That(telemetry.Timestamp).IsEqualTo(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsDurationFromReportTotalDuration()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        _ = await Assert.That(telemetry.Duration).IsEqualTo(TimeSpan.FromMilliseconds(123));
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsRunLocationToMachineName()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        _ = await Assert.That(telemetry.RunLocation).IsEqualTo(Environment.MachineName);
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsNameToHealthReport()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        _ = await Assert.That(telemetry.Name).IsEqualTo("HealthReport");
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SerializesEntriesIntoPropertiesJson()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var telemetry = channel.SentItems.OfType<AvailabilityTelemetry>().Single();
        using (Assert.Multiple())
        {
            _ = await Assert.That(telemetry.Properties["Entries"]).Contains("\"self\"");
            _ = await Assert.That(telemetry.Properties["Entries"]).Contains("\"Status\":\"Healthy\"");
            _ = await Assert.That(telemetry.Properties["Entries"]).Contains("\"Description\":\"all good\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_FlushesTelemetryClientAsynchronously()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.That(channel.FlushAsyncCount).IsEqualTo(1);
    }

    [Test]
    public async Task PublishAsync_WhenCalledMultipleTimes_GeneratesUniqueIds()
    {
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
        await publisher.PublishAsync(report, CancellationToken.None);
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var ids = channel.SentItems.OfType<AvailabilityTelemetry>().Select(telemetry => telemetry.Id).ToList();
        _ = await Assert.That(ids[0]).IsNotEqualTo(ids[1]);
    }

    private static TelemetryConfiguration CreateTelemetryConfiguration(TestTelemetryChannel channel) =>
        new() { ConnectionString = TestConnectionString, TelemetryChannel = channel };

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
