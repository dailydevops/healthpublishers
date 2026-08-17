namespace NetEvolve.HealthPublishers.Tests.Unit.Email;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Email;

[TestGroup(nameof(Email))]
public sealed class EmailOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new EmailOptions();

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
            { "HealthPublishers:Email:Default:Host", "smtp.example.com" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new EmailOptionsConfigure(configuration);
        var options = new EmailOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.Host).IsEqualTo("smtp.example.com");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Email:Default:Host", "smtp.example.com" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new EmailOptionsConfigure(configuration);
        var options = new EmailOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.Host).IsEqualTo("smtp.example.com");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Email:Default:Host", "smtp.example.com" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new EmailOptionsConfigure(configuration);
        var options = new EmailOptions();

        // Act
        ((IConfigureOptions<EmailOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.Host).IsEqualTo("smtp.example.com");
    }

    [Test]
    public async Task Configure_WhenConfigurationAvailable_ExpectedValues()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Email:Test:Host", "smtp.example.com" },
            { "HealthPublishers:Email:Test:Port", "587" },
            { "HealthPublishers:Email:Test:From", "health-checks@example.com" },
            { "HealthPublishers:Email:Test:To:0", "ops-team@example.com" },
            { "HealthPublishers:Email:Test:To:1", "second-team@example.com" },
            { "HealthPublishers:Email:Test:SystemIdentifier", "checkout-service" },
            { "HealthPublishers:Email:Test:Username", "smtp-user" },
            { "HealthPublishers:Email:Test:Password", "smtp-password" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new EmailOptionsConfigure(configuration);
        var options = new EmailOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.Host).IsEqualTo("smtp.example.com");
            _ = await Assert.That(options.Port).IsEqualTo(587);
            _ = await Assert.That(options.From).IsEqualTo("health-checks@example.com");
            _ = await Assert.That(options.To).IsEquivalentTo(["ops-team@example.com", "second-team@example.com"]);
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
            _ = await Assert.That(options.Username).IsEqualTo("smtp-user");
            _ = await Assert.That(options.Password).IsEqualTo("smtp-password");
        }
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();

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
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();

        // Act
        var result = configure.Validate(name, options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsNull_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());

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
    public async Task Validate_WhenHostNullOrWhiteSpace_ReturnFailure(string? host)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.Host = host!;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Host must be set.");
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(65536)]
    public async Task Validate_WhenPortOutOfRange_ReturnFailure(int port)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.Port = port;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Port must be between 1 and 65535.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenFromNullOrWhiteSpace_ReturnFailure(string? from)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.From = from!;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The From must be set.");
        }
    }

    [Test]
    public async Task Validate_WhenFromInvalid_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.From = "not-an-email";

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The From must be a valid email address.");
        }
    }

    [Test]
    public async Task Validate_WhenToEmpty_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.To = [];

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The To must contain at least one email address.");
        }
    }

    [Test]
    public async Task Validate_WhenToContainsInvalidAddress_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.To = ["ops-team@example.com", "not-an-email"];

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The To must contain only valid email addresses.");
        }
    }

    [Test]
    public async Task Validate_WhenToContainsUnparsableAddress_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.To = ["ops-team@example.com", string.Empty];

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The To must contain only valid email addresses.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.SystemIdentifier = systemIdentifier!;

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
    public async Task Validate_WhenUsernameSetWithoutPassword_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.Username = "smtp-user";
        options.Password = null;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The Username and Password must both be set or both be unset.");
        }
    }

    [Test]
    public async Task Validate_WhenPasswordSetWithoutUsername_ReturnFailure()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.Username = null;
        options.Password = "smtp-password";

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The Username and Password must both be set or both be unset.");
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(4)]
    public async Task Validate_WhenRecoveryConfirmationDelayBelowMinimum_ReturnFailure(int minutes)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(minutes);

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
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5L);

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("Not/AZone")]
    public async Task Validate_WhenTimeZoneIdInvalid_ReturnFailure(string? timeZoneId)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.TimeZoneId = timeZoneId!;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The TimeZoneId must be a valid time zone identifier.");
        }
    }

    [Test]
    [Arguments("Europe/Berlin")]
    [Arguments("UTC")]
    [Arguments("America/New_York")]
    public async Task Validate_WhenTimeZoneIdValid_ReturnSuccess(string timeZoneId)
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.TimeZoneId = timeZoneId;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsValid_ReturnSuccess()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenCredentialsBothSet_ReturnSuccess()
    {
        // Arrange
        var configure = new EmailOptionsConfigure(new ConfigurationBuilder().Build());
        var options = CreateValidOptions();
        options.Username = "smtp-user";
        options.Password = "smtp-password";

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    private static EmailOptions CreateValidOptions() =>
        new()
        {
            Host = "smtp.example.com",
            Port = 587,
            From = "health-checks@example.com",
            To = ["ops-team@example.com"],
            SystemIdentifier = "checkout-service",
        };
}
