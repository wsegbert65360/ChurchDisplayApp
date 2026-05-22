## 2024-05-19 - String Interpolation vs TimeSpan format
**Learning:** In C#, using string interpolation with format strings (`$"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}"`) can be slow and causes memory allocations. Passing format directly to `TimeSpan.ToString(@"mm\:ss")` is slightly better, but creating our own zero-allocation string using custom formatting with `Span<char>` or just a simple manual approach (if possible) is much better. In a WPF environment string format in timer updates are called very frequently.
**Action:** When formatting `TimeSpan` for UI elements (like in `ViewModels/MainViewModel.cs: FormatTime`), use `TimeSpan.ToString(@"mm\:ss")` if hours are not needed, or investigate custom span formatting if possible to avoid allocations in frequent update paths. Actually, `TimeSpan.ToString(@"mm\:ss")` performs similarly, but I should look at `Models/MediaConstants.cs` for file extensions check first.

## 2024-05-19 - File Extension parsing and zero-allocation check
**Learning:** `Path.GetExtension(string).ToLower()` allocates two strings every time it is called. Using `Path.GetExtension(ReadOnlySpan<char>)` and `StringComparison.OrdinalIgnoreCase` eliminates allocations and is >10x faster for file extension checking.
**Action:** When validating file types (like in `Models/MediaConstants.cs`), use zero-allocation techniques (`ReadOnlySpan<char>`, `OrdinalIgnoreCase`) instead of `.ToLower()` and LINQ `.Contains()` which are relatively expensive and allocate memory.

## 2024-05-19 - TimeSpan Formatting and zero-allocation with string.Create
**Learning:** For extremely frequent string formatting (like updating the UI timer every 200ms), standard string interpolation (`$"..."`) and `ToString()` cause continuous GC pressure. `string.Create` allows zero-allocation custom formatting by writing directly to a `Span<char>`. However, you must pass the state into `string.Create` rather than capturing it in the lambda closure to fully avoid allocations.
**Action:** Use `string.Create` for high-frequency formatting operations where the output length is predictable, like `MM:SS` timer strings, to keep GC pressure low in WPF applications.
