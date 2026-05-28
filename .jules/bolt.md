## 2024-05-24 - Zero-allocation Path Evaluation
**Learning:** Checking file extensions with `Path.GetExtension(filePath).ToLower().Contains(ext)` causes multiple string allocations per check, which adds up when processing large numbers of files (e.g. during playlist validation or media scans).
**Action:** Use `Path.GetExtension(filePath.AsSpan())` and iterate with `ext.Equals(extension, StringComparison.OrdinalIgnoreCase)` to achieve zero-allocation path evaluation.
