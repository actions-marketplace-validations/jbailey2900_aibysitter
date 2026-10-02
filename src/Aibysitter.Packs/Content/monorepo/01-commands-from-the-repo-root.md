## Commands (from the repo root)
- Install: `pnpm install --frozen-lockfile`
- Build everything: `pnpm turbo run build`
- Test one package: `pnpm turbo run test --filter=<package-name>`
- Test packages changed on this branch: `pnpm turbo run test --filter=...[origin/main]`
- Lint and type check: `pnpm turbo run lint typecheck`
