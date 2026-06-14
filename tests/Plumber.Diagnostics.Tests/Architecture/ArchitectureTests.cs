using System.Reflection;

namespace Plumber.Diagnostics.Tests.Architecture;

// Plumber.Diagnostics's architecture rules: the shared invariants from
// global::Architecture.Testing.ArchitectureTestsBase, with no assembly-specific additions.
public sealed class ArchitectureTests : global::Architecture.Testing.ArchitectureTestsBase
{
    protected override Assembly TargetAssembly => typeof(RequestTracingOptions<,>).Assembly;

    protected override string RootNamespace => "Plumber.Diagnostics";
}
