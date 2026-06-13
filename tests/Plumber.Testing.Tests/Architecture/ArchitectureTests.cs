using System.Reflection;

namespace Plumber.Testing.Tests.Architecture;

// Plumber.Testing's architecture rules: the shared invariants from
// global::Architecture.Testing.ArchitectureTestsBase, with no assembly-specific additions.
public sealed class ArchitectureTests : global::Architecture.Testing.ArchitectureTestsBase
{
    protected override Assembly TargetAssembly => typeof(PlumberApplicationFactory<,>).Assembly;

    protected override string RootNamespace => "Plumber.Testing";
}
