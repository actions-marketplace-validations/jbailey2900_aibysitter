## Data access
- Read-only queries use `AsNoTracking()`.
- Load related data with explicit `Include` calls. Lazy loading is disabled.
- One `SaveChangesAsync` call per POST handler.
- Schema changes go through a new migration. Do not edit a migration that has been applied.
- Connection strings come from user-secrets locally and environment variables on the server. `appsettings.json` holds no secrets.
