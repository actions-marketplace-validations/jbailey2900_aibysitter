# Storefront

Next.js with the App Router, TypeScript in strict mode, Tailwind CSS. Package manager: pnpm.

## Commands
- Install: `pnpm install --frozen-lockfile`
- Dev server: `pnpm dev`
- Type check: `pnpm tsc --noEmit`
- Lint: `pnpm lint`
- Unit tests: `pnpm test` (Vitest)
- End-to-end tests: `pnpm test:e2e` (Playwright)

## Structure
- Routes live in `app/`. Shared UI lives in `components/`. Data access lives in `lib/data/`.
- Components are Server Components by default. A component that uses state, effects, or browser APIs starts with `"use client"`.
- Client components receive data as props. Data fetching stays in the Server Component that renders them.
- Mutations are Server Actions in `app/**/actions.ts` and call `revalidatePath` for the route they change.

## TypeScript
- No `any`. Use `unknown` and narrow it.
- No non-null assertions (`!`) outside tests.
- Form data, route params, and API responses are parsed with the Zod schemas in `lib/schemas/` before use.

## Styling
- Tailwind classes only. No inline `style` attributes and no new CSS files.
- Colors come from the theme tokens in `app/globals.css`. No raw hex values in components.

## Server code
- Modules that read secrets or the database import `server-only` on their first line.
- Secrets are read from `process.env` in server code only. Secret names never start with `NEXT_PUBLIC_`.

## Tests
- A component with logic gets a Vitest test next to it: `ProductCard.test.tsx` for `ProductCard.tsx`.
- A new route gets one Playwright test in `e2e/` covering its main path.

## Before opening a PR
- `pnpm tsc --noEmit`, `pnpm lint`, and `pnpm test` pass.
- No `console.log` calls remain in `app/`, `components/`, or `lib/`.
