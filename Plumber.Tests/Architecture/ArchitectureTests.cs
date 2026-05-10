using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Plumber.Tests.Architecture;

// Encodes the design invariants from docs/agents/architecture.md and
// docs/agents/csharp-guidance.md so drift trips the build, not code review.
public sealed class ArchitectureTests
{
    private static readonly ArchitectureModel Plumber = new ArchLoader()
        .LoadAssemblies(typeof(RequestHandler).Assembly)
        .Build();

    [Fact]
    public void AllTypesResideInPlumberNamespace() =>
        Verify(Types()
            .That()
            .DoNotHaveNameContaining("<") // exclude compiler-generated closures / async state machines
            .Should()
            .ResideInNamespace("Plumber")
            .Because("Plumber is intentionally a single, flat namespace; new sub-namespaces are a design change, not a drive-by."));

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
    public void PlumberDoesNotDependOnAspNetCore() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore.*")
            .Because("Plumber is a host-free pipeline framework; pulling in ASP.NET Core would defeat its purpose."));

    [Fact]
    public void PlumberDoesNotDependOnHosting() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.Extensions\.Hosting.*")
            .Because("Plumber targets host-free .NET (console apps, AWS Lambdas, queue consumers); the consumer owns the host, not Plumber."));

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
    public void PlumberDoesNotDependOnConsole() =>
        Verify(Types()
            .Should()
            // HaveFullName is used instead of NotDependOnAny(typeof(Console)) — the typed overload requires
            // the type to be loaded into the architecture, but we only load Plumber.dll. The name predicate
            // matches against dependency targets recorded by the loader without needing the BCL assembly.
            .NotDependOnAnyTypesThat()
            .HaveFullName("System.Console")
            .Because("Plumber is a library; consumers route output through ILogger or their own sink — direct Console writes leak into hosts that suppress stdout (lambdas, services)."));

    [Fact]
    public void PlumberDoesNotDependOnStopwatch() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullName("System.Diagnostics.Stopwatch")
            .Because("RequestContext measures elapsed time via the registered TimeProvider so tests can drive the clock; Stopwatch bypasses that and is unmockable."));

    [Fact]
    public void PlumberDoesNotDependOnThread() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullName("System.Threading.Thread")
            .Because("Plumber's pipeline is async-only; Thread primitives (Sleep, Join, Abort) block the request thread and defeat the cancellation/timeout contract."));

    [Fact]
    public void HandlerAndBuilderConstructorsAreNotPublic() =>
        Verify(MethodMembers()
            .That()
            .AreConstructors()
            .And()
            .AreDeclaredIn(Types().That().HaveNameMatching(@"^RequestHandler(Builder)?(`\d+)?$"))
            .Should()
            .NotBePublic()
            .Because("RequestHandler<,> and RequestHandlerBuilder<,> are constructed only via the static factories so the framework controls IServiceProvider ownership and per-Build configuration semantics."));

    [Fact]
    public void FactoryClassesAreStatic() =>
        // C# 'static' compiles to 'abstract sealed' at the IL level — assert both.
        Verify(Classes()
            .That()
            .HaveNameMatching(@"^RequestHandler(Builder)?$") // exact match excludes the generic RequestHandler`2 / RequestHandlerBuilder`2
            .Should()
            .BeAbstract()
            .AndShould()
            .BeSealed()
            .Because("RequestHandler and RequestHandlerBuilder exist solely to expose Create<>(...) factories; making them instantiable would imply state they don't have."));

    private static void Verify(IArchRule rule)
    {
        if (!rule.HasNoViolations(Plumber))
        {
            Assert.Fail(rule.Evaluate(Plumber).ToErrorMessage());
        }
    }
}
