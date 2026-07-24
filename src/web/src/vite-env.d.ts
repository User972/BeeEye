/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  /** `entra` | `local`. Defaults to `local` when VITE_AAD_CLIENT_ID is absent (ADR 0008, S4b). */
  readonly VITE_AUTH_MODE?: string;
  /** Entra SPA public-client (PKCE) application id. */
  readonly VITE_AAD_CLIENT_ID?: string;
  /** e.g. https://login.microsoftonline.com/{tenantId}/v2.0 — must equal the backend's Auth:Authority. */
  readonly VITE_AAD_AUTHORITY?: string;
  /** The API scope requested for the access token, e.g. api://{api-app-id}/access_as_user. */
  readonly VITE_AAD_API_SCOPE?: string;
  /** Optional; defaults to window.location.origin. */
  readonly VITE_AAD_REDIRECT_URI?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
