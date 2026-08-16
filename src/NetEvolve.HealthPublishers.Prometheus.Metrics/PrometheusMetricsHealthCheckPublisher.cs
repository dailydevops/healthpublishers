namespace NetEvolve.HealthPublishers.Prometheus.Metrics;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class PrometheusMetricsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly IOptionsMonitor<PrometheusMetricsOptions> _options;
    private readonly PrometheusMetricsInstruments _instruments;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private HashSet<(string Check, string Description)> _knownEntries = [];

    public PrometheusMetricsHealthCheckPublisher(
        string name,
        IOptionsMonitor<PrometheusMetricsOptions> options,
        PrometheusMetricsInstruments instruments,
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
        cancellationToken.ThrowIfCancellationRequested();

        var options = _options.Get(_name);
        var systemIdentifier = options.SystemIdentifier;
        var machineName = Environment.MachineName;

        _instruments.ReportStatus.WithLabels(systemIdentifier, machineName).Set(MapStatus(report.Status));
        _instruments.ReportDuration.WithLabels(systemIdentifier, machineName).Set(report.TotalDuration.TotalSeconds);
        _instruments
            .LastPublishTimestamp.WithLabels(systemIdentifier, machineName)
            .Set(_timeProvider.GetUtcNow().ToUnixTimeSeconds());

        var currentEntries = new HashSet<(string Check, string Description)>();

        foreach (var entry in report.Entries)
        {
            var description = entry.Value.Description ?? string.Empty;
            _ = currentEntries.Add((entry.Key, description));

            _instruments
                .EntryStatus.WithLabels(entry.Key, description, systemIdentifier, machineName)
                .Set(MapStatus(entry.Value.Status));
            _instruments
                .EntryDuration.WithLabels(entry.Key, description, systemIdentifier, machineName)
                .Set(entry.Value.Duration.TotalSeconds);
        }

        RemoveStaleEntries(currentEntries, systemIdentifier, machineName);

        return Task.CompletedTask;
    }

    // Entries that disappear between publishes (e.g. a check is removed at runtime) must not leave a stale
    // gauge series behind, since this publisher reflects only the latest health report.
    private void RemoveStaleEntries(
        HashSet<(string Check, string Description)> currentEntries,
        string systemIdentifier,
        string machineName
    )
    {
        HashSet<(string Check, string Description)> staleEntries;

        lock (_lock)
        {
            staleEntries = [.. _knownEntries];
            staleEntries.ExceptWith(currentEntries);
            _knownEntries = currentEntries;
        }

        foreach (var (check, description) in staleEntries)
        {
            _instruments.EntryStatus.RemoveLabelled(check, description, systemIdentifier, machineName);
            _instruments.EntryDuration.RemoveLabelled(check, description, systemIdentifier, machineName);
        }
    }

    // Maps HealthStatus to a numeric gauge value. HealthStatus is ordinal: Unhealthy = 0, Degraded = 1, Healthy = 2.
    private static int MapStatus(HealthStatus status) => (int)status;
}
