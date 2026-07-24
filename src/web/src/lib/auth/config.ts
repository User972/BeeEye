import type { Configuration } from '@azure/msal-browser';

/**
 * How the SPA authenticates (ADR 0008 §2, S4b).
 *
 * - `entra` — real MSAL against an Entra tenant. Every API call carries a bearer; an anonymous user
 *   is routed to sign-in.
 * - `local` — no-op. No bearer is attached and there is no gate, mirroring the backend's LocalDev
 *   posture so `npm run dev` needs no tenant. This is the developer default.
 */
export type AuthMode = 'entra' | 'local';

export interface EntraAuthConfig {
  mode: 'entra';
  clientId: string;
  authority: string;
  /** The API scope requested for the access token, e.g. `api://{api-app-id}/access_as_user`. */
  apiScope: string;
  redirectUri: string;
}

export interface LocalAuthConfig {
  mode: 'local';
}

export type AuthConfig = EntraAuthConfig | LocalAuthConfig;

/** The `VITE_`-prefixed variables this module reads. Injectable so it is unit-testable. */
export interface AuthEnv {
  VITE_AUTH_MODE?: string | undefined;
  VITE_AAD_CLIENT_ID?: string | undefined;
  VITE_AAD_AUTHORITY?: string | undefined;
  VITE_AAD_API_SCOPE?: string | undefined;
  VITE_AAD_REDIRECT_URI?: string | undefined;
}

/**
 * Raised when a build *intended for Entra* is missing the configuration to sign anyone in. Failing
 * loudly here is the point: a deployment must never boot into accidental anonymous mode (A.8).
 */
export class AuthConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'AuthConfigError';
  }
}

function clean(value: string | undefined): string {
  return (value ?? '').trim();
}

/**
 * Resolves the auth configuration from environment variables.
 *
 * Mode selection: an explicit `VITE_AUTH_MODE` wins; otherwise the presence of a client id implies
 * `entra`, and its absence `local` (A.1). A typo'd mode, or an `entra` build missing any of the three
 * required values, throws rather than silently disabling auth.
 */
export function resolveAuthConfig(env: AuthEnv, defaultRedirectUri: string): AuthConfig {
  const requested = clean(env.VITE_AUTH_MODE).toLowerCase();
  const clientId = clean(env.VITE_AAD_CLIENT_ID);

  let mode: AuthMode;
  if (requested === 'entra' || requested === 'local') {
    mode = requested;
  } else if (requested === '') {
    mode = clientId === '' ? 'local' : 'entra';
  } else {
    throw new AuthConfigError(
      `VITE_AUTH_MODE must be 'entra' or 'local' (received '${env.VITE_AUTH_MODE}').`,
    );
  }

  if (mode === 'local') {
    return { mode: 'local' };
  }

  const authority = clean(env.VITE_AAD_AUTHORITY);
  const apiScope = clean(env.VITE_AAD_API_SCOPE);
  const missing = (
    [
      ['VITE_AAD_CLIENT_ID', clientId],
      ['VITE_AAD_AUTHORITY', authority],
      ['VITE_AAD_API_SCOPE', apiScope],
    ] as const
  )
    .filter(([, value]) => value === '')
    .map(([name]) => name);

  if (missing.length > 0) {
    throw new AuthConfigError(
      `Auth mode 'entra' requires ${missing.join(', ')}. Refusing to start in anonymous mode ` +
        `(set VITE_AUTH_MODE=local for an intentionally anonymous local run).`,
    );
  }

  const redirectUri = clean(env.VITE_AAD_REDIRECT_URI) || defaultRedirectUri;
  return { mode: 'entra', clientId, authority, apiScope, redirectUri };
}

/**
 * MSAL configuration for the Entra mode.
 *
 * PKCE authorization-code flow (the `@azure/msal-browser` default — never implicit). The cache is
 * `sessionStorage`: tab-scoped, so an XSS foothold cannot lift a token from another tab, at the cost
 * of no cross-tab SSO. Choosing `localStorage` would trade that blast-radius reduction for cross-tab
 * SSO; it is a deliberate, documented trade-off and is not made silently here.
 */
export function msalConfiguration(config: EntraAuthConfig): Configuration {
  return {
    auth: {
      clientId: config.clientId,
      authority: config.authority,
      redirectUri: config.redirectUri,
      postLogoutRedirectUri: config.redirectUri,
    },
    cache: {
      // Tab-scoped, so an XSS foothold cannot lift a token from another tab, at the cost of no
      // cross-tab SSO. `localStorage` would trade that blast-radius reduction for cross-tab SSO — a
      // deliberate, documented trade-off, not made silently here. The redirect + PKCE flow does not
      // depend on the third-party-cookie iframe path (A.7.4), so no cookie fallback is configured.
      cacheLocation: 'sessionStorage',
    },
  };
}
