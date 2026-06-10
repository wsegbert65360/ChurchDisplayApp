## 2024-05-24 - Zero-Allocation File Extension Checks
**Learning:** Checking file extensions against string arrays using `.ToLower()` and LINQ `.Contains()` causes unnecessary string allocations and GC pressure, particularly in paths executed frequently.
**Action:** Use `ReadOnlySpan<char>` with `Path.GetExtension(string.AsSpan())` and iterate through allowed extensions explicitly using `StringComparison.OrdinalIgnoreCase` to maintain a zero-allocation optimization profile.
