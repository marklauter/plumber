using System.Reflection;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Plumber.Tests.Architecture;

// Plumber's architecture rules: the shared invariants from
// global::Architecture.Testing.ArchitectureTestsBase plus the runtime-library
// contracts that apply only to Plumber's production surface.
public sealed class ArchitectureTests : global::Architecture.Testing.ArchitectureTestsBase
{
    protected override Assembly TargetAssembly => typeof(RequestHandler).Assembly;

    protected override string RootNamespace => "Plumber";

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
}
