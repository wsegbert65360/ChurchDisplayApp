## 2024-05-24 - Zero-Allocation String Matching with ReadOnlySpan<char>
**Learning:** In C#, using LINQ `.Contains()` or `.ToLower()` for string matching against collections (e.g. checking file extensions) creates unnecessary allocations. For high-frequency paths, this leads to increased GC pressure.
**Action:** Always use `ReadOnlySpan<char>` (e.g., `Path.GetExtension(filePath.AsSpan())`) and an explicit `foreach` loop with `StringComparison.OrdinalIgnoreCase` to prevent allocations and optimize performance.
