## 2024-05-18 - Ensure AutomationProperties.Name on Standalone Interactive Components
**Learning:** ToolTips alone are not sufficient for screen reader compliance in WPF. Standalone interactive components (e.g., Sliders, GridSplitters) and icon-only buttons are silently skipped by screen readers unless explicitly tagged.
**Action:** Always add explicit `AutomationProperties.Name` properties (and ideally `ToolTip` for sighted users) to standalone interactive controls and buttons without text content.
