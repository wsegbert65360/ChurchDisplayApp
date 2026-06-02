## 2024-06-02 - String Allocations in File Extension Checks
**Learning:** `Path.GetExtension(filePath).ToLower()` allocates two strings for every check. In a WPF app handling potentially large playlists or rapid media checks, this GC pressure adds up. Using `ReadOnlySpan<char>` with `StringComparison.OrdinalIgnoreCase` eliminates these allocations entirely for simple type checking.
**Action:** Always prefer `ReadOnlySpan<char>` and `OrdinalIgnoreCase` for file extension checks in high-frequency methods.
