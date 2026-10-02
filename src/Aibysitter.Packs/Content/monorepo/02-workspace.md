## Workspace
- Packages import each other by package name (`@<scope>/ui`). No relative imports across package folders.
- Every package lists the dependencies it imports in its own `package.json`.
- Shared TypeScript settings come from `packages/tsconfig`. Package configs extend it and do not copy compiler options.
- Add dependencies with `pnpm add <dep> --filter <package-name>`. `pnpm-lock.yaml` is not edited by hand.
