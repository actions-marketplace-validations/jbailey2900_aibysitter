## Layout
- `src/<App>.Web`: Razor Pages, page models, `Program.cs`.
- `src/<App>.Data`: `<App>DbContext`, entity configurations, migrations.
- `tests/<App>.Web.Tests`: xUnit page tests using `WebApplicationFactory<Program>`.
- `tests/<App>.Data.Tests`: xUnit data tests against SQL Server.
