import { describe, expect, it } from 'vitest';
import { AuthConfigError, msalConfiguration, resolveAuthConfig } from './config';
import type { EntraAuthConfig } from './config';

const ORIGIN = 'https://beeeye.example';

describe('resolveAuthConfig — mode selection', () => {
  it('defaults to local when no mode and no client id are set (the dev default)', () => {
    expect(resolveAuthConfig({}, ORIGIN)).toEqual({ mode: 'local' });
  });

  it('infers entra when a client id is present but no mode is set', () => {
    const config = resolveAuthConfig(
      {
        VITE_AAD_CLIENT_ID: 'client-1',
        VITE_AAD_AUTHORITY: 'https://login.microsoftonline.com/tenant/v2.0',
        VITE_AAD_API_SCOPE: 'api://api-1/access_as_user',
      },
      ORIGIN,
    );
    expect(config.mode).toBe('entra');
  });

  it('honours an explicit local mode even when a client id is present', () => {
    const config = resolveAuthConfig(
      { VITE_AUTH_MODE: 'local', VITE_AAD_CLIENT_ID: 'client-1' },
      ORIGIN,
    );
    expect(config).toEqual({ mode: 'local' });
  });

  it('throws on a typo mode rather than silently disabling auth', () => {
    expect(() => resolveAuthConfig({ VITE_AUTH_MODE: 'entraa' }, ORIGIN)).toThrow(AuthConfigError);
  });
});

describe('resolveAuthConfig — entra fail-fast (A.8)', () => {
  it('throws and names every missing value when entra is intended but underconfigured', () => {
    const act = () => resolveAuthConfig({ VITE_AUTH_MODE: 'entra' }, ORIGIN);
    expect(act).toThrow(AuthConfigError);
    expect(act).toThrow(/VITE_AAD_CLIENT_ID/);
    expect(act).toThrow(/VITE_AAD_AUTHORITY/);
    expect(act).toThrow(/VITE_AAD_API_SCOPE/);
  });

  it('throws when an inferred entra build (client id present) is missing the authority/scope', () => {
    expect(() => resolveAuthConfig({ VITE_AAD_CLIENT_ID: 'client-1' }, ORIGIN)).toThrow(
      AuthConfigError,
    );
  });

  it('returns a complete config and defaults the redirect URI to the origin', () => {
    const config = resolveAuthConfig(
      {
        VITE_AUTH_MODE: 'entra',
        VITE_AAD_CLIENT_ID: 'client-1',
        VITE_AAD_AUTHORITY: 'https://login.microsoftonline.com/tenant/v2.0',
        VITE_AAD_API_SCOPE: 'api://api-1/access_as_user',
      },
      ORIGIN,
    ) as EntraAuthConfig;

    expect(config).toEqual({
      mode: 'entra',
      clientId: 'client-1',
      authority: 'https://login.microsoftonline.com/tenant/v2.0',
      apiScope: 'api://api-1/access_as_user',
      redirectUri: ORIGIN,
    });
  });

  it('uses an explicit redirect URI when provided', () => {
    const config = resolveAuthConfig(
      {
        VITE_AUTH_MODE: 'entra',
        VITE_AAD_CLIENT_ID: 'client-1',
        VITE_AAD_AUTHORITY: 'https://login.microsoftonline.com/tenant/v2.0',
        VITE_AAD_API_SCOPE: 'api://api-1/access_as_user',
        VITE_AAD_REDIRECT_URI: 'https://beeeye.example/callback',
      },
      ORIGIN,
    ) as EntraAuthConfig;

    expect(config.redirectUri).toBe('https://beeeye.example/callback');
  });
});

describe('msalConfiguration', () => {
  it('uses PKCE-friendly sessionStorage and carries the app identity', () => {
    const config: EntraAuthConfig = {
      mode: 'entra',
      clientId: 'client-1',
      authority: 'https://login.microsoftonline.com/tenant/v2.0',
      apiScope: 'api://api-1/access_as_user',
      redirectUri: ORIGIN,
    };

    const msal = msalConfiguration(config);

    expect(msal.auth.clientId).toBe('client-1');
    expect(msal.auth.authority).toBe(config.authority);
    expect(msal.auth.redirectUri).toBe(ORIGIN);
    // Tab-scoped cache, lower XSS blast radius (A.5 / A.7.4).
    expect(msal.cache?.cacheLocation).toBe('sessionStorage');
  });
});
