namespace NetEvolve.HealthPublishers.Opsgenie;

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class OpsgenieHealthCheckPublisher : IHealthCheckPublisher
{
    // The prefix used to derive a stable Opsgenie alert alias from a SystemIdentifier.
    internal const string AliasPrefix = "healthpublishers:";

    // Opsgenie's Alert API caps the `description` field at 15000 characters.
    private const int MaxDescriptionLength = 15000;
    private const string ClosingMarker = "%%%";

    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OpsgenieOptions> _options;
    private readonly TimeProvider _timeProvider;

    public OpsgenieHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpsgenieOptions> options,
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
        var alias = BuildAlias(options.SystemIdentifier);

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        client.DefaultRequestHeaders.Add("Authorization", $"GenieKey {options.ApiKey}");

        if (report.Status == HealthStatus.Healthy)
        {
            await CloseAlertAsync(client, alias, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CreateAlertAsync(client, alias, options, report, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CreateAlertAsync(
        HttpClient client,
        string alias,
        OpsgenieOptions options,
        HealthReport report,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNow();

        var alert = new Dictionary<string, object?>
        {
            ["message"] = $"Health check report: {report.Status}",
            ["alias"] = alias,
            ["description"] = BuildDescription(report),
            ["priority"] = MapPriority(report.Status),
            ["tags"] = new[]
            {
                $"system_identifier:{options.SystemIdentifier}",
                $"machine_name:{Environment.MachineName}",
                $"status:{report.Status}",
            },
            ["details"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["system_identifier"] = options.SystemIdentifier,
                ["machine_name"] = Environment.MachineName,
                ["reported_at"] = now.ToString("O", CultureInfo.InvariantCulture),
            },
        };

        using var content = new StringContent(JsonSerializer.Serialize(alert), Encoding.UTF8, "application/json");

        using var response = await client
            .PostAsync(new Uri("v2/alerts", UriKind.Relative), content, cancellationToken)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }

    private static async Task CloseAlertAsync(HttpClient client, string alias, CancellationToken cancellationToken)
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var uri = new Uri($"v2/alerts/{Uri.EscapeDataString(alias)}/close?identifierType=alias", UriKind.Relative);

        using var response = await client.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // The alert was already closed, or never existed in the first place; nothing to do.
            return;
        }

        _ = response.EnsureSuccessStatusCode();
    }

    internal static string BuildAlias(string systemIdentifier) => $"{AliasPrefix}{systemIdentifier}";

    private static string BuildDescription(HealthReport report)
    {
        if (report.Entries.Count == 0)
        {
            return $"Overall status: {report.Status}, elapsed {report.TotalDuration.TotalMilliseconds:0.##}ms.";
        }

        var builder = new StringBuilder(capacity: 256)
            .Append("Overall status: ")
            .Append(report.Status)
            .Append(", elapsed ")
            .Append(report.TotalDuration.TotalMilliseconds)
            .AppendLine("ms.")
            .AppendLine(ClosingMarker);

        var maxContentLength = MaxDescriptionLength - ClosingMarker.Length;

        foreach (var entry in report.Entries)
        {
            var description = string.IsNullOrWhiteSpace(entry.Value.Description)
                ? string.Empty
                : $" - {entry.Value.Description}";
            var line =
                $"- **{entry.Key}**: {entry.Value.Status} ({entry.Value.Duration.TotalMilliseconds}ms){description}{Environment.NewLine}";

            // Drop whole entries that would overflow the limit, rather than cutting one in half.
            if (builder.Length + line.Length > maxContentLength)
            {
                break;
            }

            _ = builder.Append(line);
        }

        return builder.Append(ClosingMarker).ToString();
    }

    private static string MapPriority(HealthStatus status) =>
        status switch
        {
            HealthStatus.Unhealthy => "P1",
            _ => "P3",
        };
}
