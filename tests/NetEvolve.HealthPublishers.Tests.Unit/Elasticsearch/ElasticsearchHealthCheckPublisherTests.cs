namespace NetEvolve.HealthPublishers.Tests.Unit.Elasticsearch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Elasticsearch;
using TUnit.Mocks;
using TUnit.Mocks.Http;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

[TestGroup(nameof(Elasticsearch))]
#pragma warning disable CA2000 // Dispose objects before losing scope: mock handlers/invokers here are lightweight test doubles whose lifetime spans the test method; explicit disposal adds no value.
public sealed class ElasticsearchHealthCheckPublisherTests
{
    private const string TestName = "Test";
    private const string IndexPath = "/health-checks/_doc";

    private const string SuccessBody = """
        {"_index":"health-checks","_id":"abc123","_version":1,"result":"created","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":0,"_primary_term":1}
        """;

    [Test]
    [Arguments(HealthStatus.Healthy)]
    [Arguments(HealthStatus.Degraded)]
    [Arguments(HealthStatus.Unhealthy)]
    public async Task PublishAsync_WhenReportHasStatus_SendsDocumentWithStatus(HealthStatus status)
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var (publisher, _) = CreatePublisher(handler);
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
        var request = handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo(IndexPath);
            _ = await Assert.That(request.Body).Contains($"\"status\":\"{status}\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_SendsMachineNameAndSystemIdentifier()
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var (publisher, _) = CreatePublisher(handler, options => options.SystemIdentifier = "checkout-service");
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = handler.Requests[0];
        using (Assert.Multiple())
        {
            _ = await Assert.That(request.Body).Contains($"\"machine_name\":\"{Environment.MachineName}\"");
            _ = await Assert.That(request.Body).Contains("\"system_identifier\":\"checkout-service\"");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForTimestamp()
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var (publisher, _) = CreatePublisher(handler, timeProvider: timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var expected = timeProvider.GetUtcNow();
        _ = await Assert.That(document.RootElement.GetProperty("timestamp").GetDateTimeOffset()).IsEqualTo(expected);
    }

    [Test]
    public async Task PublishAsync_WhenIndexNameConfigured_IndexesIntoConfiguredIndex()
    {
        // Arrange
        var handler = Mock.HttpHandler();
        _ = handler
            .OnPost("/custom-index/_doc")
            .Respond(HttpStatusCode.Created)
            .WithJsonContent(SuccessBody)
            .WithHeader("X-Elastic-Product", "Elasticsearch");
        var (publisher, _) = CreatePublisher(handler, options => options.IndexName = "custom-index");
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.That(handler.Requests[0].RequestUri!.AbsolutePath).IsEqualTo("/custom-index/_doc");
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInDocument()
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var (publisher, _) = CreatePublisher(handler);
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
        var request = handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var entry = document.RootElement.GetProperty("entries").GetProperty("database");
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
    public async Task PublishAsync_WhenReportHasNoEntries_SendsEmptyEntriesAndOverallStatus()
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var (publisher, _) = CreatePublisher(handler);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        using (Assert.Multiple())
        {
            _ = await Assert.That(document.RootElement.GetProperty("status").GetString()).IsEqualTo("Healthy");
            _ = await Assert.That(document.RootElement.GetProperty("elapsed_ms").GetDouble()).IsEqualTo(42d);
            _ = await Assert.That(document.RootElement.GetProperty("entries").EnumerateObject().Any()).IsFalse();
        }
    }

    [Test]
    public async Task PublishAsync_WhenEntryDescriptionNull_SendsNullDescription()
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var (publisher, _) = CreatePublisher(handler);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
            },
            TimeSpan.Zero
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var request = handler.Requests[0];
        using var document = JsonDocument.Parse(request.Body!);
        var entry = document.RootElement.GetProperty("entries").GetProperty("self");
        var hasDescription = entry.TryGetProperty("description", out var description);
        var isNullOrMissing = !hasDescription || description.ValueKind == JsonValueKind.Null;
        _ = await Assert.That(isNullOrMissing).IsTrue();
    }

    [Test]
    public async Task PublishAsync_WhenResponseIsNotSuccessStatusCode_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = Mock.HttpHandler();
        _ = handler
            .OnPost(IndexPath)
            .Respond(HttpStatusCode.InternalServerError)
            .WithJsonContent("""{"error":{"type":"boom_exception","reason":"boom"},"status":500}""");
        var (publisher, _) = CreatePublisher(handler);
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

    [Test]
    public async Task PublishAsync_WhenTransportThrows_ThrowsHttpRequestExceptionWithOriginalExceptionAsInnerException()
    {
        // Arrange
        var handler = Mock.HttpHandler();
        handler.OnPost(IndexPath).Throws(new HttpRequestException("connection refused"));
        var (publisher, _) = CreatePublisher(handler);
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
        using (Assert.Multiple())
        {
            _ = await Assert.That(caught).IsNotNull();
            _ = await Assert.That(caught!.InnerException).IsNotNull();
        }
    }

    private static MockHttpHandler CreateSuccessHandler()
    {
        var handler = Mock.HttpHandler();
        _ = handler
            .OnPost(IndexPath)
            .Respond(HttpStatusCode.Created)
            .WithJsonContent(SuccessBody)
            .WithHeader("X-Elastic-Product", "Elasticsearch");
        return handler;
    }

    private static (ElasticsearchHealthCheckPublisher Publisher, ElasticsearchClient Client) CreatePublisher(
        MockHttpHandler handler,
        Action<ElasticsearchOptions>? configure = null,
        TimeProvider? timeProvider = null
    )
    {
        var optionsMonitor = CreateOptionsMonitor(configure ?? (_ => { }));
        var options = optionsMonitor.Get(TestName);
        var invoker = new HttpRequestInvoker((_, _) => handler);
        var client = DependencyInjectionExtensions.CreateClient(options, invoker);
        var publisher = new ElasticsearchHealthCheckPublisher(
            TestName,
            client,
            optionsMonitor,
            timeProvider ?? TimeProvider.System
        );
        return (publisher, client);
    }

    private static IOptionsMonitor<ElasticsearchOptions> CreateOptionsMonitor(Action<ElasticsearchOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<ElasticsearchOptions>(
            TestName,
            options =>
            {
                options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
                options.IndexName = "health-checks";
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<ElasticsearchOptions>>();
    }
}
#pragma warning restore CA2000
