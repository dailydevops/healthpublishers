namespace NetEvolve.HealthPublishers.Tests.Unit.PagerDuty;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.PagerDuty;

[TestGroup(nameof(PagerDuty))]
public sealed class PagerDutyOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions();

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
            { "HealthPublishers:PagerDuty:Default:RoutingKey", "test-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PagerDutyOptionsConfigure(configuration);
        var options = new PagerDutyOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.RoutingKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:PagerDuty:Default:RoutingKey", "test-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PagerDutyOptionsConfigure(configuration);
        var options = new PagerDutyOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.RoutingKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:PagerDuty:Default:RoutingKey", "test-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PagerDutyOptionsConfigure(configuration);
        var options = new PagerDutyOptions();

        // Act
        ((IConfigureOptions<PagerDutyOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.RoutingKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions { RoutingKey = "test-key", SystemIdentifier = "checkout-service" };

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
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions { RoutingKey = "test-key", SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate(name, options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsNull_ReturnFailure()
    {
        // Arrange
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());

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
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions
        {
            ApiUrl = new Uri("/relative", UriKind.Relative),
            RoutingKey = "test-key",
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
    public async Task Validate_WhenRoutingKeyNullOrWhiteSpace_ReturnFailure(string? routingKey)
    {
        // Arrange
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions { RoutingKey = routingKey!, SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The RoutingKey must be set.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions { RoutingKey = "test-key", SystemIdentifier = systemIdentifier! };

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
        var configure = new PagerDutyOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PagerDutyOptions { RoutingKey = "test-key", SystemIdentifier = "checkout-service" };

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
            { "HealthPublishers:PagerDuty:Test:ApiUrl", "https://events.eu.pagerduty.com" },
            { "HealthPublishers:PagerDuty:Test:RoutingKey", "test-key" },
            { "HealthPublishers:PagerDuty:Test:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PagerDutyOptionsConfigure(configuration);
        var options = new PagerDutyOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.ApiUrl).IsEqualTo(new Uri("https://events.eu.pagerduty.com"));
            _ = await Assert.That(options.RoutingKey).IsEqualTo("test-key");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
        }
    }
}
