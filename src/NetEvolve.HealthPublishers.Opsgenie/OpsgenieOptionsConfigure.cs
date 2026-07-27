namespace NetEvolve.HealthPublishers.Opsgenie;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class OpsgenieOptionsConfigure
    : IConfigureNamedOptions<OpsgenieOptions>,
        IValidateOptions<OpsgenieOptions>
{
    private readonly IConfiguration _configuration;

    public OpsgenieOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, OpsgenieOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Opsgenie:{resolvedName}", options);
    }

    public void Configure(OpsgenieOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, OpsgenieOptions options)
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

        if (options.ApiUrl is not null && !options.ApiUrl.IsAbsoluteUri)
        {
            return Fail("The ApiUrl must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return Fail("The ApiKey must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
