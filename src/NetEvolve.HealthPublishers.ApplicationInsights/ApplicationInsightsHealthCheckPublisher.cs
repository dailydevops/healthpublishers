namespace NetEvolve.HealthPublishers.ApplicationInsights;

using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class ApplicationInsightsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly string _name;
    private readonly Func<TelemetryClient> _telemetryClientFactory;
    private readonly IOptionsMonitor<ApplicationInsightsOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ApplicationInsightsHealthCheckPublisher(
        string name,
        Func<TelemetryClient> telemetryClientFactory,
        IOptionsMonitor<ApplicationInsightsOptions> options,
        TimeProvider timeProvider
    )
    {
        _name = name;
        _telemetryClientFactory = telemetryClientFactory;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var options = _options.Get(_name);

        var availability = new AvailabilityTelemetry
        {
#if NET9_0_OR_GREATER
            Id = Guid.CreateVersion7().ToString("N"),
#else
            Id = Guid.NewGuid().ToString("N"),
#endif
            Name = "HealthReport",
            Timestamp = _timeProvider.GetUtcNow(),
            Duration = report.TotalDuration,
            RunLocation = Environment.MachineName,
            Success = report.Status == HealthStatus.Healthy,
            Message = report.Status.ToString(),
        };

        availability.Properties["SystemIdentifier"] = options.SystemIdentifier;
        availability.Properties["MachineName"] = Environment.MachineName;
        availability.Properties["Entries"] = JsonSerializer.Serialize(
            report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    entry.Value.Description,
                    ElapsedMilliseconds = entry.Value.Duration.TotalMilliseconds,
                    entry.Value.Tags,
                }
            )
        );

        var telemetryClient = _telemetryClientFactory();

        telemetryClient.TrackAvailability(availability);

        _ = await telemetryClient.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
