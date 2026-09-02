# AGENTS.md

## What this is

Pressio is a cross-platform blood-pressure tracking app: Avalonia UI + .NET 10 + C# + ReactiveUI + SQLite. The product spec is `PRD.md` (in Portuguese) — read it before changing product behavior.

## Project layout & solution format

- Solution file is `Pressio.slnx` (the new XML `.slnx` format, **not** classic `.sln`). It is valid for `dotnet` CLI, but may not open in older tooling.
- All real code (views, viewmodels, models, services, styling) lives in the `Pressio/` project (`net10.0`). The other projects are thin launch hosts that only reference `Pressio`:
  - `Pressio.Desktop` — `Program.cs`, the main local-dev entry. `dotnet run --project Pressio.Desktop`.
  - `Pressio.Android`, `Pressio.iOS`, `Pressio.Browser` — platform hosts; require the respective .NET workloads (not installed by default on a dev machine).
- UI text and identifiers are in **Portuguese** (e.g. `"Registrar pressão"`, `"Claro"/"Escuro"`, `"Índigo"`). Keep new strings and values in Portuguese to match.

## Commands

```sh
dotnet build Pressio.Desktop/Pressio.Desktop.csproj   # fast core check
dotnet run --project Pressio.Desktop                   # run the desktop app
dotnet build                                           # whole solution
```

There is **no test project** yet; do not assume a test runner exists.

## Package management

Versions are centralized in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`). **Never** add a `Version` directly in a `.csproj`; add it to `Directory.Packages.props` instead.

The Avalonia packages (Avalonia, Themes.Fluent, Fonts.Inter, Desktop, iOS, Browser, Android) **must all share the same version** — the file has a comment to keep them in sync. When bumping, bump them together. ReactiveUI.Avalonia and Microsoft.Data.Sqlite are versioned separately.

## Build/CI gotchas

- The build emits a reproducible `NU1903` high-severity warning about `SQLitePCLRaw.lib.e_sqlite3 2.1.11`. This is currently expected — not an error. Fix/triage before treating builds as clean.
- iOS Debug uses `<UseInterpreter>true</UseInterpreter>` to avoid premature AOT loading; Release keeps `MtouchNoSymbolStrip` as a workaround for Xcode 26.6. Don't remove these without reason.

## Persistence

- SQLite via raw `Microsoft.Data.Sqlite` (no EF Core/Dapper, despite what `PRD.md` suggests).
- DB file: `%LocalAppData%\Pressio\pressio.db`. Dates are stored as ISO-8601 strings; UTC on write, `.ToLocalTime()` on read.
- Both `MeasurementRepository` and `SettingsRepository` open the same DB file, each computing its own connection string.
- `MeasurementRepository.Initialize()` creates tables with `CREATE TABLE IF NOT EXISTS` plus a hand-rolled migration that checks `PRAGMA table_info` before `ALTER TABLE`. If you add a column, follow this same pattern (the existing checks detect `PatientId` and `Context`; both are added with an explicit `ALTER TABLE` when missing).
- `BloodPressureMeasurement` stores a `[Flags] MeasurementContext` (stress, pain, fever, exercise, caffeine, alcohol, smoking, poor sleep, missed medication, diet, symptoms, other) as an `INTEGER` bitmask in the `Context` column. `MeasurementContextInfo` maps values to Portuguese labels (`Describe`/`AllContexts`); the measurement form renders toggle chips from `ContextOption` (`MainViewModel.ContextOptions`).
- `SettingsRepository` stores a simple key/value `Settings (Key TEXT PRIMARY KEY, Value TEXT)` table (used for theme + primary color via `SaveAppearance`). Appearance/primary color are loaded in `MainViewModel.LoadAppSettings()`, applied via `App.ApplyAppearance`, and applied+persisted **only** on confirming the Settings dialog (`SaveSettingsCommand`). Selecting a theme or a color swatch does NOT change the app before that (no live global preview); the selected color is indicated by a check inside the swatch via the `ColorMatchConverter`.
- Reusable looks live in `App.axaml`: button style classes (`primary-button`, `secondary-button`, `danger-button`, `icon-button`, `color-swatch`, `toggle-chip`) plus `ControlTheme`s like `PressioPrimaryButton`/`PressioColorSwatch`/`PressioIconButton`/`PressioToggleChip`. Dialogs and cards should use the `Pressio*` `DynamicResource` brushes (surface/text/muted/primary/border/banner) so they adapt to light/dark — avoid hardcoded hex for surfaces/text.
- Deletes of patients and measurements are gated behind a confirmation overlay driven by `MainViewModel` (`ConfirmDeleteCommand`/`CancelDeleteCommand`, `IsConfirmDialogVisible`).

## Blood pressure parsing (`BloodPressureParser`)

- Separators are `/`, space, `x`, `X` (matches `PRD.md`). Normalizes the shorthand `13/8` → `130/80` by multiplying by 10 when the value is `< 30`. Ranges: systolic 50–300, diastolic 30–200. Error/validation messages are user-friendly Portuguese.

## Architecture notes

- ReactiveUI MVVM. Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault=true`), so `x:DataType` / strong-typed bindings are expected in XAML.
- Views are located from viewmodels by naming convention in `ViewLocator.cs` (a `FooViewModel` resolves to a `FooView`).
- `MainViewModel` is constructed with `isMobileLayout`; desktop shows forms as centered **modal dialogs**, mobile/tablet (<11") shows them as **full-screen pages**. Platform hosts wire this up in `App.OnFrameworkInitializationCompleted()`.
- Theming/resources live in `App.axaml` (brush keys like `PressioPrimaryBrush`) and are toggled at runtime by `App.ApplyAppearance(appearance, primaryColor)` in `App.axaml.cs`. Reuse the `Pressio*` brushes and button classes instead of hardcoding colors in views. View-level converters live in `Pressio/Converters/` (e.g. `ColorMatchConverter`).
