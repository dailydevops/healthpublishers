namespace NetEvolve.HealthPublishers.Tests.Unit.ApplicationInsights;

using Microsoft.ApplicationInsights.Channel;

internal sealed class TestTelemetryChannel : ITelemetryChannel, IAsyncFlushable
{
    public List<ITelemetry> SentItems { get; } = [];

    public int FlushCount { get; private set; }

    public int FlushAsyncCount { get; private set; }

    public bool? DeveloperMode { get; set; }

    public string EndpointAddress { get; set; } = string.Empty;

    public void Dispose() { }

    public void Flush() => FlushCount++;

    public Task<bool> FlushAsync(CancellationToken cancellationToken)
    {
        FlushAsyncCount++;
        return Task.FromResult(true);
    }

    public void Send(ITelemetry item) => SentItems.Add(item);
}
