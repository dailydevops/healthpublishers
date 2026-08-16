namespace NetEvolve.HealthPublishers.Tests.Unit.PagerDuty;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.PagerDuty;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(PagerDuty))]
public sealed class PagerDutyHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    public async Task PublishAsync_WhenReportHealthy_SendsResolveEventWithoutPayload(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/v2/enqueue");
            _ = await Assert.That(root.GetProperty("event_action").GetString()).IsEqualTo("resolve");
            _ = await Assert.That(root.TryGetProperty("payload", out _)).IsFalse();
        }
    }

    [Test]
    [Arguments(HealthStatus.Degraded, "warning")]
    [Arguments(HealthStatus.Unhealthy, "critical")]
    public async Task PublishAsync_WhenReportNotHealthy_SendsTriggerEventWithMappedSeverity(
        HealthStatus status,
        string expectedSeverity,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        using (Assert.Multiple())
        {
            _ = await Assert.That(root.GetProperty("event_action").GetString()).IsEqualTo("trigger");
            _ = await Assert
                .That(root.GetProperty("payload").GetProperty("severity").GetString())
                .IsEqualTo(expectedSeverity);
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsStableDedupKey(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var healthyReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.Zero
        );
        var unhealthyReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    null,
                    TimeSpan.FromMilliseconds(5),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(unhealthyReport, cancellationToken);
        await publisher.PublishAsync(healthyReport, cancellationToken);

        // Assert
        using var triggerDocument = JsonDocument.Parse(factory.Handler.Requests[0].Body!);
        using var resolveDocument = JsonDocument.Parse(factory.Handler.Requests[1].Body!);
        var triggerDedupKey = triggerDocument.RootElement.GetProperty("dedup_key").GetString();
        var resolveDedupKey = resolveDocument.RootElement.GetProperty("dedup_key").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(triggerDedupKey).IsEqualTo(resolveDedupKey);
            _ = await Assert.That(triggerDedupKey).Contains("checkout-service");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsRoutingKey(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.RoutingKey = "test-routing-key");
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Body).Contains("\"routing_key\":\"test-routing-key\"");
    }

    [Test]
    public async Task PublishAsync_WhenTriggering_SendsMachineNameAsSource(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    null,
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
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var source = document.RootElement.GetProperty("payload").GetProperty("source").GetString();
        _ = await Assert.That(source).IsEqualTo(Environment.MachineName);
    }

    [Test]
    public async Task PublishAsync_WhenTriggering_UsesTimeProviderForTimestamp(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Degraded, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var timestamp = document.RootElement.GetProperty("payload").GetProperty("timestamp").GetString();
        _ = await Assert.That(timestamp).IsEqualTo(timeProvider.GetUtcNow().ToString("O"));
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInCustomDetails(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var customDetails = document.RootElement.GetProperty("payload").GetProperty("custom_details");
        var entries = customDetails.GetProperty("entries");
        var databaseEntry = entries.GetProperty("database");
        using (Assert.Multiple())
        {
            _ = await Assert.That(customDetails.GetProperty("overall_status").GetString()).IsEqualTo("Degraded");
            _ = await Assert.That(databaseEntry.GetProperty("status").GetString()).IsEqualTo("Degraded");
            _ = await Assert.That(databaseEntry.GetProperty("description").GetString()).IsEqualTo("slow response");
        }
    }

    [Test]
    public async Task PublishAsync_WhenServerRespondsWithFailureStatusCode_ThrowsHttpRequestException(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://events.pagerduty.com");
        _ = factory.Handler.OnPost("/v2/enqueue").Respond(HttpStatusCode.BadRequest);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new PagerDutyHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        async Task Act(CancellationToken token = default) => await publisher.PublishAsync(report, token);

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => Act(cancellationToken));
    }

    private static IOptionsMonitor<PagerDutyOptions> CreateOptionsMonitor(Action<PagerDutyOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<PagerDutyOptions>(
            TestName,
            options =>
            {
                options.RoutingKey = "test-key";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<PagerDutyOptions>>();
    }
}
