namespace NetEvolve.HealthPublishers.Webhook;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the webhook health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// The prefix used for the named <see cref="IHttpClientFactory"/> client of a webhook publisher.
    /// </summary>
    internal const string HttpClientNamePrefix = "NetEvolve.HealthPublishers.Webhook:";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that posts health report results as JSON to a webhook endpoint,
    /// registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddWebhookPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<WebhookOptions>? options = null
    ) => builder.AddWebhookPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that posts health report results as JSON to a webhook endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple webhook targets.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddWebhookPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<WebhookOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(WebhookPublisherMarker) && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<WebhookPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<WebhookOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddHttpClient($"{HttpClientNamePrefix}{name}");

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(provider => new WebhookHealthCheckPublisher(
            name,
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptionsMonitor<WebhookOptions>>(),
            provider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }

#pragma warning disable S2094 // Classes should not be empty
    private sealed class WebhookPublisherMarker;
#pragma warning restore S2094
}
