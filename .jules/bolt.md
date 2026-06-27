## 2024-06-27 - Zero-Allocation Path Extension Checking
**Learning:** In high-frequency paths, using `.ToLower()` and LINQ `.Contains()` for string matching against collections forces unnecessary string allocations and boxing, which increases GC pressure.
**Action:** Use `.AsSpan()` on strings (e.g., `filePath.AsSpan()`) with `ReadOnlySpan<char>`, explicit loops, and `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.
