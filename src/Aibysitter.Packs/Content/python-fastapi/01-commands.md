## Commands
- Install: `uv sync`
- Run: `uv run fastapi dev src/<app>/main.py`
- Test: `uv run pytest`
- Lint: `uv run ruff check .`
- Format: `uv run ruff format .`
- Type check: `uv run mypy src`
- New migration: `uv run alembic revision --autogenerate -m "<summary>"`, then read the generated file before committing it.
