## 2024-05-24 - Zero-allocation String Matching in High-Frequency Paths
**Learning:** In high-frequency paths like file extension checking during directory scans, using LINQ `.Contains()` with string `.ToLower()` creates unnecessary string allocations.
**Action:** Use `ReadOnlySpan<char>` with `StringComparison.OrdinalIgnoreCase` in explicit loops for zero-allocation performance.
