# HealthPublishers

![GitHub](https://img.shields.io/github/license/dailydevops/healthpublishers?logo=github)
![GitHub top language](https://img.shields.io/github/languages/top/dailydevops/healthpublishers?logo=github)
![GitHub repo size](https://img.shields.io/github/repo-size/dailydevops/healthpublishers?logo=github)
[![GitHub Pipeline CI](https://github.com/dailydevops/healthpublishers/actions/workflows/cicd.yml/badge.svg?branch=main&event=push)](https://github.com/dailydevops/healthpublishers/actions/workflows/cicd.yml)

## What is this repository about?

This is a mono repository for several NuGet packages implementing `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheckPublisher`. While [NetEvolve.HealthChecks](https://github.com/dailydevops/healthchecks) checks whether a service is healthy, this repository publishes the resulting health report to external systems - observability, chatops, incident management, and messaging sinks - so results don't just sit behind a `/health` endpoint.

This repository has a sister project, [NetEvolve.HealthChecks](https://github.com/dailydevops/healthchecks), which provides `IHealthCheck` implementations that produce the reports this repository publishes. The two projects are designed to work together, but they can also be used independently.

### At a glance

- Publishes `HealthReport` results to external sinks (logging platforms, chat, messaging, incident management).
- Configuration-first, fully configurable either via code or configuration.
- Sensible defaults with low allocations while keeping configurability a priority.
- Sister project to [NetEvolve.HealthChecks](https://github.com/dailydevops/healthchecks) - checks produce the report, publishers deliver it.

### Why choose NetEvolve.HealthChecks over [AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)?

- **Actively maintained**: We are committed to keeping this project up-to-date with the latest .NET versions and best practices.
- **Configurable everywhere**: Tune health checks from `Program.cs`, `appsettings.json`, or any configuration provider.
- **Client choice**: Alternative implementations let you stay aligned with the client libraries you already use.
- **Forward-looking defaults**: Practical performance optimizations without sacrificing clarity or configurability.

### Quickstart (ASP.NET Core minimal API)

1. Install a package, for example `NetEvolve.HealthPublishers.Seq`.
2. Register the publisher:

    ```csharp
    var builder = WebApplication.CreateBuilder(args);

    builder.Services
        .AddHealthChecks()
        .AddSeqPublisher(options =>
        {
            options.Uri = new Uri("https://seq.example.com");
            options.SystemIdentifier = "my-service";
        });

    var app = builder.Build();

    app.Run();
    ```

Use any other publisher package the same way - swap `AddSeqPublisher` with the corresponding extension.

In addition, we try to support the latest LTS and STS versions of .NET ([.NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)) as well as the latest preview version of .NET for at least 3 years, but we **can't guarantee** this. This depends on the support of related NuGet packages and the .NET platform itself. See the [Supported .NET Version](#supported-net-version) section for more details.

## NuGet packages

The following table lists all currently available NuGet packages. For more details about the packages, please visit the corresponding NuGet page.

<!-- packages:start -->
| Package Name | NuGet Link      |
|:-------------|:---------------:|
| [NetEvolve.HealthPublishers.AWS.CloudWatch](https://www.nuget.org/packages/NetEvolve.HealthPublishers.AWS.CloudWatch/) <br/><small>Contains an IHealthCheckPublisher implementation that publishes health report results as metrics to Amazon CloudWatch, using AWSSDK.CloudWatch.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.AWS.CloudWatch?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.AWS.CloudWatch/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Datadog](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Datadog/) <br/><small>Contains an IHealthCheckPublisher implementation that pushes health report results to Datadog as events.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Datadog?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Datadog/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Elasticsearch](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Elasticsearch/) <br/><small>Contains an IHealthCheckPublisher implementation that indexes health report results as documents into an Elasticsearch cluster, using Elastic.Clients.Elasticsearch.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Elasticsearch?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Elasticsearch/#readme-body-tab) |
| [NetEvolve.HealthPublishers.OpenTelemetry](https://www.nuget.org/packages/NetEvolve.HealthPublishers.OpenTelemetry/) <br/><small>Contains an IHealthCheckPublisher implementation that reports health report results as .NET metrics (System.Diagnostics.Metrics), consumable by any OpenTelemetry-compatible collector.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.OpenTelemetry?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.OpenTelemetry/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Opsgenie](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Opsgenie/) <br/><small>Contains an IHealthCheckPublisher implementation that creates and closes Opsgenie alerts based on health report results.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Opsgenie?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Opsgenie/#readme-body-tab) |
| [NetEvolve.HealthPublishers.PagerDuty](https://www.nuget.org/packages/NetEvolve.HealthPublishers.PagerDuty/) <br/><small>Contains an IHealthCheckPublisher implementation that triggers and resolves PagerDuty incidents based on health report results, using the Events API v2.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.PagerDuty?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.PagerDuty/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Prometheus.Metrics](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.Metrics/) <br/><small>Contains an IHealthCheckPublisher implementation that updates prometheus-net Gauge metrics in an in-process CollectorRegistry, reflecting the latest health report results, for scraping via prometheus-net's own ASP.NET Core middleware.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Prometheus.Metrics?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.Metrics/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Prometheus.PushGateway](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.PushGateway/) <br/><small>Contains an IHealthCheckPublisher implementation that pushes health report results to a Prometheus Pushgateway instance as metrics.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Prometheus.PushGateway?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Prometheus.PushGateway/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Seq](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Seq/) <br/><small>Contains an IHealthCheckPublisher implementation that pushes health report results to a Seq server.</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Seq?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Seq/#readme-body-tab) |
| [NetEvolve.HealthPublishers.Splunk](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Splunk/) <br/><small>Contains an IHealthCheckPublisher implementation that pushes health report results to Splunk via the HTTP Event Collector (HEC).</small> | [![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.HealthPublishers.Splunk?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/NetEvolve.HealthPublishers.Splunk/#readme-body-tab) |
<!-- packages:end -->

Additional packages are tracked as [GitHub issues](https://github.com/dailydevops/healthpublishers/issues) and will be added incrementally.

## Package naming explanation

The package names are based on the following naming schema - `NetEvolve.HealthPublishers.<ServiceName>`

- `NetEvolve` is the name of the organization that maintains this repository.
- `HealthPublishers` indicates that this package publishes health report results to an external system.
- `<ServiceName>` is the name of the target system the health report is published to, for example `Seq`.

## Supported .NET version

We try to support the LTS and STS versions of .NET ([.NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)), as well as the latest preview version of .NET. We will try to support each framework version for at least 3 years, but we can't guarantee it. This depends on the support of related NuGet packages and the .NET platform itself.

| .NET Version                     | Supported              |
| -------------------------------- | :--------------------- |
| **.NET Standard**                | :x: No                 |
| **.NET 7.0 or earlier versions** | :x: No                 |
| **.NET 8.0**                     | :white_check_mark: Yes |
| **.NET 9.0**                     | :white_check_mark: Yes |
| **.NET 10.0**                    | :white_check_mark: Yes |

Why did we choose this approach? Because we want to be able to take advantage of the latest language features of the .NET platform and the performance gains that come with them. We know that not all of our NuGet packages will gain performance from this, but this is our general strategy and nobody knows what the future will bring.

### Where can I find more information about the end-of-life (EOL) date for the relevant components?

To get more information about the end-of-life (EOL) date for the relevant components, please visit the website of the creators of the components or try the website [endoflife.date](https://endoflife.date/).

## Related projects

- [NetEvolve.HealthChecks](https://github.com/dailydevops/healthchecks) - the sister repository, providing the `IHealthCheck` implementations that produce the reports this repository publishes.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

> [!NOTE]
> **Made with ❤️ by the NetEvolve Team**
> Visit us at [https://www.daily-devops.net](https://www.daily-devops.net) for more information about our services and solutions.
