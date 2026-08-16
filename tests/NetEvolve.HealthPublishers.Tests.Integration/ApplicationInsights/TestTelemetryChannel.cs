namespace NetEvolve.HealthPublishers.Tests.Integration.ApplicationInsights;

using global::OpenTelemetry;
using global::OpenTelemetry.Logs;
using Microsoft.ApplicationInsights.Extensibility;

// Application Insights 3.x removed the ITelemetryChannel abstraction; telemetry is now
// exported via the OpenTelemetry pipeline, so tests capture it through an in-memory
// log exporter instead of a fake channel.
internal sealed class TestTelemetryChannel
{
    public List<LogRecord> LogRecords { get; } = [];

    public void Configure(TelemetryConfiguration configuration) =>
        configuration.ConfigureOpenTelemetryBuilder(builder =>
            builder.WithLogging(logging => logging.AddInMemoryExporter(LogRecords))
        );
}
