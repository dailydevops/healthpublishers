namespace NetEvolve.HealthPublishers.Tests.Integration.AWS.CloudWatch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.AWS.CloudWatch;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

[TestGroup($"{nameof(AWS)}.{nameof(CloudWatch)}")]
[ClassDataSource<CloudWatchFlociContainer>(Shared = SharedType.PerClass)]
public sealed class CloudWatchHealthCheckPublisherTests
{
    private readonly CloudWatchFlociContainer _container;

    public CloudWatchHealthCheckPublisherTests(CloudWatchFlociContainer container) => _container = container;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var @namespace = CreateNamespace();
        var publisher = CreatePublisher(options =>
        {
            options.Region = CloudWatchFlociContainer.Region;
            options.ServiceUrl = _container.ServiceUrl;
            options.AccessKeyId = CloudWatchFlociContainer.AccessKeyId;
            options.SecretAccessKey = CloudWatchFlociContainer.SecretAccessKey;
            options.Namespace = @namespace;
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
        await VerifyPublishedMetrics(@namespace, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var @namespace = CreateNamespace();
        var publisher = CreatePublisher(options =>
        {
            options.Region = CloudWatchFlociContainer.Region;
            options.ServiceUrl = _container.ServiceUrl;
            options.AccessKeyId = CloudWatchFlociContainer.AccessKeyId;
            options.SecretAccessKey = CloudWatchFlociContainer.SecretAccessKey;
            options.Namespace = @namespace;
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
        await VerifyPublishedMetrics(@namespace, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var @namespace = CreateNamespace();
        var publisher = CreatePublisher(options =>
        {
            options.Region = CloudWatchFlociContainer.Region;
            options.ServiceUrl = _container.ServiceUrl;
            options.AccessKeyId = CloudWatchFlociContainer.AccessKeyId;
            options.SecretAccessKey = CloudWatchFlociContainer.SecretAccessKey;
            options.Namespace = @namespace;
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
        await VerifyPublishedMetrics(@namespace, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var @namespace = CreateNamespace();
        var publisher = CreatePublisher(options =>
        {
            options.Region = CloudWatchFlociContainer.Region;
            options.ServiceUrl = _container.ServiceUrl;
            options.AccessKeyId = CloudWatchFlociContainer.AccessKeyId;
            options.SecretAccessKey = CloudWatchFlociContainer.SecretAccessKey;
            options.Namespace = @namespace;
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
                    null
                ),
                ["cache"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(123)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyPublishedMetrics(@namespace, cancellationToken);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var @namespace = CreateNamespace();
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:AWS:CloudWatch:Default:Region", CloudWatchFlociContainer.Region },
            { "HealthPublishers:AWS:CloudWatch:Default:ServiceUrl", _container.ServiceUrl.ToString() },
            { "HealthPublishers:AWS:CloudWatch:Default:AccessKeyId", CloudWatchFlociContainer.AccessKeyId },
            { "HealthPublishers:AWS:CloudWatch:Default:SecretAccessKey", CloudWatchFlociContainer.SecretAccessKey },
            { "HealthPublishers:AWS:CloudWatch:Default:Namespace", @namespace },
            { "HealthPublishers:AWS:CloudWatch:Default:SystemIdentifier", "integration-tests" },
        };
        var publisher = CreatePublisher(configureConfiguration: config => config.AddInMemoryCollection(values));
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyPublishedMetrics(@namespace, cancellationToken);
    }

    [Test]
    public void AddAWSCloudWatchPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddAWSCloudWatchPublisher(name, options => ConfigureValidOptions(options, CreateNamespace()))
                .AddAWSCloudWatchPublisher(name, options => ConfigureValidOptions(options, CreateNamespace()));

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var internalNamespace = CreateNamespace();
        var externalNamespace = CreateNamespace();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddAWSCloudWatchPublisher("Internal", options => ConfigureValidOptions(options, internalNamespace));
        _ = builder.AddAWSCloudWatchPublisher("External", options => ConfigureValidOptions(options, externalNamespace));

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
            _ = await AssertMetricsPublished(internalNamespace, cancellationToken);
            _ = await AssertMetricsPublished(externalNamespace, cancellationToken);
        }
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenRegisteredViaHealthChecksPipeline_PublishesRealHealthReport(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var @namespace = CreateNamespace();
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddAWSCloudWatchPublisher(options => ConfigureValidOptions(options, @namespace));

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IHealthCheckPublisher>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            _ = await AssertMetricsPublished(@namespace, cancellationToken);
        }
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenMultipleRegisteredViaHealthChecksPipeline_PublishesIndependentRealHealthReports(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var internalNamespace = CreateNamespace();
        var externalNamespace = CreateNamespace();
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddAWSCloudWatchPublisher("Internal", options => ConfigureValidOptions(options, internalNamespace))
            .AddAWSCloudWatchPublisher("External", options => ConfigureValidOptions(options, externalNamespace));

        var provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>().ToArray();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        // Act
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(report, cancellationToken);
        }

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(report.Status).IsEqualTo(HealthStatus.Healthy);
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await AssertMetricsPublished(internalNamespace, cancellationToken);
            _ = await AssertMetricsPublished(externalNamespace, cancellationToken);
        }
    }

    private void ConfigureValidOptions(CloudWatchOptions options, string @namespace)
    {
        options.Region = CloudWatchFlociContainer.Region;
        options.ServiceUrl = _container.ServiceUrl;
        options.AccessKeyId = CloudWatchFlociContainer.AccessKeyId;
        options.SecretAccessKey = CloudWatchFlociContainer.SecretAccessKey;
        options.Namespace = @namespace;
        options.SystemIdentifier = "integration-tests";
    }

    private static string CreateNamespace() => $"HealthChecks/{Guid.NewGuid():N}";

    private async Task VerifyPublishedMetrics(string @namespace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metrics = await AssertMetricsPublished(@namespace, cancellationToken);

        _ = await Verify(Normalize(metrics)).IgnoreParametersForVerified();
    }

    private async Task<IReadOnlyList<Metric>> AssertMetricsPublished(
        string @namespace,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var client = DependencyInjectionExtensions.CreateClient(
            new CloudWatchOptions
            {
                Region = CloudWatchFlociContainer.Region,
                ServiceUrl = _container.ServiceUrl,
                AccessKeyId = CloudWatchFlociContainer.AccessKeyId,
                SecretAccessKey = CloudWatchFlociContainer.SecretAccessKey,
                Namespace = @namespace,
                SystemIdentifier = "integration-tests",
            }
        );

        var response = await client.ListMetricsAsync(
            new ListMetricsRequest { Namespace = @namespace },
            cancellationToken
        );

        var metricNames = response
            .Metrics.Select(metric => metric.MetricName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        using (Assert.Multiple())
        {
            _ = await Assert.That(metricNames).Contains("OverallStatus");
            _ = await Assert.That(metricNames).Contains("Duration");
        }

        return response.Metrics;
    }

    private static object[] Normalize(IEnumerable<Metric> metrics) =>
        [
            .. metrics
                .Select(metric => new
                {
                    metric.MetricName,
                    // Dimension values (e.g. MachineName) vary per environment; only their presence is asserted.
                    DimensionNames = metric
                        .Dimensions.Select(dimension => dimension.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray(),
                })
                .OrderBy(metric => metric.MetricName, StringComparer.Ordinal)
                .ThenBy(metric => string.Join(',', metric.DimensionNames), StringComparer.Ordinal)
                .Cast<object>(),
        ];

    private static IHealthCheckPublisher CreatePublisher(
        Action<CloudWatchOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddAWSCloudWatchPublisher(DependencyInjectionExtensions.DefaultName, options);

        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IHealthCheckPublisher>();
    }
}
