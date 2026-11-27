# API-hosted OIDC for a separate UI and API

This guide describes how CallCadence keeps OpenID Connect (OIDC) provider credentials and protocol handling in the API while the separate UI presents the sign-in experience and calls the API. It is also a reusable design guide for projects with a UI frontend and API backend.

## Design at a glance

The API is the confidential OIDC client and security boundary. It owns the provider authority/metadata, client ID and client secret, registered OIDC callback path, identity validation, local-account lookup, role assignment, and JWT signing/validation. The UI does not need the provider's OIDC settings or client secret. It needs the API's base URL, a sign-in button/return route, and a way to retain and send the API-issued access token.

```text
Browser/UI                API                         Identity provider
    |                       |                                  |
    | GET /api/auth/status  |                                  |
    |---------------------->|                                  |
    | provider + sign-in URL|                                  |
    |<----------------------|                                  |
    | navigate to API sign-in URL                               |
    |---------------------->| redirect to provider ------------>|
    |                       |<-------- OIDC authorization code --|
    |                       | validate code, establish identity |
    |                       | map identity to local user/roles   |
    |                       | mint API JWT + short-lived code    |
    |<----------------------| redirect to UI with code           |
    | POST code to API      |                                  |
    |---------------------->|                                  |
    | JWT in response       |                                  |
    |<----------------------|                                  |
    | store session; use Bearer JWT for protected API requests  |
```

The provider sends its authorization response to the **API's OIDC middleware callback path** (for example, `/signin-oidc-example`). After OIDC middleware validates it, the API completes application sign-in at its own `/api/auth/sso-callback` route. These are separate callback stages and both URLs must be reachable over HTTPS.

## CallCadence flow

1. **The UI discovers providers.** `GET /api/auth/status` returns enabled providers and API-generated `SignInUrl` values. The UI displays one button per provider and performs a full-page navigation to the selected API URL, adding its own base URL as `returnUrl` (`src/CallCadence.UI/Components/Pages/Home.razor`, `src/CallCadence.API/Controllers/AuthController.cs`).
2. **The API starts OIDC.** `GET /api/auth/sso-challenge?provider=...&returnUrl=...` checks that the provider exists and is enabled, then challenges its named OIDC scheme. The API builds the protocol redirect URI from its own public scheme and host. Behind a TLS-terminating proxy, forwarded scheme/host information must be handled correctly (`src/CallCadence.API/Program.cs`).
3. **The API validates the provider response.** ASP.NET Core OIDC middleware validates the authorization response and writes the external identity to the API's `Identity.External` cookie. It requests `openid`, `profile`, and `email` scopes; email is needed for local-account lookup (`src/CallCadence.API/Auth/DynamicOidcConfigureOptions.cs`).
4. **The API maps the identity to local authorization.** `/api/auth/sso-callback` reads the provider email, finds a pre-provisioned local `AdminUser`, obtains that user's locally assigned roles, and rejects sign-in if no local account exists. In this implementation, a provider login establishes identity; provider group/role claims are not automatically used to grant application roles.
5. **The API hands off a one-time code, not the JWT in the redirect.** It creates a JWT, stores the authentication response under a random code in memory for five minutes, clears the external cookie, and redirects to the allow-listed UI's `/sso-callback?code=...`. The UI posts that code to `POST /api/auth/sso/exchange`; the API removes it when exchanged and returns the JWT response (`AuthController.cs`, `src/CallCadence.UI/Components/Pages/SsoCallback.razor`, `src/CallCadence.UI/Services/CallCadenceApiClient.cs`).
6. **The UI retains the API session.** The Blazor Server UI persists the returned token with `ProtectedLocalStorage`, projects its claims into UI authentication state, and attaches `Authorization: Bearer <token>` to API calls (`src/CallCadence.UI/Services/TokenAuthenticationStateProvider.cs`, `src/CallCadence.UI/Services/BearerTokenHandler.cs`).
7. **The API enforces access.** API authentication defaults to JWT bearer validation. Protected controllers use `[Authorize]`; role- or policy-specific operations check the API-validated principal (for example, the `AdminOnly` policy). UI-side route/role checks are for presentation only and must never replace API authorization.

## Configuration

### API: OIDC provider registration

CallCadence supports two configuration sources:

- **Environment variable:** `CALLCADENCE_SSO_CONFIG` is a JSON array. When present, it completely replaces database-backed SSO settings and makes them read-only in the UI. A service restart is needed to load changed provider configuration.
- **Database/UI settings:** without the environment override, provider settings are stored in the API database and can be managed through the authenticated settings UI. OIDC schemes are registered when the API starts, so restart the API after changing the set of providers.

Example environment configuration (replace all sample values; keep the secret in a deployment secret store, not source control):

```json
[
  {
    "name": "Example identity provider",
    "schemeName": "oidc-example",
    "isEnabled": true,
    "authority": "https://identity.example.com/tenant",
    "clientId": "<api-client-id>",
    "clientSecret": "<secret-from-secret-store>",
    "callbackPath": "/signin-oidc-example"
  }
]
```

`name` and `clientId` are required; supply `authority` or `metadataAddress`. `schemeName` is optional (when omitted, a URL-safe name is derived from `name`); `callbackPath` is optional but should be explicit and unique when multiple providers are configured. Register the API's externally visible callback URL with the provider, for example `https://api.example.com/signin-oidc-example`. Do not register the UI callback route as the OIDC redirect URI.

The API also needs JWT and return-URL configuration. In container deployments, ASP.NET Core maps double-underscore environment variable names to configuration sections:

```text
Jwt__SigningKey=<long-random-secret-from-secret-store>
Jwt__Issuer=<stable-api-issuer>
Jwt__Audience=<expected-api-token-audience>
Jwt__ExpiryMinutes=60
AllowedUiReturnUrls=https://ui.example.com
```

Use a strong private signing key and keep issuer/audience values consistent with JWT validation. `AllowedUiReturnUrls` must contain the trusted UI base URL(s) used after sign-in; do not allow arbitrary user-supplied return URLs. CallCadence checks this setting before redirecting back to the UI. Use exact trusted origins and paths in new implementations rather than broad prefix matching.

### UI: no OIDC client configuration

The UI does not receive `authority`, `clientId`, `clientSecret`, or provider callback configuration. It only needs the API URL, for example:

```text
Api__BaseUrl=https://api.example.com
```

It discovers enabled providers from the API status response. The UI does still need an application callback page (CallCadence uses `/sso-callback`) to exchange the short-lived code and establish its local UI session. This route is not an OIDC middleware callback and has no provider secret.

## Authorization responsibilities

- Treat the identity provider as proof of external identity, not as the application's authorization database unless deliberate group/role mapping is implemented.
- Map the external subject to a stable local account. CallCadence currently looks up a provisioned account by email and derives roles from local Identity records.
- Mint the API token only after identity mapping and authorization data are resolved. Include only needed claims, set expiry, issuer, and audience, and sign with a secret the UI never receives.
- Validate the token signature, expiry, issuer, and audience on every protected API request. Enforce roles/policies at API endpoints and on real-time endpoints as applicable.
- UI authorization state improves navigation and user experience; it is untrusted from the API's perspective.

## Deployment and security checklist

- Use HTTPS for the UI, API, and provider redirect URI. Configure the provider's redirect URI to the API callback path exactly. When running behind a proxy, only trust forwarded headers from trusted proxies/networks; otherwise redirect URI generation can be wrong or spoofable.
- Store the OIDC client secret and JWT signing key in a secret manager or protected deployment configuration. Never put either in UI settings, browser code, logs, or version control.
- Keep the UI return destination allow-list narrow. Validate the destination as an expected origin/path before redirecting; never redirect directly to an unchecked query parameter.
- Keep the authorization code short-lived and single-use. The current implementation keeps it in process memory, so a multi-replica API deployment needs a shared/distributed cache (or reliable affinity) for code exchange to work across replicas. Consider binding the handoff to the initiating browser/session and add replay protections in a production design.
- Persist and share ASP.NET Core Data Protection keys across API replicas so OIDC state/correlation cookies can be decrypted consistently. Set secure cookie options appropriate for the HTTPS deployment.
- Configure CORS only when browser JavaScript calls the API cross-origin. In a server-rendered UI such as this Blazor Server app, the UI server calls the API, so separate public hostnames alone do not necessarily require browser-to-API CORS.
- Protect operational endpoints and verify authentication and authorization on API and SignalR endpoints independently. A successful UI login is not evidence that every API operation is authorized.

## Reusable implementation sequence

1. Register an OIDC confidential client for the API and set the exact API callback URL at the identity provider.
2. Configure OIDC middleware and secret material in the API only; use the authorization-code flow and validate state, nonce, issuer, signature, and audience through the framework middleware.
3. Add an API sign-in-start endpoint that selects a configured provider and accepts only an allow-listed UI return destination.
4. In the API callback, map the external identity to an active local user, resolve server-side roles/permissions, and issue your application's API credential.
5. Redirect to the UI using a short-lived, single-use exchange code rather than putting an access token in a URL. Have the UI exchange it with the API over HTTPS and establish its own session.
6. Attach the API credential to API requests; validate it and enforce authorization policies in the API. Keep UI role checks as UX only.
7. Test successful login, unknown/disabled provider, unprovisioned user, invalid/expired/replayed exchange code, disallowed return URL, expired or wrong-audience token, and insufficient role.

For CallCadence's implementation details, see `src/CallCadence.API/Program.cs`, `src/CallCadence.API/Controllers/AuthController.cs`, `src/CallCadence.API/Auth/DynamicOidcConfigureOptions.cs`, `src/CallCadence.API/Auth/EnvVarSsoConfigurationProvider.cs`, and the corresponding UI files listed above.