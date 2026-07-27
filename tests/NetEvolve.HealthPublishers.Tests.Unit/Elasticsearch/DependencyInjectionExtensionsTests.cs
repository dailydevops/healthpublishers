namespace NetEvolve.HealthPublishers.Tests.Unit.Elasticsearch;

using System;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Elasticsearch;

[TestGroup(nameof(Elasticsearch))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddElasticsearchPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddElasticsearchPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddElasticsearchPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddElasticsearchPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddElasticsearchPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddElasticsearchPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddElasticsearchPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddElasticsearchPublisher(name).AddElasticsearchPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddElasticsearchPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddElasticsearchPublisher(options =>
        {
            options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
            options.IndexName = "health-checks";
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<ElasticsearchOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.IndexName).IsEqualTo("health-checks");
    }

    [Test]
    public async Task AddElasticsearchPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddElasticsearchPublisher(
            name,
            options =>
            {
                options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
                options.IndexName = "health-checks";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<ElasticsearchOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.IndexName).IsEqualTo("health-checks");
    }

    [Test]
    public async Task AddElasticsearchPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddElasticsearchPublisher(
            name,
            options =>
            {
                options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
                options.IndexName = "health-checks";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<ElasticsearchHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddElasticsearchPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddElasticsearchPublisher(
                "Internal",
                options =>
                {
                    options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
                    options.IndexName = "health-checks";
                    options.SystemIdentifier = "checkout-service";
                }
            )
            .AddElasticsearchPublisher(
                "External",
                options =>
                {
                    options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
                    options.IndexName = "health-checks";
                    options.SystemIdentifier = "checkout-service";
                }
            );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<ElasticsearchHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task CreateClient_WhenServerUriSet_UsesConfiguredServerUri()
    {
        // Arrange
        var options = new ElasticsearchOptions { ServerUri = new Uri("https://elasticsearch.example.com:9200") };

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        var node = client.ElasticsearchClientSettings.NodePool.Nodes.Single();
        _ = await Assert.That(node.Uri).IsEqualTo(new Uri("https://elasticsearch.example.com:9200"));
    }

    [Test]
    public async Task CreateClient_WhenServerUriNotSet_UsesDefaultServerUri()
    {
        // Arrange
        var options = new ElasticsearchOptions();

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        var node = client.ElasticsearchClientSettings.NodePool.Nodes.Single();
        _ = await Assert.That(node.Uri).IsEqualTo(DependencyInjectionExtensions.DefaultServerUri);
    }

    [Test]
    public async Task CreateClient_WhenNoCredentialsSet_AuthenticationIsNull()
    {
        // Arrange
        var options = new ElasticsearchOptions { ServerUri = new Uri("https://elasticsearch.example.com:9200") };

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client.ElasticsearchClientSettings.Authentication).IsNull();
    }

    [Test]
    public async Task CreateClient_WhenApiKeySet_UsesApiKeyAuthentication()
    {
        // Arrange
        var options = new ElasticsearchOptions
        {
            ServerUri = new Uri("https://elasticsearch.example.com:9200"),
            ApiKey = "test-api-key",
        };

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        var authentication = client.ElasticsearchClientSettings.Authentication;
        using (Assert.Multiple())
        {
            _ = await Assert.That(authentication).IsNotNull();
            _ = await Assert.That(authentication).IsTypeOf<ApiKey>();
            _ = await Assert.That(authentication!.TryGetAuthorizationParameters(out var parameters)).IsTrue();
            _ = await Assert.That(parameters).IsEqualTo("test-api-key");
        }
    }

    [Test]
    public async Task CreateClient_WhenUsernameAndPasswordSet_UsesBasicAuthentication()
    {
        // Arrange
        var options = new ElasticsearchOptions
        {
            ServerUri = new Uri("https://elasticsearch.example.com:9200"),
            Username = "elastic",
            Password = "secret",
        };

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client.ElasticsearchClientSettings.Authentication).IsTypeOf<BasicAuthentication>();
    }

    [Test]
    public async Task CreateClient_WhenApiKeyAndUsernamePasswordSet_ApiKeyTakesPrecedence()
    {
        // Arrange
        var options = new ElasticsearchOptions
        {
            ServerUri = new Uri("https://elasticsearch.example.com:9200"),
            ApiKey = "test-api-key",
            Username = "elastic",
            Password = "secret",
        };

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client.ElasticsearchClientSettings.Authentication).IsTypeOf<ApiKey>();
    }

    [Test]
    public async Task CreateClient_WhenConfigureSettingsProvided_InvokesCallback()
    {
        // Arrange
        var options = new ElasticsearchOptions { ServerUri = new Uri("https://elasticsearch.example.com:9200") };
        var invoked = false;

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options, configureSettings: _ => invoked = true);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(invoked).IsTrue();
            _ = await Assert.That(client).IsNotNull();
        }
    }

    [Test]
    public async Task CreateClient_WhenRequestInvokerProvided_UsesProvidedInvoker()
    {
        // Arrange
        var options = new ElasticsearchOptions { ServerUri = new Uri("https://elasticsearch.example.com:9200") };
        using var invoker = new HttpRequestInvoker();

        // Act
        var client = DependencyInjectionExtensions.CreateClient(options, invoker);

        // Assert
        _ = await Assert.That(client).IsNotNull();
    }
}
