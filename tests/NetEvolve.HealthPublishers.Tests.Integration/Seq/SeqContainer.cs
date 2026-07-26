namespace NetEvolve.HealthPublishers.Tests.Integration.Seq;

using System;
using System.Threading.Tasks;
using Testcontainers.Seq;
using TUnit.Core.Interfaces;

public sealed class SeqContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly Testcontainers.Seq.SeqContainer _container = new SeqBuilder(SeqBuilder.SeqImage)
        .WithAcceptLicenseAgreement(true)
        .Build();

    public Uri ServerUrl => new(_container.GetEndpoint());

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);
}
