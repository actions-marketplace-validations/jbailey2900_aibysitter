## Tests
- Integration tests in `tests/<App>.Api.Tests` use `WebApplicationFactory<Program>` and a Testcontainers PostgreSQL instance.
- Each new endpoint gets four tests: success, 400 on invalid input, 404 on an unknown ID, 403 without the required policy.
- Test names follow `Method_Condition_Result`.
