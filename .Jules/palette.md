## 2024-05-24 - Missing Screen Reader Context on Media Controls
**Learning:** In WPF, icon-only media transport buttons (Play, Pause, Stop) and standalone interactive components (Sliders, GridSplitters) are often announced vaguely by screen readers because ToolTips alone are not sufficient for compliance.
**Action:** Always add `AutomationProperties.Name` for screen readers to icon-only buttons, Sliders, and GridSplitters, and provide clear `ToolTip` text for visual users on ambiguously styled controls.
