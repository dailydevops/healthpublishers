namespace NetEvolve.HealthPublishers.MicrosoftTeams;

using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    private readonly object _notificationLock = new();

    private HealthStatus _lastNotifiedStatus = HealthStatus.Healthy;
    private DateTimeOffset? _pendingSince;

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

        if (!ShouldNotify(report.Status, options.RecoveryConfirmationDelay))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        var card = BuildMessage(report, options, now);

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        using var content = new StringContent(JsonSerializer.Serialize(card), Encoding.UTF8, "application/json");

        using var response = await client
            .PostAsync(options.WebhookUrl, content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
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

    // A conservative cap for the details text block, well within the ~28 KB Adaptive Card payload
    // recommendation, to avoid excessively large webhook requests when a report has many entries.
    private const int MaxDetailsLength = 4000;

    private static Dictionary<string, object?> BuildMessage(
        HealthReport report,
        MicrosoftTeamsOptions options,
        DateTimeOffset now
    )
    {
        var color = MapColor(report.Status);

        var body = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["type"] = "TextBlock",
                ["text"] = $"Health check report: {report.Status}",
                ["weight"] = "Bolder",
                ["size"] = "Medium",
                ["color"] = color,
                ["wrap"] = true,
            },
            new Dictionary<string, object?>
            {
                ["type"] = "FactSet",
                ["facts"] = new object[]
                {
                    new Dictionary<string, object?> { ["title"] = "System", ["value"] = options.SystemIdentifier },
                    new Dictionary<string, object?> { ["title"] = "Machine", ["value"] = Environment.MachineName },
                    new Dictionary<string, object?>
                    {
                        ["title"] = "Duration",
                        ["value"] = string.Create(
                            CultureInfo.InvariantCulture,
                            $"{report.TotalDuration.TotalMilliseconds:0.##}ms"
                        ),
                    },
                    new Dictionary<string, object?> { ["title"] = "Checked at", ["value"] = now.ToString("O") },
                },
            },
        };

        var details = BuildDetails(report);
        if (!string.IsNullOrEmpty(details))
        {
            body.Add(
                new Dictionary<string, object?>
                {
                    ["type"] = "TextBlock",
                    ["text"] = details,
                    ["wrap"] = true,
                }
            );
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "message",
            ["attachments"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["contentType"] = "application/vnd.microsoft.card.adaptive",
                    ["content"] = new Dictionary<string, object?>
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

        return builder.ToString();
    }

    private static string MapColor(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => "good",
            HealthStatus.Degraded => "warning",
            _ => "attention",
        };
}
