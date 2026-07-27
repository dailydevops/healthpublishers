# NetEvolve.HealthPublishers.Prometheus.PushGateway

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Prometheus.PushGateway.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.PushGateway/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Prometheus.PushGateway.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.PushGateway/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to a [Prometheus Pushgateway](https://github.com/prometheus/pushgateway) instance, using the text exposition format directly over HTTP (`PUT /metrics/job/<job>/instance/<instance>`). `PUT` fully replaces the metric group on every publish, so checks that are removed or renamed don't leave stale series behind.

## Features

- Pushes a full set of gauge metrics per publish: overall report status, report duration, last publish timestamp, and per-check status and duration
- Maps `HealthStatus` to a numeric gauge value using the enum's own ordinal (`Unhealthy` → `0`, `Degraded` → `1`, `Healthy` → `2`)
- Labels every metric with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Requires a distinct `Instance` per publisher so Pushgateway groups each publisher's metrics under its own path, instead of publishers sharing a `Job` overwriting each other's same-named metrics
- `Job` and `Instance` values containing a `/` are sent using Pushgateway's `@base64` grouping-key syntax, since percent-encoding a slash does not stop Pushgateway from treating it as a path separator
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Pushgateway targets side by side
- No `prometheus-net` client dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Prometheus.PushGateway
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Prometheus.PushGateway
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Prometheus.PushGateway" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Prometheus.PushGateway;

var builder = services.AddHealthChecks();

builder.AddPrometheusPushGateway(options =>
{
    options.ServerUrl = new Uri("https://pushgateway.example.com");
    options.Job = "checkout-service";
    options.Instance = "checkout-service-01";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddPrometheusPushGateway(options =>
{
    options.ServerUrl = new Uri("https://pushgateway.example.com"); // Required
    options.Job = "checkout-service"; // Required, used as the `job` path segment
    options.Instance = "checkout-service-01"; // Required, used as the `instance` path segment
    options.SystemIdentifier = "checkout-service"; // Required, labels every metric alongside the machine name
});
```

### Advanced Example

Register multiple named Pushgateway targets, each pushing the same health reports to a different instance:

```csharp
var builder = services.AddHealthChecks();

builder.AddPrometheusPushGateway("Internal", options =>
{
    options.ServerUrl = new Uri("https://pushgateway-internal.example.com");
    options.Job = "checkout-service";
    options.Instance = "checkout-service-01";
    options.SystemIdentifier = "checkout-service";
});
builder.AddPrometheusPushGateway("External", options =>
{
    options.ServerUrl = new Uri("https://pushgateway-external.example.com");
    options.Job = "checkout-service";
    options.Instance = "checkout-service-01";
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddPrometheusPushGateway(options =>
{
    options.ServerUrl = new Uri("https://pushgateway.example.com"); // Required
    options.Job = "checkout-service"; // Required
    options.Instance = "checkout-service-01"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
});
```

### appsettings.json-based

```csharp
builder.AddPrometheusPushGateway(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Prometheus": {
      "PushGateway": {
        "Default": {
          "ServerUrl": "https://pushgateway.example.com",
          "Job": "checkout-service",
          "Instance": "checkout-service-01",
          "SystemIdentifier": "checkout-service"
        }
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddPrometheusPushGateway("External")` reads `HealthPublishers:Prometheus:PushGateway:External`.

## Published Metrics

| Metric                                       | Type  | Labels                                              | Description                                          |
| --------------------------------------------- | ----- | ---------------------------------------------------- | ------------------------------------------------------ |
| `healthcheck_report_status`                   | gauge | `system_identifier`, `machine_name`                   | Overall health report status (`0`/`1`/`2`)              |
| `healthcheck_report_duration_seconds`         | gauge | `system_identifier`, `machine_name`                   | Total duration of the health report execution          |
| `healthcheck_last_publish_timestamp_seconds`  | gauge | `system_identifier`, `machine_name`                   | Unix timestamp of the last publish attempt              |
| `healthcheck_status`                          | gauge | `check`, `description`, `system_identifier`, `machine_name` | Status of an individual health check entry (`0`/`1`/`2`) |
| `healthcheck_duration_seconds`                | gauge | `check`, `description`, `system_identifier`, `machine_name` | Duration of an individual health check entry            |

The `job` and `instance` labels are not part of the exposition body - the Pushgateway derives them from the request path (`options.Job` and `options.Instance`) and applies them automatically.

## Requirements

- .NET 8.0 or higher
- A running Prometheus Pushgateway instance

## Related Packages

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
