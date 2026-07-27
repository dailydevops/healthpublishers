# NetEvolve.HealthPublishers.AWS.CloudWatch

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.AWS.CloudWatch.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.AWS.CloudWatch/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.AWS.CloudWatch.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.AWS.CloudWatch/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that publishes `HealthReport` results as metrics to [Amazon CloudWatch](https://aws.amazon.com/cloudwatch/), using the official [`AWSSDK.CloudWatch`](https://www.nuget.org/packages/AWSSDK.CloudWatch/) client.

## Features

- Publishes an overall status and duration metric per report, plus a status and duration metric per individual health check
- Tags every metric with the machine name and a required, free-form `SystemIdentifier` to tell instances apart, and each per-check metric additionally with the check name
- Supports explicit AWS credentials or the default AWS credential resolution chain
- Supports a custom service endpoint, useful for VPC endpoints or CloudWatch-compatible services such as LocalStack
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthPublishers.*` conventions
- Named registrations to publish to multiple CloudWatch targets side by side
- Uses the official `AWSSDK.CloudWatch` client

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.AWS.CloudWatch
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.AWS.CloudWatch
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.AWS.CloudWatch" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.AWS.CloudWatch;

var builder = services.AddHealthChecks();

builder.AddAWSCloudWatchPublisher(options =>
{
    options.Region = "eu-central-1";
    options.Namespace = "HealthChecks";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddAWSCloudWatchPublisher(options =>
{
    options.Region = "eu-central-1"; // Required, AWS region system name
    options.Namespace = "HealthChecks"; // Required, CloudWatch metric namespace
    options.SystemIdentifier = "checkout-service"; // Required, tags metrics alongside the machine name
});
```

### Advanced Example

Register multiple named CloudWatch targets, each publishing the same health reports into a different account/region using explicit credentials:

```csharp
var builder = services.AddHealthChecks();

builder.AddAWSCloudWatchPublisher("Production", options =>
{
    options.Region = "eu-central-1";
    options.Namespace = "Prod/HealthChecks";
    options.AccessKeyId = "<prod-access-key-id>";
    options.SecretAccessKey = "<prod-secret-access-key>";
    options.SystemIdentifier = "checkout-service";
});
builder.AddAWSCloudWatchPublisher("Staging", options =>
{
    options.Region = "eu-west-1";
    options.Namespace = "Staging/HealthChecks";
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddAWSCloudWatchPublisher(options =>
{
    options.Region = "eu-central-1"; // Required
    options.Namespace = "HealthChecks"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.AccessKeyId = "<access-key-id>"; // Optional, must be set together with SecretAccessKey
    options.SecretAccessKey = "<secret-access-key>"; // Optional, must be set together with AccessKeyId
    options.ServiceUrl = new Uri("https://localhost:4566"); // Optional, e.g. for LocalStack or a VPC endpoint
});
```

### appsettings.json-based

```csharp
builder.AddAWSCloudWatchPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "AWS": {
      "CloudWatch": {
        "Default": {
          "Region": "eu-central-1",
          "Namespace": "HealthChecks",
          "SystemIdentifier": "checkout-service"
        }
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddAWSCloudWatchPublisher("Production")` reads `HealthPublishers:AWS:CloudWatch:Production`.

## Requirements

- .NET 8.0 or higher
- An AWS account with access to CloudWatch, or a CloudWatch-compatible service reachable via `ServiceUrl`

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
