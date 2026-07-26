namespace NetEvolve.HealthPublishers.OpenTelemetry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class OpenTelemetryOptionsConfigure
    : IConfigureNamedOptions<OpenTelemetryOptions>,
        IValidateOptions<OpenTelemetryOptions>
{
    private readonly IConfiguration _configuration;

    public OpenTelemetryOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, OpenTelemetryOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:OpenTelemetry:{resolvedName}", options);
    }

    public void Configure(OpenTelemetryOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, OpenTelemetryOptions options)
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
