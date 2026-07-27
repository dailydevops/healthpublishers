namespace NetEvolve.HealthPublishers.Tests.Unit.ApplicationInsights;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.ApplicationInsights;

[TestGroup(nameof(ApplicationInsights))]
public sealed class DependencyInjectionExtensionsTests
{
    private const string TestConnectionString = "InstrumentationKey=11111111-1111-1111-1111-111111111111";

    [Test]
    public void AddApplicationInsightsPublisher_WhenArgumentBuilderNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = default(IHealthChecksBuilder);

        // Act
        void Act() => builder.AddApplicationInsightsPublisher();

        // Assert
        _ = Assert.Throws<ArgumentNullException>("builder", Act);
    }

    [Test]
    public void AddApplicationInsightsPublisher_WhenArgumentNameNull_ThrowArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = default;

        // Act
        void Act() => builder.AddApplicationInsightsPublisher(name!);

        // Assert
        _ = Assert.Throws<ArgumentNullException>("name", Act);
    }

    [Test]
    public void AddApplicationInsightsPublisher_WhenArgumentNameEmpty_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        var name = string.Empty;

        // Act
        void Act() => builder.AddApplicationInsightsPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>("name", Act);
    }

    [Test]
    public void AddApplicationInsightsPublisher_WhenArgumentNameIsAlreadyUsed_ThrowArgumentException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        void Act() => builder.AddApplicationInsightsPublisher(name).AddApplicationInsightsPublisher(name);

        // Assert
        _ = Assert.Throws<ArgumentException>(nameof(name), Act);
    }

    [Test]
    public async Task AddApplicationInsightsPublisher_WhenCalledWithoutName_RegistersUnderDefaultName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder.AddApplicationInsightsPublisher(options =>
        {
            options.ConnectionString = TestConnectionString;
            options.SystemIdentifier = "checkout-service";
        });
        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<ApplicationInsightsOptions>>()
            .Get(DependencyInjectionExtensions.DefaultName);

        // Assert
        _ = await Assert.That(options.ConnectionString).IsEqualTo(TestConnectionString);
    }

    [Test]
    public async Task AddApplicationInsightsPublisher_WhenArgumentOptionsProvided_RegisterOptionsWithName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddApplicationInsightsPublisher(
            name,
            options =>
            {
                options.ConnectionString = TestConnectionString;
                options.SystemIdentifier = "checkout-service";
            }
        );
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<ApplicationInsightsOptions>>().Get(name);

        // Assert
        _ = await Assert.That(options.ConnectionString).IsEqualTo(TestConnectionString);
    }

    [Test]
    public async Task AddApplicationInsightsPublisher_WhenCalled_RegistersServicesAndHealthCheckPublisher()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();
        const string? name = "Test";

        // Act
        _ = builder.AddApplicationInsightsPublisher(name, options => options.ConnectionString = TestConnectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider
            .GetServices<IHealthCheckPublisher>()
            .OfType<ApplicationInsightsHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AddApplicationInsightsPublisher_WhenCalledWithDifferentNames_RegistersBothPublishers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddSingleton<IConfiguration>(configuration).AddHealthChecks();

        // Act
        _ = builder
            .AddApplicationInsightsPublisher("Internal", options => options.ConnectionString = TestConnectionString)
            .AddApplicationInsightsPublisher("External", options => options.ConnectionString = TestConnectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var publishers = provider
            .GetServices<IHealthCheckPublisher>()
            .OfType<ApplicationInsightsHealthCheckPublisher>();
        _ = await Assert.That(publishers.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task CreateTelemetryConfiguration_WhenCalled_SetsConnectionStringFromNamedOptions()
    {
        // Arrange
        const string? name = "Test";
        var services = new ServiceCollection();
        _ = services.Configure<ApplicationInsightsOptions>(
            name,
            options => options.ConnectionString = TestConnectionString
        );
        var provider = services.BuildServiceProvider();

        // Act
        using var configuration = DependencyInjectionExtensions.CreateTelemetryConfiguration(name, provider);

        // Assert
        _ = await Assert.That(configuration.ConnectionString).IsEqualTo(TestConnectionString);
    }

    [Test]
    public async Task CreateTelemetryConfiguration_WhenNameDiffersFromConfiguredOptions_ConnectionStringStaysNull()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.Configure<ApplicationInsightsOptions>(
            "Other",
            options => options.ConnectionString = TestConnectionString
        );
        var provider = services.BuildServiceProvider();

        // Act
        using var configuration = DependencyInjectionExtensions.CreateTelemetryConfiguration("Test", provider);

        // Assert
        _ = await Assert.That(configuration.ConnectionString).IsNull();
    }
}
