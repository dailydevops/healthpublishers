# NetEvolve.HealthPublishers.Slack

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Slack.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Slack/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Slack.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Slack/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to a [Slack](https://slack.com/) channel, using an [incoming webhook](https://api.slack.com/messaging/webhooks) directly over HTTP.

## Features

- Posts a single Slack message per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Maps `HealthStatus` to a Slack attachment color (`Healthy` → `good`, `Degraded` → `warning`, `Unhealthy` → `danger`)
- Tags every message with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Slack channels (e.g. different webhooks) side by side
- No Slack SDK dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Slack
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Slack
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Slack" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Slack;

var builder = services.AddHealthChecks();

builder.AddSlackPublisher(options =>
{
    options.WebhookUrl = new Uri("https://hooks.slack.com/services/T000/B000/XXX");
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddSlackPublisher(options =>
{
    options.WebhookUrl = new Uri("https://hooks.slack.com/services/T000/B000/XXX"); // Required
    options.SystemIdentifier = "checkout-service"; // Required, tags messages alongside the machine name
});
```

### Advanced Example

Register multiple named Slack targets, each pushing the same health reports to a different channel:

```csharp
var builder = services.AddHealthChecks();

builder.AddSlackPublisher("Ops", options =>
{
    options.WebhookUrl = new Uri("https://hooks.slack.com/services/T000/B000/OPS");
    options.SystemIdentifier = "checkout-service";
});
builder.AddSlackPublisher("Engineering", options =>
{
    options.WebhookUrl = new Uri("https://hooks.slack.com/services/T000/B000/ENG");
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddSlackPublisher(options =>
{
    options.WebhookUrl = new Uri("https://hooks.slack.com/services/T000/B000/XXX"); // Required
    options.SystemIdentifier = "checkout-service"; // Required
});
```

### appsettings.json-based

```csharp
builder.AddSlackPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Slack": {
      "Default": {
        "WebhookUrl": "https://hooks.slack.com/services/T000/B000/XXX",
        "SystemIdentifier": "checkout-service"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddSlackPublisher("Ops")` reads `HealthPublishers:Slack:Ops`.

## Requirements

- .NET 8.0 or higher
- A Slack workspace with an [incoming webhook](https://api.slack.com/messaging/webhooks) configured for the target channel

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
