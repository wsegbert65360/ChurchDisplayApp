## 2024-05-21 - Essential Screen Reader Accessibility for WPF Interactive Controls
**Learning:** In WPF, interactive controls like `Slider` without adjacent text labels are completely invisible or uninformative to screen readers without explicitly setting `AutomationProperties.Name`.
**Action:** Always ensure `AutomationProperties.Name` is applied to standalone interactive elements in WPF applications to provide an equivalent experience to HTML's `aria-label`.
