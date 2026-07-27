namespace NetEvolve.HealthPublishers.Tests.Integration.Prometheus.PushGateway;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Prometheus.PushGateway;
using NetEvolve.HealthPublishers.Tests.Integration.Internals;

[TestGroup(nameof(PushGateway))]
[ClassDataSource<PrometheusPushGatewayMockServer>(Shared = SharedType.PerClass)]
public sealed class PrometheusPushGatewayHealthCheckPublisherTests
{
    private readonly PrometheusPushGatewayMockServer _server;

    public PrometheusPushGatewayHealthCheckPublisherTests(PrometheusPushGatewayMockServer server) => _server = server;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _server.ServerUrl;
            options.Job = "checkout-service";
            options.Instance = "checkout-service-01";
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
            options.ServerUrl = _server.ServerUrl;
            options.Job = "checkout-service";
            options.Instance = "checkout-service-01";
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
            options.ServerUrl = _server.ServerUrl;
            options.Job = "checkout-service";
            options.Instance = "checkout-service-01";
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
            options.ServerUrl = _server.ServerUrl;
            options.Job = "checkout-service";
            options.Instance = "checkout-service-01";
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
    public async Task PublishAsync_WhenInstanceProvided_PostsToJobAndInstancePathAndSucceeds()
    {
        // Arrange
        var (publisher, handler) = CreatePublisher(options =>
        {
            options.ServerUrl = _server.ServerUrl;
            options.Job = "checkout-service";
            options.Instance = "checkout-service-01";
            options.SystemIdentifier = "integration-tests";
        });
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.That(handler.CapturedRequestBody).IsNotNull();
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Prometheus:PushGateway:Default:ServerUrl", _server.ServerUrl.ToString() },
            { "HealthPublishers:Prometheus:PushGateway:Default:Job", "checkout-service" },
            { "HealthPublishers:Prometheus:PushGateway:Default:Instance", "checkout-service-01" },
            { "HealthPublishers:Prometheus:PushGateway:Default:SystemIdentifier", "integration-tests" },
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
    public void AddPrometheusPushGateway_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddPrometheusPushGateway(
                    name,
                    options =>
                    {
                        options.ServerUrl = _server.ServerUrl;
                        options.Job = "checkout-service";
                        options.SystemIdentifier = "integration-tests";
                    }
                )
                .AddPrometheusPushGateway(
                    name,
                    options =>
                    {
                        options.ServerUrl = _server.ServerUrl;
                        options.Job = "checkout-service";
                        options.SystemIdentifier = "integration-tests";
                    }
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddPrometheusPushGateway_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget()
    {
        // Arrange
        await using var secondServer = new PrometheusPushGatewayMockServer();
        await secondServer.InitializeAsync();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddPrometheusPushGateway(
            "Internal",
            options =>
            {
                options.ServerUrl = _server.ServerUrl;
                options.Job = "checkout-service";
                options.Instance = "checkout-service-01";
                options.SystemIdentifier = "internal-system";
            }
        );
        _ = builder.AddPrometheusPushGateway(
            "External",
            options =>
            {
                options.ServerUrl = secondServer.ServerUrl;
                options.Job = "checkout-service";
                options.Instance = "checkout-service-01";
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
                .Contains("system_identifier=\"internal-system\"");
            _ = await Assert
                .That(externalHandler.CapturedRequestBody)
                .Contains("system_identifier=\"external-system\"");
        }
    }

    private static readonly Regex LastPublishTimestampLine = new(
        @"^healthcheck_last_publish_timestamp_seconds\{[^}]*\} \d+$",
        RegexOptions.Multiline | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1)
    );

    private static async Task VerifyCapturedRequest(CapturingHttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler.CapturedRequestBody);

        using (Assert.Multiple())
        {
            _ = await Assert.That(handler.CapturedRequestBody).Contains($"machine_name=\"{Environment.MachineName}\"");
            _ = await Assert.That(LastPublishTimestampLine.IsMatch(handler.CapturedRequestBody)).IsTrue();
        }

        // machine_name is excluded: it varies per environment and would break the snapshot elsewhere.
        var normalized = handler.CapturedRequestBody.Replace(
            $"machine_name=\"{Environment.MachineName}\"",
            "machine_name=\"placeholder\"",
            StringComparison.Ordinal
        );

        // The last-publish timestamp is excluded too: it is the current unix time and changes on every run.
        normalized = LastPublishTimestampLine.Replace(
            normalized,
            match => match.Value[..match.Value.LastIndexOf(' ')] + " <timestamp>"
        );

        _ = await Verify(normalized).IgnoreParametersForVerified();
    }

    private static (IHealthCheckPublisher Publisher, CapturingHttpMessageHandler Handler) CreatePublisher(
        Action<PrometheusPushGatewayOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddPrometheusPushGateway(options);

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
