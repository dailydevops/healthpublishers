namespace NetEvolve.HealthPublishers.Slack;

using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class SlackHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SlackOptions> _options;
    private readonly TimeProvider _timeProvider;

    public SlackHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SlackOptions> options,
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
        var options = _options.Get(_name);

        var color = MapColor(report.Status);
        var now = _timeProvider.GetUtcNow();

        var payload = new Dictionary<string, object?>
        {
            ["text"] = $"Health check report: {report.Status}",
            ["attachments"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["color"] = color,
                    ["ts"] = now.ToUnixTimeSeconds(),
                    ["fields"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["title"] = "System Identifier",
                            ["value"] = options.SystemIdentifier,
                            ["short"] = true,
                        },
                        new Dictionary<string, object?>
                        {
                            ["title"] = "Machine",
                            ["value"] = Environment.MachineName,
                            ["short"] = true,
                        },
                    },
                    ["text"] = BuildText(report),
                },
            },
        };

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.PostAsync((Uri?)null, content, cancellationToken).ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private static string BuildText(HealthReport report)
    {
        var builder = new StringBuilder(capacity: 256)
            .Append("Overall status: ")
            .Append(report.Status)
            .Append(", elapsed ")
            .Append(report.TotalDuration.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture))
            .Append("ms.");

        if (report.Entries.Count == 0)
        {
            return builder.ToString();
        }

        foreach (var entry in report.Entries)
        {
            var description = string.IsNullOrWhiteSpace(entry.Value.Description)
                ? string.Empty
                : $" - {entry.Value.Description}";

            _ = builder
                .AppendLine()
                .Append("- *")
                .Append(entry.Key)
                .Append("*: ")
                .Append(entry.Value.Status)
                .Append(" (")
                .Append(entry.Value.Duration.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture))
                .Append("ms)")
                .Append(description);
        }

        return builder.ToString();
    }

    private static string MapColor(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => "good",
            HealthStatus.Degraded => "warning",
            _ => "danger",
        };
}
