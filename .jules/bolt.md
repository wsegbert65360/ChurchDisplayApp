## 2024-06-15 - Zero-Allocation String Matching in MediaConstants
**Learning:** High-frequency paths checking file extensions using `System.IO.Path.GetExtension(filePath).ToLower()` and LINQ `.Contains()` create unnecessary string allocations and boxing, which increases GC pressure.
**Action:** Replace LINQ `.Contains()` with explicit `foreach` loops using `ReadOnlySpan<char>` (via `filePath.AsSpan()`) and `StringComparison.OrdinalIgnoreCase` to achieve zero-allocation string matching in C# WPF models.
