## 2024-06-19 - Zero-allocation string matching in high-frequency paths
**Learning:** Using LINQ `.Contains()` and `.ToLower()` on strings for extension matching creates unnecessary heap allocations and GC pressure.
**Action:** Use `ReadOnlySpan<char>` with `Path.GetExtension(filePath.AsSpan())` and explicit loops with `StringComparison.OrdinalIgnoreCase` to avoid allocations.
