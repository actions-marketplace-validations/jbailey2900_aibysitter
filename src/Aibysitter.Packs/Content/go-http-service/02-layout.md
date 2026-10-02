## Layout
- `cmd/<app>/main.go`: wiring only. Config, logger, server start.
- `internal/http/`: handlers and routes.
- `internal/<domain>/`: domain logic. No `net/http` imports.
- `internal/db/`: generated sqlc code. Files in this directory are not edited by hand.
