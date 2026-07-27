namespace NetEvolve.HealthPublishers.Elasticsearch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class ElasticsearchOptionsConfigure
    : IConfigureNamedOptions<ElasticsearchOptions>,
        IValidateOptions<ElasticsearchOptions>
{
    private readonly IConfiguration _configuration;

    public ElasticsearchOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, ElasticsearchOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Elasticsearch:{resolvedName}", options);
    }

    public void Configure(ElasticsearchOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, ElasticsearchOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return Fail("The name cannot be null or whitespace.");
        }

        if (options is null)
        {
            return Fail("The option cannot be null.");
        }

        if (options.ServerUri is null)
        {
            return Fail("The ServerUri must be set.");
        }

        if (!options.ServerUri.IsAbsoluteUri)
        {
            return Fail("The ServerUri must be a valid absolute URI.");
        }

        if (options.ServerUri.Scheme is not ("http" or "https"))
        {
            return Fail("The ServerUri must use the http or https scheme.");
        }

        if (string.IsNullOrWhiteSpace(options.IndexName))
        {
            return Fail("The IndexName must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);

        if (hasUsername != hasPassword)
        {
            return Fail("The Username and Password must both be set when using basic authentication.");
        }

        return Success;
    }
}
