namespace NetEvolve.HealthPublishers.Tests.Unit.Prometheus.PushGateway;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Prometheus.PushGateway;

[TestGroup(nameof(PushGateway))]
public sealed class PrometheusPushGatewayOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions();

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
            { "HealthPublishers:Prometheus:PushGateway:Default:Job", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PrometheusPushGatewayOptionsConfigure(configuration);
        var options = new PrometheusPushGatewayOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.Job).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Prometheus:PushGateway:Default:Job", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PrometheusPushGatewayOptionsConfigure(configuration);
        var options = new PrometheusPushGatewayOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.Job).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Prometheus:PushGateway:Default:Job", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PrometheusPushGatewayOptionsConfigure(configuration);
        var options = new PrometheusPushGatewayOptions();

        // Act
        ((IConfigureOptions<PrometheusPushGatewayOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.Job).IsEqualTo("checkout-service");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            ServerUrl = new Uri("https://pushgateway.example.com"),
            Job = "checkout-service",
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
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            ServerUrl = new Uri("https://pushgateway.example.com"),
            Job = "checkout-service",
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
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());

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
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            Job = "checkout-service",
            SystemIdentifier = "checkout-service",
        };

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
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            ServerUrl = new Uri("/relative", UriKind.Relative),
            Job = "checkout-service",
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
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenJobNullOrWhiteSpace_ReturnFailure(string? job)
    {
        // Arrange
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            ServerUrl = new Uri("https://pushgateway.example.com"),
            Job = job!,
            SystemIdentifier = "checkout-service",
        };

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The Job must be set.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            ServerUrl = new Uri("https://pushgateway.example.com"),
            Job = "checkout-service",
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
        var configure = new PrometheusPushGatewayOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new PrometheusPushGatewayOptions
        {
            ServerUrl = new Uri("https://pushgateway.example.com"),
            Job = "checkout-service",
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
            { "HealthPublishers:Prometheus:PushGateway:Test:ServerUrl", "https://pushgateway.example.com" },
            { "HealthPublishers:Prometheus:PushGateway:Test:Job", "checkout-service" },
            { "HealthPublishers:Prometheus:PushGateway:Test:Instance", "checkout-service-01" },
            { "HealthPublishers:Prometheus:PushGateway:Test:SystemIdentifier", "checkout-service" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new PrometheusPushGatewayOptionsConfigure(configuration);
        var options = new PrometheusPushGatewayOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.ServerUrl).IsEqualTo(new Uri("https://pushgateway.example.com"));
            _ = await Assert.That(options.Job).IsEqualTo("checkout-service");
            _ = await Assert.That(options.Instance).IsEqualTo("checkout-service-01");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
        }
    }
}
