## 2025-05-18 - Parallel File Validation during Drag-and-Drop
 **Learning:** Validating large sets of dragged-and-dropped files sequentially with `File.Exists` blocks the UI thread during drag operations. Using PLINQ (`AsParallel().AsOrdered()`) reduces file I/O latency on the main thread before inserting items into the observable collection.
 **Action:** Parallelize file validation for multi-file operations before performing UI-bound collection mutations.
