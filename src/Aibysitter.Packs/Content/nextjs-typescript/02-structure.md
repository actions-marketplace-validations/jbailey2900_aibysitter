## Structure
- Routes live in `app/`. Shared UI lives in `components/`. Data access lives in `lib/data/`.
- Components are Server Components by default. A component that uses state, effects, or browser APIs starts with `"use client"`.
- Client components receive data as props. Data fetching stays in the Server Component that renders them.
- Mutations are Server Actions in `app/**/actions.ts` and call `revalidatePath` for the route they change.
