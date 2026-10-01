# Payroll Exporter

WinForms desktop app on .NET 10 (Windows only) using DevExpress WinForms controls. Exports payroll CSV files for a payroll provider.

## Commands
- Build: `dotnet build PayrollExporter.slnx -c Release -warnaserror`
- Test: `dotnet test PayrollExporter.slnx`
- Publish: `dotnet publish src/PayrollExporter.App -c Release -r win-x64 --self-contained false`

## Projects
- `src/PayrollExporter.App`: forms, user controls, `Program.cs`.
- `src/PayrollExporter.Core`: export rules and the CSV writer. No WinForms or DevExpress references.
- `tests/PayrollExporter.Core.Tests`: xUnit tests for Core.

## Forms
- Do not edit `*.Designer.cs` files by hand. Describe layout changes in the PR for a person to make in the designer.
- Event handlers call a presenter or a Core service. They contain no export logic.
- Long-running work runs through `await Task.Run(...)` inside an `async void` event handler with a try/catch that shows `XtraMessageBox`.
- New forms use DevExpress controls (`GridControl`, `LookUpEdit`, `DateEdit`) instead of the standard WinForms equivalents.
- Grid columns bind to properties by name through `FieldName`. No column access by index.

## Export format
- Money is `decimal`, rounded to 2 places with `MidpointRounding.AwayFromZero` at output time only.
- Dates use `yyyy-MM-dd`. Times use 24-hour `HH:mm`.
- Output is UTF-8 without BOM with CRLF line endings. Fields are quoted only when they contain a comma, a quote, or a line break.
- Column order is defined once, in `ExportColumns.cs`.

## Settings
- User settings live in `%LOCALAPPDATA%\PayrollExporter\settings.json`.
- Do not write to the install directory or the registry.

## DevExpress
- The DevExpress version is pinned in `Directory.Packages.props`. Feature PRs do not change it.
- License keys stay on developer machines. Do not commit `licenses.licx` or any key.

## Tests
- Every export rule change adds a Core test with a fixture CSV in `tests/PayrollExporter.Core.Tests/Fixtures`.
- Generated CSV is compared to the fixture byte for byte.
