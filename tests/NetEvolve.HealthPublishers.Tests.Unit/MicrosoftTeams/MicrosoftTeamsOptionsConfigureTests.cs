namespace NetEvolve.HealthPublishers.Tests.Unit.MicrosoftTeams;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.MicrosoftTeams;

[TestGroup(nameof(MicrosoftTeams))]
public sealed class MicrosoftTeamsOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions();

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
            { "HealthPublishers:MicrosoftTeams:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new MicrosoftTeamsOptionsConfigure(configuration);
        var options = new MicrosoftTeamsOptions();

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
            { "HealthPublishers:MicrosoftTeams:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new MicrosoftTeamsOptionsConfigure(configuration);
        var options = new MicrosoftTeamsOptions();

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
            { "HealthPublishers:MicrosoftTeams:Default:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new MicrosoftTeamsOptionsConfigure(configuration);
        var options = new MicrosoftTeamsOptions();

        // Act
        ((IConfigureOptions<MicrosoftTeamsOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x"),
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
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x"),
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
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());

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
    public async Task Validate_WhenWebhookUrlNull_ReturnFailure()
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions { SystemIdentifier = "checkout-service" };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The WebhookUrl must be set.");
        }
    }

    [Test]
    public async Task Validate_WhenWebhookUrlNotAbsolute_ReturnFailure()
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("/relative", UriKind.Relative),
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The WebhookUrl must be a valid absolute URI.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x"),
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
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(4)]
    public async Task Validate_WhenRecoveryConfirmationDelayBelowMinimum_ReturnFailure(int minutes)
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x"),
            SystemIdentifier = "checkout-service",
            RecoveryConfirmationDelay = TimeSpan.FromMinutes(minutes),
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The RecoveryConfirmationDelay must be at least 5 minutes.");
        }
    }

    [Test]
    public async Task Validate_WhenRecoveryConfirmationDelayAtMinimum_ReturnSuccess()
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x"),
            SystemIdentifier = "checkout-service",
            RecoveryConfirmationDelay = TimeSpan.FromMinutes(5L),
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsValid_ReturnSuccess()
    {
        // Arrange
        var configure = new MicrosoftTeamsOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new MicrosoftTeamsOptions
        {
            WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/x"),
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
            { "HealthPublishers:MicrosoftTeams:Test:WebhookUrl", "https://example.webhook.office.com/webhookb2/x" },
            { "HealthPublishers:MicrosoftTeams:Test:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new MicrosoftTeamsOptionsConfigure(configuration);
        var options = new MicrosoftTeamsOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert
                .That(options.WebhookUrl)
                .IsEqualTo(new Uri("https://example.webhook.office.com/webhookb2/x"));
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
        }
    }
}
