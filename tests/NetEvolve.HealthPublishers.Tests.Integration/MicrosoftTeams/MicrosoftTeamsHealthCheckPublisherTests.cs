namespace NetEvolve.HealthPublishers.Tests.Integration.MicrosoftTeams;

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
using NetEvolve.HealthPublishers.MicrosoftTeams;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(MicrosoftTeams))]
[ClassDataSource<MicrosoftTeamsMockServer>(Shared = SharedType.PerClass)]
public sealed class MicrosoftTeamsHealthCheckPublisherTests
{
    private readonly MicrosoftTeamsMockServer _server;

    public MicrosoftTeamsHealthCheckPublisherTests(MicrosoftTeamsMockServer server) => _server = server;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.WebhookUrl = _server.WebhookUrl;
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
            options.WebhookUrl = _server.WebhookUrl;
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
            options.WebhookUrl = _server.WebhookUrl;
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
            options.WebhookUrl = _server.WebhookUrl;
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
            { "HealthPublishers:MicrosoftTeams:Default:WebhookUrl", _server.WebhookUrl.ToString() },
            { "HealthPublishers:MicrosoftTeams:Default:SystemIdentifier", "integration-tests" },
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
    public void AddMicrosoftTeamsPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddMicrosoftTeamsPublisher(
                    name,
                    options =>
                    {
                        options.WebhookUrl = _server.WebhookUrl;
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddMicrosoftTeamsPublisher(
                    name,
                    options =>
                    {
                        options.WebhookUrl = _server.WebhookUrl;
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddMicrosoftTeamsPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
        // Arrange
        await using var secondServer = new MicrosoftTeamsMockServer();
        await secondServer.InitializeAsync();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddMicrosoftTeamsPublisher(
            "Internal",
            options =>
            {
                options.WebhookUrl = _server.WebhookUrl;
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddMicrosoftTeamsPublisher(
            "External",
            options =>
            {
                options.WebhookUrl = secondServer.WebhookUrl;
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
            _ = await Assert.That(internalHandler.CapturedRequestBody).Contains("\"value\":\"internal-system\"");
            _ = await Assert.That(externalHandler.CapturedRequestBody).Contains("\"value\":\"external-system\"");
        }
    }

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = document.RootElement;
        var content = root.GetProperty("attachments")[0].GetProperty("content");
        var facts = content
            .GetProperty("body")[1]
            .GetProperty("facts")
            .EnumerateArray()
            .ToDictionary(
                fact => fact.GetProperty("title").GetString()!,
                fact => fact.GetProperty("value").GetString()
            );

        using (Assert.Multiple())
        {
            _ = await Assert.That(facts["Machine"]).IsEqualTo(Environment.MachineName);
            _ = await Assert
                .That(
                    DateTimeOffset.Parse(facts["Checked at"]!, CultureInfo.InvariantCulture) > DateTimeOffset.MinValue
                )
                .IsTrue();
        }

        _ = await Verify(Normalize(content)).IgnoreParametersForVerified();
    }

    private static object Normalize(JsonElement content)
    {
        var body = content.GetProperty("body");
        var titleBlock = body[0];
        var facts = body[1]
            .GetProperty("facts")
            .EnumerateArray()
            .Select(fact => new
            {
                Title = fact.GetProperty("title").GetString(),
                Value = fact.GetProperty("value").GetString(),
            })
            // machine_name and checked-at vary per environment/run and would break the snapshot elsewhere.
            .Where(fact => fact.Title is not ("Machine" or "Checked at"))
            .ToArray();

        return new
        {
            Type = content.GetProperty("type").GetString(),
            Title = titleBlock.GetProperty("text").GetString(),
            Color = titleBlock.GetProperty("color").GetString(),
            Facts = facts,
            Details = body.GetArrayLength() > 2 ? body[2].GetProperty("text").GetString() : null,
        };
    }

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<MicrosoftTeamsOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddMicrosoftTeamsPublisher(options);

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
