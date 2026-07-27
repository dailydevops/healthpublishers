namespace NetEvolve.HealthPublishers.Tests.Unit.PagerDuty;

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.PagerDuty;

[TestGroup(nameof(PagerDuty))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddPagerDutyPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddPagerDutyPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddPagerDutyPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddPagerDutyPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddPagerDutyPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddPagerDutyPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddPagerDutyPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddPagerDutyPublisher(name).AddPagerDutyPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddPagerDutyPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddPagerDutyPublisher(options =>
        {
            options.RoutingKey = "test-key";
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<PagerDutyOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.RoutingKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task AddPagerDutyPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddPagerDutyPublisher(
            name,
            options =>
            {
                options.RoutingKey = "test-key";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<PagerDutyOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.RoutingKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task AddPagerDutyPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddPagerDutyPublisher(name, options => options.RoutingKey = "test-key");
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<PagerDutyHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddPagerDutyPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddPagerDutyPublisher("Internal", options => options.RoutingKey = "test-key")
            .AddPagerDutyPublisher("External", options => options.RoutingKey = "test-key");
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<PagerDutyHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task ConfigureHttpClient_WhenApiUrlSet_UsesConfiguredApiUrl()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<PagerDutyOptions>(
            name,
            options => options.ApiUrl = new Uri("https://events.eu.pagerduty.com")
        );
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient(name, provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsEqualTo(new Uri("https://events.eu.pagerduty.com"));
    }

    [Test]
    public async Task ConfigureHttpClient_WhenApiUrlNotSet_FallsBackToDefaultApiUrl()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<PagerDutyOptions>(name, options => { });
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient(name, provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsEqualTo(DependencyInjectionExtensions.DefaultApiUrl);
    }
}
