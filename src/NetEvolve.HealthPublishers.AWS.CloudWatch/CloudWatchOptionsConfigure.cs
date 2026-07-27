namespace NetEvolve.HealthPublishers.AWS.CloudWatch;

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class CloudWatchOptionsConfigure
    : IConfigureNamedOptions<CloudWatchOptions>,
        IValidateOptions<CloudWatchOptions>
{
    private const int MaxNamespaceLength = 255;
    private const string AllowedSymbols = "._/#:-";

    private readonly IConfiguration _configuration;

    public CloudWatchOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, CloudWatchOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:AWS:CloudWatch:{resolvedName}", options);
    }

    public void Configure(CloudWatchOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, CloudWatchOptions options)
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

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            return Fail("The Region must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Namespace))
        {
            return Fail("The Namespace must be set.");
        }

        if (!IsValidNamespace(options.Namespace))
        {
            return Fail(
                "The Namespace must be 1-255 characters long, contain only ASCII alphanumerics and the characters `. - _ / # :`, and must not start with the reserved `AWS/` prefix."
            );
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        var hasAccessKeyId = !string.IsNullOrWhiteSpace(options.AccessKeyId);
        var hasSecretAccessKey = !string.IsNullOrWhiteSpace(options.SecretAccessKey);

        if (hasAccessKeyId != hasSecretAccessKey)
        {
            return Fail("The AccessKeyId and SecretAccessKey must both be set when using explicit credentials.");
        }

        return Success;
    }

    private static bool IsValidNamespace(string value) =>
        value.Length is > 0 and <= MaxNamespaceLength
        && !value.StartsWith("AWS/", StringComparison.OrdinalIgnoreCase)
        && AllValidCharacters(value);

    private static bool AllValidCharacters(string value) =>
        value.All(character => char.IsAsciiLetterOrDigit(character) || AllowedSymbols.AsSpan().Contains(character));
}
