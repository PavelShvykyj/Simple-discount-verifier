# Frontend Setup

## Runtime

- Node.js: 24.16.0 or newer Node 24 LTS release.
- npm: 11.13.0 or newer npm 11 release.
- Version hint: `frontend/.nvmrc` pins the currently validated local runtime.

The project uses NVM locally, but NVM commands are not part of the repository
workflow. Switch Node in a normal PowerShell or cmd session before running npm
commands.

## Frameworks

- Angular: 21.2.x.
- Ionic: Ionic Angular 8.8.x.
- Ionic CLI: 7.2.x as local development tooling.
- Ionic project type: `angular` in `ionic.config.json`.
- SSR: disabled.
- Platform target: web-only.

Capacitor is intentionally not installed. The Ionic CLI is used for web
development commands such as `ionic serve`; Capacitor, Cordova, and native
Android or iOS distribution should only be added if they become real product
requirements.

## Change Detection

The initial application uses zone-based Angular:

- `zone.js` is included in Angular polyfills.
- `provideZoneChangeDetection({ eventCoalescing: true })` is configured.

Zoneless Angular is deferred. Ionic Angular support for a fully zoneless setup
must be verified against Ionic navigation, overlays, form controls, and the
project's mobile-first workflows before changing this decision.

## Commands

Run commands from `frontend/`:

```powershell
npm install
npx ionic serve --port 8001 --no-open
npm start
npm run build
npm run lint
npm run format:check
npm test
```

## Structure

The frontend follows Feature-Sliced Design layers under `frontend/src`:

- `app`
- `pages`
- `widgets`
- `features`
- `entities`
- `shared`

The initial shell places the first route in `pages/home/ui` and keeps shared
business logic out of `app`.
