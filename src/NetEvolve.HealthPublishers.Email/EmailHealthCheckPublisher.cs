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

#if NET9_0_OR_GREATER
    private readonly Lock _notificationLock = new();
#else
    private readonly object _notificationLock = new();
#endif

    private HealthStatus _lastNotifiedStatus = HealthStatus.Healthy;
    private DateTimeOffset? _pendingSince;

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
        cancellationToken.ThrowIfCancellationRequested();

        var options = _options.Get(_name);

        if (!ShouldNotify(report.Status, options.RecoveryConfirmationDelay))
        {
            return;
        }

#pragma warning disable CA2000 // The message only wraps a plain-text body (no streams/attachments), so there is
        // nothing unmanaged to release; the ISmtpSender implementation owns sending it and disposing it early would
        // break senders that need to (re-)read the message after it was handed off.
        var message = BuildMessage(report, options);
#pragma warning restore CA2000

        await _sender.SendAsync(options, message, cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldNotify(HealthStatus newStatus, TimeSpan recoveryConfirmationDelay)
    {
        lock (_notificationLock)
        {
            var newSeverity = Severity(newStatus);
            var lastSeverity = Severity(_lastNotifiedStatus);

            if (newSeverity == lastSeverity)
            {
                // Status matches the last-notified status: cancel any pending recovery confirmation.
                _pendingSince = null;
                return false;
            }

            if (newSeverity > lastSeverity)
            {
                // Worsening: notify immediately and clear any pending recovery confirmation.
                _lastNotifiedStatus = newStatus;
                _pendingSince = null;
                return true;
            }

            // Improvement: only notify once sustained for at least the configured delay.
            var now = _timeProvider.GetUtcNow();

            _pendingSince ??= now;

            if (now - _pendingSince.Value < recoveryConfirmationDelay)
            {
                return false;
            }

            _lastNotifiedStatus = newStatus;
            _pendingSince = null;
            return true;
        }
    }

    private static int Severity(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => 0,
            HealthStatus.Degraded => 1,
            _ => 2,
        };

    private MimeMessage BuildMessage(HealthReport report, EmailOptions options)
    {
        var now = _timeProvider.GetUtcNow();
        var message = new MimeMessage { Date = now };

        message.From.Add(MailboxAddress.Parse(options.From));

        foreach (var to in options.To)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }

        message.Subject = string.Create(
            CultureInfo.InvariantCulture,
            $"[{report.Status}] Health check report - {options.SystemIdentifier}"
        );

        message.Body = new TextPart("plain") { Text = BuildBody(report, options, now) };

        return message;
    }

    private static string BuildBody(HealthReport report, EmailOptions options, DateTimeOffset now)
    {
        var builder = new StringBuilder();

        _ = builder.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Health check report {report.Status} in {report.TotalDuration.TotalMilliseconds:0.##}ms"
            )
        );
        _ = builder.AppendLine(
            string.Create(CultureInfo.InvariantCulture, $"Timestamp: {FormatTimestamp(now, options.TimeZoneId)}")
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

    private static string FormatTimestamp(DateTimeOffset now, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var converted = TimeZoneInfo.ConvertTime(now, timeZone);

        return string.Create(CultureInfo.InvariantCulture, $"{converted:yyyy-MM-dd HH:mm:ss zzz} ({timeZoneId})");
    }

    private static string FormatDescription(HealthReportEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Description)
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" - {entry.Description}");
}
