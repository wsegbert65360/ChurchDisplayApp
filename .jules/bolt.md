## 2024-03-24 - Zero-Allocation File Extension Checks
**Learning:** In high-frequency UI updates or playlist validations, parsing file extensions with `System.IO.Path.GetExtension(filePath).ToLower()` allocates unnecessary strings and contributes to GC pressure. Furthermore, using LINQ `.Contains()` on string arrays adds further overhead.
**Action:** Replace string allocations with `ReadOnlySpan<char>` by using `filePath.AsSpan()` and compare extensions using an iterative `StringComparison.OrdinalIgnoreCase` against pre-defined extension string constants.
