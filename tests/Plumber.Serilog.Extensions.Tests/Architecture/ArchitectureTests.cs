using System.Reflection;

namespace Plumber.Serilog.Extensions.Tests.Architecture;

// Plumber.Serilog.Extensions's architecture rules: the shared invariants from
// global::Architecture.Testing.ArchitectureTestsBase, with no assembly-specific additions.
public sealed class ArchitectureTests : global::Architecture.Testing.ArchitectureTestsBase
{
    protected override Assembly TargetAssembly => typeof(RequestLoggerOptions<,>).Assembly;

    protected override string RootNamespace => "Plumber.Serilog.Extensions";
}
