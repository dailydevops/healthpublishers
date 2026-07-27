namespace NetEvolve.HealthPublishers.Email;

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MimeKit;

internal sealed class EmailHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly ISmtpSender _sender;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly TimeProvider _timeProvider;

    public EmailHealthCheckPublisher(
        string name,
        ISmtpSender sender,
        IOptionsMonitor<EmailOptions> options,
        TimeProvider timeProvider
    )
    {
        _name = name;
        _sender = sender;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var options = _options.Get(_name);

#pragma warning disable CA2000 // The message only wraps a plain-text body (no streams/attachments), so there is
        // nothing unmanaged to release; the ISmtpSender implementation owns sending it and disposing it early would
        // break senders that need to (re-)read the message after it was handed off.
        var message = BuildMessage(report, options);
#pragma warning restore CA2000

        await _sender.SendAsync(options, message, cancellationToken).ConfigureAwait(false);
    }

    private MimeMessage BuildMessage(HealthReport report, EmailOptions options)
    {
        var message = new MimeMessage { Date = _timeProvider.GetUtcNow() };

        message.From.Add(MailboxAddress.Parse(options.From));

        foreach (var to in options.To)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }

        message.Subject = string.Create(
            CultureInfo.InvariantCulture,
            $"[{report.Status}] Health check report - {options.SystemIdentifier}"
        );

        message.Body = new TextPart("plain") { Text = BuildBody(report, options) };

        return message;
    }

    private static string BuildBody(HealthReport report, EmailOptions options)
    {
        var builder = new StringBuilder();

        _ = builder.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Health check report {report.Status} in {report.TotalDuration.TotalMilliseconds:0.##}ms"
            )
        );
        _ = builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"System: {options.SystemIdentifier}"));
        _ = builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Machine: {Environment.MachineName}"));

        if (report.Entries.Count > 0)
        {
            _ = builder.AppendLine().AppendLine("Entries:");

            foreach (var (key, entry) in report.Entries)
            {
                _ = builder.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"- {key}: {entry.Status} ({entry.Duration.TotalMilliseconds:0.##}ms){FormatDescription(entry)}"
                    )
                );
            }
        }

        return builder.ToString();
    }

    private static string FormatDescription(HealthReportEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Description)
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" - {entry.Description}");
}
