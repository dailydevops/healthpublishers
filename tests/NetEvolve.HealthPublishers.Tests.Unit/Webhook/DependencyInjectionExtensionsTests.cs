namespace NetEvolve.HealthPublishers.Tests.Unit.Webhook;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Webhook;

[TestGroup(nameof(Webhook))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddWebhookPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddWebhookPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddWebhookPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddWebhookPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddWebhookPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddWebhookPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddWebhookPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddWebhookPublisher(name).AddWebhookPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddWebhookPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddWebhookPublisher(options =>
        {
            options.Uri = new Uri("https://example.com/webhooks/health");
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<WebhookOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task AddWebhookPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddWebhookPublisher(
            name,
            options =>
            {
                options.Uri = new Uri("https://example.com/webhooks/health");
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<WebhookOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task AddWebhookPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddWebhookPublisher(name, options => options.Uri = new Uri("https://example.com/webhooks/health"));
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<WebhookHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddWebhookPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddWebhookPublisher(
                "Internal",
                options => options.Uri = new Uri("https://internal.example.com/webhooks/health")
            )
            .AddWebhookPublisher(
                "External",
                options => options.Uri = new Uri("https://external.example.com/webhooks/health")
            );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<WebhookHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }
}
