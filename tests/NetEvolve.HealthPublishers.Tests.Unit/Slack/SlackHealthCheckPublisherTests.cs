namespace NetEvolve.HealthPublishers.Tests.Unit.Slack;

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
using NetEvolve.HealthPublishers.Slack;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(Slack))]
public sealed class SlackHealthCheckPublisherTests
{
    private const string TestName = "Test";
    private const string WebhookPath = "/services/T000/B000/XXX";

    [Test]
    [Arguments(HealthStatus.Healthy, "good")]
    [Arguments(HealthStatus.Degraded, "warning")]
    [Arguments(HealthStatus.Unhealthy, "danger")]
    public async Task PublishAsync_WhenReportHasStatus_SendsRequestWithMappedColor(
        HealthStatus status,
        string expectedColor
    )
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
            _ = await Assert.That(request.Body).Contains($"\"color\":\"{expectedColor}\"");
            _ = await Assert.That(request.Body).Contains($"\"text\":\"Health check report: {status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifierFields()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains("checkout-service");
            _ = await Assert.That(request.Body).Contains(Environment.MachineName);
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Body).Contains($"\"ts\":{timeProvider.GetUtcNow().ToUnixTimeSeconds()}");
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_SendsPlainSummaryText()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var text = document.RootElement.GetProperty("attachments")[0].GetProperty("text").GetString();
        _ = await Assert.That(text).IsEqualTo("Overall status: Healthy, elapsed 42ms.");
    }

    [Test]
    public async Task PublishAsync_WhenEntryHasNoDescription_OmitsDescriptionSeparator()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var text = document.RootElement.GetProperty("attachments")[0].GetProperty("text").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(text).Contains("- *self*: Healthy (5ms)");
            _ = await Assert.That(text).DoesNotContain("(5ms) -");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasMultipleEntries_ListsEachEntryOnItsOwnLine()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var text = document.RootElement.GetProperty("attachments")[0].GetProperty("text").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(text!.StartsWith("Overall status: Degraded, elapsed 123ms.", StringComparison.Ordinal))
                .IsTrue();
            _ = await Assert.That(text).Contains("- *database*: Healthy (3ms)");
            _ = await Assert.That(text).Contains("- *cache*: Degraded (120ms) - slow response");
            _ = await Assert
                .That(text.IndexOf("database", StringComparison.Ordinal))
                .IsLessThan(text.IndexOf("cache", StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task PublishAsync_WhenResponseIsNotSuccessful_ThrowsHttpRequestException()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress($"https://hooks.slack.com{WebhookPath}");
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.InternalServerError);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new SlackHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        Task Act() => publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(Act);
    }

    private static IOptionsMonitor<SlackOptions> CreateOptionsMonitor(Action<SlackOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<SlackOptions>(
            TestName,
            options =>
            {
                options.WebhookUrl = new Uri($"https://hooks.slack.com{WebhookPath}");
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<SlackOptions>>();
    }
}
