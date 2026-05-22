## 2026-05-22 - Missing Screen Reader Labels on WPF Sliders
**Learning:** Standalone interactive elements like `Slider` in WPF without explicit text labels are invisible or unintelligible to screen readers without ARIA-equivalent properties.
**Action:** Always ensure that interactive components that are only visually identified by icons or context (like Volume, Progress, or Font Size sliders) include `AutomationProperties.Name` to provide a clear label for screen readers, and consider adding `ToolTip` properties for sighted users who need context.
