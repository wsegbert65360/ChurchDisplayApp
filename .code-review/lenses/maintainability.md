# Maintainability

Evaluates whether the diff keeps the codebase consistent with its established structure and the
`.editorconfig` conventions, so a future reader (or the next diff) isn't fighting style drift,
magic values, or layering leaks. This app is MVVM-with-services with centralized constants and
heavy XML docs; a change that ignores those conventions adds friction for every later edit.

## Criteria

- **Code style per `.editorconfig`.** New/changed code must use: file-scoped namespaces
  (`namespace Foo;`), braces on a new line before `else`/`catch`/`finally` and `all` brace
  rules, 4-space indentation, CRLF, and expression-bodied properties/accessors. `dotnet format`
  in Tools is the source of truth — a `format` finding on a changed file is a warning.
- **Imports.** `ImplicitUsings` is enabled, so `System`, `System.Collections.Generic`,
  `System.IO`, `System.Threading`, `System.Linq`, etc. are implicit. Flag redundant explicit
  `using` directives of implicit namespaces on new files, and out-of-order / non-grouped
  imports (`.editorconfig`: `dotnet_sort_system_directives_first = true`,
  `dotnet_separate_import_directive_groups = false`). The `IMPORTS:` format diagnostic covers
  this — surface it when it touches a changed file.
- **MVVM layering.** ViewModels derive from `BaseViewModel` (INotifyPropertyChanged) and expose
  commands via `RelayCommand`; services must not reach into WPF controls directly except through
  `IDisplayController` or an explicit dispatcher hop. Flag a Service touching UI elements, or a
  ViewModel building WPF controls inline.
- **No magic values.** Ports (`8088`, `8090`), the AppData folder name, the settings filename,
  snapshot dimensions, supported media extensions, and timing constants live in
  `Models/AppConstants.cs` and `Models/MediaConstants.cs`. Flag a new hardcoded port/path/
  extension/dimension/timeout literal that should reference the constants instead of duplicating
  it. Duplicated literal (e.g. another inline `8088`) is a warning.
- **XML documentation.** The codebase documents every public member with `/// <summary>`.
  Flag a new `public` class/method/property/event in the diff without a doc comment.
- **Logging.** Use Serilog (`Serilog.Log.Information` / `Log.Warning(ex, "...")` /
  `Log.Error`). Flag `Console.WriteLine`, `System.Diagnostics.Debug.WriteLine`, or
  `System.Windows.MessageBox.Show` used for diagnostics in non-UI service code.
- **Singletons & DI.** The only intended global singleton is `AppSettings.Current` (Lazy,
  thread-safe). Flag new static mutable singletons, service-locator patterns, or hidden static
  state introduced in services/viewmodels — prefer constructor injection.
- **Volume units.** Volumes are doubles in `0.0–1.0` internally; the UI shows a percentage.
  Keep that boundary explicit — flag code that mixes the two scales (e.g. assigning a `0–100`
  value into a `0.0–1.0` field without dividing).
- **Dead code & churn.** Flag commented-out blocks, unreachable branches, and leftover
  `// TODO` / scratch helpers with no owner. Also flag large reformatting noise mixed into a
  logic change (it obscures the real diff).
- **Settings/playlist migration.** When changing a persisted shape, mirror the
  `MigrateToServiceSlots` / legacy-playlist-array pattern rather than silently dropping old
  fields. Flag a settings/playlist property change with no migration path noted.

## Tools

- `dotnet build ChurchDisplayApp.csproj -c Debug --nologo -v q`
- `dotnet format ChurchDisplayApp.sln --verify-no-changes --severity warn --verbosity quiet`

> Both tools are fast (~6–8s each) and self-terminating. `format --verify-no-changes` does not
> modify files. There is no unit-test suite in the repo, so none is listed.

## Severity

- **blocker:** (rare here) — usually downgraded; reserve for a layering violation that will
  directly cause a correctness regression (e.g. a service writing to WPF controls on a
  background thread).
- **warning:** a `format`/`IMPORTS` finding on a changed file, new `public` member without
  docs, magic-value duplication, wrong logging API, or a volume-scale mix-up.
- **note:** minor style drift, redundant implicit `using`, cosmetic dead code, or noisy
  reformatting mixed into a logic change.
