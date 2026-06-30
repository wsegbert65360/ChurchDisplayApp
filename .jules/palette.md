## 2025-02-05 - [WPF Accessibility for Standalone Components]
**Learning:** In WPF, standalone interactive components like Sliders and icon-only buttons require explicit `AutomationProperties.Name` for screen readers to announce them correctly, as `ToolTip` alone is not sufficient.
**Action:** Always add `AutomationProperties.Name` to any icon-only `Button` and interactive controls like `Slider` to ensure they are accessible.
