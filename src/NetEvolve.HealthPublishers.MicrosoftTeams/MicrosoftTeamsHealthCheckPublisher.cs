namespace NetEvolve.HealthPublishers.MicrosoftTeams;

using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class MicrosoftTeamsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<MicrosoftTeamsOptions> _options;
    private readonly TimeProvider _timeProvider;

#if NET9_0_OR_GREATER
    private readonly Lock _notificationLock = new();
#else
    private readonly object _notificationLock = new();
#endif

    private HealthStatus _lastNotifiedStatus = HealthStatus.Healthy;
    private DateTimeOffset? _pendingSince;
    private HealthStatus? _currentStatus;
    private DateTimeOffset _currentStatusSince;

    public MicrosoftTeamsHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<MicrosoftTeamsOptions> options,
        TimeProvider timeProvider
    )
    {
        _name = name;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = _options.Get(_name);
        var now = _timeProvider.GetUtcNow();

        var (notify, statusSince) = Evaluate(report.Status, options.RecoveryConfirmationDelay, now);

        if (!notify)
        {
            return;
        }

        var card = BuildMessage(report, options, now, statusSince);

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        using var content = JsonContent.Create(card);

        using var response = await client
            .PostAsync(options.WebhookUrl, content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private (bool Notify, DateTimeOffset StatusSince) Evaluate(
        HealthStatus newStatus,
        TimeSpan recoveryConfirmationDelay,
        DateTimeOffset now
    )
    {
        lock (_notificationLock)
        {
            if (_currentStatus != newStatus)
            {
                // Track how long the raw (pre-notification) status has been in effect,
                // independent of whether a notification for it was actually sent.
                _currentStatus = newStatus;
                _currentStatusSince = now;
            }

            var statusSince = _currentStatusSince;

            var newSeverity = Severity(newStatus);
            var lastSeverity = Severity(_lastNotifiedStatus);

            if (newSeverity == lastSeverity)
            {
                // Status matches the last-notified status: cancel any pending recovery confirmation.
                _pendingSince = null;
                return (false, statusSince);
            }

            if (newSeverity > lastSeverity)
            {
                // Worsening: notify immediately and clear any pending recovery confirmation.
                _lastNotifiedStatus = newStatus;
                _pendingSince = null;
                return (true, statusSince);
            }

            // Improvement: only notify once sustained for at least the configured delay.
            _pendingSince ??= now;

            if (now - _pendingSince.Value < recoveryConfirmationDelay)
            {
                return (false, statusSince);
            }

            // Report since the improvement was first observed, not since the raw status
            // last changed - that's what "sustained for the delay" actually means here.
            var confirmedSince = _pendingSince.Value;
            _lastNotifiedStatus = newStatus;
            _pendingSince = null;
            return (true, confirmedSince);
        }
    }

    private static int Severity(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => 0,
            HealthStatus.Degraded => 1,
            _ => 2,
        };

    // A conservative cap for the details text block, well within the ~28 KB Adaptive Card payload
    // recommendation, to avoid excessively large webhook requests when a report has many entries.
    private const int MaxDetailsLength = 4000;

    private static object BuildMessage(
        HealthReport report,
        MicrosoftTeamsOptions options,
        DateTimeOffset now,
        DateTimeOffset statusSince
    )
    {
        var body = new List<object>
        {
            new
            {
                type = "ColumnSet",
                columns = new object[]
                {
                    new
                    {
                        type = "Column",
                        width = "auto",
                        verticalContentAlignment = "Center",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = MapIcon(report.Status),
                                size = "ExtraLarge",
                                wrap = true,
                            },
                        },
                    },
                    new
                    {
                        type = "Column",
                        width = "stretch",
                        verticalContentAlignment = "Center",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = $"Health check report: {report.Status}",
                                weight = "Bolder",
                                size = "Large",
                                color = MapColor(report.Status),
                                wrap = true,
                            },
                        },
                    },
                },
            },
            new
            {
                type = "FactSet",
                facts = new object[]
                {
                    new { title = "System", value = options.SystemIdentifier },
                    new { title = "Machine", value = Environment.MachineName },
                    new { title = "Checked at", value = now.ToString("O") },
                    new { title = "Since", value = statusSince.ToString("O") },
                },
            },
        };

        var details = BuildDetails(report);
        if (!string.IsNullOrEmpty(details))
        {
            body.Add(
                new
                {
                    type = "TextBlock",
                    text = details,
                    wrap = true,
                }
            );
        }

        return new
        {
            type = "message",
            attachments = new object[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    // "$schema" isn't a valid anonymous-type member name, so this one level needs a dictionary.
                    content = new Dictionary<string, object?>
                    {
                        ["$schema"] = "https://adaptivecards.io/schemas/adaptive-card.json",
                        ["type"] = "AdaptiveCard",
                        ["version"] = "1.4",
                        ["body"] = body,
                    },
                },
            },
        };
    }

    private static string BuildDetails(HealthReport report)
    {
        if (report.Entries.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(capacity: 256);

        foreach (var entry in report.Entries)
        {
            var description = string.IsNullOrWhiteSpace(entry.Value.Description)
                ? string.Empty
                : $" - {entry.Value.Description}";
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"- **{entry.Key}**: {entry.Value.Status} ({entry.Value.Duration.TotalMilliseconds:0.##}ms){description}\n\n"
            );

            // Drop whole entries that would overflow the limit, rather than cutting one in half.
            if (builder.Length + line.Length > MaxDetailsLength)
            {
                break;
            }

            _ = builder.Append(line);
        }

        return builder.ToString().TrimEnd();
    }

    private static string MapColor(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => "good",
            HealthStatus.Degraded => "warning",
            _ => "attention",
        };

    private static string MapIcon(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => "✅",
            HealthStatus.Degraded => "⚠️",
            _ => "❌",
        };
}
