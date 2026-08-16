namespace NetEvolve.HealthPublishers.Tests.Integration.ApplicationInsights;

using global::OpenTelemetry;
using global::OpenTelemetry.Logs;
using Microsoft.ApplicationInsights.Extensibility;

internal static class LogRecordExtensions
{
    public static string? GetAvailabilityAttribute(this LogRecord record, string name) =>
        record.GetAttribute($"microsoft.availability.{name}");

    public static string? GetAttribute(this LogRecord record, string key) =>
        record.Attributes?.FirstOrDefault(attribute => attribute.Key == key).Value?.ToString();
}
