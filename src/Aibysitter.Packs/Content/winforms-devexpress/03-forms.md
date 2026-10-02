## Forms
- Do not edit `*.Designer.cs` files by hand. Describe layout changes in the PR for a person to make in the designer.
- Event handlers call a presenter or a Core service. They contain no export logic.
- Long-running work runs through `await Task.Run(...)` inside an `async void` event handler with a try/catch that shows `XtraMessageBox`.
- New forms use DevExpress controls (`GridControl`, `LookUpEdit`, `DateEdit`) instead of the standard WinForms equivalents.
- Grid columns bind to properties by name through `FieldName`. No column access by index.
