## 2024-05-24 - Zero-Allocation Extension Checking
**Learning:** In high-frequency C# paths, using `.ToLower()` and LINQ `.Contains()` for string matching against collections forces string allocations and boxing, which increases GC pressure.
**Action:** Implement explicit loops using `ReadOnlySpan<char>` (e.g., `Path.GetExtension(filePath.AsSpan())`) and `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.
