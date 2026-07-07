## 2024-05-24 - Zero-allocation string matching in C#
**Learning:** Using LINQ `.Contains()` or string `.ToLower()` for checking file extensions in high-frequency loops forces unnecessary string allocations and boxing, increasing Garbage Collection pressure in .NET.
**Action:** In high-frequency C# paths, implement explicit loops using `ReadOnlySpan<char>` and `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.
