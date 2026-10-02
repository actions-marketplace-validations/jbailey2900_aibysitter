## Errors
- Unhandled exceptions become RFC 9457 problem details through `app.UseExceptionHandler()`. Handlers do not catch exceptions to return 500 themselves.
- Optimistic-concurrency failures throw `ConflictException`. The exception handler maps it to 409.
