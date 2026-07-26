namespace NetEvolve.HealthPublishers.Seq;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class SeqHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SeqOptions> _options;
    private readonly TimeProvider _timeProvider;

    public SeqHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SeqOptions> options,
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

        var clefEvent = new Dictionary<string, object?>
        {
            ["@t"] = _timeProvider.GetUtcNow().ToString("o"),
            ["@mt"] = "Health check report {Status} in {ElapsedMilliseconds}ms",
            ["@l"] = MapLevel(report.Status),
            ["Status"] = report.Status.ToString(),
            ["ElapsedMilliseconds"] = report.TotalDuration.TotalMilliseconds,
            ["MachineName"] = Environment.MachineName,
            ["SystemIdentifier"] = options.SystemIdentifier,
            ["Entries"] = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    entry.Value.Description,
                    ElapsedMilliseconds = entry.Value.Duration.TotalMilliseconds,
                    entry.Value.Tags,
                }
            ),
        };

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-Seq-ApiKey", options.ApiKey);
        }

        using var content = new StringContent(JsonSerializer.Serialize(clefEvent), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.serilog.clef");

        using var response = await client
            .PostAsync(new Uri("ingest/clef", UriKind.Relative), content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private static string MapLevel(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => "Information",
            HealthStatus.Degraded => "Warning",
            _ => "Error",
        };
}
