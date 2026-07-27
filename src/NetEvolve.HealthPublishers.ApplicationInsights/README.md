# NetEvolve.HealthPublishers.ApplicationInsights

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.ApplicationInsights.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.ApplicationInsights/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.ApplicationInsights.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.ApplicationInsights/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to [Azure Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview) as availability telemetry, using the `Microsoft.ApplicationInsights` SDK.

## Features

- Publishes a single `AvailabilityTelemetry` event per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Maps the overall `HealthStatus` to `Success` (`Healthy` → `true`, anything else → `false`)
- Tags every event with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Application Insights resources side by side

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.ApplicationInsights
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.ApplicationInsights
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.ApplicationInsights" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.ApplicationInsights;

var builder = services.AddHealthChecks();

builder.AddApplicationInsightsPublisher(options =>
{
    options.ConnectionString = "InstrumentationKey=...;IngestionEndpoint=...";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddApplicationInsightsPublisher(options =>
{
    options.ConnectionString = "InstrumentationKey=...;IngestionEndpoint=...";
    options.SystemIdentifier = "checkout-service"; // Required, tags events alongside the machine name
});
```

### Advanced Example

Register multiple named Application Insights targets, each pushing the same health reports to a different resource:

```csharp
var builder = services.AddHealthChecks();

builder.AddApplicationInsightsPublisher("Internal", options =>
{
    options.ConnectionString = "InstrumentationKey=<internal-key>;IngestionEndpoint=...";
    options.SystemIdentifier = "checkout-service";
});
builder.AddApplicationInsightsPublisher("External", options =>
{
    options.ConnectionString = "InstrumentationKey=<external-key>;IngestionEndpoint=...";
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddApplicationInsightsPublisher(options =>
{
    options.ConnectionString = "InstrumentationKey=...;IngestionEndpoint=..."; // Required
    options.SystemIdentifier = "checkout-service"; // Required
});
```

### appsettings.json-based

```csharp
builder.AddApplicationInsightsPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "ApplicationInsights": {
      "Default": {
        "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=...",
        "SystemIdentifier": "checkout-service"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddApplicationInsightsPublisher("Internal")` reads `HealthPublishers:ApplicationInsights:Internal`.

## Requirements

- .NET 8.0 or higher
- An Azure Application Insights resource and its connection string

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
