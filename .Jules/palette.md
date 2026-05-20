## 2026-05-20 - Accessibility for WPF Sliders
**Learning:** In WPF applications, standard components like `Slider` that operate without explicit visual text labels require `AutomationProperties.Name` for screen readers (equivalent to ARIA labels in HTML) to provide necessary context.
**Action:** Always verify if standalone interactive components like `Slider` have either `AutomationProperties.Name` or `AutomationProperties.LabeledBy` defined when making accessibility improvements in XAML.
