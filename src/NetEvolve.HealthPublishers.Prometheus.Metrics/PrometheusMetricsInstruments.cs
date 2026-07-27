namespace NetEvolve.HealthPublishers.Prometheus.Metrics;

using global::Prometheus;

internal sealed class PrometheusMetricsInstruments
{
    private static readonly string[] ReportLabelNames = ["system_identifier", "machine_name"];
    private static readonly string[] EntryLabelNames = ["check", "description", "system_identifier", "machine_name"];

    public PrometheusMetricsInstruments(IMetricFactory factory)
    {
        ReportStatus = factory.CreateGauge(
            "healthcheck_report_status",
            "Overall health report status (0=Unhealthy, 1=Degraded, 2=Healthy).",
            ReportLabelNames
        );
        ReportDuration = factory.CreateGauge(
            "healthcheck_report_duration_seconds",
            "Total duration of the health report execution in seconds.",
            ReportLabelNames
        );
        LastPublishTimestamp = factory.CreateGauge(
            "healthcheck_last_publish_timestamp_seconds",
            "Unix timestamp of the last publish attempt.",
            ReportLabelNames
        );
        EntryStatus = factory.CreateGauge(
            "healthcheck_status",
            "Health check status per entry (0=Unhealthy, 1=Degraded, 2=Healthy).",
            EntryLabelNames
        );
        EntryDuration = factory.CreateGauge(
            "healthcheck_duration_seconds",
            "Duration of the health check execution in seconds.",
            EntryLabelNames
        );
    }

    public Gauge ReportStatus { get; }

    public Gauge ReportDuration { get; }

    public Gauge LastPublishTimestamp { get; }

    public Gauge EntryStatus { get; }

    public Gauge EntryDuration { get; }
}
