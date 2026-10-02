## Authorization
- Every endpoint requires a policy: `.RequireAuthorization(Policies.Read)` or `.RequireAuthorization(Policies.Write)`.
- `/health` is the only endpoint with `AllowAnonymous`.
