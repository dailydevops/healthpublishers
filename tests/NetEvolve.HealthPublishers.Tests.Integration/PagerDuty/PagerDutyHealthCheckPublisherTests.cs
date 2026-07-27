namespace NetEvolve.HealthPublishers.Tests.Integration.PagerDuty;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.PagerDuty;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(PagerDuty))]
[ClassDataSource<PagerDutyMockServer>(Shared = SharedType.PerClass)]
public sealed class PagerDutyHealthCheckPublisherTests
{
    private readonly PagerDutyMockServer _server;

    public PagerDutyHealthCheckPublisherTests(PagerDutyMockServer server) => _server = server;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ApiUrl = _server.ServerUrl;
            options.RoutingKey = "integration-test-key";
            options.SystemIdentifier = "integration-tests";
        });
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
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ApiUrl = _server.ServerUrl;
            options.RoutingKey = "integration-test-key";
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow",
                    TimeSpan.FromMilliseconds(5),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ApiUrl = _server.ServerUrl;
            options.RoutingKey = "integration-test-key";
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "boom",
                    TimeSpan.FromMilliseconds(5),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ApiUrl = _server.ServerUrl;
            options.RoutingKey = "integration-test-key";
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    null,
                    TimeSpan.FromMilliseconds(3),
                    null,
                    null,
                    tags: ["db", "sql"]
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null,
                    tags: ["cache"]
                ),
            },
            TimeSpan.FromMilliseconds(123)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_WhenRoutingKeyProvided_SendsRoutingKeyAndSucceeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ApiUrl = _server.ServerUrl;
            options.RoutingKey = "integration-test-key";
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);
        _ = await Assert.That(handler.CapturedRequestBody).Contains("\"routing_key\":\"integration-test-key\"");
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:PagerDuty:Default:ApiUrl", _server.ServerUrl.ToString() },
            { "HealthPublishers:PagerDuty:Default:RoutingKey", "integration-test-key" },
            { "HealthPublishers:PagerDuty:Default:SystemIdentifier", "integration-tests" },
        };
        var (publisher, handler) = CreatePublisher(configureConfiguration: config =>
            config.AddInMemoryCollection(values)
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public void AddPagerDutyPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddPagerDutyPublisher(
                    name,
                    options =>
                    {
                        options.ApiUrl = _server.ServerUrl;
                        options.RoutingKey = "integration-test-key";
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddPagerDutyPublisher(
                    name,
                    options =>
                    {
                        options.ApiUrl = _server.ServerUrl;
                        options.RoutingKey = "integration-test-key";
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddPagerDutyPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
        // Arrange
        await using var secondServer = new PagerDutyMockServer();
        await secondServer.InitializeAsync();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddPagerDutyPublisher(
            "Internal",
            options =>
            {
                options.ApiUrl = _server.ServerUrl;
                options.RoutingKey = "integration-test-key";
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddPagerDutyPublisher(
            "External",
            options =>
            {
                options.ApiUrl = secondServer.ServerUrl;
                options.RoutingKey = "integration-test-key";
                options.SystemIdentifier = "external-system";
            }
        );

        var internalHandler = new CapturingHttpMessageHandler();
        var externalHandler = new CapturingHttpMessageHandler();
        _ = services
            .AddHttpClient($"{DependencyInjectionExtensions.HttpClientNamePrefix}Internal")
            .AddHttpMessageHandler(() => internalHandler);
        _ = services
            .AddHttpClient($"{DependencyInjectionExtensions.HttpClientNamePrefix}External")
            .AddHttpMessageHandler(() => externalHandler);

        var provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>().ToArray();

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
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalHandler.CapturedRequestBody).IsNotNull();
            _ = await Assert.That(externalHandler.CapturedRequestBody).IsNotNull();
            _ = await Assert.That(internalHandler.CapturedRequestBody).Contains("internal-system");
            _ = await Assert.That(externalHandler.CapturedRequestBody).Contains("external-system");
        }
    }

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = document.RootElement;

        if (root.TryGetProperty("payload", out var payload))
        {
            _ = await Assert.That(payload.GetProperty("source").GetString()).IsEqualTo(Environment.MachineName);
            _ = await Assert.That(payload.GetProperty("timestamp").GetString()).IsNotNullOrEmpty();
        }

        _ = await Verify(Normalize(root)).IgnoreParametersForVerified();
    }

    private static object Normalize(JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            return new
            {
                EventAction = root.GetProperty("event_action").GetString(),
                DedupKey = root.GetProperty("dedup_key").GetString(),
            };
        }

        return new
        {
            EventAction = root.GetProperty("event_action").GetString(),
            DedupKey = root.GetProperty("dedup_key").GetString(),
            // source and timestamp are excluded: they vary per environment/run and would break the snapshot elsewhere.
            Summary = payload.GetProperty("summary").GetString(),
            Severity = payload.GetProperty("severity").GetString(),
            CustomDetails = payload.GetProperty("custom_details").GetRawText(),
        };
    }

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<PagerDutyOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddPagerDutyPublisher(options);

        var handler = new CapturingHttpMessageHandler();
        _ = services
            .AddHttpClient(
                $"{DependencyInjectionExtensions.HttpClientNamePrefix}{DependencyInjectionExtensions.DefaultName}"
            )
            .AddHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<IHealthCheckPublisher>(), handler);
    }
}
