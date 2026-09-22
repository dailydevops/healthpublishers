namespace NetEvolve.HealthPublishers.Webhook;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class WebhookOptionsConfigure : IConfigureNamedOptions<WebhookOptions>, IValidateOptions<WebhookOptions>
{
    private readonly IConfiguration _configuration;

    public WebhookOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, WebhookOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Webhook:{resolvedName}", options);
    }

    public void Configure(WebhookOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, WebhookOptions options)
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

        if (options.Uri is null)
        {
            return Fail("The Uri must be set.");
        }

        if (!options.Uri.IsAbsoluteUri)
        {
            return Fail("The Uri must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
