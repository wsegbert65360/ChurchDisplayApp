## 2024-05-24 - Zero-Allocation String Matching in Media Constants
**Learning:** Using LINQ `.Contains()` and string `.ToLower()` on paths in high-frequency validation checks (`IsImage`, `IsVideo`) causes unnecessary allocations. By using `Path.GetExtension(filePath.AsSpan())` and `StringComparison.OrdinalIgnoreCase`, we can achieve zero-allocation matching while preserving case-insensitivity and gaining built-in null safety.
**Action:** Always favor `ReadOnlySpan<char>` loops over LINQ array matching for path and extension checking in high-frequency C# code.
