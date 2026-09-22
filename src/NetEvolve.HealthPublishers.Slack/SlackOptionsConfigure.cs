namespace NetEvolve.HealthPublishers.Slack;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class SlackOptionsConfigure : IConfigureNamedOptions<SlackOptions>, IValidateOptions<SlackOptions>
{
    private readonly IConfiguration _configuration;

    public SlackOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, SlackOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Slack:{resolvedName}", options);
    }

    public void Configure(SlackOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, SlackOptions options)
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

        return Success;
    }
}
