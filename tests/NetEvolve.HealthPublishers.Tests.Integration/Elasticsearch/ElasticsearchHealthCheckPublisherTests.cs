namespace NetEvolve.HealthPublishers.Tests.Integration.Elasticsearch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Elasticsearch;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

[TestGroup(nameof(Elasticsearch))]
[ClassDataSource<ElasticsearchContainer>(Shared = SharedType.PerClass)]
public sealed class ElasticsearchHealthCheckPublisherTests
{
    private readonly ElasticsearchContainer _container;

    public ElasticsearchHealthCheckPublisherTests(ElasticsearchContainer container) => _container = container;

    [Test]
    public async Task PublishAsync_UseOptions_HealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var indexName = CreateIndexName();
        var publisher = CreatePublisher(options =>
        {
            options.ServerUri = _container.ServerUri;
            options.Username = ElasticsearchContainer.Username;
            options.Password = ElasticsearchContainer.Password;
            options.IndexName = indexName;
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
        await VerifyIndexedDocument(indexName);
    }

    [Test]
    public async Task PublishAsync_UseOptions_DegradedReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var indexName = CreateIndexName();
        var publisher = CreatePublisher(options =>
        {
            options.ServerUri = _container.ServerUri;
            options.Username = ElasticsearchContainer.Username;
            options.Password = ElasticsearchContainer.Password;
            options.IndexName = indexName;
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
        await VerifyIndexedDocument(indexName);
    }

    [Test]
    public async Task PublishAsync_UseOptions_UnhealthyReport_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var indexName = CreateIndexName();
        var publisher = CreatePublisher(options =>
        {
            options.ServerUri = _container.ServerUri;
            options.Username = ElasticsearchContainer.Username;
            options.Password = ElasticsearchContainer.Password;
            options.IndexName = indexName;
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
        await VerifyIndexedDocument(indexName);
    }

    [Test]
    public async Task PublishAsync_UseOptions_MultipleEntries_Succeeds(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var indexName = CreateIndexName();
        var publisher = CreatePublisher(options =>
        {
            options.ServerUri = _container.ServerUri;
            options.Username = ElasticsearchContainer.Username;
            options.Password = ElasticsearchContainer.Password;
            options.IndexName = indexName;
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
        await VerifyIndexedDocument(indexName);
    }

    [Test]
    public async Task PublishAsync_UseConfiguration_HealthyReport_Succeeds(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        var indexName = CreateIndexName();
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Elasticsearch:Default:ServerUri", _container.ServerUri.ToString() },
            { "HealthPublishers:Elasticsearch:Default:Username", ElasticsearchContainer.Username },
            { "HealthPublishers:Elasticsearch:Default:Password", ElasticsearchContainer.Password },
            { "HealthPublishers:Elasticsearch:Default:IndexName", indexName },
            { "HealthPublishers:Elasticsearch:Default:SystemIdentifier", "integration-tests" },
        };
        var publisher = CreatePublisher(configureConfiguration: config => config.AddInMemoryCollection(values));
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(5)
        );

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        await VerifyIndexedDocument(indexName);
    }

    [Test]
    public void AddElasticsearchPublisher_WhenNameAlreadyUsed_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();
        const string? name = "Duplicate";

        // Act
        void Act() =>
            builder
                .AddElasticsearchPublisherForTests(
                    name,
                    options =>
                    {
                        options.ServerUri = _container.ServerUri;
                        options.Username = ElasticsearchContainer.Username;
                        options.Password = ElasticsearchContainer.Password;
                        options.IndexName = CreateIndexName();
                        options.SystemIdentifier = "integration-tests";
                    },
                    ConfigureTestSettings
                )
                .AddElasticsearchPublisherForTests(
                    name,
                    options =>
                    {
                        options.ServerUri = _container.ServerUri;
                        options.Username = ElasticsearchContainer.Username;
                        options.Password = ElasticsearchContainer.Password;
                        options.IndexName = CreateIndexName();
                        options.SystemIdentifier = "integration-tests";
                    },
                    ConfigureTestSettings
                );

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddElasticsearchPublisher_WhenRegisteredWithDifferentNames_PublishesIndependentlyToEachTarget(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Arrange
        await using var secondContainer = new ElasticsearchContainer();
        await secondContainer.InitializeAsync();

        var internalIndex = CreateIndexName();
        var externalIndex = CreateIndexName();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddHealthChecks();

        _ = builder.AddElasticsearchPublisherForTests(
            "Internal",
            options =>
            {
                options.ServerUri = _container.ServerUri;
                options.Username = ElasticsearchContainer.Username;
                options.Password = ElasticsearchContainer.Password;
                options.IndexName = internalIndex;
                options.SystemIdentifier = "internal-system";
            },
            ConfigureTestSettings
        );
        _ = builder.AddElasticsearchPublisherForTests(
            "External",
            options =>
            {
                options.ServerUri = secondContainer.ServerUri;
                options.Username = ElasticsearchContainer.Username;
                options.Password = ElasticsearchContainer.Password;
                options.IndexName = externalIndex;
                options.SystemIdentifier = "external-system";
            },
            ConfigureTestSettings
        );

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
        var internalDocument = await FetchDocument(_container.ServerUri, internalIndex, cancellationToken);
        var externalDocument = await FetchDocument(secondContainer.ServerUri, externalIndex, cancellationToken);
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Length).IsEqualTo(2);
            _ = await Assert.That(internalDocument.SystemIdentifier).IsEqualTo("internal-system");
            _ = await Assert.That(externalDocument.SystemIdentifier).IsEqualTo("external-system");
        }
    }

    private static void ConfigureTestSettings(ElasticsearchClientSettings settings) =>
        settings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);

    private static string CreateIndexName() => $"health-checks-{Guid.NewGuid():N}";

    private async Task VerifyIndexedDocument(string indexName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = await FetchDocument(_container.ServerUri, indexName, cancellationToken);

        using (Assert.Multiple())
        {
            _ = await Assert.That(document.MachineName).IsEqualTo(Environment.MachineName);
            _ = await Assert.That(document.ElapsedMilliseconds >= 0).IsTrue();
        }

        _ = await Verify(Normalize(document)).IgnoreParametersForVerified();
    }

    private static async Task<ElasticsearchHealthDocument> FetchDocument(
        Uri serverUri,
        string indexName,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = DependencyInjectionExtensions.CreateClient(
            new ElasticsearchOptions
            {
                ServerUri = serverUri,
                Username = ElasticsearchContainer.Username,
                Password = ElasticsearchContainer.Password,
                IndexName = indexName,
                SystemIdentifier = "integration-tests",
            },
            configureSettings: ConfigureTestSettings
        );

        _ = await client.Indices.RefreshAsync(indexName, cancellationToken).ConfigureAwait(false);

        var response = await client
            .SearchAsync<ElasticsearchHealthDocument>(request => request.Indices(indexName), cancellationToken)
            .ConfigureAwait(false);

        return response.Documents.Single();
    }

    private static object Normalize(ElasticsearchHealthDocument document) =>
        new
        {
            document.Status,
            document.SystemIdentifier,
            // machine_name and timestamp are excluded: they vary per environment and would break the
            // snapshot elsewhere.
            Entries = document
                .Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => NormalizeEntry(entry.Value)),
        };

    private static object NormalizeEntry(ElasticsearchHealthEntry entry) =>
        new
        {
            entry.Status,
            entry.Description,
            Tags = entry.Tags.ToArray(),
        };

    private static IHealthCheckPublisher CreatePublisher(
        Action<ElasticsearchOptions>? options = null,
        Action<IConfigurationBuilder>? configureConfiguration = null
    )
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        _ = builder.AddElasticsearchPublisherForTests(
            DependencyInjectionExtensions.DefaultName,
            options,
            ConfigureTestSettings
        );

        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IHealthCheckPublisher>();
    }
}
