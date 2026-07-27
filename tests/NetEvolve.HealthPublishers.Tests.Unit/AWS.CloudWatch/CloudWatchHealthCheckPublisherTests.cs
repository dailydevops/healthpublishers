namespace NetEvolve.HealthPublishers.Tests.Unit.AWS.CloudWatch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.AWS.CloudWatch;
using TUnit.Mocks;
using static TUnit.Mocks.Arguments.Arg;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

[TestGroup(nameof(CloudWatch))]
public sealed class CloudWatchHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy, 1d)]
    [Arguments(HealthStatus.Degraded, 0.5d)]
    [Arguments(HealthStatus.Unhealthy, 0d)]
    public async Task PublishAsync_WhenReportHasStatus_SendsOverallStatusMetricWithExpectedValue(
        HealthStatus status,
        double expectedValue
    )
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock);
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
        var request = CapturedRequest(mock);
        var overall = request.MetricData.Single(datum => datum.MetricName == "OverallStatus");
        _ = await Assert.That(overall.Value).IsEqualTo(expectedValue);
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsToConfiguredNamespace()
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock, options => options.Namespace = "Custom/Namespace");
        var report = EmptyReport();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        _ = await Assert.That(request.Namespace).IsEqualTo("Custom/Namespace");
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_TagsMetricsWithSystemIdentifierAndMachineName()
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock, options => options.SystemIdentifier = "checkout-service");
        var report = EmptyReport();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        var overall = request.MetricData.Single(datum => datum.MetricName == "OverallStatus");
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(overall.Dimensions.Single(d => d.Name == "SystemIdentifier").Value)
                .IsEqualTo("checkout-service");
            _ = await Assert
                .That(overall.Dimensions.Single(d => d.Name == "MachineName").Value)
                .IsEqualTo(Environment.MachineName);
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp()
    {
        // Arrange
        var mock = CreateMock();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = CreatePublisher(mock, timeProvider: timeProvider);
        var report = EmptyReport();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        var expected = timeProvider.GetUtcNow().UtcDateTime;
        _ = await Assert.That(request.MetricData[0].TimestampUtc).IsEqualTo(expected);
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsDurationMetricInMilliseconds()
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(123)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        var duration = request.MetricData.Single(datum => datum.MetricName == "Duration");
        _ = await Assert.That(duration.Value).IsEqualTo(123d);
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesPerCheckMetricsWithCheckNameDimension()
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(120)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        var status = request.MetricData.Single(datum => datum.MetricName == "CheckStatus");
        var duration = request.MetricData.Single(datum => datum.MetricName == "CheckDuration");
        using (Assert.Multiple())
        {
            _ = await Assert.That(status.Value).IsEqualTo(0.5d);
            _ = await Assert.That(status.Dimensions.Single(d => d.Name == "CheckName").Value).IsEqualTo("database");
            _ = await Assert.That(duration.Value).IsEqualTo(120d);
            _ = await Assert.That(duration.Dimensions.Single(d => d.Name == "CheckName").Value).IsEqualTo("database");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_SendsOnlyOverallMetrics()
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock);
        var report = EmptyReport();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        _ = await Assert.That(request.MetricData.Count).IsEqualTo(2);
    }

    [Test]
    public async Task PublishAsync_WhenReportHasMultipleEntries_SendsMetricsForEachEntry()
    {
        // Arrange
        var mock = CreateMock();
        var publisher = CreatePublisher(mock);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3),
                    null,
                    null
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(123)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = CapturedRequest(mock);
        // 2 overall metrics + 2 metrics per entry * 2 entries
        _ = await Assert.That(request.MetricData.Count).IsEqualTo(6);
    }

    [Test]
    public async Task PublishAsync_WhenServiceThrowsThrottlingException_PropagatesException()
    {
        // Arrange
        var mock = CreateMock();
        _ = mock.PutMetricDataAsync(Any<PutMetricDataRequest>(), Any<CancellationToken>())
            .Throws(new LimitExceededException("Rate exceeded"));
        var publisher = CreatePublisher(mock);
        var report = EmptyReport();

        // Act
        LimitExceededException? caught = null;
        try
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }
        catch (LimitExceededException ex)
        {
            caught = ex;
        }

        // Assert
        _ = await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task PublishAsync_WhenServiceThrowsInternalServiceException_PropagatesException()
    {
        // Arrange
        var mock = CreateMock();
        _ = mock.PutMetricDataAsync(Any<PutMetricDataRequest>(), Any<CancellationToken>())
            .Throws(new InternalServiceException("boom"));
        var publisher = CreatePublisher(mock);
        var report = EmptyReport();

        // Act
        InternalServiceException? caught = null;
        try
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }
        catch (InternalServiceException ex)
        {
            caught = ex;
        }

        // Assert
        _ = await Assert.That(caught).IsNotNull();
    }

    private static HealthReport EmptyReport() =>
        new(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.FromMilliseconds(42));

    private static Mock<IAmazonCloudWatch> CreateMock()
    {
        var mock = Mock.Of<IAmazonCloudWatch>();
        _ = mock.PutMetricDataAsync(Any<PutMetricDataRequest>(), Any<CancellationToken>())
            .Returns(new PutMetricDataResponse());
        return mock;
    }

    private static PutMetricDataRequest CapturedRequest(Mock<IAmazonCloudWatch> mock) =>
        (PutMetricDataRequest)Mock.Invocations(mock).Single().Arguments[0]!;

    private static CloudWatchHealthCheckPublisher CreatePublisher(
        Mock<IAmazonCloudWatch> mock,
        Action<CloudWatchOptions>? configure = null,
        TimeProvider? timeProvider = null
    )
    {
        var optionsMonitor = CreateOptionsMonitor(configure ?? (_ => { }));
        return new CloudWatchHealthCheckPublisher(
            TestName,
            mock.Object,
            optionsMonitor,
            timeProvider ?? TimeProvider.System
        );
    }

    private static IOptionsMonitor<CloudWatchOptions> CreateOptionsMonitor(Action<CloudWatchOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<CloudWatchOptions>(
            TestName,
            options =>
            {
                options.Region = "eu-central-1";
                options.Namespace = "HealthChecks";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<CloudWatchOptions>>();
    }
}
