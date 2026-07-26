namespace NetEvolve.HealthPublishers.Tests.Integration.Internals;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

internal sealed record RecordedMeasurement(
    string InstrumentName,
    double Value,
    IReadOnlyDictionary<string, string?> Tags
);

/// <summary>
/// A <see cref="MeterListener"/> wrapper that captures every measurement recorded on a named <see cref="Meter"/>,
/// so tests can assert on what a publisher actually recorded without depending on a specific exporter.
/// </summary>
internal sealed class MetricsRecorder : IDisposable
{
    private readonly MeterListener _listener = new();

    /// <summary>
    /// Starts observing measurements recorded on the given <paramref name="meter"/> instance.
    /// </summary>
    /// <param name="meter">
    /// The exact <see cref="Meter"/> instance to observe. Filtering by instance rather than by name keeps
    /// parallel tests isolated from each other, since every test builds its own <see cref="Meter"/> under the
    /// same well-known name.
    /// </param>
    public MetricsRecorder(Meter meter)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) =>
            {
                var tagDictionary = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var tag in tags)
                {
                    tagDictionary[tag.Key] = tag.Value?.ToString();
                }
                Measurements.Add(new RecordedMeasurement(instrument.Name, measurement, tagDictionary));
            }
        );
        _listener.Start();
    }

    public List<RecordedMeasurement> Measurements { get; } = [];

    public void Dispose() => _listener.Dispose();
}
