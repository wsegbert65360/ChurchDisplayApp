
## 2024-06-06 - Test Files in Root Break Builds
**Learning:** In modern C# SDK-style projects, any stray `.cs` file in the directory structure (like temporary test scripts created in the root) is automatically compiled into the project assembly.
**Action:** Always clean up temporary test scripts (e.g., `test_span.cs`) before attempting to build the main project or finalizing a commit to avoid polluting the production build or causing namespace collisions.
