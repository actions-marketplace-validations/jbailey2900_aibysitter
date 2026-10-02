## TypeScript
- No `any`. Use `unknown` and narrow it.
- No non-null assertions (`!`) outside tests.
- Form data, route params, and API responses are parsed with the Zod schemas in `lib/schemas/` before use.
