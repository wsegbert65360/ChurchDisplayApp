## 2024-05-18 - Standalone Interactive Elements Accessibility
**Learning:** Standalone interactive elements like WPF Sliders without explicit text labels are inaccessible to screen readers. `AutomationProperties.Name` serves as the WPF equivalent of ARIA labels, providing necessary context for accessibility.
**Action:** Always add `AutomationProperties.Name` to standalone interactive controls (e.g. Sliders, icon-only buttons) to ensure screen reader compatibility, and consider adding a `ToolTip` for sighted users.
