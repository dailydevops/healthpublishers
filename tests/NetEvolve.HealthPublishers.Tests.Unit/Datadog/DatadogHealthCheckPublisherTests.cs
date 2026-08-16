namespace NetEvolve.HealthPublishers.Tests.Unit.Datadog;

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
using NetEvolve.HealthPublishers.Datadog;
using TUnit.Mocks;

[TestGroup(nameof(Datadog))]
public sealed class DatadogHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy, "success")]
    [Arguments(HealthStatus.Degraded, "warning")]
    [Arguments(HealthStatus.Unhealthy, "error")]
    public async Task PublishAsync_WhenReportHasStatus_SendsRequestWithMappedAlertType(
        HealthStatus status,
        string expectedAlertType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/api/v1/events");
            _ = await Assert.That(request.Body).Contains($"\"alert_type\":\"{expectedAlertType}\"");
            _ = await Assert.That(request.Body).Contains($"\"status:{status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SendsApiKeyHeader(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.ApiKey = "test-key");
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Headers["DD-API-KEY"]).Contains("test-key");
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifierTags(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains($"\"machine_name:{Environment.MachineName}\"");
            _ = await Assert.That(request.Body).Contains("\"system_identifier:checkout-service\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForDateHappened(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert
            .That(request.Body)
            .Contains($"\"date_happened\":{timeProvider.GetUtcNow().ToUnixTimeSeconds()}");
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInText(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
    public async Task PublishAsync_WhenReportHasNoEntries_SendsPlainSummaryTextWithoutMarkers(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        var request = factory.Handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var text = document.RootElement.GetProperty("text").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(text).IsEqualTo("Overall status: Healthy, elapsed 42ms.");
            _ = await Assert.That(text).DoesNotContain("%%%");
        }
    }

    [Test]
    public async Task PublishAsync_WhenEntryHasNoDescription_OmitsDescriptionSeparator(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        using var document = JsonDocument.Parse(request.Body!);
        var text = document.RootElement.GetProperty("text").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(text).Contains("- **self**: Healthy (5ms)");
            _ = await Assert.That(text).DoesNotContain("**self**: Healthy (5ms) -");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasMultipleEntries_ListsEachEntryOnItsOwnLineWithinMarkers(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var text = document.RootElement.GetProperty("text").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(text!.StartsWith("Overall status: Degraded, elapsed 123ms.", StringComparison.Ordinal))
                .IsTrue();
            _ = await Assert.That(text).Contains("- **database**: Healthy (3ms)");
            _ = await Assert.That(text).Contains("- **cache**: Degraded (120ms) - slow response");
            _ = await Assert
                .That(text.IndexOf("database", StringComparison.Ordinal))
                .IsLessThan(text.IndexOf("cache", StringComparison.Ordinal));
            _ = await Assert.That(text.EndsWith("%%%", StringComparison.Ordinal)).IsTrue();
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportTextExceedsMaxLength_DropsWholeOverflowingEntriesAndKeepsClosingMarker(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://api.datadoghq.com");
        _ = factory.Handler.OnPost("/api/v1/events").Respond(HttpStatusCode.Accepted);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new DatadogHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
        var text = document.RootElement.GetProperty("text").GetString();
        using (Assert.Multiple())
        {
            _ = await Assert.That(text!.Length).IsLessThanOrEqualTo(4000);
            _ = await Assert.That(text).EndsWith("%%%");
            // No half-written entry immediately before the closing marker.
            _ = await Assert.That(text.EndsWith(Environment.NewLine + "%%%", StringComparison.Ordinal)).IsTrue();
            // Every included entry line kept its full 100-char description, none were cut mid-word.
            _ = await Assert.That(text).DoesNotContain(new string('x', 99) + "%%%");
        }
    }

    private static IOptionsMonitor<DatadogOptions> CreateOptionsMonitor(Action<DatadogOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<DatadogOptions>(
            TestName,
            options =>
            {
                options.ApiKey = "test-key";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<DatadogOptions>>();
    }
}
