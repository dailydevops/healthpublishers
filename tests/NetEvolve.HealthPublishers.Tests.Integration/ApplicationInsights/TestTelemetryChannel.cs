namespace NetEvolve.HealthPublishers.Tests.Integration.ApplicationInsights;

using Microsoft.ApplicationInsights.Channel;

internal sealed class TestTelemetryChannel : ITelemetryChannel, IAsyncFlushable
{
    public List<ITelemetry> SentItems { get; } = [];

    public bool? DeveloperMode { get; set; }

    public string EndpointAddress { get; set; } = string.Empty;

    public void Dispose() { }

    public void Flush() { }

    public Task<bool> FlushAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public void Send(ITelemetry item) => SentItems.Add(item);
}
