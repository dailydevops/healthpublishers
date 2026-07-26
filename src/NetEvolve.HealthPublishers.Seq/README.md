# NetEvolve.HealthPublishers.Seq

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Seq.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Seq/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Seq.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Seq/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to a [Seq](https://datalust.co/seq) server, using Seq's [CLEF ingestion endpoint](https://datalust.co/docs/posting-raw-events) (`/ingest/clef`) directly over HTTP.

## Features

- Pushes a single structured CLEF event per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Maps `HealthStatus` to a Seq level (`Healthy` → `Information`, `Degraded` → `Warning`, `Unhealthy` → `Error`)
- Tags every event with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Seq targets side by side
- No `Seq.Api` dependency — just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Seq
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Seq
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Seq" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Seq;

var builder = services.AddHealthChecks();

builder.AddSeqPublisher(options =>
{
    options.ServerUrl = new Uri("https://seq.example.com");
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddSeqPublisher(options =>
{
    options.ServerUrl = new Uri("https://seq.example.com");
    options.SystemIdentifier = "checkout-service"; // Required, tags events alongside the machine name
    options.ApiKey = "<api-key>"; // Optional, sent as the X-Seq-ApiKey header
});
```

### Advanced Example

Register multiple named Seq targets, each pushing the same health reports to a different server:

```csharp
var builder = services.AddHealthChecks();

builder.AddSeqPublisher("Internal", options =>
{
    options.ServerUrl = new Uri("https://seq-internal.example.com");
    options.SystemIdentifier = "checkout-service";
});
builder.AddSeqPublisher("External", options =>
{
    options.ServerUrl = new Uri("https://seq-external.example.com");
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddSeqPublisher(options =>
{
    options.ServerUrl = new Uri("https://seq.example.com");
    options.SystemIdentifier = "checkout-service"; // Required
    options.ApiKey = "<api-key>"; // Optional
});
```

### appsettings.json-based

```csharp
builder.AddSeqPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Seq": {
      "Default": {
        "ServerUrl": "https://seq.example.com",
        "SystemIdentifier": "checkout-service",
        "ApiKey": "<api-key>"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddSeqPublisher("Internal")` reads `HealthPublishers:Seq:Internal`.

## Requirements

- .NET 8.0 or higher
- A reachable Seq server (v2020.x or later; any version exposing `/ingest/clef`)

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
