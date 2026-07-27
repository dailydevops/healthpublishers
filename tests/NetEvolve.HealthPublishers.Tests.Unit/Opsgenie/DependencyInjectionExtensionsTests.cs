namespace NetEvolve.HealthPublishers.Tests.Unit.Opsgenie;

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Opsgenie;

[TestGroup(nameof(Opsgenie))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddOpsgeniePublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddOpsgeniePublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddOpsgeniePublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddOpsgeniePublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddOpsgeniePublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddOpsgeniePublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddOpsgeniePublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddOpsgeniePublisher(name).AddOpsgeniePublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddOpsgeniePublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddOpsgeniePublisher(options =>
        {
            options.ApiKey = "test-key";
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<OpsgenieOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task AddOpsgeniePublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddOpsgeniePublisher(
            name,
            options =>
            {
                options.ApiKey = "test-key";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpsgenieOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task AddOpsgeniePublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddOpsgeniePublisher(name, options => options.ApiKey = "test-key");
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<OpsgenieHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddOpsgeniePublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddOpsgeniePublisher("Internal", options => options.ApiKey = "test-key")
            .AddOpsgeniePublisher("External", options => options.ApiKey = "test-key");
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<OpsgenieHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task ConfigureHttpClient_WhenApiUrlSet_UsesConfiguredApiUrl()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<OpsgenieOptions>(
            name,
            options => options.ApiUrl = new Uri("https://api.eu.opsgenie.com")
        );
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient(name, provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsEqualTo(new Uri("https://api.eu.opsgenie.com"));
    }

    [Test]
    public async Task ConfigureHttpClient_WhenApiUrlNotSet_FallsBackToDefaultApiUrl()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<OpsgenieOptions>(name, options => { });
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient(name, provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsEqualTo(DependencyInjectionExtensions.DefaultApiUrl);
    }
}
