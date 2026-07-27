namespace NetEvolve.HealthPublishers.Tests.Unit.Splunk;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Splunk;

[TestGroup(nameof(Splunk))]
public sealed class SplunkOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions();

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
            { "HealthPublishers:Splunk:Default:HecToken", "test-token" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SplunkOptionsConfigure(configuration);
        var options = new SplunkOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.HecToken).IsEqualTo("test-token");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Splunk:Default:HecToken", "test-token" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SplunkOptionsConfigure(configuration);
        var options = new SplunkOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.HecToken).IsEqualTo("test-token");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Splunk:Default:HecToken", "test-token" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SplunkOptionsConfigure(configuration);
        var options = new SplunkOptions();

        // Act
        ((IConfigureOptions<SplunkOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.HecToken).IsEqualTo("test-token");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri("https://splunk.example.com:8088"),
            HecToken = "test-token",
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
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri("https://splunk.example.com:8088"),
            HecToken = "test-token",
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
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());

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
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions { HecToken = "test-token", SystemIdentifier = "checkout-service" };

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
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri("/relative", UriKind.Relative),
            HecToken = "test-token",
            SystemIdentifier = "checkout-service",
        };

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
    [Arguments("ftp://splunk.example.com:8088")]
    [Arguments("ws://splunk.example.com:8088")]
    public async Task Validate_WhenServerUrlSchemeNotHttpOrHttps_ReturnFailure(string endpoint)
    {
        // Arrange
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri(endpoint, UriKind.Absolute),
            HecToken = "test-token",
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ServerUrl must use the http or https scheme.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenHecTokenNullOrWhiteSpace_ReturnFailure(string? hecToken)
    {
        // Arrange
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri("https://splunk.example.com:8088"),
            HecToken = hecToken!,
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The HecToken must be set.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri("https://splunk.example.com:8088"),
            HecToken = "test-token",
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
        var configure = new SplunkOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new SplunkOptions
        {
            ServerUrl = new Uri("https://splunk.example.com:8088"),
            HecToken = "test-token",
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
            { "HealthPublishers:Splunk:Test:ServerUrl", "https://splunk.example.com:8088" },
            { "HealthPublishers:Splunk:Test:HecToken", "test-token" },
            { "HealthPublishers:Splunk:Test:SystemIdentifier", "checkout-service" },
            { "HealthPublishers:Splunk:Test:SourceType", "health-check" },
            { "HealthPublishers:Splunk:Test:Source", "checkout-service" },
            { "HealthPublishers:Splunk:Test:Index", "health" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new SplunkOptionsConfigure(configuration);
        var options = new SplunkOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://splunk.example.com:8088"));
            _ = await Assert.That(options.HecToken).IsEqualTo("test-token");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
            _ = await Assert.That(options.SourceType).IsEqualTo("health-check");
            _ = await Assert.That(options.Source).IsEqualTo("checkout-service");
            _ = await Assert.That(options.Index).IsEqualTo("health");
        }
    }
}
