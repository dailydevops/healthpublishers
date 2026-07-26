namespace NetEvolve.HealthPublishers.OpenTelemetry;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the OpenTelemetry health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// The name of the <see cref="Meter"/> used to record health check metrics.
    /// </summary>
    public const string MeterName = "NetEvolve.HealthPublishers.OpenTelemetry";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that records health report results as .NET metrics,
    /// registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddOpenTelemetryPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<OpenTelemetryOptions>? options = null
    ) => builder.AddOpenTelemetryPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that records health report results as .NET metrics,
    /// via <see cref="System.Diagnostics.Metrics.Meter"/>, consumable by any OpenTelemetry-compatible collector.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple registrations.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddOpenTelemetryPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<OpenTelemetryOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(OpenTelemetryPublisherMarker) && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<OpenTelemetryPublisherMarker>(name);

        _ = builder.Services.ConfigureOptions<OpenTelemetryOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton(_ => new Meter(MeterName));
        builder.Services.TryAddSingleton<OpenTelemetryInstruments>();

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(provider => new OpenTelemetryHealthCheckPublisher(
            name,
            provider.GetRequiredService<IOptionsMonitor<OpenTelemetryOptions>>(),
            provider.GetRequiredService<OpenTelemetryInstruments>(),
            provider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }

#pragma warning disable S2094 // Classes should not be empty
    private sealed class OpenTelemetryPublisherMarker;
#pragma warning restore S2094
}
