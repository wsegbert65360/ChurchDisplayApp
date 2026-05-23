## 2026-05-23 - Adding accessibility to Slider controls
**Learning:** Found that WPF standalone interactive components like `Slider` frequently lack accessible names when they don't have explicit text labels, which makes them inaccessible to screen readers. Adding `AutomationProperties.Name` alongside `ToolTip` improves both mouse/pointer UX and assistive tech usage.
**Action:** Always verify that interactive inputs (like Sliders) have an `AutomationProperties.Name` set, especially when they only rely on nearby icons or text blocks for context visually.
