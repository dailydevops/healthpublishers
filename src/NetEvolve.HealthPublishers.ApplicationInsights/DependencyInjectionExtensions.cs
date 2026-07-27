namespace NetEvolve.HealthPublishers.ApplicationInsights;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the Application Insights health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that pushes health report results to Azure Application Insights,
    /// registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddApplicationInsightsPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<ApplicationInsightsOptions>? options = null
    ) => builder.AddApplicationInsightsPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that pushes health report results to Azure Application Insights.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple Application Insights targets.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddApplicationInsightsPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<ApplicationInsightsOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(ApplicationInsightsPublisherMarker)
                && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<ApplicationInsightsPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<ApplicationInsightsOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddKeyedSingleton(name, (provider, _) => CreateTelemetryConfiguration(name, provider));

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(
            provider => new ApplicationInsightsHealthCheckPublisher(
                name,
                () => new TelemetryClient(provider.GetRequiredKeyedService<TelemetryConfiguration>(name)),
                provider.GetRequiredService<IOptionsMonitor<ApplicationInsightsOptions>>(),
                provider.GetRequiredService<TimeProvider>()
            )
        );

        return builder;
    }

    internal static TelemetryConfiguration CreateTelemetryConfiguration(string name, IServiceProvider provider)
    {
        var configuration = TelemetryConfiguration.CreateDefault();
        var connectionString = provider
            .GetRequiredService<IOptionsMonitor<ApplicationInsightsOptions>>()
            .Get(name)
            .ConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            configuration.ConnectionString = connectionString;
        }

        return configuration;
    }

#pragma warning disable S2094 // Classes should not be empty
    private sealed class ApplicationInsightsPublisherMarker;
#pragma warning restore S2094
}
