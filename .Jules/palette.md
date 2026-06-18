## 2024-05-14 - Accessibility for WPF Sliders and GridSplitters
**Learning:** Standalone interactive elements like Sliders and GridSplitters in WPF require explicit `AutomationProperties.Name` for screen reader accessibility; `ToolTip` is not sufficient.
**Action:** Always add `AutomationProperties.Name` to these elements, ensuring their purpose is conveyed to assistive technologies.
