namespace NetEvolve.HealthPublishers.Seq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class SeqOptionsConfigure : IConfigureNamedOptions<SeqOptions>, IValidateOptions<SeqOptions>
{
    private readonly IConfiguration _configuration;

    public SeqOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, SeqOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Seq:{resolvedName}", options);
    }

    public void Configure(SeqOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, SeqOptions options)
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

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        return Success;
    }
}
