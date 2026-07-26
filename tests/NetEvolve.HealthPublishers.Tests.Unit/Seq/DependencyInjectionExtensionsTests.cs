namespace NetEvolve.HealthPublishers.Tests.Unit.Seq;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Seq;

[TestGroup(nameof(Seq))]
public sealed class DependencyInjectionExtensionsTests
{
    [Test]
    public void AddSeqPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddSeqPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddSeqPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddSeqPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddSeqPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddSeqPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddSeqPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddSeqPublisher(name).AddSeqPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddSeqPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddSeqPublisher(options =>
        {
            options.ServerUrl = new Uri("https://seq.example.com");
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<SeqOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://seq.example.com"));
    }

    [Test]
    public async Task AddSeqPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddSeqPublisher(
            name,
            options =>
            {
                options.ServerUrl = new Uri("https://seq.example.com");
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<SeqOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://seq.example.com"));
    }

    [Test]
    public async Task AddSeqPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddSeqPublisher(name);
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<SeqHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddSeqPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddSeqPublisher("Internal").AddSeqPublisher("External");
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider.GetServices<IHealthCheckPublisher>().OfType<SeqHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task ConfigureHttpClient_WhenCalled_SetsBaseAddressFromNamedOptions()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<SeqOptions>(name, options => options.ServerUrl = new Uri("https://seq.example.com"));
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient(name, provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsEqualTo(new Uri("https://seq.example.com"));
    }

    [Test]
    public async Task ConfigureHttpClient_WhenNameDiffersFromConfiguredOptions_BaseAddressStaysNull()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.Configure<SeqOptions>("Other", options => options.ServerUrl = new Uri("https://seq.example.com"));
        var provider = services.BuildServiceProvider();
        using var client = new HttpClient();

        // Act
        DependencyInjectionExtensions.ConfigureHttpClient("Test", provider, client);

        // Assert
        _ = await Assert.That(client.BaseAddress).IsNull();
    }
}
