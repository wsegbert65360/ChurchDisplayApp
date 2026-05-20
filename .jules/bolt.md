## 2026-05-20 - Zero-Allocation String Comparison in C#
**Learning:** System.IO.Path.GetExtension(string) and string.ToLower() allocate memory. In high-frequency code paths like timer loops, these allocations cause garbage collection pressure, negatively impacting performance.
**Action:** Use `System.IO.Path.GetExtension(string.AsSpan())` combined with `ReadOnlySpan<char>.Equals()` using `StringComparison.OrdinalIgnoreCase` to perform zero-allocation string comparisons.
