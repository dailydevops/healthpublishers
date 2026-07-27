# NetEvolve.HealthPublishers.Elasticsearch

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.HealthPublishers.Elasticsearch.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Elasticsearch/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Elasticsearch.svg)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Elasticsearch/)
[![License](https://img.shields.io/github/license/dailydevops/healthpublishers.svg)](https://github.com/dailydevops/healthpublishers/blob/main/LICENSE)

An `IHealthCheckPublisher` implementation that indexes `HealthReport` results as documents into an [Elasticsearch](https://www.elastic.co/elasticsearch) cluster, using the official [`Elastic.Clients.Elasticsearch`](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/) client.

## Features

- Indexes a single document per publish, summarizing the overall report status, elapsed time, and a per-check breakdown
- Authenticates using an API key or basic authentication (username/password)
- Tags every document with the machine name and a required, free-form `SystemIdentifier` to tell instances apart
- Configuration- or builder-based setup, consistent with the `NetEvolve.HealthPublishers.*` conventions
- Named registrations to publish to multiple Elasticsearch targets side by side
- Uses the official `Elastic.Clients.Elasticsearch` client

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.HealthPublishers.Elasticsearch
```

### .NET CLI

```bash
dotnet add package NetEvolve.HealthPublishers.Elasticsearch
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.HealthPublishers.Elasticsearch" Version="x.x.x" />
```

## Quick Start

```csharp
using NetEvolve.HealthPublishers.Elasticsearch;

var builder = services.AddHealthChecks();

builder.AddElasticsearchPublisher(options =>
{
    options.ServerUri = new Uri("https://elasticsearch.example.com:9200");
    options.IndexName = "health-checks";
    options.ApiKey = "<api-key>";
    options.SystemIdentifier = "checkout-service";
});
```

## Usage

### Basic Example

Register under the default name (`"Default"`), configured via code:

```csharp
var builder = services.AddHealthChecks();

builder.AddElasticsearchPublisher(options =>
{
    options.ServerUri = new Uri("https://elasticsearch.example.com:9200"); // Required, base address of the cluster
    options.IndexName = "health-checks"; // Required, index the document is written to
    options.ApiKey = "<api-key>"; // Optional, sent as an API key
    options.SystemIdentifier = "checkout-service"; // Required, tags documents alongside the machine name
});
```

### Advanced Example

Register multiple named Elasticsearch targets, each indexing the same health reports into a different cluster or index:

```csharp
var builder = services.AddHealthChecks();

builder.AddElasticsearchPublisher("Production", options =>
{
    options.ServerUri = new Uri("https://elasticsearch-prod.example.com:9200");
    options.IndexName = "prod-health-checks";
    options.ApiKey = "<prod-api-key>";
    options.SystemIdentifier = "checkout-service";
});
builder.AddElasticsearchPublisher("Staging", options =>
{
    options.ServerUri = new Uri("https://elasticsearch-staging.example.com:9200");
    options.IndexName = "staging-health-checks";
    options.Username = "elastic";
    options.Password = "<password>";
    options.SystemIdentifier = "checkout-service";
});
```

## Configuration

### Code-based

```csharp
builder.AddElasticsearchPublisher(options =>
{
    options.ServerUri = new Uri("https://elasticsearch.example.com:9200"); // Required
    options.IndexName = "health-checks"; // Required
    options.SystemIdentifier = "checkout-service"; // Required
    options.ApiKey = "<api-key>"; // Optional, mutually exclusive with Username/Password
    options.Username = "elastic"; // Optional, must be set together with Password
    options.Password = "<password>"; // Optional, must be set together with Username
});
```

### appsettings.json-based

```csharp
builder.AddElasticsearchPublisher(); // reads the "Default" section below
```

```json
{
  "HealthPublishers": {
    "Elasticsearch": {
      "Default": {
        "ServerUri": "https://elasticsearch.example.com:9200",
        "IndexName": "health-checks",
        "ApiKey": "<api-key>",
        "SystemIdentifier": "checkout-service"
      }
    }
  }
}
```

When using an explicit name, the section key must match: `builder.AddElasticsearchPublisher("Production")` reads `HealthPublishers:Elasticsearch:Production`.

## Requirements

- .NET 8.0 or higher
- An Elasticsearch cluster reachable over HTTP or HTTPS

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
