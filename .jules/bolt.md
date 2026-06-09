## 2024-05-24 - Zero-Allocation String Matching in High-Frequency Paths
**Learning:** Using LINQ `.Contains()` and `string.ToLower()` in high-frequency checks like file extension matching (e.g., `IsImage`, `IsVideo`) causes continuous string allocations, leading to unnecessary GC pressure and potential performance bottlenecks.
**Action:** Always prefer zero-allocation techniques using `ReadOnlySpan<char>` (e.g., `Path.GetExtension(filePath.AsSpan())`) and `StringComparison.OrdinalIgnoreCase` within explicit loops instead of LINQ for high-frequency string matching against fixed collections.
