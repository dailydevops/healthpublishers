namespace NetEvolve.HealthPublishers.Tests.Integration.Internals;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

internal sealed record RecordedMeasurement(
    string InstrumentName,
    double Value,
    IReadOnlyDictionary<string, string?> Tags
);
