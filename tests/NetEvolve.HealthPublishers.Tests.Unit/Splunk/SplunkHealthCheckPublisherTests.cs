namespace NetEvolve.HealthPublishers.Tests.Unit.Splunk;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Splunk;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(Splunk))]
public sealed class SplunkHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy)]
    [Arguments(HealthStatus.Degraded)]
    [Arguments(HealthStatus.Unhealthy)]
    public async Task PublishAsync_WhenReportHasStatus_SendsRequestWithStatus(HealthStatus status)
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/services/collector/event");
            _ = await Assert.That(request.Body).Contains($"\"status\":\"{status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsAuthorizationHeaderWithHecToken()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => options.HecToken = "test-hec-token");
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Headers["Authorization"]).Contains("Splunk test-hec-token");
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifier()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains($"\"machine_name\":\"{Environment.MachineName}\"");
            _ = await Assert.That(request.Body).Contains("\"system_identifier\":\"checkout-service\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTime()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var expectedTime = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() / 1000d;
        _ = await Assert.That(document.RootElement.GetProperty("time").GetDouble()).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task PublishAsync_WhenSourceTypeNotSet_OmitsSourcetypeField()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        _ = await Assert.That(document.RootElement.TryGetProperty("sourcetype", out _)).IsFalse();
    }

    [Test]
    public async Task PublishAsync_WhenSourceTypeSourceAndIndexSet_IncludesThemInPayload()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options =>
        {
            options.SourceType = "health-check";
            options.Source = "checkout-service";
            options.Index = "health";
        });
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        using (Assert.Multiple())
        {
            _ = await Assert.That(document.RootElement.GetProperty("sourcetype").GetString()).IsEqualTo("health-check");
            _ = await Assert.That(document.RootElement.GetProperty("source").GetString()).IsEqualTo("checkout-service");
            _ = await Assert.That(document.RootElement.GetProperty("index").GetString()).IsEqualTo("health");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInEvent()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null,
                    tags: ["db", "sql"]
                ),
            },
            TimeSpan.FromMilliseconds(120)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var entry = document.RootElement.GetProperty("event").GetProperty("entries").GetProperty("database");
        using (Assert.Multiple())
        {
            _ = await Assert.That(entry.GetProperty("status").GetString()).IsEqualTo("Degraded");
            _ = await Assert.That(entry.GetProperty("description").GetString()).IsEqualTo("slow response");
            _ = await Assert.That(entry.GetProperty("elapsed_ms").GetDouble()).IsEqualTo(120d);
            var tags = entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToArray();
            _ = await Assert.That(tags).Contains("db");
            _ = await Assert.That(tags).Contains("sql");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_SendsMessageWithOverallStatusAndElapsedTime()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var message = document.RootElement.GetProperty("event").GetProperty("message").GetString();
        _ = await Assert.That(message).IsEqualTo("Health check report Healthy in 42ms");
    }

    [Test]
    public async Task PublishAsync_WhenResponseIsNotSuccessStatusCode_ThrowsHttpRequestException()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://splunk.example.com:8088");
        _ = factory.Handler.OnPost("/services/collector/event").Respond(HttpStatusCode.InternalServerError);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SplunkHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        HttpRequestException? caught = null;
        try
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }
        catch (HttpRequestException ex)
        {
            caught = ex;
        }

        // Assert
        _ = await Assert.That(caught).IsNotNull();
    }

    private static IOptionsMonitor<SplunkOptions> CreateOptionsMonitor(Action<SplunkOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<SplunkOptions>(
            TestName,
            options =>
            {
                options.ServerUrl = new Uri("https://splunk.example.com:8088");
                options.HecToken = "test-token";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<SplunkOptions>>();
    }
}
