namespace NetEvolve.HealthPublishers.MicrosoftTeams;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class MicrosoftTeamsOptionsConfigure
    : IConfigureNamedOptions<MicrosoftTeamsOptions>,
        IValidateOptions<MicrosoftTeamsOptions>
{
    private readonly IConfiguration _configuration;

    public MicrosoftTeamsOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, MicrosoftTeamsOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:MicrosoftTeams:{resolvedName}", options);
    }

    public void Configure(MicrosoftTeamsOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, MicrosoftTeamsOptions options)
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

        if (options.WebhookUrl is null)
        {
            return Fail("The WebhookUrl must be set.");
        }

        if (!options.WebhookUrl.IsAbsoluteUri)
        {
            return Fail("The WebhookUrl must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        if (options.RecoveryConfirmationDelay < TimeSpan.FromMinutes(5))
        {
            return Fail("The RecoveryConfirmationDelay must be at least 5 minutes.");
        }

        return Success;
    }
}
