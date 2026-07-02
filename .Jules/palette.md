## 2026-07-02 - Explicit ARIA Labels for WPF
**Learning:** In WPF applications, setting a ToolTip on interactive elements (like icon-only buttons, sliders, and grid splitters) is not sufficient for screen reader accessibility compliance. They require explicit AutomationProperties.Name attributes to be properly announced.
**Action:** Always add AutomationProperties.Name to standalone interactive components and icon-only buttons in XAML to ensure keyboard and screen reader accessibility.
