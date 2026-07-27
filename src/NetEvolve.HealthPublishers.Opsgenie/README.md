# NetEvolve.HealthPublishers.Opsgenie

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Opsgenie.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Opsgenie/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Opsgenie.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Opsgenie/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that creates and closes [Opsgenie](https://www.atlassian.com/software/opsgenie) alerts based on `HealthReport` results, using the [Alert API](https://docs.opsgenie.com/docs/alert-api) (`/v2/alerts`) directly over HTTP.

## Features

- Maps `HealthStatus` to alert lifecycle: `Degraded`/`Unhealthy` create or update an alert, `Healthy` closes it
- Maps `HealthStatus` to an Opsgenie alert priority (`Degraded` → `P3`, `Unhealthy` → `P1`)
- Derives a stable alert alias from the required `SystemIdentifier`, so repeated unhealthy reports update the same alert instead of creating duplicates, and healthy reports close that same alert
- Tags and details every alert with the machine name and the `SystemIdentifier`, useful to distinguish reports coming from the same machine across multiple applications or instances
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Opsgenie teams/integrations side by side
- Supports the EU instance (`https://api.eu.opsgenie.com`) via `ApiUrl`
- No Opsgenie SDK dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Opsgenie
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Opsgenie
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Opsgenie" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Opsgenie;

var builder = services.AddHealthChecks();

builder.AddOpsgeniePublisher(options =>
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

builder.AddOpsgeniePublisher(options =>
{
    options.ApiKey = "<api-key>"; // Required, sent as the Authorization: GenieKey <api-key> header
    options.SystemIdentifier = "checkout-service"; // Required, derives the alert alias and tags every alert
    options.ApiUrl = new Uri("https://api.eu.opsgenie.com"); // Optional, defaults to https://api.opsgenie.com
});
```

### Advanced Example

Register multiple named Opsgenie targets, each pushing the same health reports to a different team or integration:

```csharp
var builder = services.AddHealthChecks();

builder.AddOpsgeniePublisher("Platform", options =>
{
    options.ApiKey = "<platform-api-key>";
    options.SystemIdentifier = "checkout-service";
});
builder.AddOpsgeniePublisher("OnCall", options =>
{
    options.ApiKey = "<oncall-api-key>";
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddOpsgeniePublisher(options =>
{
    options.ApiKey = "<api-key>"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.ApiUrl = new Uri("https://api.eu.opsgenie.com"); // Optional
});
```

### appsettings.json-based

```csharp
builder.AddOpsgeniePublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Opsgenie": {
      "Default": {
        "ApiKey": "<api-key>",
        "SystemIdentifier": "checkout-service",
        "ApiUrl": "https://api.eu.opsgenie.com"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddOpsgeniePublisher("OnCall")` reads `HealthPublishers:Opsgenie:OnCall`.

## Requirements

- .NET 8.0 or higher
- An Opsgenie account and API key (integration key) with access to the Alert API

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
