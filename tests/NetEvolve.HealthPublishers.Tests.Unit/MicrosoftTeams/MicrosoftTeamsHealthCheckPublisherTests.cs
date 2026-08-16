namespace NetEvolve.HealthPublishers.Tests.Unit.MicrosoftTeams;

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
using NetEvolve.HealthPublishers.MicrosoftTeams;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(MicrosoftTeams))]
public sealed class MicrosoftTeamsHealthCheckPublisherTests
{
    private const string TestName = "Test";
    private const string WebhookPath = "/webhookb2/00000000-0000-0000-0000-000000000000";
    private static readonly Uri WebhookUrl = new($"https://example.webhook.office.com{WebhookPath}");

    [Test]
    [Arguments(HealthStatus.Healthy, "good")]
    [Arguments(HealthStatus.Degraded, "warning")]
    [Arguments(HealthStatus.Unhealthy, "attention")]
    public async Task PublishAsync_WhenReportHasStatus_SendsRequestWithMappedColor(
        HealthStatus status,
        string expectedColor,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo(WebhookPath);
            _ = await Assert.That(request.Body).Contains($"\"text\":\"Health check report: {status}\"");
            _ = await Assert.That(request.Body).Contains($"\"color\":\"{expectedColor}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsAdaptiveCardAttachment(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        using (Assert.Multiple())
        {
            _ = await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("message");
            var attachment = root.GetProperty("attachments")[0];
            _ = await Assert
                .That(attachment.GetProperty("contentType").GetString())
                .IsEqualTo("application/vnd.microsoft.card.adaptive");
            var content = attachment.GetProperty("content");
            _ = await Assert.That(content.GetProperty("type").GetString()).IsEqualTo("AdaptiveCard");
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifierFacts(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains($"\"value\":\"{Environment.MachineName}\"");
            _ = await Assert.That(request.Body).Contains("\"value\":\"checkout-service\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForCheckedAt(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var facts = document
            .RootElement.GetProperty("attachments")[0]
            .GetProperty("content")
            .GetProperty("body")[1]
            .GetProperty("facts");
        var checkedAt = facts
            .EnumerateArray()
            .Single(fact => fact.GetProperty("title").GetString() == "Checked at")
            .GetProperty("value")
            .GetString();
        _ = await Assert.That(checkedAt).IsEqualTo(timeProvider.GetUtcNow().ToString("O"));
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInTextBlock(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains("database");
            _ = await Assert.That(request.Body).Contains("slow response");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_OmitsDetailsTextBlock(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var body = document.RootElement.GetProperty("attachments")[0].GetProperty("content").GetProperty("body");
        _ = await Assert.That(body.GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task PublishAsync_WhenEntryHasNoDescription_OmitsDescriptionSeparator(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains("**self**: Healthy (5ms)");
            _ = await Assert.That(request.Body).DoesNotContain("**self**: Healthy (5ms) -");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasMultipleEntries_ListsEachEntry(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var details = document
            .RootElement.GetProperty("attachments")[0]
            .GetProperty("content")
            .GetProperty("body")[2]
            .GetProperty("text")
            .GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(details).Contains("- **database**: Healthy (3ms)");
            _ = await Assert.That(details).Contains("- **cache**: Degraded (120ms) - slow response");
            _ = await Assert
                .That(details!.IndexOf("database", StringComparison.Ordinal))
                .IsLessThan(details.IndexOf("cache", StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportTextExceedsMaxLength_DropsWholeOverflowingEntries(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.OK);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var entries = new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal);
        for (var i = 0; i < 200; i++)
        {
            entries[$"check-{i}"] = new HealthReportEntry(
                HealthStatus.Healthy,
                new string('x', 100),
                TimeSpan.FromMilliseconds(1),
                null,
                null
            );
        }
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(200));

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var details = document
            .RootElement.GetProperty("attachments")[0]
            .GetProperty("content")
            .GetProperty("body")[2]
            .GetProperty("text")
            .GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(details!.Length).IsLessThanOrEqualTo(4000);
            // Only whole entries are included: every entry line present kept its full 100-char
            // description; entries that would overflow the cap were dropped completely, not cut in half.
            _ = await Assert.That(details).DoesNotContain("check-199");
            foreach (var line in details.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                _ = await Assert.That(line).Contains(new string('x', 100));
            }
        }
    }

    [Test]
    public async Task PublishAsync_WhenResponseIsNotSuccess_ThrowsHttpRequestException(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Arrange
        using var factory = Mock.HttpClientFactory();
        _ = factory.Handler.OnPost(WebhookPath).Respond(HttpStatusCode.InternalServerError);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new MicrosoftTeamsHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        Task Act() => publisher.PublishAsync(report, cancellationToken);

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(Act);
    }

    private static IOptionsMonitor<MicrosoftTeamsOptions> CreateOptionsMonitor(Action<MicrosoftTeamsOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<MicrosoftTeamsOptions>(
            TestName,
            options =>
            {
                options.WebhookUrl = WebhookUrl;
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<MicrosoftTeamsOptions>>();
    }
}
