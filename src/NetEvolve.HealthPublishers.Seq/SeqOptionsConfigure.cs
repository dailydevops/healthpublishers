namespace NetEvolve.HealthPublishers.Seq;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class SeqOptionsConfigure : IConfigureNamedOptions<SeqOptions>, IValidateOptions<SeqOptions>
{
    private readonly IConfiguration _configuration;

    public SeqOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, SeqOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _configuration.Bind($"HealthPublishers:Seq:{name}", options);
    }

    [ExcludeFromCodeCoverage]
    public void Configure(SeqOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, SeqOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
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
