namespace NetEvolve.HealthPublishers.Prometheus.Metrics;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using global::Prometheus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the Prometheus Metrics health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that updates Prometheus gauges reflecting the latest health
    /// report results, registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddPrometheusMetricsPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<PrometheusMetricsOptions>? options = null
    ) => builder.AddPrometheusMetricsPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that updates Prometheus gauges reflecting the latest health
    /// report results, in a dedicated <see cref="CollectorRegistry"/> that does not affect the default registry.
    /// </summary>
    /// <remarks>
    /// This publisher only mutates in-process gauge state; it relies on prometheus-net's own ASP.NET Core
    /// middleware, wired up separately by the consuming application, to expose the values via a <c>/metrics</c>
    /// endpoint. Since the metrics are kept in a dedicated registry rather than the global default one, the
    /// consuming application must resolve it via <c>serviceProvider.GetRequiredKeyedService&lt;CollectorRegistry&gt;(name)</c>
    /// and pass it explicitly to the middleware, e.g. <c>app.MapMetrics(registry: registry)</c>.
    /// </remarks>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration, its dedicated <see cref="CollectorRegistry"/>, and to allow multiple registrations.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddPrometheusMetricsPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<PrometheusMetricsOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(PrometheusMetricsPublisherMarker)
                && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<PrometheusMetricsPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<PrometheusMetricsOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddKeyedSingleton(name, (_, _) => global::Prometheus.Metrics.NewCustomRegistry());

        _ = builder.Services.AddKeyedSingleton(
            name,
            (provider, key) =>
                new PrometheusMetricsInstruments(
                    global::Prometheus.Metrics.WithCustomRegistry(
                        provider.GetRequiredKeyedService<CollectorRegistry>((string?)key)
                    )
                )
        );

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(provider => new PrometheusMetricsHealthCheckPublisher(
            name,
            provider.GetRequiredService<IOptionsMonitor<PrometheusMetricsOptions>>(),
            provider.GetRequiredKeyedService<PrometheusMetricsInstruments>(name),
            provider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }

#pragma warning disable S2094 // Classes should not be empty
    private sealed class PrometheusMetricsPublisherMarker;
#pragma warning restore S2094
}
