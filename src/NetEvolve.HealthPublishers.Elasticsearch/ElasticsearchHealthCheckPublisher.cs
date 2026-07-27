namespace NetEvolve.HealthPublishers.Elasticsearch;

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class ElasticsearchHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly ElasticsearchClient _client;
    private readonly IOptionsMonitor<ElasticsearchOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ElasticsearchHealthCheckPublisher(
        string name,
        ElasticsearchClient client,
        IOptionsMonitor<ElasticsearchOptions> options,
        TimeProvider timeProvider
    )
    {
        _name = name;
        _client = client;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var options = _options.Get(_name);
        var now = _timeProvider.GetUtcNow();

        var document = new ElasticsearchHealthDocument
        {
            Timestamp = now,
            Status = report.Status.ToString(),
            ElapsedMilliseconds = report.TotalDuration.TotalMilliseconds,
            SystemIdentifier = options.SystemIdentifier,
            MachineName = Environment.MachineName,
            Entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => BuildEntry(entry.Value),
                StringComparer.Ordinal
            ),
        };

        var response = await _client
            .IndexAsync(document, (IndexName)options.IndexName, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            _ = response.TryGetOriginalException(out var originalException);

            throw new HttpRequestException(response.DebugInformation, originalException);
        }
    }

    private static ElasticsearchHealthEntry BuildEntry(HealthReportEntry entry) =>
        new()
        {
            Status = entry.Status.ToString(),
            Description = entry.Description,
            ElapsedMilliseconds = entry.Duration.TotalMilliseconds,
            Tags = [.. entry.Tags],
        };
}
