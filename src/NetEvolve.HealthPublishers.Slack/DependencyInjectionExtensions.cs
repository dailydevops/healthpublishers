namespace NetEvolve.HealthPublishers.Slack;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the Slack health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// The prefix used for the named <see cref="IHttpClientFactory"/> client of a Slack publisher.
    /// </summary>
    internal const string HttpClientNamePrefix = "NetEvolve.HealthPublishers.Slack:";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that pushes health report results to a Slack channel
    /// via an incoming webhook, registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddSlackPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<SlackOptions>? options = null
    ) => builder.AddSlackPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that pushes health report results to a Slack channel
    /// via an incoming webhook.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple Slack targets.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddSlackPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<SlackOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(SlackPublisherMarker) && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<SlackPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<SlackOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddHttpClient(
            $"{HttpClientNamePrefix}{name}",
            (provider, client) => ConfigureHttpClient(name, provider, client)
        );

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(provider => new SlackHealthCheckPublisher(
            name,
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptionsMonitor<SlackOptions>>(),
            provider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }

    internal static void ConfigureHttpClient(string name, IServiceProvider provider, HttpClient client) =>
        client.BaseAddress = provider.GetRequiredService<IOptionsMonitor<SlackOptions>>().Get(name).WebhookUrl;

#pragma warning disable S2094 // Classes should not be empty
    private sealed class SlackPublisherMarker;
#pragma warning restore S2094
}
