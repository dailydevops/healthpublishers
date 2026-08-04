# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

A mono repository of NuGet packages that each implement `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheckPublisher`. Each package publishes the result of a `HealthReport` to one external system (Seq, Splunk, Datadog, Opsgenie, PagerDuty, Elasticsearch, Prometheus, CloudWatch, Application Insights, OpenTelemetry). Sister project [NetEvolve.HealthChecks](https://github.com/dailydevops/healthchecks) produces the checks; this repo only ships the publishing side.

Package naming: `NetEvolve.HealthPublishers.<ServiceName>`, one project under `src/` per service, matching one folder in `tests/NetEvolve.HealthPublishers.Tests.Unit/<ServiceName>` and (where the target system can be containerized for testing) `tests/NetEvolve.HealthPublishers.Tests.Integration/<ServiceName>`.

## Commands

Run everything from the repository root (solution: `HealthPublishers.slnx`).

- Restore / build / test: `dotnet restore`, `dotnet build`, `dotnet test`
- Run a single test project: `dotnet test tests/NetEvolve.HealthPublishers.Tests.Unit/NetEvolve.HealthPublishers.Tests.Unit.csproj`
- Run a single test (TUnit/Microsoft.Testing.Platform runner, pinned via `global.json`): pass a filter, e.g.
  `dotnet test tests/NetEvolve.HealthPublishers.Tests.Unit -- --treenode-filter "/*/*/SeqOptionsConfigureTests/*"`
- Format code (required before committing): `dotnet csharpier format .`
- Target frameworks are centrally defined in `Directory.Build.props` (`_ProjectTargetFrameworks` / `_TestTargetFrameworks`): net8.0, net9.0, net10.0. Inside Visual Studio, test TFMs collapse to net10.0 only.

There is no separate lint step outside of `csharpier format .` and the analyzers that run as part of `dotnet build` (see `Directory.Build.props`/`.editorconfig`).

## Architecture

### The publisher pattern (repeated per service under `src/`)

Every `src/NetEvolve.HealthPublishers.<Service>` project follows the same shape — look at `NetEvolve.HealthPublishers.Seq` as the reference implementation when adding or modifying a publisher:

- `<Service>HealthCheckPublisher.cs` — `internal sealed class` implementing `IHealthCheckPublisher.PublishAsync`. Takes the resolved publisher `name`, its dependencies (e.g. `IHttpClientFactory`, `TimeProvider`), and `IOptionsMonitor<TOptions>`, resolving options via `_options.Get(_name)`. Non-constructor, non-`PublishAsync` members must not be public (enforced by architecture tests — see below).
- `<Service>Options.cs` — `public sealed record` of configurable knobs, XML-doc'd.
- `<Service>OptionsConfigure.cs` — `internal sealed class` implementing `IConfigureNamedOptions<TOptions>` (+ `IValidateOptions<TOptions>` when validation is needed). Binds from configuration section `HealthPublishers:<Service>:<name>`, falling back to `DependencyInjectionExtensions.DefaultName` ("Default") when `name` is null/empty.
- `DependencyInjectionExtensions.cs` — `public static class` with `Add<Service>Publisher(this IHealthChecksBuilder, ...)` overloads (default-name and named). Registers a keyed marker singleton to detect duplicate names (throws `ArgumentException` on reuse), wires `ConfigureOptions<TOptions Configure>`, registers `TimeProvider.System` and any named `HttpClient`, and adds the publisher as `IHealthCheckPublisher`.
- `README.md` — per-package usage doc; contributes to the root README's package table via the `<!-- packages:start/end -->` markers (updated by `scripts/Update-Readme.ps1`, do not hand-edit that table).

Register multiple instances of the same publisher type side-by-side via distinct `name` values; each gets its own configuration section and options instance.

### Architecture enforcement (`tests/NetEvolve.HealthPublishers.Tests.Architecture`)

`ArchUnitNET` rules (`HealthPublisherTests.cs`) load all publisher assemblies (`HealthPublisherArchitecture.cs`) and assert, for every non-abstract class assignable to `IHealthCheckPublisher`:
- must be `internal` and `sealed`
- must reside in a `NetEvolve.HealthPublishers` namespace
- class name must end with `HealthCheckPublisher`
- constructors must be `public` (or the implicit private default)
- all other members except `PublishAsync` must not be `public`

When adding a new publisher, register its assembly (via any public type, e.g. `<Service>Options`) in `HealthPublisherArchitecture.cs`'s assembly list or these rules silently skip it.

### Test projects

- `tests/NetEvolve.HealthPublishers.Tests.Unit` — one folder per service, project-references every `src/` project, uses TUnit (`NetEvolve.Extensions.TUnit`, `[TestGroup(nameof(<Service>))]`), `TUnit.Mocks`/`TUnit.Mocks.Http` for fakes, and `Microsoft.Extensions.TimeProvider.Testing` for time. Assertions use the `await Assert.That(...)` fluent style; group related assertions in `using (Assert.Multiple()) { ... }`.
- `tests/NetEvolve.HealthPublishers.Tests.Integration` — spins up real dependencies (e.g. `SeqContainer.cs` via Testcontainers) for services where that's feasible; not every publisher has an integration counterpart.
- `tests/NetEvolve.HealthPublishers.Tests.Architecture` — see above; solution-wide structural rules, not per-service tests.

### Cross-cutting conventions

- `NetEvolve.HealthPublishers.Abstractions` holds only shared/internal marker infrastructure — check it before adding cross-package shared types.
- NuGet package versions are centralized in `Directory.Packages.props`; project files reference packages without a version attribute.
- `Directory.Build.props` sets shared package metadata (`PackageTags`, `RepositoryUrl`, etc.) and `LangVersion` (`preview`); don't duplicate these per-project except to append to `PackageTags`.
- Renovate (`renovate.json`) manages dependency PRs with conventional-commit-scoped messages (`chore(deps): ...`); a custom regex manager tracks Docker image references embedded in C# via a `/* dockerimage */ "image:tag"` comment marker (see `ElasticsearchContainer`-style integration test fixtures).
- Mutation testing (Stryker.NET) runs on a schedule against `stryker-config.json` via `.github/workflows/mutation.yml`, separate from the main CI (`cicd.yml`).

## Conventions from CONTRIBUTING.md

- English only, for code, docs, and commit messages.
- Trunk-based workflow: short-lived feature branches merged via PR.
- Production code under `src/`, tests under `tests/`, following the `{ProjectName}.Tests.Unit` / `{ProjectName}.Tests.Integration` naming pattern.
- Leave repo-wide config (`.editorconfig`, `Directory.Build.props`, `Directory.Packages.props` structure) unchanged unless explicitly requested.
- Commit messages follow Conventional Commits (`<type>[optional scope]: <description>`); allowed types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`, `revert`.
