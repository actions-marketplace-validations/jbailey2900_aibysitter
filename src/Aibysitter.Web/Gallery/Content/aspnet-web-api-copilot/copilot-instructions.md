# Inventory API

ASP.NET Core minimal APIs on .NET 10. PostgreSQL through EF Core and Npgsql. OpenAPI document generated at runtime.

## Commands
- Build: `dotnet build -warnaserror`
- Test: `dotnet test`
- Run: `dotnet run --project src/Inventory.Api` (listens on `http://localhost:5080`)
- OpenAPI document: `GET /openapi/v1.json` while the API is running

## Endpoints
- Group endpoints by resource in `src/Inventory.Api/Endpoints/<Resource>Endpoints.cs` with a `Map<Resource>Endpoints` extension method.
- Request and response types are records in `Contracts/`. Entities never leave the data layer.
- Return `TypedResults` (`Ok`, `Created`, `NotFound`, `ValidationProblem`) and declare them in the handler's return type.
- Validation failures return 400 with `ValidationProblem` and one error per field.
- Unknown IDs return 404 with an empty body.
- Routes are lowercase plural nouns: `/items`, `/items/{id}`, `/items/{id}/movements`.

## Errors
- Unhandled exceptions become RFC 9457 problem details through `app.UseExceptionHandler()`. Handlers do not catch exceptions to return 500 themselves.
- Optimistic-concurrency failures throw `ConflictException`. The exception handler maps it to 409.

## Data
- `InventoryDbContext` lives in `src/Inventory.Data`.
- Bulk changes use `ExecuteUpdateAsync` and `ExecuteDeleteAsync`.
- Stock quantities change only through `StockLedger.Record(...)`.

## Authorization
- Every endpoint requires a policy: `.RequireAuthorization(Policies.Read)` or `.RequireAuthorization(Policies.Write)`.
- `/health` is the only endpoint with `AllowAnonymous`.

## Tests
- Integration tests in `tests/Inventory.Api.Tests` use `WebApplicationFactory<Program>` and a Testcontainers PostgreSQL instance.
- Each new endpoint gets four tests: success, 400 on invalid input, 404 on an unknown ID, 403 without the required policy.
- Test names follow `Method_Condition_Result`.

## Pull requests
- One endpoint or one bug fix per PR.
- Add a line to `CHANGELOG.md` under the Unreleased heading.
