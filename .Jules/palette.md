## 2024-05-24 - Slider Accessibility in WPF
**Learning:** Sliders in WPF often lack visual labels directly associated with them via `Target`. Screen readers rely on `AutomationProperties.Name` to announce the slider's purpose, and sighted users benefit from a `ToolTip` when the slider context is ambiguous (like a standalone progress or font-size slider).
**Action:** Always pair `AutomationProperties.Name` and `ToolTip` on standalone `Slider` controls in WPF.
