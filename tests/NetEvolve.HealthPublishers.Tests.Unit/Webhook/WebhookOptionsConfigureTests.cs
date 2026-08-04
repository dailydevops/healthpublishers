namespace NetEvolve.HealthPublishers.Tests.Unit.Webhook;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Webhook;

[TestGroup(nameof(Webhook))]
public sealed class WebhookOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions();

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
            { "HealthPublishers:Webhook:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new WebhookOptionsConfigure(configuration);
        var options = new WebhookOptions();

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
            { "HealthPublishers:Webhook:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new WebhookOptionsConfigure(configuration);
        var options = new WebhookOptions();

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
            { "HealthPublishers:Webhook:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new WebhookOptionsConfigure(configuration);
        var options = new WebhookOptions();

        // Act
        ((IConfigureOptions<WebhookOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions
        {
            Uri = new Uri("https://example.com"),
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
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions
        {
            Uri = new Uri("https://example.com"),
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
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());

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
    public async Task Validate_WhenUriNull_ReturnFailure()
    {
        // Arrange
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions { SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Uri must be set.");
        }
    }

    [Test]
    public async Task Validate_WhenUriNotAbsolute_ReturnFailure()
    {
        // Arrange
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions
        {
            Uri = new Uri("/relative", UriKind.Relative),
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Uri must be a valid absolute URI.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions { Uri = new Uri("https://example.com"), SystemIdentifier = systemIdentifier! };

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
        var configure = new WebhookOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new WebhookOptions
        {
            Uri = new Uri("https://example.com"),
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
            { "HealthPublishers:Webhook:Test:Uri", "https://example.com/webhooks/health" },
            { "HealthPublishers:Webhook:Test:SystemIdentifier", "checkout-service" },
            { "HealthPublishers:Webhook:Test:Headers:Authorization", "Bearer token" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new WebhookOptionsConfigure(configuration);
        var options = new WebhookOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.Uri).IsEqualTo(new Uri("https://example.com/webhooks/health"));
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
            _ = await Assert.That(options.Headers["Authorization"]).IsEqualTo("Bearer token");
        }
    }
}
