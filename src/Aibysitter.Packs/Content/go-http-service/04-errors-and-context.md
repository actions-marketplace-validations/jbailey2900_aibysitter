## Errors and context
- Wrap errors with context: `fmt.Errorf("load account: %w", err)`. Compare them with `errors.Is` and `errors.As`.
- Every function that does I/O takes `ctx context.Context` as its first parameter.
- Request paths do not call `context.Background()`.
