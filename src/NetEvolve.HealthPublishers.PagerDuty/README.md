# NetEvolve.HealthPublishers.PagerDuty

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.PagerDuty.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.PagerDuty/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.PagerDuty.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.PagerDuty/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that triggers and resolves [PagerDuty](https://www.pagerduty.com/) incidents based on `HealthReport` results, using the [Events API v2](https://developer.pagerduty.com/api-reference/368ae3d938c9e-send-an-event-to-pager-duty) (`/v2/enqueue`) directly over HTTP.

## Features

- Maps `HealthStatus` to a PagerDuty event action: `Healthy` → `resolve`, `Degraded`/`Unhealthy` → `trigger`
- Maps `Degraded`/`Unhealthy` to a PagerDuty severity (`warning`/`critical`) on triggered incidents
- Derives a stable `dedup_key` from the required `SystemIdentifier`, so a triggered incident is automatically resolved once the same system reports healthy again
- Tags triggered incidents with the machine name as the event `source`, and a per-check breakdown as `custom_details`
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple PagerDuty services side by side
- No PagerDuty client dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.PagerDuty
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.PagerDuty
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.PagerDuty" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.PagerDuty;

var builder = services.AddHealthChecks();

builder.AddPagerDutyPublisher(options =>
{
    options.RoutingKey = "<integration-key>";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddPagerDutyPublisher(options =>
{
    options.RoutingKey = "<integration-key>"; // Required, PagerDuty Events API v2 integration/routing key
    options.SystemIdentifier = "checkout-service"; // Required, derives the dedup_key and tags the event source
});
```

### Advanced Example

Register multiple named PagerDuty targets, each pushing the same health reports to a different service:

```csharp
var builder = services.AddHealthChecks();

builder.AddPagerDutyPublisher("Database", options =>
{
    options.RoutingKey = "<database-integration-key>";
    options.SystemIdentifier = "checkout-service-database";
});
builder.AddPagerDutyPublisher("Cache", options =>
{
    options.RoutingKey = "<cache-integration-key>";
    options.SystemIdentifier = "checkout-service-cache";
});
```

## Configuration

### Code-based

```csharp
builder.AddPagerDutyPublisher(options =>
{
    options.RoutingKey = "<integration-key>"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
});
```

### appsettings.json-based

```csharp
builder.AddPagerDutyPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "PagerDuty": {
      "Default": {
        "RoutingKey": "<integration-key>",
        "SystemIdentifier": "checkout-service"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddPagerDutyPublisher("Database")` reads `HealthPublishers:PagerDuty:Database`.

## Requirements

- .NET 8.0 or higher
- A PagerDuty account and an Events API v2 integration/routing key

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
