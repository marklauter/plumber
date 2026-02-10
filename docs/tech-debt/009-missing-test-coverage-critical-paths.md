# Missing test coverage for critical paths

- **Area:** Plumber.Tests
- **Priority:** Medium
- **Status:** Open

## Problem
No tests cover: timeout behavior, disposal (`ObjectDisposedException`), cancellation, error cases in middleware registration (adding after build, invalid middleware classes), concurrent invocations, configuration loading, the `Create(args, configure)` overload, or the `Build(TimeSpan)` overload. Only happy-path middleware execution is tested.

## Suggested Fix
Add test cases for each of the above scenarios.

## Code References
- `Plumber.Tests/PlumberTests.cs` — existing test file with only happy-path coverage

## Notes
None.
