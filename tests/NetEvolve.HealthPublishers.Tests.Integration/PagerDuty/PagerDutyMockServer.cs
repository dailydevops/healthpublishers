namespace NetEvolve.HealthPublishers.Tests.Integration.PagerDuty;

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Core.Interfaces;

/// <summary>
/// A minimal, real HTTP listener standing in for the PagerDuty Events API v2, since no
/// Testcontainers-hosted equivalent (or public container image) exists for this pure external SaaS
/// endpoint. Accepts every request with <see cref="HttpStatusCode.Accepted"/> so publishers can perform a
/// real network round-trip; per-request assertions are made through a
/// <see cref="Internals.CapturingHttpMessageHandler"/> attached to the calling publisher's <see cref="HttpClient"/>.
/// </summary>
public sealed class PagerDutyMockServer : IAsyncInitializer, IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public Uri ServerUrl { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var port = GetFreeTcpPort();
        ServerUrl = new Uri($"http://127.0.0.1:{port}/");

        _listener.Prefixes.Add(ServerUrl.ToString());
        _listener.Start();

        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }
        _listener.Stop();
        _listener.Close();

        if (_acceptLoop is not null)
        {
            try
            {
#pragma warning disable VSTHRD003 // Task was intentionally started in InitializeAsync; awaiting it here just drains the accept loop before disposal.
                await _acceptLoop.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
            catch (Exception ex)
                when (ex is OperationCanceledException or ObjectDisposedException or HttpListenerException)
            {
                // Expected during shutdown.
            }
        }

        _cts?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using (context.Request.InputStream)
            {
                // Drain the request body so the client's write completes without a connection reset.
                await context.Request.InputStream.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            }

            context.Response.StatusCode = (int)HttpStatusCode.Accepted;
            context.Response.Close();
        }
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
