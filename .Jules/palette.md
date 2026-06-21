## 2024-05-15 - Interactive Control Accessibility in WPF
**Learning:** For WPF accessibility, standalone interactive components like Sliders, GridSplitters, and icon-only buttons require explicit `AutomationProperties.Name` for screen readers to properly announce them, as standard ToolTips alone are not sufficient for full accessibility compliance.
**Action:** Always ensure that icon-only `Button`, `Slider`, and `GridSplitter` elements include a descriptive `AutomationProperties.Name` attribute to provide proper semantic context for assistive technologies.
