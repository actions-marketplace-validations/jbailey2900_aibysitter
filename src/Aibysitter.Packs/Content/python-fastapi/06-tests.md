## Tests
- pytest with `pytest-asyncio`. Async tests use the `async_client` fixture from `tests/conftest.py`.
- Database tests run against the Postgres container started by `docker compose up -d db`.
- A bug fix starts with a failing test that reproduces the bug.
