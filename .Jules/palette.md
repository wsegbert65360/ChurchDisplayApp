## 2026-06-12 - [WPF Accessibility for Standalone Elements]
**Learning:** Standalone interactive elements (like Sliders and GridSplitters) require explicit `AutomationProperties.Name` in WPF for screen readers to correctly interpret them. `ToolTip` alone is insufficient for compliance.
**Action:** Always verify that interactive elements without text content have an `AutomationProperties.Name` explicitly defined.
