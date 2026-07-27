namespace NetEvolve.HealthPublishers.Tests.Unit.Seq;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Seq;
using TUnit.Mocks;

[TestGroup(nameof(Seq))]
public sealed class SeqHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy, "Information")]
    [Arguments(HealthStatus.Degraded, "Warning")]
    [Arguments(HealthStatus.Unhealthy, "Error")]
    public async Task PublishAsync_WhenReportHasStatus_SendsRequestWithMappedLevel(
        HealthStatus status,
        string expectedLevel
    )
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://seq.example.com");
        _ = factory.Handler.OnPost("/ingest/clef").Respond(HttpStatusCode.Created);
        var optionsMonitor = CreateOptionsMonitor(options => options.ServerUrl = new Uri("https://seq.example.com"));
        var publisher = new SeqHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
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
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/ingest/clef");
            _ = await Assert.That(request.Headers.ContainsKey("X-Seq-ApiKey")).IsFalse();
            _ = await Assert.That(request.Body).Contains($"\"@l\":\"{expectedLevel}\"");
            _ = await Assert.That(request.Body).Contains($"\"Status\":\"{status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenApiKeyProvided_SendsApiKeyHeader()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://seq.example.com");
        _ = factory.Handler.OnPost("/ingest/clef").Respond(HttpStatusCode.Created);
        var optionsMonitor = CreateOptionsMonitor(options =>
        {
            options.ServerUrl = new Uri("https://seq.example.com");
            options.ApiKey = "test-key";
        });
        var publisher = new SeqHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Headers["X-Seq-ApiKey"]).Contains("test-key");
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifier()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://seq.example.com");
        _ = factory.Handler.OnPost("/ingest/clef").Respond(HttpStatusCode.Created);
        var optionsMonitor = CreateOptionsMonitor(options =>
        {
            options.ServerUrl = new Uri("https://seq.example.com");
            options.SystemIdentifier = "checkout-service";
        });
        var publisher = new SeqHealthCheckPublisher(TestName, factory, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains($"\"MachineName\":\"{Environment.MachineName}\"");
            _ = await Assert.That(request.Body).Contains("\"SystemIdentifier\":\"checkout-service\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp()
    {
        // Arrange
        using var factory = Mock.HttpClientFactory().WithBaseAddress("https://seq.example.com");
        _ = factory.Handler.OnPost("/ingest/clef").Respond(HttpStatusCode.Created);
        var optionsMonitor = CreateOptionsMonitor(options => options.ServerUrl = new Uri("https://seq.example.com"));
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new SeqHealthCheckPublisher(TestName, factory, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = factory.Handler.Requests[0];
        _ = await Assert.That(request.Body).Contains("\"@t\":\"2026-01-02T03:04:05.0000000\\u002B00:00\"");
    }

    private static IOptionsMonitor<SeqOptions> CreateOptionsMonitor(Action<SeqOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<SeqOptions>(
            TestName,
            options =>
            {
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<SeqOptions>>();
    }
}
