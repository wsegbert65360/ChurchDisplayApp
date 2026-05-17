## 2026-05-17 - Adding AutomationProperties.Name to Sliders
**Learning:** WPF Sliders often act as standalone interactive components without explicit text labels physically bound to them. While tooltips help sighted users, screen readers need `AutomationProperties.Name` attached directly to the Slider to announce its purpose.
**Action:** Always add `AutomationProperties.Name` to interactive inputs like Sliders that rely on surrounding text or icons for context.
