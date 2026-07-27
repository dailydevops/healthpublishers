namespace NetEvolve.HealthPublishers.ApplicationInsights;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class ApplicationInsightsOptionsConfigure
    : IConfigureNamedOptions<ApplicationInsightsOptions>,
        IValidateOptions<ApplicationInsightsOptions>
{
    private readonly IConfiguration _configuration;

    public ApplicationInsightsOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, ApplicationInsightsOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:ApplicationInsights:{resolvedName}", options);
    }

    public void Configure(ApplicationInsightsOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, ApplicationInsightsOptions options)
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

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return Fail("The ConnectionString must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
