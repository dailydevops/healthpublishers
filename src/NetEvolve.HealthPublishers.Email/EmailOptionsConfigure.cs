namespace NetEvolve.HealthPublishers.Email;

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;
using static Microsoft.Extensions.Options.ValidateOptionsResult;

internal sealed class EmailOptionsConfigure : IConfigureNamedOptions<EmailOptions>, IValidateOptions<EmailOptions>
{
    private readonly IConfiguration _configuration;

    public EmailOptionsConfigure(IConfiguration configuration) => _configuration = configuration;

    public void Configure(string? name, EmailOptions options)
    {
        var resolvedName = string.IsNullOrEmpty(name) ? DependencyInjectionExtensions.DefaultName : name;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedName);
        _configuration.Bind($"HealthPublishers:Email:{resolvedName}", options);
    }

    public void Configure(EmailOptions options) => Configure(Options.DefaultName, options);

    public ValidateOptionsResult Validate(string? name, EmailOptions options)
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

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            return Fail("The Host must be set.");
        }

        if (options.Port is <= 0 or > 65535)
        {
            return Fail("The Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.From))
        {
            return Fail("The From must be set.");
        }

        if (!IsValidEmailAddress(options.From))
        {
            return Fail("The From must be a valid email address.");
        }

        if (options.To.Count == 0)
        {
            return Fail("The To must contain at least one email address.");
        }

        if (options.To.Any(to => !IsValidEmailAddress(to)))
        {
            return Fail("The To must contain only valid email addresses.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemIdentifier))
        {
            return Fail("The SystemIdentifier must be set.");
        }

        if (string.IsNullOrEmpty(options.Username) != string.IsNullOrEmpty(options.Password))
        {
            return Fail("The Username and Password must both be set or both be unset.");
        }

        if (options.RecoveryConfirmationDelay < TimeSpan.FromMinutes(5))
        {
            return Fail("The RecoveryConfirmationDelay must be at least 5 minutes.");
        }

        if (!IsValidTimeZoneId(options.TimeZoneId))
        {
            return Fail("The TimeZoneId must be a valid time zone identifier.");
        }

        return Success;
    }

    private static bool IsValidEmailAddress(string address) =>
        MailboxAddress.TryParse(address, out var mailbox) && mailbox.Address.Contains('@', StringComparison.Ordinal);

    private static bool IsValidTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
