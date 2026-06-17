## 2024-10-24 - Zero-allocation extension checking
**Learning:** In high-frequency paths, `System.IO.Path.GetExtension(filePath).ToLower()` followed by LINQ `.Contains()` allocates multiple strings.
**Action:** Use `Path.GetExtension(filePath.AsSpan())` and iterate with explicit loops and `MemoryExtensions.Equals(extSpan, ext, StringComparison.OrdinalIgnoreCase)` to maintain zero-allocation optimization.
