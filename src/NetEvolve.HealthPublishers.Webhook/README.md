# NetEvolve.HealthPublishers.Webhook

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Webhook.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Webhook/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Webhook.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Webhook/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that posts `HealthReport` results as JSON to an arbitrary, user-configured HTTP endpoint - no third-party SaaS format, just a generic payload delivered over `HttpClient`.

## Features

- Posts a single JSON document per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Tags the payload with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Supports arbitrary custom HTTP headers (e.g. for authentication against the receiving endpoint)
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple webhook targets side by side
- No third-party dependency - just `HttpClient` via `IHttpClientFactory`

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Webhook
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Webhook
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Webhook" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Webhook;

var builder = services.AddHealthChecks();

builder.AddWebhookPublisher(options =>
{
    options.Uri = new Uri("https://example.com/webhooks/health");
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddWebhookPublisher(options =>
{
    options.Uri = new Uri("https://example.com/webhooks/health"); // Required, the target endpoint
    options.SystemIdentifier = "checkout-service"; // Required, tags the payload alongside the machine name
    options.Headers["Authorization"] = "Bearer <token>"; // Optional, sent as-is with every request
});
```

### Advanced Example

Register multiple named webhook targets, each pushing the same health reports to a different endpoint:

```csharp
var builder = services.AddHealthChecks();

builder.AddWebhookPublisher("Internal", options =>
{
    options.Uri = new Uri("https://internal.example.com/webhooks/health");
    options.SystemIdentifier = "checkout-service";
});
builder.AddWebhookPublisher("External", options =>
{
    options.Uri = new Uri("https://external.example.com/webhooks/health");
    options.SystemIdentifier = "checkout-service";
    options.Headers["X-Api-Key"] = "<api-key>";
});
```

## Configuration

### Code-based

```csharp
builder.AddWebhookPublisher(options =>
{
    options.Uri = new Uri("https://example.com/webhooks/health"); // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.Headers["Authorization"] = "Bearer <token>"; // Optional
});
```

### appsettings.json-based

```csharp
builder.AddWebhookPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Webhook": {
      "Default": {
        "Uri": "https://example.com/webhooks/health",
        "SystemIdentifier": "checkout-service",
        "Headers": {
          "Authorization": "Bearer <token>"
        }
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddWebhookPublisher("EU")` reads `HealthPublishers:Webhook:EU`.

## Payload

Every publish sends a single JSON document via `POST`:

```json
{
  "systemIdentifier": "checkout-service",
  "machineName": "web-01",
  "status": "Healthy",
  "totalDurationMs": 12.5,
  "timestamp": "2026-01-02T03:04:05.0000000Z",
  "entries": [
    {
      "name": "database",
      "status": "Healthy",
      "durationMs": 3.1,
      "description": null,
      "tags": []
    }
  ]
}
```

A non-`2xx` response causes `PublishAsync` to throw, consistent with the other `NetEvolve.HealthPublishers.*` packages.

## Requirements

- .NET 8.0 or higher
- An HTTP endpoint capable of receiving a `POST` request with a JSON body

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
