## 2024-05-18 - Replacing string manipulation and `.ToLower()` with ReadOnlySpan and OrdinalIgnoreCase
**Learning:** Replaced string allocations and `.ToLower()` with zero-allocation `ReadOnlySpan<char>` and `OrdinalIgnoreCase` string comparisons when comparing file extension checking.
**Action:** When inspecting high-frequency paths (like loops validating a large playlist), replace string allocations by using `ReadOnlySpan<char>` and `OrdinalIgnoreCase`.
