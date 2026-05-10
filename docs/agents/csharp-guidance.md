# C# Guidance

C# 14 / .NET 10. Performance-aware, functional, immutable-by-default.

Formatting, naming, `var`, expression bodies, pattern matching, modifier order, and similar mechanical rules are enforced by `.editorconfig` — fix violations before committing. The notes below cover judgment calls the analyzer cannot catch.

## Type design
- `record` over `class`; positional records (primary-constructor syntax) — never property-bodied
- `readonly record struct` for value types ≤16 bytes
- seal records and classes by default (enables devirtualization)
- `readonly` all fields; no mutable state — return a new instance instead of mutating
- `required init` only when a positional record is impossible

## Performance / zero-alloc
- `Span<T>` / `ReadOnlySpan<T>` / `Memory<T>` over arrays
- `stackalloc` for small fixed buffers; `ArrayPool<T>.Shared` for temporaries
- `string.Create()` or `Span<char>` for string building
- no LINQ, closures, or capturing lambdas in hot paths — use `static` lambdas
- `ref struct` for span-wrapping stack-only types; `in` for large readonly structs
- `ValueTask<T>` for frequently-sync async; `FrozenDictionary`/`FrozenSet` for read-heavy lookups

## API surface
- accept `ReadOnlySpan<T>`, return `IReadOnlyList<T>`
- avoid `IEnumerable<T>` in hot paths (allocations)
- `Async` suffix on async methods
- `ArgumentNullException.ThrowIfNull` and friends at boundaries

## Decomposition
- decompose methods until expression bodies emerge naturally — don't force complex logic into one expression
- when ternary/conditional logic nests, extract methods rather than write one long expression
