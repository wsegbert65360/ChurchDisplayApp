## 2024-05-18 - Essential WPF Accessibility Properties
**Learning:** ToolTips are not sufficient for full accessibility compliance. Standalone interactive components (Sliders, GridSplitters) and icon-only buttons require explicit `AutomationProperties.Name` for screen readers.
**Action:** Always add `AutomationProperties.Name` to Sliders, GridSplitters, and icon-only buttons in WPF XAML, and include `ToolTip` properties for visual clarity on ambiguous UI elements.
