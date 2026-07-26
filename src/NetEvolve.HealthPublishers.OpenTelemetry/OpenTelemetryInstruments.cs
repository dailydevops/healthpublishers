namespace NetEvolve.HealthPublishers.OpenTelemetry;

using System.Diagnostics.Metrics;

internal sealed class OpenTelemetryInstruments
{
    public OpenTelemetryInstruments(Meter meter)
    {
        ReportDuration = meter.CreateHistogram<double>(
            "healthchecks.report.duration",
            unit: "ms",
            description: "The total duration of a health check report, in milliseconds."
        );
        EntryDuration = meter.CreateHistogram<double>(
            "healthchecks.entry.duration",
            unit: "ms",
            description: "The duration of a single health check entry, in milliseconds."
        );
    }

    public Histogram<double> ReportDuration { get; }

    public Histogram<double> EntryDuration { get; }
}
