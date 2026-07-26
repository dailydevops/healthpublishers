namespace NetEvolve.HealthPublishers.OpenTelemetry;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class OpenTelemetryHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IOptionsMonitor<OpenTelemetryOptions> _options;
    private readonly OpenTelemetryInstruments _instruments;
    private readonly TimeProvider _timeProvider;

    public OpenTelemetryHealthCheckPublisher(
        string name,
        IOptionsMonitor<OpenTelemetryOptions> options,
        OpenTelemetryInstruments instruments,
        TimeProvider timeProvider
    )
    {
        _name = name;
        _options = options;
        _instruments = instruments;
        _timeProvider = timeProvider;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var options = _options.Get(_name);

        // Shared across the report- and entry-level tags; TagList is a struct, so each copy below
        // (still within its 8-slot inline capacity) mutates independently of this one.
        var commonTags = new TagList
        {
            { "healthchecks.publisher.name", _name },
            { "healthchecks.system.identifier", options.SystemIdentifier },
            { "healthchecks.machine.name", Environment.MachineName },
            { "healthchecks.timestamp", _timeProvider.GetUtcNow().ToString("o") },
        };

        var reportTags = commonTags;
        reportTags.Add("healthchecks.status", report.Status.ToString());
        _instruments.ReportDuration.Record(report.TotalDuration.TotalMilliseconds, reportTags);

        foreach (var entry in report.Entries)
        {
            var entryTags = commonTags;
            entryTags.Add("healthchecks.entry.name", entry.Key);
            entryTags.Add("healthchecks.status", entry.Value.Status.ToString());
            _instruments.EntryDuration.Record(entry.Value.Duration.TotalMilliseconds, entryTags);
        }

        return Task.CompletedTask;
    }
}
