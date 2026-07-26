namespace NetEvolve.HealthPublishers.Datadog;

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class DatadogHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<DatadogOptions> _options;
    private readonly TimeProvider _timeProvider;

    public DatadogHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DatadogOptions> options,
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

        var alertType = MapAlertType(report.Status);
        var now = _timeProvider.GetUtcNow();

        var datadogEvent = new Dictionary<string, object?>
        {
            ["title"] = $"Health check report: {report.Status}",
            ["text"] = BuildText(report),
            ["date_happened"] = now.ToUnixTimeSeconds(),
            ["alert_type"] = alertType,
            ["tags"] = new[]
            {
                $"system_identifier:{options.SystemIdentifier}",
                $"machine_name:{Environment.MachineName}",
                $"status:{report.Status}",
            },
        };

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        client.DefaultRequestHeaders.Add("DD-API-KEY", options.ApiKey);

        using var content = new StringContent(
            JsonSerializer.Serialize(datadogEvent),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await client
            .PostAsync(new Uri("api/v1/events", UriKind.Relative), content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private static string BuildText(HealthReport report)
    {
        if (report.Entries.Count == 0)
        {
            return $"Overall status: {report.Status}, elapsed {report.TotalDuration.TotalMilliseconds}ms.";
        }

        var builder = new StringBuilder()
            .Append("Overall status: ")
            .Append(report.Status)
            .Append(", elapsed ")
            .Append(report.TotalDuration.TotalMilliseconds)
            .AppendLine("ms.")
            .AppendLine("%%%");

        foreach (var entry in report.Entries)
        {
            _ = builder
                .Append("- **")
                .Append(entry.Key)
                .Append("**: ")
                .Append(entry.Value.Status)
                .Append(" (")
                .Append(entry.Value.Duration.TotalMilliseconds)
                .Append("ms)");

            if (!string.IsNullOrWhiteSpace(entry.Value.Description))
            {
                _ = builder.Append(" - ").Append(entry.Value.Description);
            }

            _ = builder.AppendLine();
        }

        return builder.Append("%%%").ToString();
    }

    private static string MapAlertType(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => "success",
            HealthStatus.Degraded => "warning",
            _ => "error",
        };
}
