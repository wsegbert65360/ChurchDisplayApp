## 2024-06-17 - Essential AutomationProperties for Standalone Interactive UI Elements
**Learning:** In WPF, standalone interactive elements such as Sliders (e.g., volume, font size, progress) and GridSplitters, as well as icon-only buttons, are not properly announced to screen readers by default. Relying solely on the `ToolTip` property is insufficient for accessibility compliance.
**Action:** Always explicitly define `AutomationProperties.Name` on standalone Sliders, GridSplitters, and icon-only buttons to ensure their purpose is correctly conveyed to screen reader users.
