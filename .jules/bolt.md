## 2024-05-16 - Async File Reading is not Async Decoding
**Learning:** Even if `File.ReadAllBytesAsync()` is used in WPF, synchronous image decoding via `BitmapImage.EndInit()` still blocks the UI thread.
**Action:** Move both I/O and `BitmapImage` decoding to a background thread using `Task.Run` and make it cross-thread accessible by calling `.Freeze()` on the resulting bitmap.
