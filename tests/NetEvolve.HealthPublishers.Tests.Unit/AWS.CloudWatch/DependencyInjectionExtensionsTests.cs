namespace NetEvolve.HealthPublishers.Tests.Unit.AWS.CloudWatch;

using System;
using System.Linq;
using Amazon;
using Amazon.CloudWatch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.AWS.CloudWatch;

[TestGroup(nameof(CloudWatch))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddAWSCloudWatchPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddAWSCloudWatchPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddAWSCloudWatchPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddAWSCloudWatchPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddAWSCloudWatchPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddAWSCloudWatchPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddAWSCloudWatchPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddAWSCloudWatchPublisher(name).AddAWSCloudWatchPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddAWSCloudWatchPublisher(options =>
        {
            options.Region = "eu-central-1";
            options.Namespace = "HealthChecks";
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<CloudWatchOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.Namespace).IsEqualTo("HealthChecks");
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddAWSCloudWatchPublisher(
            name,
            options =>
            {
                options.Region = "eu-central-1";
                options.Namespace = "HealthChecks";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CloudWatchOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.Namespace).IsEqualTo("HealthChecks");
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddAWSCloudWatchPublisher(
            name,
            options =>
            {
                options.Region = "eu-central-1";
                options.Namespace = "HealthChecks";
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>();
        using (Assert.Multiple())
        {
            _ = await Assert.That(publishers.Count()).IsEqualTo(1);
            _ = await Assert.That(provider.GetRequiredKeyedService<IAmazonCloudWatch>(name)).IsNotNull();
        }
    }

    [Test]
    public async Task AddAWSCloudWatchPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddAWSCloudWatchPublisher(
                "Internal",
                options =>
                {
                    options.Region = "eu-central-1";
                    options.Namespace = "HealthChecks";
                    options.SystemIdentifier = "checkout-service";
                }
            )
            .AddAWSCloudWatchPublisher(
                "External",
                options =>
                {
                    options.Region = "eu-west-1";
                    options.Namespace = "HealthChecks";
                    options.SystemIdentifier = "checkout-service";
                }
            );
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task CreateClient_WhenRegionSet_UsesConfiguredRegion()
    {
        // Arrange
        var options = new CloudWatchOptions { Region = "eu-central-1" };

        // Act
        using var client = (AmazonCloudWatchClient)DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client.Config.RegionEndpoint).IsEqualTo(RegionEndpoint.EUCentral1);
    }

    [Test]
    public async Task CreateClient_WhenServiceUrlSet_UsesConfiguredServiceUrl()
    {
        // Arrange
        var options = new CloudWatchOptions { Region = "eu-central-1", ServiceUrl = new Uri("https://localhost:4566") };

        // Act
        using var client = (AmazonCloudWatchClient)DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client.Config.ServiceURL).IsEqualTo("https://localhost:4566/");
    }

    [Test]
    public async Task CreateClient_WhenNoCredentialsSet_UsesDefaultCredentials()
    {
        // Arrange
        var options = new CloudWatchOptions { Region = "eu-central-1" };

        // Act
        using var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task CreateClient_WhenAccessKeyIdAndSecretAccessKeySet_UsesExplicitCredentials()
    {
        // Arrange
        var options = new CloudWatchOptions
        {
            Region = "eu-central-1",
            AccessKeyId = "test-access-key",
            SecretAccessKey = "test-secret-key",
        };

        // Act
        using var client = DependencyInjectionExtensions.CreateClient(options);

        // Assert
        _ = await Assert.That(client).IsNotNull();
    }
}
