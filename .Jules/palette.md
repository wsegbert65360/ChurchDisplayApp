## 2025-02-18 - WPF Accessibility for Standalone and Icon Controls
**Learning:** In WPF, ToolTips on standalone interactive components (like Sliders, GridSplitters) and icon-only buttons are not sufficient for screen reader compliance.
**Action:** Always add explicit `AutomationProperties.Name` attributes to these UI elements to ensure full accessibility for screen readers.