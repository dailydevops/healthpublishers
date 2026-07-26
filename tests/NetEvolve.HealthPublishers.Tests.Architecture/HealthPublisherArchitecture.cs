namespace NetEvolve.HealthPublishers.Tests.Architecture;

using System;
using System.Threading;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

internal static class HealthPublisherArchitecture
{
    // TIP: load your architecture once at the start to maximize performance of your tests
    private static readonly Lazy<Architecture> _instance = new Lazy<Architecture>(
        LoadArchitecture,
        LazyThreadSafetyMode.PublicationOnly
    );

    public static Architecture Instance => _instance.Value;

    private static Architecture LoadArchitecture()
    {
        System.Reflection.Assembly[] assemblies = [typeof(Seq.SeqOptions).Assembly];

        return new ArchLoader()
            .LoadAssembliesRecursively(
                assemblies,
                x =>
                    x.Name.Name.StartsWith("NetEvolve.HealthPublishers", StringComparison.OrdinalIgnoreCase)
                        ? FilterResult.LoadAndContinue
                        : FilterResult.SkipAndContinue
            )
            .Build();
    }
}
