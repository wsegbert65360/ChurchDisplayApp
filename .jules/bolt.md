## 2024-06-30 - Zero-allocation string matching
**Learning:** Checking file extensions using `.ToLower()` and LINQ `.Contains()` forces string allocations and boxing on every call, creating unnecessary GC pressure in high-frequency paths.
**Action:** Use `.AsSpan()` with `System.IO.Path.GetExtension()` and explicit loops using `StringComparison.OrdinalIgnoreCase` to maintain zero-allocation optimization.
