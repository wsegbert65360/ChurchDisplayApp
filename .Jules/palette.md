## 2024-05-13 - Add AutomationProperties to Standalone Controls
**Learning:** In WPF, standalone controls that do not have explicit visible text labels (such as `Slider`, icon-only `Button`, and `GridSplitter`) are inaccessible to screen readers without explicit names.
**Action:** When adding or modifying interactive controls in WPF applications, ensure that `AutomationProperties.Name` is provided for icon-only buttons or ambiguous sliders to support screen readers, alongside `ToolTip` for sighted users.
