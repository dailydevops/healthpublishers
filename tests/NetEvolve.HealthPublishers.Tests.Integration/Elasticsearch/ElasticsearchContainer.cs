namespace NetEvolve.HealthPublishers.Tests.Integration.Elasticsearch;

using System;
using System.Threading.Tasks;
using Testcontainers.Elasticsearch;
using TUnit.Core.Interfaces;

/// <summary>
/// Wraps the official <c>Testcontainers.Elasticsearch</c> module, pinned to a specific Elasticsearch release for
/// reproducible builds.
/// </summary>
public sealed class ElasticsearchContainer : IAsyncInitializer, IAsyncDisposable
{
    private const string Image = /*dockerimage*/ "docker.elastic.co/elasticsearch/elasticsearch:9.4.2";

    /// <summary>
    /// The password configured for the built-in <c>elastic</c> superuser.
    /// </summary>
    public const string Password = "Integration-Tests-Passw0rd!";

    private readonly Testcontainers.Elasticsearch.ElasticsearchContainer _container = new ElasticsearchBuilder(Image)
        .WithPassword(Password)
        .Build();

    /// <summary>
    /// The username of the built-in Elasticsearch superuser.
    /// </summary>
    public const string Username = "elastic";

    /// <summary>
    /// Gets the base address of the Elasticsearch cluster exposed by the container, without embedded credentials.
    /// The container is always reachable over HTTPS with a self-signed certificate, so callers must bypass
    /// certificate validation.
    /// </summary>
    public Uri ServerUri
    {
        get
        {
            var connectionString = new Uri(_container.GetConnectionString());
            return new UriBuilder(connectionString) { UserName = string.Empty, Password = string.Empty }.Uri;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);
}
