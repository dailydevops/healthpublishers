namespace NetEvolve.HealthPublishers.Tests.Unit.Seq;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Seq;

[TestGroup(nameof(Seq))]
public sealed class SeqOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions();

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
            { "HealthPublishers:Seq:Default:ServerUrl", "https://seq.example.com" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SeqOptionsConfigure(configuration);
        var options = new SeqOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://seq.example.com"));
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Seq:Default:ServerUrl", "https://seq.example.com" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SeqOptionsConfigure(configuration);
        var options = new SeqOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://seq.example.com"));
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Seq:Default:ServerUrl", "https://seq.example.com" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SeqOptionsConfigure(configuration);
        var options = new SeqOptions();

        // Act
        ((IConfigureOptions<SeqOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://seq.example.com"));
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions
        {
            ServerUrl = new Uri("https://seq.example.com"),
            SystemIdentifier = "checkout-service",
        };

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
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions
        {
            ServerUrl = new Uri("https://seq.example.com"),
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate(name, options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsNull_ReturnFailure()
    {
        // Arrange
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());

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
    public async Task Validate_WhenServerUrlNull_ReturnFailure()
    {
        // Arrange
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions();

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ServerUrl must be set.");
        }
    }

    [Test]
    public async Task Validate_WhenServerUrlNotAbsolute_ReturnFailure()
    {
        // Arrange
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions { ServerUrl = new Uri("/relative", UriKind.Relative) };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ServerUrl must be a valid absolute URI.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions
        {
            ServerUrl = new Uri("https://seq.example.com"),
            SystemIdentifier = systemIdentifier!,
        };

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
        var configure = new SeqOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SeqOptions
        {
            ServerUrl = new Uri("https://seq.example.com"),
            SystemIdentifier = "checkout-service",
        };

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
            { "HealthPublishers:Seq:Test:ServerUrl", "https://seq.example.com" },
            { "HealthPublishers:Seq:Test:ApiKey", "test-key" },
            { "HealthPublishers:Seq:Test:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SeqOptionsConfigure(configuration);
        var options = new SeqOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://seq.example.com"));
            _ = await Assert.That(options.ApiKey).IsEqualTo("test-key");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
        }
    }
}
