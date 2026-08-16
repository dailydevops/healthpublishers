namespace NetEvolve.HealthPublishers.Prometheus.PushGateway;

using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class PrometheusPushGatewayHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<PrometheusPushGatewayOptions> _options;
    private readonly TimeProvider _timeProvider;

    public PrometheusPushGatewayHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PrometheusPushGatewayOptions> options,
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

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        using var content = new StringContent(BuildExpositionBody(report, options, _timeProvider), Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; version=0.0.4; charset=utf-8");

        using var response = await client
            .PutAsync(BuildRequestUri(options), content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    // The job and instance labels are supplied by the Pushgateway itself, derived from the request path;
    // they must not also be emitted as labels in the exposition body.
    internal static Uri BuildRequestUri(PrometheusPushGatewayOptions options)
    {
        var path = $"metrics/{BuildPathSegment("job", options.Job)}/{BuildPathSegment("instance", options.Instance)}";

        return new Uri(path, UriKind.Relative);
    }

    // Slashes in a grouping-key value are not escapable via percent-encoding - the Pushgateway router treats them
    // as path separators regardless. Values containing a slash must instead use the `@base64` path-segment form.
    private static string BuildPathSegment(string label, string value) =>
        value.Contains('/', StringComparison.Ordinal)
            ? $"{label}@base64/{Base64UrlEncode(value)}"
            : $"{label}/{Uri.EscapeDataString(value)}";

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    // Maps HealthStatus to a numeric gauge value. HealthStatus is ordinal: Unhealthy = 0, Degraded = 1, Healthy = 2.
    private static int MapStatus(HealthStatus status) => (int)status;

    private static string FormatDouble(double value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string BuildExpositionBody(
        HealthReport report,
        PrometheusPushGatewayOptions options,
        TimeProvider timeProvider
    )
    {
        var machineName = Environment.MachineName;
        var systemIdentifier = options.SystemIdentifier;
        var reportLabels = $"system_identifier=\"{Escape(systemIdentifier)}\",machine_name=\"{Escape(machineName)}\"";

        var builder = new StringBuilder(512);

        _ = builder
            .Append(
                "# HELP healthcheck_report_status Overall health report status (0=Unhealthy, 1=Degraded, 2=Healthy)."
            )
            .Append('\n')
            .Append("# TYPE healthcheck_report_status gauge")
            .Append('\n')
            .Append("healthcheck_report_status{")
            .Append(reportLabels)
            .Append("} ")
            .Append(MapStatus(report.Status))
            .Append('\n');

        _ = builder
            .Append(
                "# HELP healthcheck_report_duration_seconds Total duration of the health report execution in seconds."
            )
            .Append('\n')
            .Append("# TYPE healthcheck_report_duration_seconds gauge")
            .Append('\n')
            .Append("healthcheck_report_duration_seconds{")
            .Append(reportLabels)
            .Append("} ")
            .Append(FormatDouble(report.TotalDuration.TotalSeconds))
            .Append('\n');

        _ = builder
            .Append("# HELP healthcheck_last_publish_timestamp_seconds Unix timestamp of the last publish attempt.")
            .Append('\n')
            .Append("# TYPE healthcheck_last_publish_timestamp_seconds gauge")
            .Append('\n')
            .Append("healthcheck_last_publish_timestamp_seconds{")
            .Append(reportLabels)
            .Append("} ")
            .Append(timeProvider.GetUtcNow().ToUnixTimeSeconds())
            .Append('\n');

        if (report.Entries.Count > 0)
        {
            _ = builder
                .Append("# HELP healthcheck_status Health check status per entry (0=Unhealthy, 1=Degraded, 2=Healthy).")
                .Append('\n')
                .Append("# TYPE healthcheck_status gauge")
                .Append('\n');

            foreach (var entry in report.Entries)
            {
                _ = builder
                    .Append("healthcheck_status{check=\"")
                    .Append(Escape(entry.Key))
                    .Append("\",description=\"")
                    .Append(Escape(entry.Value.Description ?? string.Empty))
                    .Append("\",")
                    .Append(reportLabels)
                    .Append("} ")
                    .Append(MapStatus(entry.Value.Status))
                    .Append('\n');
            }

            _ = builder
                .Append("# HELP healthcheck_duration_seconds Duration of the health check execution in seconds.")
                .Append('\n')
                .Append("# TYPE healthcheck_duration_seconds gauge")
                .Append('\n');

            foreach (var entry in report.Entries)
            {
                _ = builder
                    .Append("healthcheck_duration_seconds{check=\"")
                    .Append(Escape(entry.Key))
                    .Append("\",description=\"")
                    .Append(Escape(entry.Value.Description ?? string.Empty))
                    .Append("\",")
                    .Append(reportLabels)
                    .Append("} ")
                    .Append(FormatDouble(entry.Value.Duration.TotalSeconds))
                    .Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
