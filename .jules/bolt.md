## 2024-05-14 - WPF DataTrigger String Requirement
**Learning:** While zero-allocation Span<char> checking is highly effective for internal validations like MediaConstants, properties directly bound to WPF DataTriggers in models (e.g., PlaylistItem.Extension) must still be stored as materialized strings. DataTriggers match on specific string values.
**Action:** When applying zero-allocation span checks to a WPF codebase, always trace the property usage. If it hits an XAML DataTrigger, materialize the string using culturally-agnostic methods like ToLowerInvariant().
