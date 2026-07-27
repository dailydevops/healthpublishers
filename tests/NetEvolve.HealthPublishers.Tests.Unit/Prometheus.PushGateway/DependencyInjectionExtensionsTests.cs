namespace NetEvolve.HealthPublishers.Tests.Unit.Prometheus.PushGateway;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Prometheus.PushGateway;

[TestGroup(nameof(PushGateway))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddPrometheusPushGateway_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddPrometheusPushGateway();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddPrometheusPushGateway_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddPrometheusPushGateway(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddPrometheusPushGateway_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddPrometheusPushGateway(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddPrometheusPushGateway_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddPrometheusPushGateway(name).AddPrometheusPushGateway(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddPrometheusPushGateway_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddPrometheusPushGateway(options =>
        {
            options.ServerUrl = new Uri("https://pushgateway.example.com");
            options.Job = "checkout-service";
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<PrometheusPushGatewayOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://pushgateway.example.com"));
    }

    [Test]
    public async Task AddPrometheusPushGateway_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddPrometheusPushGateway(
            name,
            options =>
            {
                options.ServerUrl = new Uri("https://pushgateway.example.com");
                options.Job = "checkout-service";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<PrometheusPushGatewayOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://pushgateway.example.com"));
    }

    [Test]
    public async Task AddPrometheusPushGateway_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddPrometheusPushGateway(name);
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider
            .GetServices<IHealthCheckPublisher>()
            .OfType<PrometheusPushGatewayHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddPrometheusPushGateway_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddPrometheusPushGateway("Internal").AddPrometheusPushGateway("External");
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider
            .GetServices<IHealthCheckPublisher>()
            .OfType<PrometheusPushGatewayHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task ConfigureHttpClient_WhenCalled_SetsBaseAddressFromNamedOptions()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<PrometheusPushGatewayOptions>(
            name,
            options => options.ServerUrl = new Uri("https://pushgateway.example.com")
        );
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient(name, provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsEqualTo(new Uri("https://pushgateway.example.com"));
    }

    [Test]
    public async Task ConfigureHttpClient_WhenNameDiffersFromConfiguredOptions_BaseAddressStaysNull()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.Configure<PrometheusPushGatewayOptions>(
            "Other",
            options => options.ServerUrl = new Uri("https://pushgateway.example.com")
        );
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient("Test", provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsNull();
    }
}
