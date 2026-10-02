## Code style
- Every function signature has type hints. `mypy --strict` passes for `src/`.
- Ruff rules live in `pyproject.toml`. A `# noqa` comment always names the rule code.
- Timestamps use `datetime.now(tz=UTC)`. Naive datetimes are a bug.
