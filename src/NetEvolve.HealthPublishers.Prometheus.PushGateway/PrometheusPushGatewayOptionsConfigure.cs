namespace NetEvolve.HealthPublishers.Prometheus.PushGateway;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class PrometheusPushGatewayOptionsConfigure
    : IConfigureNamedOptions<PrometheusPushGatewayOptions>,
        IValidateOptions<PrometheusPushGatewayOptions>
{
    private readonly IConfiguration _configuration;

    public PrometheusPushGatewayOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, PrometheusPushGatewayOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Prometheus:PushGateway:{resolvedName}", options);
    }

    public void Configure(PrometheusPushGatewayOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, PrometheusPushGatewayOptions options)
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

        if (string.IsNullOrWhiteSpace(options.Job))
        {
            return Fail("The Job must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
