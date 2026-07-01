## 2024-05-24 - [WPF Screen Reader Accessibility for Sliders and Splitters]
**Learning:** In WPF, `ToolTip` properties do not automatically provide accessible names to screen readers for standalone interactive components like Sliders and GridSplitters.
**Action:** Always explicitly define `AutomationProperties.Name` alongside `ToolTip` for Sliders, GridSplitters, and icon-only buttons to ensure screen reader compliance.
