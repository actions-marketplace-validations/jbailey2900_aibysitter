## Endpoints
- Group endpoints by resource in `src/<App>.Api/Endpoints/<Resource>Endpoints.cs` with a `Map<Resource>Endpoints` extension method.
- Request and response types are records in `Contracts/`. Entities never leave the data layer.
- Return `TypedResults` (`Ok`, `Created`, `NotFound`, `ValidationProblem`) and declare them in the handler's return type.
- Validation failures return 400 with `ValidationProblem` and one error per field.
- Unknown IDs return 404 with an empty body.
- Routes are lowercase plural nouns: `/items`, `/items/{id}`, `/items/{id}/movements`.
