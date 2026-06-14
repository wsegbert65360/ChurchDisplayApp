## 2024-05-24 - Accessibility: Added AutomationProperties.Name to standalone UI elements
**Learning:** Standalone interactive components like Sliders and icon-only buttons need explicit `AutomationProperties.Name` for screen readers in WPF, as ToolTips are insufficient for compliance.
**Action:** Always add `AutomationProperties.Name` to icon-only buttons and sliders to ensure screen reader compatibility in WPF applications.
