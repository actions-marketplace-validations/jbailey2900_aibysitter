## API
- Request and response bodies are Pydantic v2 models in `src/<app>/schemas/`. ORM models are never returned from a route.
- Routes declare `response_model` and an explicit `status_code`.
- `HTTPException` is raised only in routers. Services raise domain errors from `<app>.errors`, and `api/errors.py` maps them to status codes.
- Database sessions come from the `get_session` dependency. Services do not open sessions.
