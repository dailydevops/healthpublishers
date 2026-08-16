namespace NetEvolve.HealthPublishers.Tests.Unit.Opsgenie;

using System;
using System.Collections.Generic;
using System.Globalization;
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
using NetEvolve.HealthPublishers.Opsgenie;
using TUnit.Mocks;
using TUnit.Mocks.Http;

[TestGroup(nameof(Opsgenie))]
public sealed class OpsgenieHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Degraded, "P3")]
    [Arguments(HealthStatus.Unhealthy, "P1")]
    public async Task PublishAsync_WhenReportNotHealthy_CreatesAlertWithMappedPriority(
        HealthStatus status,
        string expectedPriority,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
            _ = await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/v2/alerts");
            _ = await Assert.That(request.Body).Contains($"\"priority\":\"{expectedPriority}\"");
            _ = await Assert.That(request.Body).Contains($"\"status:{status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHealthy_ClosesAlertByAlias(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory
            .Handler.OnPost("/v2/alerts/healthpublishers%3Acheckout-service/close?identifierType=alias")
            .Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
            _ = await Assert
                .That(request.RequestUri!.AbsolutePath)
                .IsEqualTo("/v2/alerts/healthpublishers%3Acheckout-service/close");
            _ = await Assert.That(request.RequestUri.Query).Contains("identifierType=alias");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHealthyAndAlertAlreadyClosed_DoesNotThrow(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory
            .Handler.OnPost("/v2/alerts/healthpublishers%3Acheckout-service/close?identifierType=alias")
            .Respond(HttpStatusCode.NotFound);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act & Assert
        await publisher.PublishAsync(report, cancellationToken);
        _ = await Assert.That(factory.Handler.Requests[0].Matched).IsTrue();
    }

    [Test]
    public async Task PublishAsync_WhenCloseFailsWithServerError_Throws(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory
            .Handler.OnPost("/v2/alerts/healthpublishers%3Acheckout-service/close?identifierType=alias")
            .Respond(HttpStatusCode.InternalServerError);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        Task Act(CancellationToken token = default) => publisher.PublishAsync(report, token);

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => Act(cancellationToken));
    }

    [Test]
    public async Task PublishAsync_WhenCreateFailsWithServerError_Throws(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.InternalServerError);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        Task Act(CancellationToken token = default) => publisher.PublishAsync(report, token);

        // Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => Act(cancellationToken));
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsApiKeyAsGenieKeyAuthorizationHeader(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.ApiKey = "test-key");
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        _ = await Assert.That(request.Headers["Authorization"]).Contains("GenieKey test-key");
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifierTagsAndDetails(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains($"\"machine_name:{Environment.MachineName}\"");
            _ = await Assert.That(request.Body).Contains("\"system_identifier:checkout-service\"");
            _ = await Assert.That(request.Body).Contains("\"alias\":\"healthpublishers:checkout-service\"");
            using var document = JsonDocument.Parse(request.Body!);
            var details = document.RootElement.GetProperty("details");
            _ = await Assert.That(details.GetProperty("system_identifier").GetString()).IsEqualTo("checkout-service");
            _ = await Assert.That(details.GetProperty("machine_name").GetString()).IsEqualTo(Environment.MachineName);
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForReportedAt(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
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
        var reportedAt = document.RootElement.GetProperty("details").GetProperty("reported_at").GetString();
        _ = await Assert
            .That(reportedAt)
            .IsEqualTo(timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInDescription(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
    public async Task PublishAsync_WhenReportHasNoEntries_SendsPlainSummaryDescriptionWithoutMarkers(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Degraded,
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var description = document.RootElement.GetProperty("description").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(description).IsEqualTo("Overall status: Degraded, elapsed 42ms.");
            _ = await Assert.That(description).DoesNotContain("%%%");
        }
    }

    [Test]
    public async Task PublishAsync_WhenEntryHasNoDescription_OmitsDescriptionSeparator(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var description = document.RootElement.GetProperty("description").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(description).Contains("- **self**: Unhealthy (5ms)");
            _ = await Assert.That(description).DoesNotContain("**self**: Unhealthy (5ms) -");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasMultipleEntries_ListsEachEntryOnItsOwnLineWithinMarkers(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var description = document.RootElement.GetProperty("description").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(description!.StartsWith("Overall status: Degraded, elapsed 123ms.", StringComparison.Ordinal))
                .IsTrue();
            _ = await Assert.That(description).Contains("- **database**: Healthy (3ms)");
            _ = await Assert.That(description).Contains("- **cache**: Degraded (120ms) - slow response");
            _ = await Assert
                .That(description.IndexOf("database", StringComparison.Ordinal))
                .IsLessThan(description.IndexOf("cache", StringComparison.Ordinal));
            _ = await Assert.That(description.EndsWith("%%%", StringComparison.Ordinal)).IsTrue();
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportDescriptionExceedsMaxLength_DropsWholeOverflowingEntriesAndKeepsClosingMarker(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.opsgenie.com");
        _ = factory.Handler.OnPost("/v2/alerts").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new OpsgenieHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var entries = new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal);
        for (var i = 0; i < 1000; i++)
        {
            entries[$"check-{i}"] = new HealthReportEntry(
                HealthStatus.Degraded,
                new string('x', 100),
                TimeSpan.FromMilliseconds(1),
                null,
                null
            );
        }
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(1000));

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var description = document.RootElement.GetProperty("description").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(description!.Length).IsLessThanOrEqualTo(15000);
            _ = await Assert.That(description).EndsWith("%%%");
            _ = await Assert.That(description.EndsWith(Environment.NewLine + "%%%", StringComparison.Ordinal)).IsTrue();
            _ = await Assert.That(description).DoesNotContain(new string('x', 99) + "%%%");
        }
    }

    private static IOptionsMonitor<OpsgenieOptions> CreateOptionsMonitor(Action<OpsgenieOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<OpsgenieOptions>(
            TestName,
            options =>
            {
                options.ApiKey = "test-key";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<OpsgenieOptions>>();
    }
}
