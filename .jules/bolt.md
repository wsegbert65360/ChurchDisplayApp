## 2026-06-18 - C# Zero-Allocation String Matching
**Learning:** In high-frequency C# paths, using LINQ methods like `.Contains()` with `.ToLower()` for string matching against collections forces string allocations and boxing.
**Action:** Implement explicit loops using `ReadOnlySpan<char>` and `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.