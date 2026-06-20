## 2025-01-20 - [Zero-Allocation String Matching]
**Learning:** Found string allocations and LINQ `.Contains()` calls during frequent operations like checking file extensions in `Models/MediaConstants.cs` (which may happen repeatedly). In C# high-frequency paths, avoiding `.ToLower()` and LINQ `.Contains()` against arrays prevents unnecessary string allocations and reduces GC pressure.
**Action:** Use `Path.GetExtension(filePath.AsSpan())` and a manual loop with `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.
