namespace NetEvolve.HealthPublishers.AWS.CloudWatch;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Amazon;
using Amazon.CloudWatch;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensions methods for <see cref="IHealthChecksBuilder"/> to add the AWS CloudWatch health check publisher.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// The name used when no explicit name is provided.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that publishes health report results as metrics to Amazon
    /// CloudWatch, registered under <see cref="DefaultName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    public static IHealthChecksBuilder AddAWSCloudWatchPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        Action<CloudWatchOptions>? options = null
    ) => builder.AddAWSCloudWatchPublisher(DefaultName, options);

    /// <summary>
    /// Adds an <see cref="IHealthCheckPublisher"/> that publishes health report results as metrics to Amazon
    /// CloudWatch.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The name of the publisher. Used to resolve its configuration and to allow multiple CloudWatch targets.</param>
    /// <param name="options">An optional action to configure.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="name"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is <see langword="null" /> or <c>whitespace</c>.</exception>
    /// <exception cref="ArgumentException">The <paramref name="name"/> is already in use.</exception>
    public static IHealthChecksBuilder AddAWSCloudWatchPublisher(
        [NotNull] this IHealthChecksBuilder builder,
        [NotNull] string name,
        Action<CloudWatchOptions>? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (
            builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(CloudWatchPublisherMarker) && Equals(descriptor.ServiceKey, name)
            )
        )
        {
            throw new ArgumentException($"Name `{name}` already in use.", nameof(name));
        }

        _ = builder.Services.AddKeyedSingleton<CloudWatchPublisherMarker>(name);

        builder.Services.TryAddSingleton(TimeProvider.System);

        _ = builder.Services.ConfigureOptions<CloudWatchOptionsConfigure>();

        if (options is not null)
        {
            _ = builder.Services.Configure(name, options);
        }

        _ = builder.Services.AddKeyedSingleton<IAmazonCloudWatch>(
            name,
            (provider, key) =>
                CreateClient(provider.GetRequiredService<IOptionsMonitor<CloudWatchOptions>>().Get((string?)key))
        );

        _ = builder.Services.AddSingleton<IHealthCheckPublisher>(provider => new CloudWatchHealthCheckPublisher(
            name,
            provider.GetRequiredKeyedService<IAmazonCloudWatch>(name),
            provider.GetRequiredService<IOptionsMonitor<CloudWatchOptions>>(),
            provider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }

    /// <summary>
    /// Creates an <see cref="IAmazonCloudWatch"/> client configured from the given <paramref name="options"/>.
    /// </summary>
#pragma warning disable CA2000 // Dispose objects before losing scope - ownership is transferred to the returned client, which disposes them.
    internal static IAmazonCloudWatch CreateClient(CloudWatchOptions options)
    {
        var config = new AmazonCloudWatchConfig();

        if (options.ServiceUrl is not null)
        {
            // RegionEndpoint and ServiceURL are mutually exclusive on ClientConfig; setting RegionEndpoint after
            // ServiceURL would silently reset ServiceURL back to null. AuthenticationRegion is the supported way
            // to keep SigV4 signing working (e.g. against a VPC endpoint or LocalStack) while using a custom
            // service endpoint.
            config.ServiceURL = options.ServiceUrl.ToString();

            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                config.AuthenticationRegion = options.Region;
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        if (!string.IsNullOrWhiteSpace(options.AccessKeyId) && !string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            return new AmazonCloudWatchClient(
                new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey),
                config
            );
        }

        return new AmazonCloudWatchClient(config);
    }
#pragma warning restore CA2000

#pragma warning disable S2094 // Classes should not be empty
    private sealed class CloudWatchPublisherMarker;
#pragma warning restore S2094
}
