namespace NetEvolve.HealthPublishers.Tests.Unit.AWS.CloudWatch;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.AWS.CloudWatch;

[TestGroup(nameof(CloudWatch))]
public sealed class CloudWatchOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new CloudWatchOptions();

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
            { "HealthPublishers:AWS:CloudWatch:Default:Namespace", "HealthChecks" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new CloudWatchOptionsConfigure(configuration);
        var options = new CloudWatchOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.Namespace).IsEqualTo("HealthChecks");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:AWS:CloudWatch:Default:Namespace", "HealthChecks" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new CloudWatchOptionsConfigure(configuration);
        var options = new CloudWatchOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.Namespace).IsEqualTo("HealthChecks");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:AWS:CloudWatch:Default:Namespace", "HealthChecks" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new CloudWatchOptionsConfigure(configuration);
        var options = new CloudWatchOptions();

        // Act
        ((IConfigureOptions<CloudWatchOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.Namespace).IsEqualTo("HealthChecks");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();

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
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();

        // Act
        var result = configure.Validate(name, options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenOptionsNull_ReturnFailure()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());

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
    public async Task Validate_WhenRegionNullOrWhiteSpace_ReturnFailure(string? region)
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Region = region;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Region must be set.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenNamespaceNullOrWhiteSpace_ReturnFailure(string? @namespace)
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Namespace = @namespace!;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Namespace must be set.");
        }
    }

    [Test]
    [Arguments("AWS/EC2")]
    [Arguments("aws/ec2")]
    [Arguments("has space")]
    [Arguments("has$symbol")]
    public async Task Validate_WhenNamespaceInvalid_ReturnFailure(string @namespace)
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Namespace = @namespace;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo(
                    "The Namespace must be 1-255 characters long, contain only ASCII alphanumerics and the characters `. - _ / # :`, and must not start with the reserved `AWS/` prefix."
                );
        }
    }

    [Test]
    public async Task Validate_WhenNamespaceTooLong_ReturnFailure()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Namespace = new string('a', 256);

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result.Failed).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
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
    public async Task Validate_WhenAccessKeyIdSetWithoutSecretAccessKey_ReturnFailure()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.AccessKeyId = "access-key";
        options.SecretAccessKey = null;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The AccessKeyId and SecretAccessKey must both be set when using explicit credentials.");
        }
    }

    [Test]
    public async Task Validate_WhenSecretAccessKeySetWithoutAccessKeyId_ReturnFailure()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.AccessKeyId = null;
        options.SecretAccessKey = "secret-access-key";

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The AccessKeyId and SecretAccessKey must both be set when using explicit credentials.");
        }
    }

    [Test]
    public async Task Validate_WhenOptionsValid_ReturnSuccess()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenAccessKeyIdAndSecretAccessKeyBothSet_ReturnSuccess()
    {
        // Arrange
        var configure = new CloudWatchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.AccessKeyId = "access-key";
        options.SecretAccessKey = "secret-access-key";

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
            { "HealthPublishers:AWS:CloudWatch:Test:Region", "eu-central-1" },
            { "HealthPublishers:AWS:CloudWatch:Test:Namespace", "HealthChecks" },
            { "HealthPublishers:AWS:CloudWatch:Test:SystemIdentifier", "checkout-service" },
            { "HealthPublishers:AWS:CloudWatch:Test:ServiceUrl", "https://localhost:4566" },
            { "HealthPublishers:AWS:CloudWatch:Test:AccessKeyId", "test-access-key" },
            { "HealthPublishers:AWS:CloudWatch:Test:SecretAccessKey", "test-secret-key" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new CloudWatchOptionsConfigure(configuration);
        var options = new CloudWatchOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.Region).IsEqualTo("eu-central-1");
            _ = await Assert.That(options.Namespace).IsEqualTo("HealthChecks");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
            _ = await Assert.That(options.ServiceUrl).IsEqualTo(new Uri("https://localhost:4566"));
            _ = await Assert.That(options.AccessKeyId).IsEqualTo("test-access-key");
            _ = await Assert.That(options.SecretAccessKey).IsEqualTo("test-secret-key");
        }
    }

    private static CloudWatchOptions ValidOptions() =>
        new()
        {
            Region = "eu-central-1",
            Namespace = "HealthChecks",
            SystemIdentifier = "checkout-service",
        };
}
