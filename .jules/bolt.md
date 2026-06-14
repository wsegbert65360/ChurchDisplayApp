## 2024-05-24 - Zero-allocation String Matching in Models
**Learning:** LINQ methods (`.Contains()`) over string arrays for extension matching (e.g., `IsImage`, `IsVideo`) force string allocations or boxing in high-frequency validation paths.
**Action:** Replace string allocations with zero-allocation techniques using `ReadOnlySpan<char>` and `StringComparison.OrdinalIgnoreCase` to prevent allocations and reduce GC pressure.
