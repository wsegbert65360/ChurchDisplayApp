## 2026-05-26 - Adding accessibility labels to WPF components
 **Learning:** In WPF applications, icon-only buttons and interactive elements like Sliders and GridSplitters often lack text labels, making them inaccessible to screen readers.
 **Action:** Always add `AutomationProperties.Name` to provide semantic labels for screen readers and `ToolTip`s to clarify purpose for sighted users.
