## 2024-06-08 - Zero-allocation string comparisons
**Learning:** Using `Path.GetExtension(filePath).ToLower()` combined with LINQ `.Contains()` creates unnecessary string allocations and puts pressure on the Garbage Collector, particularly when used in high-frequency paths like file validation routines.
**Action:** Utilize zero-allocation techniques such as `ReadOnlySpan<char>` via `filePath.AsSpan()` and `StringComparison.OrdinalIgnoreCase` within a `foreach` loop to perform string comparisons without allocating new string objects on the heap.
