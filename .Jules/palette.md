## 2024-05-18 - WPF Accessibility Patterns
**Learning:** In WPF applications, `AutomationProperties.Name` serves the equivalent purpose of ARIA labels in web interfaces. Elements like Sliders, GridSplitters, and Icon-only Buttons often lack intrinsic accessible names.
**Action:** When adding accessibility to WPF interfaces, ensure controls without text content (Sliders, icon buttons) receive `AutomationProperties.Name` attributes that accurately describe their function to screen readers.
