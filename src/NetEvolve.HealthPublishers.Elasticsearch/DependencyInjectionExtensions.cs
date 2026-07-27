namespace NetEvolve.HealthPublishers.Elasticsearch;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the Elasticsearch health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// The default Elasticsearch cluster address, used as a fallback when <see cref="ElasticsearchOptions.ServerUri"/>
    /// is not set. Validation already requires <see cref="ElasticsearchOptions.ServerUri"/> to be set, so this is
    /// only ever used defensively.
    /// </summary>
#pragma warning disable S1075 // URIs should not be hardcoded
    internal static readonly Uri DefaultServerUri = new("http://localhost:9200", UriKind.Absolute);
#pragma warning restore S1075

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that indexes health report results as documents into an
    /// Elasticsearch cluster, registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddElasticsearchPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<ElasticsearchOptions>? options = null
    ) => builder.AddElasticsearchPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that indexes health report results as documents into an
    /// Elasticsearch cluster.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple Elasticsearch targets.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddElasticsearchPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<ElasticsearchOptions>? options = null
    ) => builder.AddElasticsearchPublisherCore(name, options, configureSettings: null);

    /// <summary>
    /// Test-only seam allowing integration tests to customize the underlying <see cref="ElasticsearchClientSettings"/>,
    /// e.g. to bypass certificate validation against a Testcontainers-hosted Elasticsearch cluster using a
    /// self-signed certificate. Not intended for production use, hence internal.
    /// </summary>
    internal static IHealthChecksBuilder AddElasticsearchPublisherForTests(
        this IHealthChecksBuilder builder,
        string name,
        Action<ElasticsearchOptions>? options,
        Action<ElasticsearchClientSettings> configureSettings
    ) => builder.AddElasticsearchPublisherCore(name, options, configureSettings);

    private static IHealthChecksBuilder AddElasticsearchPublisherCore(
        this IHealthChecksBuilder builder,
        string name,
        Action<ElasticsearchOptions>? options,
        Action<ElasticsearchClientSettings>? configureSettings
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(ElasticsearchPublisherMarker) && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<ElasticsearchPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<ElasticsearchOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddKeyedSingleton(
            name,
            (IServiceProvider provider, object? key) =>
                CreateClient(
                    provider.GetRequiredService<IOptionsMonitor<ElasticsearchOptions>>().Get((string?)key),
                    configureSettings: configureSettings
                )
        );

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(provider => new ElasticsearchHealthCheckPublisher(
            name,
            provider.GetRequiredKeyedService<ElasticsearchClient>(name),
            provider.GetRequiredService<IOptionsMonitor<ElasticsearchOptions>>(),
            provider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }

    /// <summary>
    /// Creates an <see cref="ElasticsearchClient"/> configured from the given <paramref name="options"/>.
    /// </summary>
#pragma warning disable CA2000 // Dispose objects before losing scope - ownership is transferred to the returned ElasticsearchClient, which disposes them.
    internal static ElasticsearchClient CreateClient(
        ElasticsearchOptions options,
        IRequestInvoker? requestInvoker = null,
        Action<ElasticsearchClientSettings>? configureSettings = null
    )
    {
        var pool = new SingleNodePool(options.ServerUri ?? DefaultServerUri);

        var settings = requestInvoker is null
            ? new ElasticsearchClientSettings(pool)
            : new ElasticsearchClientSettings(pool, requestInvoker);

        settings = settings.EnableHttpCompression(false);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            settings = settings.Authentication(new ApiKey(options.ApiKey));
        }
        else if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
        {
            settings = settings.Authentication(new BasicAuthentication(options.Username, options.Password));
        }

        configureSettings?.Invoke(settings);

        return new ElasticsearchClient(settings);
    }
#pragma warning restore CA2000

#pragma warning disable S2094 // Classes should not be empty
    private sealed class ElasticsearchPublisherMarker;
#pragma warning restore S2094
}
