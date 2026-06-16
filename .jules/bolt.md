## 2024-05-24 - Zero-Allocation String Matching in C#
**Learning:** Using LINQ `.Contains()` for checking string presence against string arrays forces string allocations and boxing, causing GC pressure in high-frequency C# paths.
**Action:** Replace `.Contains()` with explicit loops using `ReadOnlySpan<char>` (via `filePath.AsSpan()`) and `StringComparison.OrdinalIgnoreCase` to achieve zero-allocation validation.
