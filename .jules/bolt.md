## 2024-06-03 - Zero-Allocation Path Extension Checks
**Learning:** Checking file extensions with `System.IO.Path.GetExtension(string).ToLower()` allocates new string objects every time, causing GC pressure in high-frequency scenarios like media list generation or UI binding validations.
**Action:** Use `Path.GetExtension(filePath.AsSpan())` in combination with `.Equals(allowed.AsSpan(), StringComparison.OrdinalIgnoreCase)` to perform zero-allocation string matching for critical UI and Media path verification loops in WPF applications.
