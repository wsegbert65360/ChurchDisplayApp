## 2026-05-27 - Adding accessibility names to interactive controls
**Learning:** Standalone WPF input elements like `Slider` and `GridSplitter` require explicit `AutomationProperties.Name` attributes so screen readers can identify them, as they don't inherently possess text labels like buttons or textblocks do. ToolTips alone are not sufficient for full accessibility compliance.
**Action:** Always check standalone interactive components (Sliders, GridSplitters, icon-only buttons) for `AutomationProperties.Name` to ensure proper screen reader support.
