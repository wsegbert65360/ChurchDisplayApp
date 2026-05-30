
## 2024-05-18 - String allocation optimization
**Learning:** Using `ReadOnlySpan<char>` with `StringComparison.OrdinalIgnoreCase` eliminates string allocations for comparisons. However, for WPF properties bound to UI data triggers, avoiding all string allocations might not be feasible since a string format `.ToString().ToLowerInvariant()` might still be required for the property to function correctly.
**Action:** Be mindful when applying zero-allocation techniques in C# WPF applications. Verify if the optimized property is directly bound to UI elements and if the trigger mechanism necessitates standard C# strings.
