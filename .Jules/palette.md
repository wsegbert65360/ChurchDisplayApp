## 2026-05-19 - WPF Slider Accessibility
**Learning:** In WPF applications, standalone interactive components like `Slider`s that lack explicit text labels are inaccessible to screen readers without UI automation properties.
**Action:** When working on WPF UX/accessibility improvements, always check for interactive elements without text and ensure they have `AutomationProperties.Name` attributes defined to serve as the screen reader label, similar to `aria-label` on the web. Additionally, ensure they have `ToolTip` properties set if they are ambiguous.
