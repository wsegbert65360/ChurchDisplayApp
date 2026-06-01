## 2024-05-19 - WPF Screen Reader Accessibility via AutomationProperties
**Learning:** Standard WPF ToolTips are not fully read by some screen readers on complex controls like Sliders or GridSplitters without an explicit Automation property attached. Adding `AutomationProperties.Name` is the XAML equivalent of `aria-label` for ensuring complete interactive element accessibility.
**Action:** Always pair `ToolTip` (for sighted mouse users) with `AutomationProperties.Name` (for screen readers) on icon-only buttons and standalone interactive inputs (Sliders, Splitters) in WPF XAML code.
