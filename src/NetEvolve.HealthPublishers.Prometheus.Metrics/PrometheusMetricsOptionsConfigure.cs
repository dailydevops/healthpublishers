namespace NetEvolve.HealthPublishers.Prometheus.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class PrometheusMetricsOptionsConfigure
    : IConfigureNamedOptions<PrometheusMetricsOptions>,
        IValidateOptions<PrometheusMetricsOptions>
{
    private readonly IConfiguration _configuration;

    public PrometheusMetricsOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, PrometheusMetricsOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Prometheus:Metrics:{resolvedName}", options);
    }

    public void Configure(PrometheusMetricsOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, PrometheusMetricsOptions options)
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

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
