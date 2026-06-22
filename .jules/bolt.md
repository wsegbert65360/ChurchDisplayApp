## 2024-05-24 - Zero-Allocation String Matching
**Learning:** Using LINQ `.Contains()` or `.ToLower()` for string matching in high-frequency C# paths forces string allocations or boxing.
**Action:** Implement explicit loops using `ReadOnlySpan<char>` and `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.
