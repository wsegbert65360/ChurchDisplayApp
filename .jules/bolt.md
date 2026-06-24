## 2025-02-28 - Zero-allocation string matching in C#
**Learning:** In high-frequency C# paths, using LINQ methods like `.Contains()` or string methods like `.ToLower()` for string matching against collections forces string allocations and boxing, causing unnecessary GC pressure.
**Action:** Use zero-allocation techniques such as `ReadOnlySpan<char>` (e.g., `Path.GetExtension(filePath.AsSpan())`) and explicit loops with `StringComparison.OrdinalIgnoreCase` to prevent string allocations and maintain optimization.
