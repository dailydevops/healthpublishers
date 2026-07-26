namespace NetEvolve.HealthPublishers.Tests.Unit;

using System.Threading.Tasks;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Abstractions;

[TestGroup(nameof(Abstractions))]
public sealed class AbstractionsSmokeTests
{
    [Test]
    public async Task AssemblyLoads_Expected()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;

        // Act / Assert
        _ = await Assert.That(assembly).IsNotNull();
    }
}
