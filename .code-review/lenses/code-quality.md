# Code Quality

Evaluates whether the diff is correct and safe for a single-monitor-sensitive WPF media
projection app: native VLC resources must not leak or double-dispose, the UI thread must not
be violated, async loads must not show stale state, and persisted JSON / remote input must be
validated. The cost of a regression here is a freeze, a native AccessViolation, or wrong/lost
media projected during a live church service — not a flaky unit test.

## Criteria

- **UI-thread correctness.** WPF elements (the live output `VideoView`, `Image`, sliders,
  the playlist `ObservableCollection`) may only be touched on the dispatcher thread. VLC
  raises `MediaEnded` / `MediaError` / position events on background threads — any handler
  in the diff that reads or writes UI state must marshal via `Dispatcher.Invoke`/`BeginInvoke`.
  Flag a callback that mutates `_isPlaying`, `_currentMediaPath`, the ViewModel, or the
  preview image without marshalling.
- **Native resource lifecycle (LibVLC / MediaPlayer / Media).** `LibVLC`, `MediaPlayer`, and
  `Media` wrap native handles and must be `Dispose()`d exactly once. Flag: disposing without
  null-check, disposing in a branch that another path can reach again, creating a `Media`/
  `MediaPlayer` without assigning it to a field that gets disposed on `Stop`/`Blank`/shutdown,
  or a new `LibVLC` created on the cancelled-init path that isn't disposed. Double-dispose or
  a leak is a blocker (native crash or handle exhaustion).
- **Stale async image loads.** `LiveOutputWindow` uses `_mediaVersion` so a slow `Task.Run`
  image read can't overwrite newer state after `Stop`/`Blank`. Any new async display path in
  the diff must (a) capture the version before the await, (b) bump the version on `Stop`/
  `Blank`/new-media, and (c) re-check the captured version after the await before applying.
  Flag a missing version bump or a missing post-await guard.
- **Volume clamping.** Every path that sets a volume (remote `/api/volume/{level}`, per-item
  `PlaylistItem.Volume`, the operator slider, the service-slot override) must clamp to
  `0.0–1.0` before it reaches `_targetVolume` / VLC. Out-of-range input to VLC is a blocker.
- **Remote controller boundary.** `RemoteControlServer` endpoints must check that the media
  engine is ready before touching VLC, and must return `503` during shutdown. Flag a new
  endpoint (or a changed existing one) that calls into `IDisplayController`/`MediaControlService`
  without the readiness gate or the shutdown gate.
- **Cancellation & shutdown.** New `CancellationTokenSource`s must be cancelled/disposed on
  shutdown (mirror `AmenResolveService` / VLC init). Critical persisted state must go out via
  `AppSettings.SaveImmediate(force: true)` on shutdown — relying only on the debounced
  `Save()` for shutdown-critical data is a blocker.
- **Playlist load transactionality.** `PlaylistManager` parses a new playlist before swapping
  into the `ObservableCollection` so a failed load preserves the current list. Flag a diff that
  mutates the live collection before the parse/validate succeeds.
- **Blocking on the UI thread.** Flag `.Result`, `.Wait()`, or `Thread.Sleep` on a path that
  can run on the dispatcher thread — these deadlock or freeze the projection. Use `await`
  (and `ConfigureAwait` where appropriate).
- **`async void`.** Only acceptable for a top-level WPF event handler. Flag `async void`
  in services/non-handler methods (unobserved exceptions crash the process) and any `await`
  in an `async void` handler without a try/catch.
- **Nullable correctness.** The project compiles with `<Nullable>enable</Nullable>`. Flag a
  dereference of a `?`-annotated or compiler-inferred maybe-null without a guard, and `!`
  null-forgiving suppressions added without an obvious local invariant (a `!` to silence the
  compiler is a warning).
- **JSON schema stability.** `settings.json` keys and the playlist v2 schema
  (`{ "version": 2, "items": [ { "fullPath", "volume" } ] }`) are persisted on user machines.
  Flag renaming/renumbering a serialized property (e.g. the legacy
  `LastPlaylistSaveDirectory` key) without a migration, or removing `[JsonPropertyName]`
  compatibility — silent settings loss is a blocker.
- **Exception handling.** The established pattern is `catch (Exception ex) { Log.Warning(ex, "..."); }`.
  Flag empty `catch { }`, `catch (Exception) { }` with no log, or catching and rethrowing as
  a new exception that hides the original stack.

## Tools

- `dotnet build ChurchDisplayApp.csproj -c Debug --nologo -v q`
- `dotnet format ChurchDisplayApp.sln --verify-no-changes --severity warn --verbosity quiet`

> No test project exists in the repo (no xUnit/NUnit/MSTest reference, no `*.Tests` assembly),
> so there is no unit-test command to run. Build = the compile + nullable-flow typecheck;
> `format --verify-no-changes` = non-mutating `.editorconfig`/analyzer checks.

## Severity

- **blocker:** UI-thread violation, native double-dispose/handle leak, missing volume clamp,
  remote endpoint missing the readiness/shutdown gate, blocking call that can run on the UI
  thread, broken persisted JSON schema, lost shutdown-critical settings, playlist mutation
  that breaks load transactionality.
- **warning:** missing stale-load version guard, unguarded nullable deref, unjustified `!`,
  `async void` outside a handler, exception swallowed without a Serilog log.
- **note:** redundant/unsafe cancellation handling, minor defensive-ordering nits that don't
  yet cause a failure.
