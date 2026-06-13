using System.Collections.Concurrent;
using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Architecture.Testing;

/// <summary>
/// One source of truth for the architecture rules that encode the design invariants from
/// docs/agents/architecture.md and docs/agents/csharp-guidance.md. Each test assembly derives a
/// sealed class, points <see cref="TargetAssembly"/> at its system under test, names its
/// <see cref="RootNamespace"/>, and adds any assembly-specific rules as extra <c>[Fact]</c>s.
/// </summary>
public abstract class ArchitectureTestsBase
{
    // Loading and building an architecture model is expensive; cache one per assembly so every
    // derived class and every [Fact] (xUnit news up the class per test) reuses the same model.
    private static readonly ConcurrentDictionary<Assembly, ArchitectureModel> Models = new();

    /// <summary>The assembly whose types the rules are evaluated against.</summary>
    protected abstract Assembly TargetAssembly { get; }

    /// <summary>The single flat namespace every type in <see cref="TargetAssembly"/> must reside in.</summary>
    protected abstract string RootNamespace { get; }

    private ArchitectureModel Model =>
        Models.GetOrAdd(TargetAssembly, static assembly => new ArchLoader().LoadAssemblies(assembly).Build());

    [Fact]
    public void AllTypesResideInRootNamespace() =>
        Verify(Types()
            .That()
            .DoNotHaveNameContaining("<") // exclude compiler-generated closures / async state machines
            .Should()
            .ResideInNamespace(RootNamespace)
            .Because($"{RootNamespace} is intentionally a single, flat namespace; new sub-namespaces are a design change, not a drive-by."));

    [Fact]
    public void ConcreteClassesAreSealed() =>
        Verify(Classes()
            .That()
            .AreNotAbstract() // C# 'static' compiles to 'abstract sealed' — this also excludes static factories
            .And()
            .DoNotHaveNameContaining("<")
            .Should()
            .BeSealed()
            .Because("csharp-guidance.md: seal records and classes by default (enables devirtualization)."));

    [Fact]
    public void InstanceFieldsAreNotPublic() =>
        Verify(FieldMembers()
            .That()
            .AreNotStatic() // const / static readonly may be public; instance state must not be.
            .And()
            .DoNotHaveNameContaining("<") // exclude compiler-generated backing fields
            .Should()
            .NotBePublic()
            .Because("csharp-guidance.md: immutable-by-default; no public mutable instance state."));

    [Fact]
    public void DoesNotDependOnAspNetCore() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore.*")
            .Because("Plumber is a host-free pipeline framework; pulling in ASP.NET Core would defeat its purpose."));

    [Fact]
    public void DoesNotDependOnHosting() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.Extensions\.Hosting.*")
            .Because("Plumber targets host-free .NET (console apps, AWS Lambdas, queue consumers); the consumer owns the host, not Plumber."));

    /// <summary>Evaluate a rule against <see cref="TargetAssembly"/>, failing with ArchUnit's diagnostic on violation.</summary>
    /// <param name="rule">The ArchUnit rule to evaluate.</param>
    protected void Verify(IArchRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.HasNoViolations(Model))
        {
            Assert.Fail(rule.Evaluate(Model).ToErrorMessage());
        }
    }
}
