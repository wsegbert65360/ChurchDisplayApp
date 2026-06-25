
## 2024-05-18 - Zero-allocation file extension checking
**Learning:** In C#, checking file extensions with `.ToLower()` and LINQ `.Contains()` forces unnecessary string allocations and boxing on high-frequency paths.
**Action:** Use `System.IO.Path.GetExtension(filePath.AsSpan())` and explicitly iterate over allowed extensions using `ext.Equals(e, StringComparison.OrdinalIgnoreCase)` to eliminate allocations.
