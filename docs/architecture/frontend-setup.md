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
- Browser camera barcode/QR reading on the admin scanner survey page:
  `@zxing-js/ngx-scanner`, the Angular integration recommended from the
  `@zxing/browser` package documentation.

Capacitor is intentionally not installed. The Ionic CLI is used for web
development commands such as `ionic serve`; Capacitor, Cordova, and native
Android or iOS distribution should only be added if they become real product
requirements.

## Azure Static Web Apps

- The Angular SPA is hosted by Azure Static Web Apps.
- MVP API endpoints are Azure Static Web Apps managed Azure Functions under
  `/api`.
- The SPA calls only same-origin `/api` endpoints, avoiding CORS for the MVP.
- Azure Static Web Apps built-in authentication is available through `/.auth/*`.
- Service auth endpoints:
  - `/.auth/login/aad` starts login through Microsoft Entra ID.
  - `/.auth/logout` ends the Static Web Apps session.
  - `/.auth/me` returns the current user.
- Built-in Static Web Apps roles are `anonymous` and `authenticated`.
- Do not protect the admin area with `authenticated`; it is too broad because it
  includes any user who successfully signs in through the provider.
- Protect the admin area with the custom Static Web Apps role `admin`.
- The `admin` role is managed by Static Web Apps. It is not an Azure RBAC role
  and not a Microsoft Entra group.
- Protected routes, friendly auth routes, SPA navigation fallback, response
  overrides, and managed Functions runtime are configured in
  `frontend/public/staticwebapp.config.json`.
- The file lives in `frontend/public` because `frontend/angular.json` copies all
  files from `public` into the Angular browser build output root, where Azure
  Static Web Apps reads `staticwebapp.config.json`.
- Keep the source file in `frontend/public` while the SWA app location is the
  Angular frontend project. Move it only if deployment changes the app location
  or output pipeline so the config no longer lands at the deployed app root.
- Use this MVP configuration as the baseline:

```json
{
  "routes": [
    {
      "route": "/login",
      "redirect": "/.auth/login/aad"
    },
    {
      "route": "/logout",
      "redirect": "/.auth/logout"
    },
    {
      "route": "/scanner-survey*",
      "allowedRoles": ["admin"],
      "rewrite": "/index.html"
    },
    {
      "route": "/admin*",
      "allowedRoles": ["admin"],
      "rewrite": "/index.html"
    },
    {
      "route": "/api/admin/*",
      "allowedRoles": ["admin"]
    },
    {
      "route": "/api/pos/*",
      "allowedRoles": ["anonymous"]
    },
    {
      "route": "/api/scanner-survey",
      "allowedRoles": ["admin"]
    },
    {
      "route": "/api/public/*",
      "allowedRoles": ["anonymous", "authenticated"]
    }
  ],
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": [
      "/api/*",
      "/.auth/*",
      "/assets/*",
      "/*.{css,js,ico,png,jpg,jpeg,svg,json}"
    ]
  },
  "responseOverrides": {
    "401": {
      "statusCode": 302,
      "redirect": "/.auth/login/aad?post_login_redirect_uri=.referrer"
    },
    "403": {
      "rewrite": "/forbidden.html"
    }
  },
  "platform": {
    "apiRuntime": "dotnet-isolated:8.0"
  }
}
```

- For the small MVP, add concrete administrators in Azure Portal:
  Static Web App -> Settings -> Role Management -> Invite.
- Invitation values:
  - Provider: Microsoft Entra ID / `aad`.
  - Email: the email for the Microsoft identity the user will use to sign in.
  - Domain: the user's sign-in domain.
  - Roles: `admin`.
  - Expiration: set according to the operational need.
- Static Web Apps creates an invitation link. The user opens the link, signs in
  with their Microsoft account or Entra ID account, and SWA associates that
  identity with the `admin` role.
- A successful `/.auth/me` response for an admin may include:

```json
{
  "clientPrincipal": {
    "identityProvider": "aad",
    "userId": "...",
    "userDetails": "admin@example.com",
    "userRoles": ["anonymous", "authenticated", "admin"]
  }
}
```

- The invitation does not create a Microsoft account. If a user has only a
  regular Gmail address and no Microsoft account for it, they must first create
  a Microsoft account using that email and then accept the invitation.
- The invitation email should be the same email the user will actually use to
  sign in.
- Frontend auth state is read through `AuthService` in
  `frontend/src/shared/auth/auth.service.ts`.
- `AuthService` calls `/.auth/me`, caches the current user with `shareReplay(1)`,
  checks the Static Web Apps `admin` custom role, and redirects login/logout
  through `/.auth/login/aad` and `/.auth/logout`.
- Admin Angular routes should use `adminGuard` from
  `frontend/src/shared/auth/admin.guard.ts`. The guard reads the current SWA
  user, allows users with the `admin` role, and redirects all other users to
  SWA login with the current route as the return URL.
- For admin auth verification, the home page remains public while
  `/scanner-survey*` and `/api/scanner-survey` require the custom `admin` role.
  The Angular `scanner-survey` route also uses `adminGuard` so both client-side
  navigation and Static Web Apps edge routing are checked.
- External POS server-to-server APIs live under `/api/pos/*`. Static Web Apps
  allows these requests through as `anonymous`; the Azure Function must validate
  HMAC internally with `x-client-id`, `x-timestamp`, and `x-signature`.
- The initial POS client id is `main-pos-system`. The HMAC secret is stored only
  in server-side SWA/API configuration. Nonce replay protection is deferred to a
  future phase with a used-nonce table and periodic cleanup.
- The frontend does not use MSAL and does not talk directly to Azure Storage
  Tables.
- Managed Functions access Azure Storage Tables with a Storage connection
  string from API configuration.

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
- Use Ionic layout primitives for page layout. In particular, page-level
  responsive layout should use `ion-grid`, `ion-row`, and `ion-col` instead of
  ad hoc CSS grid/flex containers. Keep content inside `ion-col`, matching the
  Ionic grid model.

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
posts results to the Static Web Apps protected API route `/api/scanner-survey`.
The page must support two kinds of checks:

- POS/workplace compatibility checks for generated Code 128 samples.
- Admin browser-camera checks where one app instance displays a QR code or test
  barcode and another mobile browser instance reads it through
  `@zxing-js/ngx-scanner`.

The QR-code scenario is confirmed for admin/support navigation. The primary
support QR is generated from `correlationId` returned by the first
`POST /api/public/redemptions` response, including business-error responses when
the backend can create a correlation id. This lets an administrator inspect an
attempt even if the problem happened before barcode generation. A separate
manual support code is not part of the current scope.
