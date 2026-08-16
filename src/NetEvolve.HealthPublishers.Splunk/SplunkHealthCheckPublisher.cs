namespace NetEvolve.HealthPublishers.Splunk;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class SplunkHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SplunkOptions> _options;
    private readonly TimeProvider _timeProvider;

    public SplunkHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SplunkOptions> options,
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

        var payload = new Dictionary<string, object?>
        {
            ["time"] = now.ToUnixTimeMilliseconds() / 1000d,
            ["event"] = BuildEvent(report, options),
        };

        if (!string.IsNullOrWhiteSpace(options.SourceType))
        {
            payload["sourcetype"] = options.SourceType;
        }

        if (!string.IsNullOrWhiteSpace(options.Source))
        {
            payload["source"] = options.Source;
        }

        if (!string.IsNullOrWhiteSpace(options.Index))
        {
            payload["index"] = options.Index;
        }

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        client.DefaultRequestHeaders.Add("Authorization", $"Splunk {options.HecToken}");

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client
            .PostAsync(new Uri("services/collector/event", UriKind.Relative), content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private static Dictionary<string, object?> BuildEvent(HealthReport report, SplunkOptions options) =>
        new()
        {
            ["message"] = string.Create(
                CultureInfo.InvariantCulture,
                $"Health check report {report.Status} in {report.TotalDuration.TotalMilliseconds:0.##}ms"
            ),
            ["status"] = report.Status.ToString(),
            ["elapsed_ms"] = report.TotalDuration.TotalMilliseconds,
            ["system_identifier"] = options.SystemIdentifier,
            ["machine_name"] = Environment.MachineName,
            ["entries"] = report.Entries.ToDictionary(entry => entry.Key, entry => BuildEntry(entry.Value)),
        };

    private static Dictionary<string, object?> BuildEntry(HealthReportEntry entry) =>
        new()
        {
            ["status"] = entry.Status.ToString(),
            ["description"] = entry.Description,
            ["elapsed_ms"] = entry.Duration.TotalMilliseconds,
            ["tags"] = entry.Tags,
        };
}
