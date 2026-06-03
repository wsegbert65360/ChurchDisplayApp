## 2024-05-19 - WPF Standalone Control Accessibility
**Learning:** In WPF, screen readers do not always announce `ToolTip` content for standalone interactive elements like `Slider` and `GridSplitter`. While `ToolTip` is good for visual users on mouse hover, `AutomationProperties.Name` is explicitly required for screen readers to convey the purpose of these generic controls.
**Action:** When adding or reviewing interactive controls (Sliders, GridSplitters, icon-only buttons), ensure both `ToolTip` (for sighted users) and `AutomationProperties.Name` (for screen readers) are set.
