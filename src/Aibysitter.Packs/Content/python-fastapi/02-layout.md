## Layout
- `src/<app>/api/`: routers, one module per resource.
- `src/<app>/services/`: business logic. No FastAPI imports.
- `src/<app>/db/`: models, session, repositories.
- `tests/` mirrors `src/<app>/`.
