namespace NetEvolve.HealthPublishers.Tests.Integration.Internals;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A <see cref="DelegatingHandler"/> that captures the last outgoing request body while still
/// forwarding the request to the inner handler, so tests can snapshot what a publisher actually sent
/// while performing a real network round-trip against a Testcontainers-hosted target.
/// </summary>
internal sealed class CapturingHttpMessageHandler : DelegatingHandler
{
    public string? CapturedRequestBody { get; private set; }

    public HttpRequestHeaders? CapturedRequestHeaders { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        CapturedRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        CapturedRequestHeaders = request.Headers;

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
