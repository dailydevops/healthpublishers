namespace NetEvolve.HealthPublishers.Tests.Architecture;

using ArchUnitNET.Domain;
using ArchUnitNET.TUnit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetEvolve.Extensions.TUnit;
using TUnit.Core.Enums;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

[TestGroup(nameof(Architecture))]
[RunOn(OS.Windows)]
public class HealthPublisherTests
{
    private readonly IObjectProvider<Class> _publishers = Classes()
        .That()
        .AreNotAbstract()
        .And()
        .AreAssignableTo(typeof(IHealthCheckPublisher));

    [Test]
    public void HealthPublisherClass_ShouldBeInternal_Expected()
    {
        var rule = Classes().That().Are(_publishers).Should().BeInternal();

        rule.Check(HealthPublisherArchitecture.Instance);
    }

    [Test]
    public void HealthPublisherClass_ShouldBeSealed_Expected()
    {
        var rule = Classes().That().Are(_publishers).Should().BeSealed();

        rule.Check(HealthPublisherArchitecture.Instance);
    }

    [Test]
    public void HealthPublisherClass_ShouldResideInNamespace_StartsWithNetEvolveExpected()
    {
        var rule = Classes().That().Are(_publishers).Should().ResideInNamespaceMatching(@"NetEvolve\.HealthPublishers");

        rule.Check(HealthPublisherArchitecture.Instance);
    }

    [Test]
    public void HealthPublisherClass_ShouldHaveNameEndingWithHealthCheckPublisher_Expected()
    {
        var rule = Classes().That().Are(_publishers).Should().HaveNameEndingWith("HealthCheckPublisher");

        rule.Check(HealthPublisherArchitecture.Instance);
    }

    [Test]
    public void HealthPublisherConstructors_ShouldBePublic_Expected()
    {
        var rule = MethodMembers()
            .That()
            .AreDeclaredIn(_publishers)
            .And()
            .AreConstructors()
            .Should()
            .BePublic()
            .OrShould()
            // Fallback default constructor
            .BePrivate();

        rule.Check(HealthPublisherArchitecture.Instance);
    }

    [Test]
    public void HealthPublisherMembers_ShouldNotBePublic_Expected()
    {
        var rule = MethodMembers()
            .That()
            .AreDeclaredIn(_publishers)
            .And()
            .DoNotHaveNameContaining("PublishAsync")
            .And()
            .AreNoConstructors()
            .Should()
            .NotBePublic();

        rule.Check(HealthPublisherArchitecture.Instance);
    }
}
