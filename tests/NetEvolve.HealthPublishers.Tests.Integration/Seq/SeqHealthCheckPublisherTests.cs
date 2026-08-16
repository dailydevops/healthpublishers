namespace NetEvolve.HealthPublishers.Tests.Integration.Seq;

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
using NetEvolve.HealthPublishers.Seq;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(Seq))]
[ClassDataSource<SeqContainer>(Shared = SharedType.PerClass)]
public sealed class SeqHealthCheckPublisherTests
{
    private readonly SeqContainer _container;

    public SeqHealthCheckPublisherTests(SeqContainer container) => _container = container;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _container.ServerUrl;
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _container.ServerUrl;
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
            options.ServerUrl = _container.ServerUrl;
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
            options.ServerUrl = _container.ServerUrl;
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
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public async Task PublishAsync_WhenApiKeyProvided_SendsApiKeyHeaderAndSucceeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _container.ServerUrl;
            options.SystemIdentifier = "integration-tests";
            options.ApiKey = "integration-test-key";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        _ = await Assert.That(handler.CapturedRequestBody).IsNotNull();
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Seq:Default:ServerUrl", _container.ServerUrl.ToString() },
            { "HealthPublishers:Seq:Default:SystemIdentifier", "integration-tests" },
        };
        var (publisher, handler) = CreatePublisher(configureConfiguration: config =>
            config.AddInMemoryCollection(values)
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyCapturedRequest(handler);
    }

    [Test]
    public void AddSeqPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddSeqPublisher(
                    name,
                    options =>
                    {
                        options.ServerUrl = _container.ServerUrl;
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddSeqPublisher(
                    name,
                    options =>
                    {
                        options.ServerUrl = _container.ServerUrl;
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddSeqPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        await using var secondContainer = new SeqContainer();
        await secondContainer.InitializeAsync();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddSeqPublisher(
            "Internal",
            options =>
            {
                options.ServerUrl = _container.ServerUrl;
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddSeqPublisher(
            "External",
            options =>
            {
                options.ServerUrl = secondContainer.ServerUrl;
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
            await publisher.PublishAsync(report, cancellationToken);
        }

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalHandler.CapturedRequestBody).IsNotNull();
            _ = await Assert.That(externalHandler.CapturedRequestBody).IsNotNull();
            _ = await Assert
                .That(internalHandler.CapturedRequestBody)
                .Contains("\"SystemIdentifier\":\"internal-system\"");
            _ = await Assert
                .That(externalHandler.CapturedRequestBody)
                .Contains("\"SystemIdentifier\":\"external-system\"");
        }
    }

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = document.RootElement;

        using (Assert.Multiple())
        {
            _ = await Assert.That(string.IsNullOrWhiteSpace(root.GetProperty("@t").GetString())).IsFalse();
            _ = await Assert.That(root.GetProperty("MachineName").GetString()).IsEqualTo(Environment.MachineName);
        }

        _ = await Verify(Normalize(root)).IgnoreParametersForVerified();
    }

    private static object Normalize(JsonElement root) =>
        new
        {
            MessageTemplate = root.GetProperty("@mt").GetString(),
            Level = root.GetProperty("@l").GetString(),
            Status = root.GetProperty("Status").GetString(),
            ElapsedMilliseconds = root.GetProperty("ElapsedMilliseconds").GetDouble(),
            SystemIdentifier = root.GetProperty("SystemIdentifier").GetString(),
            Entries = root.GetProperty("Entries")
                .EnumerateObject()
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Name, entry => NormalizeEntry(entry.Value)),
        };

    private static object NormalizeEntry(JsonElement entry) =>
        new
        {
            Status = entry.GetProperty("Status").GetString(),
            Description = entry.GetProperty("Description").ValueKind == JsonValueKind.Null
                ? null
                : entry.GetProperty("Description").GetString(),
            ElapsedMilliseconds = entry.GetProperty("ElapsedMilliseconds").GetDouble(),
            Tags = entry.GetProperty("Tags").EnumerateArray().Select(tag => tag.GetString()).ToArray(),
        };

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<SeqOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddSeqPublisher(options);

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
