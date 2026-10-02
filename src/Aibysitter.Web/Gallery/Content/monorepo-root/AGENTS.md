# Acme monorepo

pnpm workspaces with Turborepo. Apps live in `apps/`, shared packages in `packages/`. A package can have its own `AGENTS.md`. The file closest to the code being edited takes precedence over this one.

## Commands (from the repo root)
- Install: `pnpm install --frozen-lockfile`
- Build everything: `pnpm turbo run build`
- Test one package: `pnpm turbo run test --filter=<package-name>`
- Test packages changed on this branch: `pnpm turbo run test --filter=...[origin/main]`
- Lint and type check: `pnpm turbo run lint typecheck`

## Workspace
- Packages import each other by package name (`@acme/ui`). No relative imports across package folders.
- Every package lists the dependencies it imports in its own `package.json`.
- Shared TypeScript settings come from `packages/tsconfig`. Package configs extend it and do not copy compiler options.
- Add dependencies with `pnpm add <dep> --filter <package-name>`. `pnpm-lock.yaml` is not edited by hand.

## Scope
- Change only the packages the task names. When another package needs a change, stop and describe the change instead of making it.
- `turbo.json`, root `package.json` scripts, and CI workflows change only on explicit request.

## Versioning
- Packages under `packages/` are versioned with Changesets. A change to a published package includes a changeset from `pnpm changeset`.
- Apps are not versioned.

## Pull requests
- Title format: `<package-name>: <summary>`.
- The description lists every package the PR touches.
