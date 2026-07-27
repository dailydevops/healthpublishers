namespace NetEvolve.HealthPublishers.Tests.Integration.Splunk;

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
using NetEvolve.HealthPublishers.Splunk;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(Splunk))]
[ClassDataSource<SplunkContainer>(Shared = SharedType.PerClass)]
public sealed class SplunkHealthCheckPublisherTests
{
    private readonly SplunkContainer _container;

    public SplunkHealthCheckPublisherTests(SplunkContainer container) => _container = container;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _container.ServerUrl;
            options.HecToken = SplunkContainer.HecToken;
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
            options.ServerUrl = _container.ServerUrl;
            options.HecToken = SplunkContainer.HecToken;
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
            options.ServerUrl = _container.ServerUrl;
            options.HecToken = SplunkContainer.HecToken;
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
            options.ServerUrl = _container.ServerUrl;
            options.HecToken = SplunkContainer.HecToken;
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
    public async Task PublishAsync_WhenHecTokenProvided_SendsAuthorizationHeaderAndSucceeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _container.ServerUrl;
            options.HecToken = SplunkContainer.HecToken;
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert
            .That(handler.CapturedRequestHeaders?.GetValues("Authorization"))
            .Contains($"Splunk {SplunkContainer.HecToken}");
    }

    [Test]
    public async Task PublishAsync_WhenSourceTypeSourceAndIndexProvided_SendsThemAndSucceeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _container.ServerUrl;
            options.HecToken = SplunkContainer.HecToken;
            options.SystemIdentifier = "integration-tests";
            options.SourceType = "health-check";
            options.Source = "integration-tests";
            options.Index = "main";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);
        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        using (Assert.Multiple())
        {
            _ = await Assert.That(document.RootElement.GetProperty("sourcetype").GetString()).IsEqualTo("health-check");
            _ = await Assert
                .That(document.RootElement.GetProperty("source").GetString())
                .IsEqualTo("integration-tests");
            _ = await Assert.That(document.RootElement.GetProperty("index").GetString()).IsEqualTo("main");
        }
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Splunk:Default:ServerUrl", _container.ServerUrl.ToString() },
            { "HealthPublishers:Splunk:Default:HecToken", SplunkContainer.HecToken },
            { "HealthPublishers:Splunk:Default:SystemIdentifier", "integration-tests" },
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
    public void AddSplunkPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddSplunkPublisher(
                    name,
                    options =>
                    {
                        options.ServerUrl = _container.ServerUrl;
                        options.HecToken = SplunkContainer.HecToken;
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddSplunkPublisher(
                    name,
                    options =>
                    {
                        options.ServerUrl = _container.ServerUrl;
                        options.HecToken = SplunkContainer.HecToken;
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddSplunkPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddSplunkPublisher(
            "Internal",
            options =>
            {
                options.ServerUrl = _container.ServerUrl;
                options.HecToken = SplunkContainer.HecToken;
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddSplunkPublisher(
            "External",
            options =>
            {
                options.ServerUrl = _container.ServerUrl;
                options.HecToken = SplunkContainer.HecToken;
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
                .Contains("\"system_identifier\":\"internal-system\"");
            _ = await Assert
                .That(externalHandler.CapturedRequestBody)
                .Contains("\"system_identifier\":\"external-system\"");
        }
    }

    [Test]
    public async Task AddSplunkPublisher_WhenRegisteredViaHealthChecksPipeline_PublishesRealHealthReport()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddSplunkPublisher(options =>
            {
                options.ServerUrl = _container.ServerUrl;
                options.HecToken = SplunkContainer.HecToken;
                options.SystemIdentifier = "integration-tests";
            });

        var handler = new CapturingHttpMessageHandler();
        _ = services
            .AddHttpClient(
                $"{DependencyInjectionExtensions.HttpClientNamePrefix}{DependencyInjectionExtensions.DefaultName}"
            )
            .AddHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IHealthCheckPublisher>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(CancellationToken.None);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            _ = await Assert.That(handler.CapturedRequestBody).IsNotNull();
        }
    }

    [Test]
    public async Task AddSplunkPublisher_WhenMultipleRegisteredViaHealthChecksPipeline_PublishesIndependentRealHealthReports()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddSplunkPublisher(
                "Internal",
                options =>
                {
                    options.ServerUrl = _container.ServerUrl;
                    options.HecToken = SplunkContainer.HecToken;
                    options.SystemIdentifier = "internal-system";
                }
            )
            .AddSplunkPublisher(
                "External",
                options =>
                {
                    options.ServerUrl = _container.ServerUrl;
                    options.HecToken = SplunkContainer.HecToken;
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
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(CancellationToken.None);

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalHandler.CapturedRequestBody).IsNotNull();
            _ = await Assert.That(externalHandler.CapturedRequestBody).IsNotNull();
            _ = await Assert
                .That(internalHandler.CapturedRequestBody)
                .Contains("\"system_identifier\":\"internal-system\"");
            _ = await Assert
                .That(externalHandler.CapturedRequestBody)
                .Contains("\"system_identifier\":\"external-system\"");
        }
    }

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using var document = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = document.RootElement;

        using (Assert.Multiple())
        {
            _ = await Assert.That(root.GetProperty("time").GetDouble() > 0).IsTrue();
            _ = await Assert
                .That(root.GetProperty("event").GetProperty("machine_name").GetString())
                .IsEqualTo(Environment.MachineName);
        }

        _ = await Verify(Normalize(root)).IgnoreParametersForVerified();
    }

    private static object Normalize(JsonElement root)
    {
        var eventElement = root.GetProperty("event");

        return new
        {
            Message = eventElement.GetProperty("message").GetString(),
            Status = eventElement.GetProperty("status").GetString(),
            ElapsedMilliseconds = eventElement.GetProperty("elapsed_ms").GetDouble(),
            SystemIdentifier = eventElement.GetProperty("system_identifier").GetString(),
            // machine_name (on the event) and time (top-level) are excluded: they vary per environment
            // and would break the snapshot elsewhere.
            Entries = eventElement
                .GetProperty("entries")
                .EnumerateObject()
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Name, entry => NormalizeEntry(entry.Value)),
        };
    }

    private static object NormalizeEntry(JsonElement entry) =>
        new
        {
            Status = entry.GetProperty("status").GetString(),
            Description = entry.GetProperty("description").ValueKind == JsonValueKind.Null
                ? null
                : entry.GetProperty("description").GetString(),
            ElapsedMilliseconds = entry.GetProperty("elapsed_ms").GetDouble(),
            Tags = entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToArray(),
        };

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<SplunkOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddSplunkPublisher(options);

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
