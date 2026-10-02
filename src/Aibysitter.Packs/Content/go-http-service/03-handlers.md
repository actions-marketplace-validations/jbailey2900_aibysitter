## Handlers
- Routes are registered with method patterns: `mux.HandleFunc("POST /messages", h.createMessage)`.
- JSON bodies are decoded with `json.NewDecoder(r.Body)` and `DisallowUnknownFields()`.
- Errors are written with `writeError(w, status, code, message)` from `internal/http/errors.go`.
- Handlers get their dependencies from the `Handler` struct. No package-level state.
