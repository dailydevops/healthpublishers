namespace NetEvolve.HealthPublishers.Tests.Integration.Slack;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Slack;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(Slack))]
[ClassDataSource<SlackMockServer>(Shared = SharedType.PerClass)]
public sealed class SlackHealthCheckPublisherTests
{
    private readonly SlackMockServer _server;

    public SlackHealthCheckPublisherTests(SlackMockServer server) => _server = server;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.WebhookUrl = _server.ServerUrl;
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
            options.WebhookUrl = _server.ServerUrl;
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
            options.WebhookUrl = _server.ServerUrl;
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
            options.WebhookUrl = _server.ServerUrl;
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
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Slack:Default:WebhookUrl", _server.ServerUrl.ToString() },
            { "HealthPublishers:Slack:Default:SystemIdentifier", "integration-tests" },
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
    public void AddSlackPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddSlackPublisher(name, options => options.WebhookUrl = _server.ServerUrl)
                .AddSlackPublisher(name, options => options.WebhookUrl = _server.ServerUrl);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddSlackPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
        // Arrange
        await using var secondServer = new SlackMockServer();
        await secondServer.InitializeAsync();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddSlackPublisher(
            "Internal",
            options =>
            {
                options.WebhookUrl = _server.ServerUrl;
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddSlackPublisher(
            "External",
            options =>
            {
                options.WebhookUrl = secondServer.ServerUrl;
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
            _ = await Assert.That(internalHandler.CapturedRequestBody).Contains("internal-system");
            _ = await Assert.That(externalHandler.CapturedRequestBody).Contains("external-system");
        }
    }

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = document.RootElement;
        var attachment = root.GetProperty("attachments")[0];

        using (Assert.Multiple())
        {
            _ = await Assert.That(attachment.GetProperty("ts").GetInt64() > 0).IsTrue();
            _ = await Assert
                .That(
                    attachment
                        .GetProperty("fields")
                        .EnumerateArray()
                        .Select(field => field.GetProperty("value").GetString())
                )
                .Contains(Environment.MachineName);
        }

        _ = await Verify(Normalize(root, attachment)).IgnoreParametersForVerified();
    }

    private static object Normalize(JsonElement root, JsonElement attachment) =>
        new
        {
            Text = root.GetProperty("text").GetString(),
            Color = attachment.GetProperty("color").GetString(),
            AttachmentText = attachment.GetProperty("text").GetString(),
            // The "Machine" field is excluded: it varies per environment and would break the snapshot elsewhere.
            Fields = attachment
                .GetProperty("fields")
                .EnumerateArray()
                .Select(field => field.GetProperty("title").GetString())
                .Where(title => title != "Machine")
                .ToArray(),
            SystemIdentifierValue = attachment
                .GetProperty("fields")
                .EnumerateArray()
                .First(field => field.GetProperty("title").GetString() == "System Identifier")
                .GetProperty("value")
                .GetString(),
        };

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<SlackOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddSlackPublisher(options);

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
