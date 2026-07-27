namespace NetEvolve.HealthPublishers.Splunk;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class SplunkOptionsConfigure : IConfigureNamedOptions<SplunkOptions>, IValidateOptions<SplunkOptions>
{
    private readonly IConfiguration _configuration;

    public SplunkOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, SplunkOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Splunk:{resolvedName}", options);
    }

    public void Configure(SplunkOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, SplunkOptions options)
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

        if (options.ServerUrl is null)
        {
            return Fail("The ServerUrl must be set.");
        }

        if (!options.ServerUrl.IsAbsoluteUri)
        {
            return Fail("The ServerUrl must be a valid absolute URI.");
        }

        if (options.ServerUrl.Scheme is not ("http" or "https"))
        {
            return Fail("The ServerUrl must use the http or https scheme.");
        }

        if (string.IsNullOrWhiteSpace(options.HecToken))
        {
            return Fail("The HecToken must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
