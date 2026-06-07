## 2024-05-24 - WPF Accessibility for Standalone Components
**Learning:** ToolTips are not sufficient for screen reader compliance in WPF. Standalone interactive components (like Sliders and GridSplitters) and icon-only buttons require explicit `AutomationProperties.Name` attributes to be accessible to screen readers.
**Action:** Always add `AutomationProperties.Name` to Sliders, GridSplitters, and icon-only UI components in WPF XAML.
