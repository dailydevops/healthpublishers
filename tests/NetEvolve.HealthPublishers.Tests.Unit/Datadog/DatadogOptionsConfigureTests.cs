namespace NetEvolve.HealthPublishers.Tests.Unit.Datadog;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Datadog;

[TestGroup(nameof(Datadog))]
public sealed class DatadogOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions();

        // Act
        void Act() => configure.Configure(" ", options);

        // Assert
        _ = Assert.Throws<ArgumentException>("resolvedName", Act);
    }

    [Test]
    public async Task Configure_WhenArgumentNameNull_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Datadog:Default:ApiKey", "test-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new DatadogOptionsConfigure(configuration);
        var options = new DatadogOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Datadog:Default:ApiKey", "test-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new DatadogOptionsConfigure(configuration);
        var options = new DatadogOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Datadog:Default:ApiKey", "test-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new DatadogOptionsConfigure(configuration);
        var options = new DatadogOptions();

        // Act
        ((IConfigureOptions<DatadogOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions { ApiKey = "test-key", SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate(" ", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The name cannot be null or whitespace.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task Validate_WhenNameNullOrEmpty_TreatsAsDefaultNameAndReturnSuccess(string? name)
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions { ApiKey = "test-key", SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate(name, options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsNull_ReturnFailure()
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());

        // Act
        var result = configure.Validate("Test", null!);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The option cannot be null.");
        }
    }

    [Test]
    public async Task Validate_WhenApiUrlNotAbsolute_ReturnFailure()
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions
        {
            ApiUrl = new Uri("/relative", UriKind.Relative),
            ApiKey = "test-key",
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ApiUrl must be a valid absolute URI.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenApiKeyNullOrWhiteSpace_ReturnFailure(string? apiKey)
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions { ApiKey = apiKey!, SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ApiKey must be set.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions { ApiKey = "test-key", SystemIdentifier = systemIdentifier! };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The SystemIdentifier must be set.");
        }
    }

    [Test]
    public async Task Validate_WhenOptionsValid_ReturnSuccess()
    {
        // Arrange
        var configure = new DatadogOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new DatadogOptions { ApiKey = "test-key", SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Configure_WhenConfigurationAvailable_ExpectedValues()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Datadog:Test:ApiUrl", "https://api.datadoghq.eu" },
            { "HealthPublishers:Datadog:Test:ApiKey", "test-key" },
            { "HealthPublishers:Datadog:Test:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new DatadogOptionsConfigure(configuration);
        var options = new DatadogOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.ApiUrl).IsEqualTo(new Uri("https://api.datadoghq.eu"));
            _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
        }
    }
}
