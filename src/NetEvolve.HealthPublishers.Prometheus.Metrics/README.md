# NetEvolve.HealthPublishers.Prometheus.Metrics

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Prometheus.Metrics.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.Metrics/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Prometheus.Metrics.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.Metrics/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that updates [`prometheus-net`](https://github.com/prometheus-net/prometheus-net) `Gauge` metrics in an in-process `CollectorRegistry` on every publish, reflecting the *latest* `HealthReport` results, ready to be scraped by Prometheus.

## Pull vs. push

This package complements [**NetEvolve.HealthPublishers.Prometheus.PushGateway**](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.PushGateway/), which *pushes* metrics over HTTP to a Prometheus Pushgateway instance on every publish. This package instead follows the standard Prometheus **pull/scrape** model: it only mutates in-process gauge state, it never performs any HTTP call. Scraping is handled entirely by `prometheus-net`'s own ASP.NET Core middleware (e.g. `AspNetCoreExporterExtensions.MapMetrics` / `UseMetricServer`, from the `prometheus-net.AspNetCore` package), which the consuming application must wire up separately.

Because a library should not silently pollute an application's global default `CollectorRegistry`, every registration of this publisher gets its own dedicated `CollectorRegistry`, created via `Metrics.NewCustomRegistry()`. This registry is registered in the DI container as a **keyed singleton**, keyed by the publisher's name (`"Default"` unless an explicit name is used). The consuming application resolves it and passes it explicitly to the `prometheus-net` middleware:

```csharp
using Prometheus;

var registry = app.Services.GetRequiredKeyedService<CollectorRegistry>("Default");

app.MapMetrics("/metrics", registry: registry);
```

## Features

- Updates a full set of gauges on every publish: overall report status, report duration, last publish timestamp, and per-check status and duration
- Maps `HealthStatus` to a numeric gauge value using the enum's own ordinal (`Unhealthy` → `0`, `Degraded` → `1`, `Healthy` → `2`)
- Labels every gauge with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Removes stale per-check gauge series for checks that no longer appear in a later report, so the registry always reflects only the latest health report
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations, each with its own dedicated `CollectorRegistry`, to keep multiple publisher setups fully isolated from each other and from the global default registry

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Prometheus.Metrics
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Prometheus.Metrics
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Prometheus.Metrics" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Prometheus.Metrics;

var builder = services.AddHealthChecks();

builder.AddPrometheusMetricsPublisher(options =>
{
    options.SystemIdentifier = "checkout-service";
});
```

Then, separately, wire up `prometheus-net`'s ASP.NET Core middleware against the dedicated registry (requires the `prometheus-net.AspNetCore` package):

```csharp
using Prometheus;

app.MapMetrics("/metrics", registry: app.Services.GetRequiredKeyedService<CollectorRegistry>("Default"));
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddPrometheusMetricsPublisher(options =>
{
    options.SystemIdentifier = "checkout-service"; // Required, labels every gauge alongside the machine name
});
```

### Advanced Example

Register multiple named publishers, each with its own dedicated `CollectorRegistry` and, therefore, its own `/metrics` endpoint:

```csharp
var builder = services.AddHealthChecks();

builder.AddPrometheusMetricsPublisher("Internal", options => options.SystemIdentifier = "checkout-service");
builder.AddPrometheusMetricsPublisher("External", options => options.SystemIdentifier = "checkout-service");
```

```csharp
using Prometheus;

app.MapMetrics("/metrics/internal", registry: app.Services.GetRequiredKeyedService<CollectorRegistry>("Internal"));
app.MapMetrics("/metrics/external", registry: app.Services.GetRequiredKeyedService<CollectorRegistry>("External"));
```

## Configuration

### Code-based

```csharp
builder.AddPrometheusMetricsPublisher(options =>
{
    options.SystemIdentifier = "checkout-service"; // Required
});
```

### appsettings.json-based

```csharp
builder.AddPrometheusMetricsPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Prometheus": {
      "Metrics": {
        "Default": {
          "SystemIdentifier": "checkout-service"
        }
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddPrometheusMetricsPublisher("Internal")` reads `HealthPublishers:Prometheus:Metrics:Internal`.

## Published Metrics

| Metric                                       | Type  | Labels                                                      | Description                                              |
| --------------------------------------------- | ----- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| `healthcheck_report_status`                   | gauge | `system_identifier`, `machine_name`                           | Overall health report status (`0`/`1`/`2`)                    |
| `healthcheck_report_duration_seconds`         | gauge | `system_identifier`, `machine_name`                           | Total duration of the health report execution                |
| `healthcheck_last_publish_timestamp_seconds`  | gauge | `system_identifier`, `machine_name`                           | Unix timestamp of the last publish attempt                    |
| `healthcheck_status`                          | gauge | `check`, `description`, `system_identifier`, `machine_name`     | Status of an individual health check entry (`0`/`1`/`2`)       |
| `healthcheck_duration_seconds`                | gauge | `check`, `description`, `system_identifier`, `machine_name`     | Duration of an individual health check entry                   |

## Requirements

- .NET 8.0 or higher
- The `prometheus-net.AspNetCore` package (or equivalent), wired up in the consuming application, to actually expose the registry via a `/metrics` endpoint

## Related Packages

- [**NetEvolve.HealthPublishers.Prometheus.PushGateway**](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.PushGateway/) - Push-model counterpart, pushes the same set of metrics to a Prometheus Pushgateway instance over HTTP
- [**NetEvolve.HealthPublishers.Abstractions**](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Abstractions/) - Shared abstractions used by all `NetEvolve.HealthPublishers.*` packages

## Documentation

For complete documentation, please visit the [official documentation](https://github.com/dailydevops/healthpublishers/blob/main/README.md).

## Contributing

Contributions are welcome! Please read the [Contributing Guidelines](https://github.com/dailydevops/healthpublishers/blob/main/CONTRIBUTING.md) before submitting a pull request.

## Support

- **Issues**: Report bugs or request features on [GitHub Issues](https://github.com/dailydevops/healthpublishers/issues)
- **Documentation**: Read the full documentation at [https://github.com/dailydevops/healthpublishers](https://github.com/dailydevops/healthpublishers)

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE) file for details.

---

> [!NOTE]
> **Made with ❤️ by the NetEvolve Team**
> Visit us at [https://www.daily-devops.net](https://www.daily-devops.net) for more information about our services and solutions.
