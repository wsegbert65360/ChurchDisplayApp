## 2024-05-18 - Zero-Allocation File Extension Checks
**Learning:** `Path.GetExtension(string).ToLower()` allocates new strings. In high-frequency code paths (like the live preview timer running every 42ms), this causes unnecessary GC pressure.
**Action:** Use `Path.GetExtension(string.AsSpan())` and `ReadOnlySpan<char>.Equals(string, StringComparison.OrdinalIgnoreCase)` for zero-allocation extension checking.
