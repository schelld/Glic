# Contributing to GLic

## Requirements

- .NET 4.7.2 SDK or later (`dotnet build` targets net472)
- Windows PowerShell 5.1 (for live testing and script analysis)
- `Install-Module PSScriptAnalyzer -Scope CurrentUser` (for the publish gate)
- `Install-Module platyPS -Scope CurrentUser` (for regenerating MAML help)

## Development setup

```powershell
git clone <repo-url>
cd GLic
dotnet build
dotnet test GLic.Tests
powershell -NoProfile -File tools\Assert-CleanModule.ps1
```

For live cmdlet testing, place `glic.json` and `service-account.json` in `%APPDATA%\GLic\`
and run `install.ps1 -Scope CurrentUser -NoVerify` to install the staged module.

## Adding a cmdlet

Follow `GetGlicAppsCmdlet.cs` exactly (see `CLAUDE.md` - Implementing a New Cmdlet):

1. Define an immutable `record XxxRow(...)` with typed properties.
2. Implement `internal static XxxRow BuildRow(...)` as a pure function.
3. Implement `GetRowsAsync` via `Paginator.FetchAllAsync`.
4. Implement `RunAsync`: load config and build clients before any `await`.
5. Register in `GLic.psd1` `CmdletsToExport`.
6. Add unit tests for `BuildRow` field mapping.
7. Add a platyPS markdown stub in `docs/help/` and recompile `en-US\GLic.dll-Help.xml`:
   ```powershell
   Import-Module .\module\GLic\GLic.psd1 -Force
   New-MarkdownHelp -Command Get-GlicXxx -OutputFolder docs\help -Force
   # Fill in the stub, then:
   New-ExternalHelp docs\help -OutputPath en-US -Force
   ```
8. Add default table columns to `GLic.format.ps1xml` if the row type is commonly piped.
9. Add the new DWD scope to `ChromeServiceFactory.Scopes` and both scope tables
   (README.md and CLAUDE.md) if a new API scope is required.

## Pull request checklist

- [ ] `dotnet build` clean
- [ ] `dotnet test GLic.Tests` passes
- [ ] `powershell -NoProfile -File tools\Assert-CleanModule.ps1` exits 0
- [ ] `Get-Help <NewCmdlet>` returns a non-empty synopsis from the installed module
- [ ] DWD scope tables in `README.md` and `CLAUDE.md` updated if a new scope is needed
- [ ] `CHANGELOG.md` updated under `[Unreleased]`
