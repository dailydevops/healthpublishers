# NetEvolve.HealthPublishers.MicrosoftTeams

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.MicrosoftTeams.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.MicrosoftTeams/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.MicrosoftTeams.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.MicrosoftTeams/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that pushes `HealthReport` results to a Microsoft Teams channel, using an [incoming webhook](https://learn.microsoft.com/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook) or a Power Automate [workflow connector](https://support.microsoft.com/office/create-incoming-webhooks-with-workflows-for-microsoft-teams-8ae491c7-0394-4861-ba59-055e33f75498), posting an [Adaptive Card](https://adaptivecards.io/) directly over HTTP.

## Features

- Posts a single Adaptive Card per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Maps `HealthStatus` to an Adaptive Card text color (`Healthy` → `good`, `Degraded` → `warning`, `Unhealthy` → `attention`)
- Tags every card with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthChecks.*` conventions
- Named registrations to publish to multiple Microsoft Teams channels side by side
- No Microsoft Teams/Bot Framework client dependency - just `HttpClient` via `IHttpClientFactory`
- Debounces notifications instead of posting on every publish: a worsening status is posted immediately, while an improving status must stay sustained for a configurable delay before a "recovery" card is posted

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.MicrosoftTeams
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.MicrosoftTeams
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.MicrosoftTeams" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.MicrosoftTeams;

var builder = services.AddHealthChecks();

builder.AddMicrosoftTeamsPublisher(options =>
{
    options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/...");
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddMicrosoftTeamsPublisher(options =>
{
    options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/..."); // Required
    options.SystemIdentifier = "checkout-service"; // Required, tags the card alongside the machine name
});
```

### Notification Behavior

The publisher keeps track of the `HealthStatus` it last actually posted about (per registered instance) and does not
post a card for every `PublishAsync` call:

- **Same status as last notified**: no card is posted.
- **Worsening status** (e.g. `Healthy` → `Degraded`, `Degraded` → `Unhealthy`, or `Healthy` → `Unhealthy`): a card
  is posted immediately, regardless of `RecoveryConfirmationDelay`.
- **Improving status** (e.g. `Degraded` → `Healthy`, `Unhealthy` → `Degraded`, or `Unhealthy` → `Healthy`): the
  improvement must be sustained for at least `RecoveryConfirmationDelay` before a "recovery" card is posted. If the
  status regresses back to the last-notified status before the delay elapses, no card is posted and the delay resets;
  a later improvement starts a new delay from scratch. If the status regresses to something worse than the
  last-notified status instead, that is treated as a new worsening event and is posted immediately.

### Advanced Example

Register multiple named Microsoft Teams targets, each pushing the same health reports to a different channel:

```csharp
var builder = services.AddHealthChecks();

builder.AddMicrosoftTeamsPublisher("Ops", options =>
{
    options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/ops...");
    options.SystemIdentifier = "checkout-service";
});
builder.AddMicrosoftTeamsPublisher("OnCall", options =>
{
    options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/oncall...");
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddMicrosoftTeamsPublisher(options =>
{
    options.WebhookUrl = new Uri("https://example.webhook.office.com/webhookb2/..."); // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(10); // Optional, defaults to 5 minutes, minimum 5 minutes
});
```

### appsettings.json-based

```csharp
builder.AddMicrosoftTeamsPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "MicrosoftTeams": {
      "Default": {
        "WebhookUrl": "https://example.webhook.office.com/webhookb2/...",
        "SystemIdentifier": "checkout-service",
        "RecoveryConfirmationDelay": "00:10:00"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddMicrosoftTeamsPublisher("Ops")` reads `HealthPublishers:MicrosoftTeams:Ops`.

## Requirements

- .NET 8.0 or higher
- A Microsoft Teams incoming webhook, or a Power Automate workflow configured to receive Adaptive Cards

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
