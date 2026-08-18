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
using Microsoft.Extensions.Time.Testing;
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
    public async Task PublishAsync_UseOptions_FreshPublisherHealthyReport_DoesNotSend(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Arrange - a fresh publisher's baseline is Healthy, so a first Healthy report is a no-op, not a post.
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.WebhookUrl = _server.WebhookUrl;
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5L), null, null),
            },
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        _ = await Assert.That(handler.CapturedRequestBody).IsNull();
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
                    TimeSpan.FromMilliseconds(5L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                    TimeSpan.FromMilliseconds(5L),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                    TimeSpan.FromMilliseconds(3L),
                    null,
                    null,
                    tags: ["db", "sql"]
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120L),
                    null,
                    null,
                    tags: ["cache"]
                ),
            },
            TimeSpan.FromMilliseconds(123L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_UnhealthyReport_Succeeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:MicrosoftTeams:Default:WebhookUrl", _server.WebhookUrl.ToString() },
            { "HealthPublishers:MicrosoftTeams:Default:SystemIdentifier", "integration-tests" },
        };
        var (publisher, handler) = CreatePublisher(configureConfiguration: config =>
            config.AddInMemoryCollection(values)
        );
        // A fresh publisher's baseline is Healthy, so the report must be a worsening to send immediately.
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_WhenStatusImprovesAfterWorsening_WaitsForRecoveryConfirmationDelayBeforeSending(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var delay = TimeSpan.FromMinutes(5L);
        var (publisher, handler) = CreatePublisher(
            options =>
            {
                options.WebhookUrl = _server.WebhookUrl;
                options.SystemIdentifier = "integration-tests";
                options.RecoveryConfirmationDelay = delay;
            },
            timeProvider: timeProvider
        );
        var unhealthyReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );
        var healthyReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Healthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act & Assert - the worsening posts immediately.
        await publisher.PublishAsync(unhealthyReport, cancellationToken);
        _ = await Assert.That(handler.CapturedRequestBody).Contains("\"color\":\"attention\"");

        // The subsequent improvement does not post right away - it only starts the recovery-confirmation timer,
        // so no new request is sent and the last captured request is still the worsening one.
        await publisher.PublishAsync(healthyReport, cancellationToken);
        _ = await Assert.That(handler.CapturedRequestBody).Contains("\"color\":\"attention\"");

        // Once the configured delay has elapsed, the still-improved status is finally reported.
        timeProvider.Advance(delay);
        await publisher.PublishAsync(healthyReport, cancellationToken);
        _ = await Assert.That(handler.CapturedRequestBody).Contains("\"color\":\"good\"");
    }

    [Test]
    public void AddMicrosoftTeamsPublisher_WhenNameAlreadyUsed_ThrowsArgumentException(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

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
    public async Task AddMicrosoftTeamsPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        // A fresh publisher's baseline is Healthy, so the report must be a worsening to send immediately.
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5L)
        );

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, cancellationToken);
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
        // body[0] is a ColumnSet: icon column, then a title column holding the actual TextBlock.
        var titleBlock = body[0].GetProperty("columns")[1].GetProperty("items")[0];
        var facts = body[1]
            .GetProperty("facts")
            .EnumerateArray()
            .Select(fact => new
            {
                Title = fact.GetProperty("title").GetString(),
                Value = fact.GetProperty("value").GetString(),
            })
            // machine name, checked-at, and since all vary per environment/run and would break the snapshot elsewhere.
            .Where(fact => fact.Title is not ("Machine" or "Checked at" or "Since"))
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
        Action<IConfigurationBuilder>? configureConfiguration = null,
        TimeProvider? timeProvider = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // AddMicrosoftTeamsPublisher only registers TimeProvider.System via TryAddSingleton, so registering one
        // upfront lets tests control time (e.g. to assert the RecoveryConfirmationDelay behavior without a real wait).
        if (timeProvider is not null)
        {
            _ = services.AddSingleton(timeProvider);
        }

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
