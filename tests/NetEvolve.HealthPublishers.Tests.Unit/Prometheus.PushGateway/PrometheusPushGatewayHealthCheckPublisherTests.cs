namespace NetEvolve.HealthPublishers.Tests.Unit.Prometheus.PushGateway;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Prometheus.PushGateway;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(PushGateway))]
public sealed class PrometheusPushGatewayHealthCheckPublisherTests
{
    private const string TestName = "Test";
    private static readonly char[] MetricNameTerminators = ['{', ' '];

    [Test]
    public async Task PublishAsync_WhenInstanceNotSet_PostsToJobPathOnly()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/metrics/job/checkout-service");
    }

    [Test]
    public async Task PublishAsync_WhenInstanceSet_PostsToJobAndInstancePath()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory
            .Handler.OnPost("/metrics/job/checkout-service/instance/checkout-service-01")
            .Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.Instance = "checkout-service-01");
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert
            .That(request.RequestUri!.AbsolutePath)
            .IsEqualTo("/metrics/job/checkout-service/instance/checkout-service-01");
    }

    [Test]
    public async Task PublishAsync_WhenJobAndInstanceContainReservedCharacters_EscapesPathSegments()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory
            .Handler.OnPost("/metrics/job/checkout%2Fservice/instance/instance%20one")
            .Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options =>
        {
            options.Job = "checkout/service";
            options.Instance = "instance one";
        });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert
            .That(request.RequestUri!.AbsolutePath)
            .IsEqualTo("/metrics/job/checkout%2Fservice/instance/instance%20one");
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsPrometheusTextExpositionContentType()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Headers["Content-Type"]).Contains("text/plain; version=0.0.4; charset=utf-8");
    }

    [Test]
    [Arguments(HealthStatus.Healthy, 2)]
    [Arguments(HealthStatus.Degraded, 1)]
    [Arguments(HealthStatus.Unhealthy, 0)]
    public async Task PublishAsync_WhenReportHasStatus_SendsMappedReportStatusGauge(
        HealthStatus status,
        int expectedValue
    )
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(status, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Body).Contains($"healthcheck_report_status{{{Labels()}}} {expectedValue}");
    }

    [Test]
    public async Task PublishAsync_WhenCalled_IncludesReportDurationInSeconds()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(1500)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Body).Contains($"healthcheck_report_duration_seconds{{{Labels()}}} 1.5");
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForLastPublishTimestamp()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert
            .That(request.Body)
            .Contains(
                $"healthcheck_last_publish_timestamp_seconds{{{Labels()}}} {timeProvider.GetUtcNow().ToUnixTimeSeconds()}"
            );
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_OmitsPerCheckMetricFamilies()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).DoesNotContain("healthcheck_status{");
            _ = await Assert.That(request.Body).DoesNotContain("healthcheck_duration_seconds{");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesPerCheckStatusAndDuration()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(250),
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
            TimeSpan.FromMilliseconds(370)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(request.Body)
                .Contains($"healthcheck_status{{check=\"database\",description=\"\",{Labels()}}} 2");
            _ = await Assert
                .That(request.Body)
                .Contains($"healthcheck_status{{check=\"cache\",description=\"slow response\",{Labels()}}} 1");
            _ = await Assert
                .That(request.Body)
                .Contains($"healthcheck_duration_seconds{{check=\"database\",description=\"\",{Labels()}}} 0.25");
            _ = await Assert
                .That(request.Body)
                .Contains(
                    $"healthcheck_duration_seconds{{check=\"cache\",description=\"slow response\",{Labels()}}} 0.12"
                );
        }
    }

    [Test]
    public async Task PublishAsync_WhenDescriptionContainsReservedCharacters_EscapesLabelValue()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "boom \"quoted\" \\ path\nnext line",
                    TimeSpan.FromMilliseconds(1),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(1)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Body).Contains("description=\"boom \\\"quoted\\\" \\\\ path\\nnext line\"");
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifierLabels()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains("system_identifier=\"checkout-service\"");
            _ = await Assert.That(request.Body).Contains($"machine_name=\"{Environment.MachineName}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_ProducesStructurallyValidExpositionBody()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://pushgateway.example.com");
        _ = factory.Handler.OnPost("/metrics/job/checkout-service").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PrometheusPushGatewayHealthCheckPublisher(
            TestName,
            factory,
            optionsMonitor,
            TimeProvider.System
        );
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
        var body = factory.Handler.Requests[0].Body!;
        var typeMatches = Regex.Matches(body, @"^# TYPE (\S+) ", RegexOptions.Multiline);
        var metricNames = typeMatches.Select(match => match.Groups[1].Value).ToArray();

        using (Assert.Multiple())
        {
            // Every metric name gets exactly one TYPE line: families are grouped, never interleaved.
            _ = await Assert.That(metricNames.Distinct().Count()).IsEqualTo(metricNames.Length);
            _ = await Assert.That(body.EndsWith('\n')).IsTrue();

            // Every non-comment, non-empty line is either a HELP/TYPE line or is preceded (somewhere above) by
            // a TYPE line declaring its metric name.
            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var declaredMetrics = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                if (line.StartsWith("# TYPE ", StringComparison.Ordinal))
                {
                    _ = declaredMetrics.Add(line.Split(' ')[2]);
                    continue;
                }

                if (line.StartsWith('#'))
                {
                    continue;
                }

                var metricName = line.Split(MetricNameTerminators)[0];
                _ = await Assert.That(declaredMetrics.Contains(metricName)).IsTrue();
            }
        }
    }

    private static string Labels() => $"system_identifier=\"test-system\",machine_name=\"{Environment.MachineName}\"";

    private static IOptionsMonitor<PrometheusPushGatewayOptions> CreateOptionsMonitor(
        Action<PrometheusPushGatewayOptions> configure
    )
    {
        var services = new ServiceCollection();
        _ = services.Configure<PrometheusPushGatewayOptions>(
            TestName,
            options =>
            {
                options.ServerUrl = new Uri("https://pushgateway.example.com");
                options.Job = "checkout-service";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<PrometheusPushGatewayOptions>>();
    }
}
