namespace NetEvolve.HealthPublishers.Tests.Unit.Webhook;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Webhook;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(Webhook))]
public sealed class WebhookHealthCheckPublisherTests
{
    private const string TestName = "Test";
    private static readonly Uri TestUri = new("https://example.com/webhooks/health");

    [Test]
    [Arguments(HealthStatus.Healthy)]
    [Arguments(HealthStatus.Degraded)]
    [Arguments(HealthStatus.Unhealthy)]
    public async Task PublishAsync_WhenReportHasStatus_SendsRequestWithMappedStatus(HealthStatus status)
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/webhooks/health");
            _ = await Assert.That(request.Body).Contains($"\"status\":\"{status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsSystemIdentifierAndMachineName()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains("\"systemIdentifier\":\"checkout-service\"");
            _ = await Assert.That(request.Body).Contains($"\"machineName\":\"{Environment.MachineName}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenHeadersProvided_SendsHeadersWithRequest()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => options.Headers["X-Api-Key"] = "secret");
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Headers["X-Api-Key"]).Contains("secret");
    }

    [Test]
    public async Task PublishAsync_WhenNoHeadersProvided_SendsRequestWithoutCustomHeaders()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Headers.ContainsKey("X-Api-Key")).IsFalse();
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var timestamp = document.RootElement.GetProperty("timestamp").GetString();
        _ = await Assert.That(timestamp).IsEqualTo("2026-01-02T03:04:05.0000000Z");
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInPayload()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var entry = document.RootElement.GetProperty("entries")[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(entry.GetProperty("name").GetString()).IsEqualTo("database");
            _ = await Assert.That(entry.GetProperty("status").GetString()).IsEqualTo("Degraded");
            _ = await Assert.That(entry.GetProperty("durationMs").GetDouble()).IsEqualTo(120d);
            _ = await Assert.That(entry.GetProperty("description").GetString()).IsEqualTo("slow response");
            _ = await Assert
                .That(entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!))
                .IsEquivalentTo(["db", "sql"]);
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_SendsEmptyEntriesArray()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        using (Assert.Multiple())
        {
            _ = await Assert.That(document.RootElement.GetProperty("entries").GetArrayLength()).IsEqualTo(0);
            _ = await Assert.That(document.RootElement.GetProperty("totalDurationMs").GetDouble()).IsEqualTo(42d);
        }
    }

    [Test]
    public async Task PublishAsync_WhenResponseIsNotSuccessful_ThrowsHttpRequestException()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://example.com");
        _ = factory.Handler.OnPost("/webhooks/health").Respond(HttpStatusCode.InternalServerError);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new WebhookHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        async Task Act() => await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(Act);
    }

    private static IOptionsMonitor<WebhookOptions> CreateOptionsMonitor(Action<WebhookOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<WebhookOptions>(
            TestName,
            options =>
            {
                options.Uri = TestUri;
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<WebhookOptions>>();
    }
}
