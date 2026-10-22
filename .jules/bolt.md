## 2025-05-18 - Materializing Deferred LINQ Queries in Profilers
**Learning:** Iterating deferred LINQ queries multiple times (e.g., `OrderBy`) triggers re-sorting and re-allocation on each loop pass.
**Action:** Always call `.ToArray()` or `.ToList()` on ordered LINQ expressions when the result is enumerated multiple times.
