# Git Hooks

The repository uses Husky hooks from the Git repository root.

Install root tooling and enable Husky:

```powershell
npm install
npm run prepare
```

## Hooks

- `pre-commit`: runs `npx lint-staged` on every branch.
- `pre-push`: runs `npm --prefix frontend run test -- --watch=false` before a
  push to a GitHub remote.

`lint-staged` is configured in `lint-staged.config.mjs` and lints only staged
frontend `ts` and `html` files. It converts root-relative staged paths to paths
relative to `frontend/`, then runs ESLint through the frontend package:

```powershell
npm --prefix frontend exec eslint -- <staged frontend files>
```

The explicit `--prefix frontend` is required because Git and Husky run from the
repository root while the Angular/Ionic app is one directory below it.
