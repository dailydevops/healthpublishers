# NetEvolve.HealthPublishers.OpenTelemetry

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.OpenTelemetry.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.OpenTelemetry/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.OpenTelemetry.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.OpenTelemetry/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that records `HealthReport` results as .NET metrics, using [`System.Diagnostics.Metrics`](https://learn.microsoft.com/dotnet/core/diagnostics/metrics) directly. No `OpenTelemetry`/`OpenTelemetry.Api` package dependency required — any OpenTelemetry-compatible `MeterListener` or exporter (e.g. `OpenTelemetry.Extensions.Hosting`'s `AddMeter("NetEvolve.HealthPublishers.OpenTelemetry")`) can consume the emitted metrics.

## Features

- Records a `healthchecks.report.duration` histogram (milliseconds) per publish, tagged with the overall report status
- Records a `healthchecks.entry.duration` histogram (milliseconds) per health check entry, tagged with its name and status
- Every tag key is namespaced with a `healthchecks.` prefix for consistency: `healthchecks.status`, `healthchecks.publisher.name`, `healthchecks.system.identifier`, `healthchecks.machine.name`, `healthchecks.timestamp`, `healthchecks.entry.name`
- Tags every metric with the machine name, a `TimeProvider`-sourced timestamp, and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to differentiate multiple publisher setups via the `healthchecks.publisher.name` tag
- No external dependencies — built entirely on the in-box `System.Diagnostics.DiagnosticSource` metrics API

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.OpenTelemetry
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.OpenTelemetry
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.OpenTelemetry" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.OpenTelemetry;

var builder = services.AddHealthChecks();

builder.AddOpenTelemetryPublisher(options =>
{
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddOpenTelemetryPublisher(options =>
{
    options.SystemIdentifier = "checkout-service"; // Required, tags metrics alongside the machine name
});
```

### Advanced Example

Register multiple named publishers; every recorded metric carries a `healthchecks.publisher.name` tag matching the name used to register it:

```csharp
var builder = services.AddHealthChecks();

builder.AddOpenTelemetryPublisher("Internal", options => options.SystemIdentifier = "checkout-service");
builder.AddOpenTelemetryPublisher("External", options => options.SystemIdentifier = "checkout-service");
```

### Consuming the metrics

Since metrics are emitted via a plain `Meter` named `NetEvolve.HealthPublishers.OpenTelemetry`, any listener can subscribe, e.g. with the OpenTelemetry SDK:

```csharp
services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter("NetEvolve.HealthPublishers.OpenTelemetry"));
```

## Configuration

### Code-based

```csharp
builder.AddOpenTelemetryPublisher(options =>
{
    options.SystemIdentifier = "checkout-service"; // Required
});
```

### appsettings.json-based

```csharp
builder.AddOpenTelemetryPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "OpenTelemetry": {
      "Default": {
        "SystemIdentifier": "checkout-service"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddOpenTelemetryPublisher("Internal")` reads `HealthPublishers:OpenTelemetry:Internal`.

## Requirements

- .NET 8.0 or higher

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
