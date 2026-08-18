namespace NetEvolve.HealthPublishers.MicrosoftTeams.Console;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// Logs the real webhook request, so it's visible whether ShouldNotify actually let it through.
internal sealed class LoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        System.Console.WriteLine($"[SENT] {request.Method} {request.RequestUri} -> {(int)response.StatusCode}");
        return response;
    }
}
