## 2024-12-05 - Zero-Allocation String Matching in C#
**Learning:** High-frequency paths checking strings against collections (like `ImageExtensions.Contains(ext)`) cause unnecessary memory allocations through `Path.GetExtension(filePath).ToLower()` (creating a new string) and LINQ enumerators.
**Action:** Replace LINQ `Contains` with explicit loops and use `Path.GetExtension(filePath.AsSpan())` alongside `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization, significantly reducing GC pressure.
