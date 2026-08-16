namespace NetEvolve.HealthPublishers.PagerDuty;

using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class PagerDutyHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<PagerDutyOptions> _options;
    private readonly TimeProvider _timeProvider;

    public PagerDutyHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PagerDutyOptions> options,
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

        var dedupKey = BuildDedupKey(options.SystemIdentifier);
        var eventAction = MapEventAction(report.Status);

        var pagerDutyEvent = new Dictionary<string, object?>
        {
            ["routing_key"] = options.RoutingKey,
            ["event_action"] = eventAction,
            ["dedup_key"] = dedupKey,
        };

        // The Events API v2 only accepts (and requires) a `payload` when triggering an incident; a
        // `resolve` event is fully identified by its `routing_key` and `dedup_key`.
        if (eventAction == TriggerAction)
        {
            var now = _timeProvider.GetUtcNow();

            pagerDutyEvent["payload"] = new Dictionary<string, object?>
            {
                ["summary"] = $"Health check report: {report.Status}",
                ["source"] = Environment.MachineName,
                ["severity"] = MapSeverity(report.Status),
                ["timestamp"] = now.ToString("O"),
                ["custom_details"] = BuildCustomDetails(report),
            };
        }

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        using var content = new StringContent(
            JsonSerializer.Serialize(pagerDutyEvent),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await client
            .PostAsync(new Uri("v2/enqueue", UriKind.Relative), content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private const string TriggerAction = "trigger";
    private const string ResolveAction = "resolve";

    private static string BuildDedupKey(string systemIdentifier) => $"healthpublishers:{systemIdentifier}";

    private static string MapEventAction(HealthStatus status) =>
        status == HealthStatus.Healthy ? ResolveAction : TriggerAction;

    private static string MapSeverity(HealthStatus status) =>
        status switch
        {
            HealthStatus.Degraded => "warning",
            _ => "critical",
        };

    private static Dictionary<string, object?> BuildCustomDetails(HealthReport report)
    {
        var entries = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var entry in report.Entries)
        {
            entries[entry.Key] = new Dictionary<string, object?>
            {
                ["status"] = entry.Value.Status.ToString(),
                ["duration_ms"] = entry.Value.Duration.TotalMilliseconds,
                ["description"] = entry.Value.Description,
            };
        }

        return new Dictionary<string, object?>
        {
            ["overall_status"] = report.Status.ToString(),
            ["total_duration_ms"] = report.TotalDuration.TotalMilliseconds,
            ["entries"] = entries,
        };
    }
}
