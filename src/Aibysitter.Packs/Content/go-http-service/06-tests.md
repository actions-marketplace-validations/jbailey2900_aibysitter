## Tests
- Table-driven tests with one `t.Run` per case.
- Handlers are tested with `httptest.NewRecorder` and fakes for external clients.
- Database tests carry the `//go:build integration` tag and run with `go test -tags integration ./...`.
