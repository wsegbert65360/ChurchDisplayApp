## 2024-05-24 - Zero-Allocation String Matching Pattern
**Learning:** High-frequency file type checks using `.ToLower()` string allocations and LINQ `.Contains()` create unnecessary garbage collection pressure and boxing overhead in this architecture.
**Action:** Always use `ReadOnlySpan<char>` (e.g., `Path.GetExtension(filePath.AsSpan())`) combined with `StringComparison.OrdinalIgnoreCase` and explicit loops instead of LINQ for string matching against collections in hot paths to ensure zero-allocation operations.
