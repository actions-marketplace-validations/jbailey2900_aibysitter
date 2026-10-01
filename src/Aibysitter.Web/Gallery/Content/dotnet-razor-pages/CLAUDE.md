# Orders portal

ASP.NET Core Razor Pages on .NET 10. EF Core 10 with SQL Server. Serilog for logging. xUnit for tests.

## Layout
- `src/Orders.Web`: Razor Pages, page models, `Program.cs`.
- `src/Orders.Data`: `OrdersDbContext`, entity configurations, migrations.
- `tests/Orders.Web.Tests`: xUnit page tests using `WebApplicationFactory<Program>`.
- `tests/Orders.Data.Tests`: xUnit data tests against SQL Server.

## Commands
- Build: `dotnet build Orders.slnx -warnaserror`
- Test: `dotnet test Orders.slnx`
- Run: `dotnet run --project src/Orders.Web`
- New migration: `dotnet ef migrations add <Name> --project src/Orders.Data --startup-project src/Orders.Web`

## Pages
- One page per file pair: `Pages/<Area>/<Name>.cshtml` and `<Name>.cshtml.cs`.
- Page models take dependencies through the primary constructor.
- Bind form input with `[BindProperty]` on a dedicated input record. Entities are never bound directly.
- Validate with data annotations. Return `Page()` when `ModelState` is invalid.
- A successful POST handler ends with `RedirectToPage(...)`.
- No JavaScript in pages unless the task asks for it.

## Data access
- Read-only queries use `AsNoTracking()`.
- Load related data with explicit `Include` calls. Lazy loading is disabled.
- One `SaveChangesAsync` call per POST handler.
- Schema changes go through a new migration. Do not edit a migration that has been applied.
- Connection strings come from user-secrets locally and environment variables on the server. `appsettings.json` holds no secrets.

## Logging
- Use `ILogger<T>` with message templates: `logger.LogInformation("Order {OrderId} shipped", order.Id)`.
- Do not log email addresses, names, or payment details.

## Tests
- Every new page gets a test that requests it through `WebApplicationFactory<Program>` and asserts the status code and one piece of rendered text.
- Data tests use the SQL Server container started by `tests/Orders.Data.Tests/DatabaseFixture.cs`.
- Every test asserts a result. A test that calls a method without an assertion is incomplete.

## Done
- `dotnet build -warnaserror` passes.
- `dotnet test` passes.
- Changed files are limited to the ones the task names, plus their tests and migrations.
