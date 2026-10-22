## 2025-05-18 - Asynchronous Process Execution for Firewall Rule Checks

**Learning:** Synchronous process creation (`Process.Start`) coupled with `proc.WaitForExit()` and `proc.StandardOutput.ReadToEnd()` on background tasks during application startup blocks the execution thread for up to 3 seconds per attempt. Using `proc.WaitForExitAsync(cancellationToken)` and `proc.StandardOutput.ReadToEndAsync(cancellationToken)` keeps process execution non-blocking without changing control flow.
**Action:** When invoking external CLI utilities (e.g. `netsh`), always use `Process.WaitForExitAsync` and `ReadToEndAsync` with a `CancellationTokenSource` timeout to avoid thread starvation or UI/startup lag.
