# Billing service

Python 3.12, FastAPI, SQLAlchemy 2.0 (async), PostgreSQL, Alembic. Dependencies and commands run through uv.

## Commands
- Install: `uv sync`
- Run: `uv run fastapi dev src/billing/main.py`
- Test: `uv run pytest`
- Lint: `uv run ruff check .`
- Format: `uv run ruff format .`
- Type check: `uv run mypy src`
- New migration: `uv run alembic revision --autogenerate -m "<summary>"`, then read the generated file before committing it.

## Layout
- `src/billing/api/`: routers, one module per resource.
- `src/billing/services/`: business logic. No FastAPI imports.
- `src/billing/db/`: models, session, repositories.
- `tests/` mirrors `src/billing/`.

## API
- Request and response bodies are Pydantic v2 models in `src/billing/schemas/`. ORM models are never returned from a route.
- Routes declare `response_model` and an explicit `status_code`.
- `HTTPException` is raised only in routers. Services raise domain errors from `billing.errors`, and `api/errors.py` maps them to status codes.
- Database sessions come from the `get_session` dependency. Services do not open sessions.

## Money
- Amounts are integers in minor units (cents). No floats for money.
- Every amount is stored with an ISO 4217 currency code next to it.

## Code style
- Every function signature has type hints. `mypy --strict` passes for `src/`.
- Ruff rules live in `pyproject.toml`. A `# noqa` comment always names the rule code.
- Timestamps use `datetime.now(tz=UTC)`. Naive datetimes are a bug.

## Tests
- pytest with `pytest-asyncio`. Async tests use the `async_client` fixture from `tests/conftest.py`.
- Database tests run against the Postgres container started by `docker compose up -d db`.
- A bug fix starts with a failing test that reproduces the bug.

## Done
- `ruff check`, `ruff format --check`, `mypy`, and `pytest` all pass.
