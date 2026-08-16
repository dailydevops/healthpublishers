namespace NetEvolve.HealthPublishers.AWS.CloudWatch;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class CloudWatchHealthCheckPublisher : IHealthCheckPublisher
{
    private const string MetricNameOverallStatus = "OverallStatus";
    private const string MetricNameDuration = "Duration";
    private const string MetricNameCheckStatus = "CheckStatus";
    private const string MetricNameCheckDuration = "CheckDuration";

    private const string DimensionSystemIdentifier = "SystemIdentifier";
    private const string DimensionMachineName = "MachineName";
    private const string DimensionCheckName = "CheckName";

    private readonly string _name;
    private readonly IAmazonCloudWatch _client;
    private readonly IOptionsMonitor<CloudWatchOptions> _options;
    private readonly TimeProvider _timeProvider;

    public CloudWatchHealthCheckPublisher(
        string name,
        IAmazonCloudWatch client,
        IOptionsMonitor<CloudWatchOptions> options,
        TimeProvider timeProvider
    )
    {
        _name = name;
        _client = client;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = _options.Get(_name);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var machineName = Environment.MachineName;

        var metricData = new List<MetricDatum>
        {
            CreateMetric(
                MetricNameOverallStatus,
                ToMetricValue(report.Status),
                StandardUnit.Count,
                now,
                BuildDimensions(options.SystemIdentifier, machineName)
            ),
            CreateMetric(
                MetricNameDuration,
                report.TotalDuration.TotalMilliseconds,
                StandardUnit.Milliseconds,
                now,
                BuildDimensions(options.SystemIdentifier, machineName)
            ),
        };

        foreach (var entry in report.Entries)
        {
            var dimensions = BuildDimensions(options.SystemIdentifier, machineName, entry.Key);

            metricData.Add(
                CreateMetric(
                    MetricNameCheckStatus,
                    ToMetricValue(entry.Value.Status),
                    StandardUnit.Count,
                    now,
                    dimensions
                )
            );
            metricData.Add(
                CreateMetric(
                    MetricNameCheckDuration,
                    entry.Value.Duration.TotalMilliseconds,
                    StandardUnit.Milliseconds,
                    now,
                    dimensions
                )
            );
        }

        const int maxDatumsPerRequest = 20;

        for (var i = 0; i < metricData.Count; i += maxDatumsPerRequest)
        {
            var batch = metricData.GetRange(i, Math.Min(maxDatumsPerRequest, metricData.Count - i));
            var request = new PutMetricDataRequest { Namespace = options.Namespace, MetricData = batch };

            _ = await _client.PutMetricDataAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static MetricDatum CreateMetric(
        string metricName,
        double value,
        StandardUnit unit,
        DateTime timestampUtc,
        List<Dimension> dimensions
    ) =>
        new()
        {
            MetricName = metricName,
            Value = value,
            Unit = unit,
            Timestamp = timestampUtc,
            Dimensions = dimensions,
        };

    private static double ToMetricValue(HealthStatus status) =>
        status switch
        {
            HealthStatus.Healthy => 1d,
            HealthStatus.Degraded => 0.5d,
            _ => 0d,
        };

    private static List<Dimension> BuildDimensions(
        string systemIdentifier,
        string machineName,
        string? checkName = null
    )
    {
        var dimensions = new List<Dimension>
        {
            new() { Name = DimensionSystemIdentifier, Value = systemIdentifier },
            new() { Name = DimensionMachineName, Value = machineName },
        };

        if (checkName is not null)
        {
            dimensions.Add(new Dimension { Name = DimensionCheckName, Value = checkName });
        }

        return dimensions;
    }
}
