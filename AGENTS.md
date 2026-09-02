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
- Schema is created in `MeasurementRepository.Initialize()` with `CREATE TABLE IF NOT EXISTS` plus a hand-rolled migration that checks `PRAGMA table_info` before `ALTER TABLE`. If you add a column, follow this same pattern.

## Blood pressure parsing (`BloodPressureParser`)

- Separators are `/`, `x`, `X` only — note **space is NOT** a separator despite `PRD.md`. Normalizes the shorthand `13/8` → `130/80` by multiplying by 10 when the value is `< 30`. Ranges: systolic 50–300, diastolic 30–200. Error/validation messages are user-friendly Portuguese.

## Architecture notes

- ReactiveUI MVVM. Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault=true`), so `x:DataType` / strong-typed bindings are expected in XAML.
- Views are located from viewmodels by naming convention in `ViewLocator.cs` (a `FooViewModel` resolves to a `FooView`).
- `MainViewModel` is constructed with `isMobileLayout`; desktop shows forms as centered **modal dialogs**, mobile/tablet (<11") shows them as **full-screen pages**. Platform hosts wire this up in `App.OnFrameworkInitializationCompleted()`.
- Theming/resources live in `App.axaml` (brush keys like `PressioPrimaryBrush`, button style classes `primary-button`, `secondary-button`, `danger-button`, `top-action-button`, `danger-icon-button`) and are toggled at runtime by `App.ApplyAppearance(appearance, primaryColor)` in `App.axaml.cs`. Reuse these instead of hardcoding colors in views.
