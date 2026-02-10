# Code Style

C# 14 / .NET 10 — high-performance, functional, zero-allocation.

## Design Philosophy
1. **Performance-aware always, zero-allocation when it matters** — design for performance by default (prefer spans, avoid allocations, think about memory); sacrifice readability only in hot paths
2. **Functional style** — expression bodies, pure functions, immutability, no side effects; decompose methods into small focused functions until expression bodies emerge naturally (don't force complex logic into one expression — extract until each method is a single expression)
3. **Immutable by default** — records over classes, `readonly` everything, no mutable state
4. **Span-first** — pass `Span<T>`/`ReadOnlySpan<T>` instead of arrays or strings

## Type Design
- prefer `record` over `class` for all data types
- use positional records (primary-constructor syntax) — not property-bodied records
  ```csharp
  // correct
  public sealed record DynamoDbLiteOptions(string ConnectionString);

  // wrong — do not use property-based record definitions
  public sealed record DynamoDbLiteOptions
  {
      public string ConnectionString { get; init; } = "";
  }
  ```
- use `readonly record struct` for small value types (≤16 bytes)
- seal all records and classes by default (enables devirtualization)
- use primary constructors for dependency injection and simple initialization
- use `required init` properties only on classes/structs that cannot be positional records
- prefer `field` keyword in property accessors over backing fields
- mark all fields `readonly`
- avoid mutable classes; if state changes, return a new instance

## Zero-Allocation & Performance
- use `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>` over arrays
- use `stackalloc` for small, fixed-size buffers
- use `ArrayPool<T>.Shared` for temporary arrays
- use `string.Create()`, `StringBuilder` pooling, or `Span<char>` for string building
- avoid LINQ in hot paths; use `for`/`foreach` with spans
- avoid closures and delegates that capture; use `static` lambdas
- use `ref struct` for stack-only types that wrap spans
- use `in` parameters for large readonly structs
- use `ValueTask<T>` over `Task<T>` for frequently-sync async methods
- use `FrozenDictionary`/`FrozenSet` for read-heavy lookup tables
- prefer `struct` enumerators and `GetEnumerator()` patterns

## Functional & Expression Style
- use expression bodies for all single-expression members
- use switch expressions over switch statements
- use pattern matching: `is`, `is not`, `and`, `or`, property patterns, list patterns
- always prefer `var` over explicit type — never use explicit types for local variables, even when the type isn't apparent from the right-hand side
- use `??`, `??=`, `?.`, `?[]` for null handling
- use `is null` / `is not null` (not `== null`)
- use index `[^1]` and range `[1..^1]` operators
- prefer ternary expressions over multi-branch `if`/`else` for simple conditional returns
- when ternary/conditional logic becomes deeply nested, decompose into small focused methods rather than writing one long expression
- chain expressions; minimize intermediate variables
- prefer pure functions with no side effects
- prefer deconstructed variable declarations
- prefer simple `default` over `default(T)`
- prefer conditional delegate calls (`handler?.Invoke()`)
- discard unused variables with `_`

## Collections
- use collection expressions: `[]`, `[1, 2, 3]`, `[..existing, newItem]`
- return `IReadOnlyList<T>` from public APIs
- use `ReadOnlySpan<T>` for method parameters accepting sequences
- avoid `IEnumerable<T>` in hot paths (causes allocations)

## Methods & Flow
- use simplified `using` declarations (no braces)
- omit braces for single-line `if`/`foreach`/`while`
- use `static` local functions (no captures)
- use `ArgumentNullException.ThrowIfNull()` and similar guard methods
- suffix async methods with `Async`
- require explicit accessibility modifiers on non-interface members
- prefer language keywords (`int`, `string`) over BCL types (`Int32`, `String`)

## Naming
| Element | Style | Example |
|---------|-------|---------|
| Types, Properties, Methods, Events | PascalCase | `UserService`, `IsValid` |
| Interfaces | IPascalCase | `IRepository` |
| Private/internal fields | camelCase | `connectionString` |
| Parameters, locals | camelCase | `userId`, `result` |
| Constants | PascalCase | `MaxRetryCount` |

No `_` prefix. No `this.` qualifier. No Hungarian notation.

## Formatting
- 4-space indentation, spaces only, CRLF line endings, final newline at end of files
- Allman braces (open brace on new line)
- file-scoped namespace declarations (not block-scoped)
- usings outside namespace, not grouped or sorted specially
- modifier order: `public` `private` `protected` `internal` `file` `static` `extern` `new` `virtual` `abstract` `sealed` `override` `readonly` `unsafe` `required` `volatile` `async`
