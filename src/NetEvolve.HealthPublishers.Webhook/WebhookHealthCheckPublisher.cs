namespace NetEvolve.HealthPublishers.Webhook;

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

internal sealed class WebhookHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly TimeProvider _timeProvider;

    public WebhookHealthCheckPublisher(
        string name,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<WebhookOptions> options,
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
        var now = _timeProvider.GetUtcNow();

        var payload = new Dictionary<string, object?>
        {
            ["systemIdentifier"] = options.SystemIdentifier,
            ["machineName"] = Environment.MachineName,
            ["status"] = report.Status.ToString(),
            ["totalDurationMs"] = report.TotalDuration.TotalMilliseconds,
            ["timestamp"] = now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["entries"] = report
                .Entries.Select(entry => new Dictionary<string, object?>
                {
                    ["name"] = entry.Key,
                    ["status"] = entry.Value.Status.ToString(),
                    ["durationMs"] = entry.Value.Duration.TotalMilliseconds,
                    ["description"] = entry.Value.Description,
                    ["tags"] = entry.Value.Tags,
                })
                .ToArray(),
        };

        using var client = _httpClientFactory.CreateClient(
            $"{DependencyInjectionExtensions.HttpClientNamePrefix}{_name}"
        );

        foreach (var header in options.Headers)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(options.Uri, content, cancellationToken).ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();
    }
}
