namespace NetEvolve.HealthPublishers.Tests.Unit.Elasticsearch;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Elasticsearch;

[TestGroup(nameof(Elasticsearch))]
public sealed class ElasticsearchOptionsConfigureTests
{
    [Test]
    public void Configure_WhenArgumentNameWhitespace_ThrowArgumentException()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = new ElasticsearchOptions();

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
            { "HealthPublishers:Elasticsearch:Default:IndexName", "health-checks" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new ElasticsearchOptionsConfigure(configuration);
        var options = new ElasticsearchOptions();

        // Act
        configure.Configure(null, options);

        // Assert
        _ = await Assert.That(options.IndexName).IsEqualTo("health-checks");
    }

    [Test]
    public async Task Configure_WhenArgumentNameEmpty_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Elasticsearch:Default:IndexName", "health-checks" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new ElasticsearchOptionsConfigure(configuration);
        var options = new ElasticsearchOptions();

        // Act
        configure.Configure(string.Empty, options);

        // Assert
        _ = await Assert.That(options.IndexName).IsEqualTo("health-checks");
    }

    [Test]
    public async Task Configure_WhenCalledWithoutName_UsesDefaultNameSection()
    {
        // Arrange
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "HealthPublishers:Elasticsearch:Default:IndexName", "health-checks" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new ElasticsearchOptionsConfigure(configuration);
        var options = new ElasticsearchOptions();

        // Act
        ((IConfigureOptions<ElasticsearchOptions>)configure).Configure(options);

        // Assert
        _ = await Assert.That(options.IndexName).IsEqualTo("health-checks");
    }

    [Test]
    public async Task Validate_WhenNameWhitespace_ReturnFailure()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
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
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
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
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());

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
    public async Task Validate_WhenServerUriNull_ReturnFailure()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.ServerUri = null;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ServerUri must be set.");
        }
    }

    [Test]
    public async Task Validate_WhenServerUriNotAbsolute_ReturnFailure()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.ServerUri = new Uri("/relative", UriKind.Relative);

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ServerUri must be a valid absolute URI.");
        }
    }

    [Test]
    [Arguments("ftp://elasticsearch.example.com:9200")]
    [Arguments("ws://elasticsearch.example.com:9200")]
    public async Task Validate_WhenServerUriSchemeNotHttpOrHttps_ReturnFailure(string endpoint)
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.ServerUri = new Uri(endpoint, UriKind.Absolute);

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The ServerUri must use the http or https scheme.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenIndexNameNullOrWhiteSpace_ReturnFailure(string? indexName)
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.IndexName = indexName!;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert.That(result.FailureMessage).IsEqualTo("The IndexName must be set.");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Validate_WhenSystemIdentifierNullOrWhiteSpace_ReturnFailure(string? systemIdentifier)
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
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
    public async Task Validate_WhenUsernameSetWithoutPassword_ReturnFailure()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Username = "elastic";
        options.Password = null;

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The Username and Password must both be set when using basic authentication.");
        }
    }

    [Test]
    public async Task Validate_WhenPasswordSetWithoutUsername_ReturnFailure()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Username = null;
        options.Password = "secret";

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(result.Failed).IsTrue();
            _ = await Assert
                .That(result.FailureMessage)
                .IsEqualTo("The Username and Password must both be set when using basic authentication.");
        }
    }

    [Test]
    public async Task Validate_WhenOptionsValid_ReturnSuccess()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();

        // Act
        var result = configure.Validate("Test", options);

        // Assert
        _ = await Assert.That(result).IsEqualTo(ValidateOptionsResult.Success);
    }

    [Test]
    public async Task Validate_WhenUsernameAndPasswordBothSet_ReturnSuccess()
    {
        // Arrange
        var configure = new ElasticsearchOptionsConfigure(new ConfigurationBuilder().Build());
        var options = ValidOptions();
        options.Username = "elastic";
        options.Password = "secret";

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
            { "HealthPublishers:Elasticsearch:Test:ServerUri", "https://elasticsearch.example.com:9200" },
            { "HealthPublishers:Elasticsearch:Test:IndexName", "health-checks" },
            { "HealthPublishers:Elasticsearch:Test:SystemIdentifier", "checkout-service" },
            { "HealthPublishers:Elasticsearch:Test:ApiKey", "test-api-key" },
            { "HealthPublishers:Elasticsearch:Test:Username", "elastic" },
            { "HealthPublishers:Elasticsearch:Test:Password", "secret" },
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var configure = new ElasticsearchOptionsConfigure(configuration);
        var options = new ElasticsearchOptions();

        // Act
        configure.Configure("Test", options);

        // Assert
        using (Assert.Multiple())
        {
            _ = await Assert.That(options.ServerUri).IsEqualTo(new Uri("https://elasticsearch.example.com:9200"));
            _ = await Assert.That(options.IndexName).IsEqualTo("health-checks");
            _ = await Assert.That(options.SystemIdentifier).IsEqualTo("checkout-service");
            _ = await Assert.That(options.ApiKey).IsEqualTo("test-api-key");
            _ = await Assert.That(options.Username).IsEqualTo("elastic");
            _ = await Assert.That(options.Password).IsEqualTo("secret");
        }
    }

    private static ElasticsearchOptions ValidOptions() =>
        new()
        {
            ServerUri = new Uri("https://elasticsearch.example.com:9200"),
            IndexName = "health-checks",
            SystemIdentifier = "checkout-service",
        };
}
