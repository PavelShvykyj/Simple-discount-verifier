# Git Hooks

The repository uses tracked hooks under `.githooks`.

Enable them in each local clone:

```powershell
git config core.hooksPath .githooks
```

## Hooks

- `pre-commit`: runs `npm --prefix frontend run lint` only on `master` and
  `develop`.
- `pre-push`: runs `npm --prefix frontend run test -- --watch=false` before a
  push to a GitHub remote.

The frontend project is intentionally addressed via `npm --prefix frontend`
because Git is initialized at the repository root while the Angular/Ionic app is
one directory below it.
