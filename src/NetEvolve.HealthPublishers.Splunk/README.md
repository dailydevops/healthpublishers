# NetEvolve.HealthPublishers.Splunk

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Splunk.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Splunk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Splunk.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Splunk/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to [Splunk](https://www.splunk.com/), using the [HTTP Event Collector (HEC)](https://docs.splunk.com/Documentation/Splunk/latest/Data/UsetheHTTPEventCollector) (`/services/collector/event`) directly over HTTP.

## Features

- Pushes a single Splunk HEC event per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Authenticates using the HEC token, sent as the `Authorization: Splunk <token>` header
- Tags every event with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Optionally sets the Splunk `sourcetype`, `source`, and `index` for the published event
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthPublishers.*` conventions
- Named registrations to publish to multiple Splunk targets (e.g. different environments or indexes) side by side
- No Splunk SDK dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Splunk
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Splunk
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Splunk" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Splunk;

var builder = services.AddHealthChecks();

builder.AddSplunkPublisher(options =>
{
    options.ServerUrl = new Uri("https://splunk.example.com:8088");
    options.HecToken = "<hec-token>";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddSplunkPublisher(options =>
{
    options.ServerUrl = new Uri("https://splunk.example.com:8088"); // Required, base address of the HEC endpoint
    options.HecToken = "<hec-token>"; // Required, sent as the Authorization: Splunk <token> header
    options.SystemIdentifier = "checkout-service"; // Required, tags events alongside the machine name
    options.SourceType = "health-check"; // Optional
});
```

### Advanced Example

Register multiple named Splunk targets, each pushing the same health reports to a different environment or index:

```csharp
var builder = services.AddHealthChecks();

builder.AddSplunkPublisher("Production", options =>
{
    options.ServerUrl = new Uri("https://splunk-prod.example.com:8088");
    options.HecToken = "<prod-hec-token>";
    options.SystemIdentifier = "checkout-service";
    options.Index = "prod_health";
});
builder.AddSplunkPublisher("Staging", options =>
{
    options.ServerUrl = new Uri("https://splunk-staging.example.com:8088");
    options.HecToken = "<staging-hec-token>";
    options.SystemIdentifier = "checkout-service";
    options.Index = "staging_health";
});
```

## Configuration

### Code-based

```csharp
builder.AddSplunkPublisher(options =>
{
    options.ServerUrl = new Uri("https://splunk.example.com:8088"); // Required
    options.HecToken = "<hec-token>"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.SourceType = "health-check"; // Optional
    options.Source = "checkout-service"; // Optional
    options.Index = "health"; // Optional
});
```

### appsettings.json-based

```csharp
builder.AddSplunkPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Splunk": {
      "Default": {
        "ServerUrl": "https://splunk.example.com:8088",
        "HecToken": "<hec-token>",
        "SystemIdentifier": "checkout-service",
        "SourceType": "health-check",
        "Source": "checkout-service",
        "Index": "health"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddSplunkPublisher("Production")` reads `HealthPublishers:Splunk:Production`.

## Requirements

- .NET 8.0 or higher
- A Splunk instance with the HTTP Event Collector (HEC) enabled and a valid HEC token

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
