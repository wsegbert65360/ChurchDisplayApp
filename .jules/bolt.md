## 2024-05-18 - Avoid unnecessary .ToLower() in hot paths
**Learning:** Using `string.ToLower()` during repetitive file extension checks creates unnecessary string allocations that can increase GC pressure, especially in a media display app that scans directories and checks files often.
**Action:** Use `Path.GetExtension(filePath.AsSpan())` and `StringComparison.OrdinalIgnoreCase` to achieve zero-allocation comparisons in high-frequency validation paths. Use `.ToLowerInvariant()` for strings that need to be cached and bound to UI properties.
