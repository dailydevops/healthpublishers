# NetEvolve.HealthPublishers.Email

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Email.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Email/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Email.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Email/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that sends `HealthReport` results as an email, using [MailKit](https://github.com/jstedfast/MailKit) to connect directly to an SMTP server.

## Features

- Sends a single plain-text email per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Subject line includes the overall `HealthStatus` and the configured `SystemIdentifier`, for quick triage in an inbox
- Tags every email with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Supports connecting without authentication, or with username/password credentials, over plain, `StartTls`, or implicit TLS/SSL connections
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthPublishers.*` conventions
- Named registrations to send to multiple SMTP servers or recipient lists side by side

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Email
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Email
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Email" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Email;

var builder = services.AddHealthChecks();

builder.AddEmailPublisher(options =>
{
    options.Host = "smtp.example.com";
    options.Port = 587;
    options.From = "health-checks@example.com";
    options.To = ["ops-team@example.com"];
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddEmailPublisher(options =>
{
    options.Host = "smtp.example.com"; // Required
    options.Port = 587; // Required
    options.From = "health-checks@example.com"; // Required
    options.To = ["ops-team@example.com"]; // Required, at least one address
    options.SystemIdentifier = "checkout-service"; // Required, tags the email alongside the machine name
    options.Username = "smtp-user"; // Optional, requires Password to be set as well
    options.Password = "<password>"; // Optional, requires Username to be set as well
});
```

### Advanced Example

Register multiple named Email targets, each sending the same health reports to a different SMTP server or recipient list:

```csharp
var builder = services.AddHealthChecks();

builder.AddEmailPublisher("Internal", options =>
{
    options.Host = "smtp.example.com";
    options.Port = 587;
    options.From = "health-checks@example.com";
    options.To = ["internal-ops@example.com"];
    options.SystemIdentifier = "checkout-service";
});
builder.AddEmailPublisher("External", options =>
{
    options.Host = "smtp-external.example.com";
    options.Port = 587;
    options.From = "health-checks@example.com";
    options.To = ["external-ops@example.com"];
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddEmailPublisher(options =>
{
    options.Host = "smtp.example.com"; // Required
    options.Port = 587; // Required
    options.SecureSocketOptions = SecureSocketOptions.StartTls; // Optional, defaults to SecureSocketOptions.Auto
    options.From = "health-checks@example.com"; // Required
    options.To = ["ops-team@example.com"]; // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.Username = "smtp-user"; // Optional
    options.Password = "<password>"; // Optional
});
```

### appsettings.json-based

```csharp
builder.AddEmailPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Email": {
      "Default": {
        "Host": "smtp.example.com",
        "Port": 587,
        "SecureSocketOptions": "StartTls",
        "From": "health-checks@example.com",
        "To": ["ops-team@example.com"],
        "SystemIdentifier": "checkout-service",
        "Username": "smtp-user",
        "Password": "<password>"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddEmailPublisher("Internal")` reads `HealthPublishers:Email:Internal`.

## Requirements

- .NET 8.0 or higher
- A reachable SMTP server

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
