## Tests
- Every new page gets a test that requests it through `WebApplicationFactory<Program>` and asserts the status code and one piece of rendered text.
- Data tests use the SQL Server container started by `tests/<App>.Data.Tests/DatabaseFixture.cs`.
- Every test asserts a result. A test that calls a method without an assertion is incomplete.
