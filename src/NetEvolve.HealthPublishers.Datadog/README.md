# NetEvolve.HealthPublishers.Datadog

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Datadog.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Datadog/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Datadog.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Datadog/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to [Datadog](https://www.datadoghq.com/), using the [Events API](https://docs.datadoghq.com/api/latest/events/#post-an-event) (`/api/v1/events`) directly over HTTP.

## Features

- Pushes a single Datadog event per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Maps `HealthStatus` to a Datadog alert type (`Healthy` → `success`, `Degraded` → `warning`, `Unhealthy` → `error`)
- Tags every event with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Datadog targets (e.g. different sites or organizations) side by side
- Supports regional Datadog sites (e.g. `https://api.datadoghq.eu`) via `ApiUrl`
- No `Datadog.Api` client dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Datadog
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Datadog
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Datadog" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Datadog;

var builder = services.AddHealthChecks();

builder.AddDatadogPublisher(options =>
{
    options.ApiKey = "<api-key>";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddDatadogPublisher(options =>
{
    options.ApiKey = "<api-key>"; // Required, sent as the DD-API-KEY header
    options.SystemIdentifier = "checkout-service"; // Required, tags events alongside the machine name
    options.ApiUrl = new Uri("https://api.datadoghq.eu"); // Optional, defaults to https://api.datadoghq.com
});
```

### Advanced Example

Register multiple named Datadog targets, each pushing the same health reports to a different organization or site:

```csharp
var builder = services.AddHealthChecks();

builder.AddDatadogPublisher("US", options =>
{
    options.ApiKey = "<us-api-key>";
    options.SystemIdentifier = "checkout-service";
});
builder.AddDatadogPublisher("EU", options =>
{
    options.ApiKey = "<eu-api-key>";
    options.ApiUrl = new Uri("https://api.datadoghq.eu");
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddDatadogPublisher(options =>
{
    options.ApiKey = "<api-key>"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.ApiUrl = new Uri("https://api.datadoghq.eu"); // Optional
});
```

### appsettings.json-based

```csharp
builder.AddDatadogPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Datadog": {
      "Default": {
        "ApiKey": "<api-key>",
        "SystemIdentifier": "checkout-service",
        "ApiUrl": "https://api.datadoghq.eu"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddDatadogPublisher("EU")` reads `HealthPublishers:Datadog:EU`.

## Requirements

- .NET 8.0 or higher
- A Datadog account and API key with access to the Events API

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
