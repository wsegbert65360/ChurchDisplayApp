## 2024-06-25 - Prevent String Allocations with Span<T> in Path Checks
**Learning:** Using `Path.GetExtension(string).ToLower()` combined with LINQ `.Contains()` creates unnecessary string allocations and garbage collection pressure, especially when checking extensions frequently (e.g. during file enumerations or list updates).
**Action:** Use `Path.GetExtension(string.AsSpan())` along with a simple loop comparing the span against predefined extension strings using `StringComparison.OrdinalIgnoreCase` to achieve zero allocations.
