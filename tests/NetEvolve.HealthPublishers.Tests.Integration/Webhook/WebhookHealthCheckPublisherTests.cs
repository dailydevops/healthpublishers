namespace NetEvolve.HealthPublishers.Tests.Integration.Webhook;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;
using NetEvolve.HealthPublishers.Webhook;

[TestGroup(nameof(Webhook))]
[ClassDataSource<WebhookMockServer>(Shared = SharedType.PerClass)]
public sealed class WebhookHealthCheckPublisherTests
{
    private readonly WebhookMockServer _server;

    public WebhookHealthCheckPublisherTests(WebhookMockServer server) => _server = server;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.Uri = _server.ServerUrl;
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
            options.Uri = _server.ServerUrl;
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
            options.Uri = _server.ServerUrl;
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
            options.Uri = _server.ServerUrl;
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
    public async Task PublishAsync_WhenHeadersProvided_SendsHeadersAndSucceeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.Uri = _server.ServerUrl;
            options.SystemIdentifier = "integration-tests";
            options.Headers["X-Api-Key"] = "integration-test-key";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.That(handler.CapturedRequestHeaders?.GetValues("X-Api-Key")).Contains("integration-test-key");
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Webhook:Default:Uri", _server.ServerUrl.ToString() },
            { "HealthPublishers:Webhook:Default:SystemIdentifier", "integration-tests" },
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
    public void AddWebhookPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddWebhookPublisher(
                    name,
                    options =>
                    {
                        options.Uri = _server.ServerUrl;
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddWebhookPublisher(
                    name,
                    options =>
                    {
                        options.Uri = _server.ServerUrl;
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddWebhookPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
        // Arrange
        await using var secondServer = new WebhookMockServer();
        await secondServer.InitializeAsync();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddWebhookPublisher(
            "Internal",
            options =>
            {
                options.Uri = _server.ServerUrl;
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddWebhookPublisher(
            "External",
            options =>
            {
                options.Uri = secondServer.ServerUrl;
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
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
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
            _ = await Assert
                .That(internalHandler.CapturedRequestBody)
                .Contains("\"systemIdentifier\":\"internal-system\"");
            _ = await Assert
                .That(externalHandler.CapturedRequestBody)
                .Contains("\"systemIdentifier\":\"external-system\"");
        }
    }

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = document.RootElement;

        using (Assert.Multiple())
        {
            _ = await Assert.That(root.GetProperty("machineName").GetString()).IsEqualTo(Environment.MachineName);
            _ = await Assert
                .That(
                    DateTimeOffset.TryParse(
                        root.GetProperty("timestamp").GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out _
                    )
                )
                .IsTrue();
        }

        _ = await Verify(Normalize(root)).IgnoreParametersForVerified();
    }

    private static object Normalize(JsonElement root) =>
        new
        {
            SystemIdentifier = root.GetProperty("systemIdentifier").GetString(),
            Status = root.GetProperty("status").GetString(),
            TotalDurationMs = root.GetProperty("totalDurationMs").GetDouble(),
            // machineName and timestamp are excluded: they vary per environment/run and would break the snapshot elsewhere.
            Entries = root.GetProperty("entries")
                .EnumerateArray()
                .Select(entry => new
                {
                    Name = entry.GetProperty("name").GetString(),
                    Status = entry.GetProperty("status").GetString(),
                    DurationMs = entry.GetProperty("durationMs").GetDouble(),
                    Description = entry.GetProperty("description").ValueKind == JsonValueKind.Null
                        ? null
                        : entry.GetProperty("description").GetString(),
                    Tags = entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToArray(),
                })
                .ToArray(),
        };

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<WebhookOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddWebhookPublisher(options);

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
