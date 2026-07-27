namespace NetEvolve.HealthPublishers.Prometheus.PushGateway;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the Prometheus Pushgateway health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// The prefix used for the named <see cref="IHttpClientFactory"/> client of a Prometheus Pushgateway publisher.
    /// </summary>
    internal const string HttpClientNamePrefix = "NetEvolve.HealthPublishers.Prometheus.PushGateway:";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that pushes health report results to a Prometheus Pushgateway
    /// instance as metrics, registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddPrometheusPushGateway(
        [NotNull] this IHealthChecksBuilder builder,
        Action<PrometheusPushGatewayOptions>? options = null
    ) => builder.AddPrometheusPushGateway(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that pushes health report results to a Prometheus Pushgateway
    /// instance as metrics.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple Pushgateway targets.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddPrometheusPushGateway(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<PrometheusPushGatewayOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(PrometheusPushGatewayPublisherMarker)
                && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<PrometheusPushGatewayPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<PrometheusPushGatewayOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddHttpClient(
            $"{HttpClientNamePrefix}{name}",
            (provider, client) => ConfigureHttpClient(name, provider, client)
        );

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(
            provider => new PrometheusPushGatewayHealthCheckPublisher(
                name,
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<IOptionsMonitor<PrometheusPushGatewayOptions>>(),
                provider.GetRequiredService<TimeProvider>()
            )
        );

        return builder;
    }

    internal static void ConfigureHttpClient(string name, IServiceProvider provider, HttpClient client) =>
        client.BaseAddress = provider
            .GetRequiredService<IOptionsMonitor<PrometheusPushGatewayOptions>>()
            .Get(name)
            .ServerUrl;

#pragma warning disable S2094 // Classes should not be empty
    private sealed class PrometheusPushGatewayPublisherMarker;
#pragma warning restore S2094
}
