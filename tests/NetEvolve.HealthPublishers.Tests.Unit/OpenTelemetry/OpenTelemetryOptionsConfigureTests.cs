namespace NetEvolve.HealthPublishers.Tests.Unit.OpenTelemetry;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.OpenTelemetry;

[TestGroup(nameof(OpenTelemetry))]
public sealed class OpenTelemetryOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new OpenTelemetryOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new OpenTelemetryOptions();

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
            { "HealthPublishers:OpenTelemetry:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new OpenTelemetryOptionsConfigure(configuration);
        var options = new OpenTelemetryOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:OpenTelemetry:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new OpenTelemetryOptionsConfigure(configuration);
        var options = new OpenTelemetryOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:OpenTelemetry:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new OpenTelemetryOptionsConfigure(configuration);
        var options = new OpenTelemetryOptions();

        // Act
        ((IConfigureOptions<OpenTelemetryOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new OpenTelemetryOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new OpenTelemetryOptions { SystemIdentifier = "checkout-service" };

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
        var configure = new OpenTelemetryOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new OpenTelemetryOptions { SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate(name, options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsNull_ReturnFailure()
    {
        // Arrange
        var configure = new OpenTelemetryOptionsConfigure(new ConfigurationBuilder().Build());

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
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new OpenTelemetryOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new OpenTelemetryOptions { SystemIdentifier = systemIdentifier! };

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
        var configure = new OpenTelemetryOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new OpenTelemetryOptions { SystemIdentifier = "checkout-service" };

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
            { "HealthPublishers:OpenTelemetry:Test:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new OpenTelemetryOptionsConfigure(configuration);
        var options = new OpenTelemetryOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }
}
