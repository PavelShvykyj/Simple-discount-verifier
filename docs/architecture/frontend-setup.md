# Frontend Setup

## Runtime

- Node.js: 24.13.0 or newer Azure Static Web Apps-supported Node 24 LTS release.
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
- Interface language: Ukrainian.
- Theme modes: system, light, and dark.
- Barcode rendering for scanner compatibility testing: `@bwip-js/browser`.

Capacitor is intentionally not installed. The Ionic CLI is used for web
development commands such as `ionic serve`; Capacitor, Cordova, and native
Android or iOS distribution should only be added if they become real product
requirements.

## Change Detection

The initial application uses zone-based Angular:

- `zone.js` is included in Angular polyfills.
- `provideZoneChangeDetection({ eventCoalescing: true })` is configured.

Zoneless Angular is not enabled yet, but code must be written as if zone support
may be removed later. Prefer signals, Angular event bindings, Angular services,
and explicit reactive state updates. Avoid relying on implicit async side
effects that only refresh UI because Zone.js patched the browser API.

## Angular-First Rules

- Use Angular `HttpClient` for API calls instead of `fetch`.
- Use Observables, signals, and RxJS/Angular reactive primitives instead of
  `async`/`await` for frontend application flows.
- Use typed Angular forms or Angular Signal Forms for non-trivial forms.
- Keep temporary form state in Angular state unless browser persistence is an
  explicit requirement.
- When both an Angular tool and a generic browser/third-party alternative are
  reasonable, choose the Angular tool first.

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

Temporary scanner compatibility testing lives in `pages/scanner-survey/ui` and
posts results to the Static Web Apps API route `/api/scanner-survey`.
