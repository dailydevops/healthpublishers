namespace NetEvolve.HealthPublishers.Tests.Unit.MicrosoftTeams;

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.MicrosoftTeams;

[TestGroup(nameof(MicrosoftTeams))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddMicrosoftTeamsPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddMicrosoftTeamsPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddMicrosoftTeamsPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddMicrosoftTeamsPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddMicrosoftTeamsPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddMicrosoftTeamsPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddMicrosoftTeamsPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddMicrosoftTeamsPublisher(name).AddMicrosoftTeamsPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddMicrosoftTeamsPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddMicrosoftTeamsPublisher(options =>
        {
            options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x");
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<MicrosoftTeamsOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task AddMicrosoftTeamsPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddMicrosoftTeamsPublisher(
            name,
            options =>
            {
                options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x");
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<MicrosoftTeamsOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task AddMicrosoftTeamsPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddMicrosoftTeamsPublisher(
            name,
            options => options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x")
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<MicrosoftTeamsHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddMicrosoftTeamsPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddMicrosoftTeamsPublisher(
                "Ops",
                options => options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/ops")
            )
            .AddMicrosoftTeamsPublisher(
                "OnCall",
                options => options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/oncall")
            );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<MicrosoftTeamsHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }
}
