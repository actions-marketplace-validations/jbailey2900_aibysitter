## Commands
- Build: `go build ./...`
- Test: `go test -race ./...`
- Vet: `go vet ./...`
- Lint: `golangci-lint run`
- Format check: `gofmt -l .` prints nothing
- Regenerate queries after editing `db/queries/*.sql`: `sqlc generate`
