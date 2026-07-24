import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryClient } from '@tanstack/react-query';
import { EventType, InteractionRequiredAuthError } from '@azure/msal-browser';
import type {
  AccountInfo,
  AuthenticationResult,
  EventMessage,
  IPublicClientApplication,
} from '@azure/msal-browser';
import { activateRedirectAccount, installMsalTokenBridge } from './msalBridge';
import type { EntraAuthConfig } from './config';
import { apiGet, resetAuthSeam } from '@/lib/api/client';
import { identityKeys } from '@/lib/api/identity';

const config: EntraAuthConfig = {
  mode: 'entra',
  clientId: 'client-1',
  authority: 'https://login.microsoftonline.com/tenant/v2.0',
  apiScope: 'api://api-1/access_as_user',
  redirectUri: 'https://beeeye.example',
};

const account = {
  homeAccountId: 'home-1',
  environment: 'login.microsoftonline.com',
  tenantId: 'tenant-1',
  username: 'exec@admc.example',
  localAccountId: 'local-1',
} as AccountInfo;

interface FakePca {
  pca: IPublicClientApplication;
  fire: (eventType: EventType) => void;
}

function makeFakePca(opts: {
  account?: AccountInfo | null;
  acquireTokenSilent?: () => Promise<AuthenticationResult>;
}): FakePca {
  let callback: ((message: EventMessage) => void) | null = null;
  const acc = opts.account ?? null;
  const pca = {
    getActiveAccount: () => acc,
    getAllAccounts: () => (acc ? [acc] : []),
    setActiveAccount: () => undefined,
    acquireTokenSilent:
      opts.acquireTokenSilent ??
      (() => Promise.resolve({ accessToken: 'silent-token' } as unknown as AuthenticationResult)),
    addEventCallback: (fn: (message: EventMessage) => void) => {
      callback = fn;
      return 'cb-id';
    },
    removeEventCallback: () => undefined,
  } as unknown as IPublicClientApplication;

  return {
    pca,
    fire: (eventType: EventType) => callback?.({ eventType } as EventMessage),
  };
}

const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();
const sentHeaders: Array<Record<string, string>> = [];

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
  sentHeaders.length = 0;
  fetchMock.mockImplementation((_input, init?: RequestInit) => {
    sentHeaders.push((init?.headers as Record<string, string>) ?? {});
    return Promise.resolve(
      new Response(JSON.stringify({}), { status: 200, headers: { 'content-type': 'application/json' } }),
    );
  });
  resetAuthSeam();
});

afterEach(() => {
  vi.unstubAllGlobals();
  resetAuthSeam();
});

describe('installMsalTokenBridge', () => {
  it('installs a provider so requests carry the silently-acquired token', async () => {
    const { pca } = makeFakePca({
      account,
      acquireTokenSilent: () =>
        Promise.resolve({ accessToken: 'silent-1' } as unknown as AuthenticationResult),
    });
    installMsalTokenBridge(pca, config, new QueryClient());

    await apiGet('/api/v1/identity/me');

    expect(sentHeaders[0]?.['Authorization']).toBe('Bearer silent-1');
  });

  it('attaches no bearer when there is no active account', async () => {
    const { pca } = makeFakePca({ account: null });
    installMsalTokenBridge(pca, config, new QueryClient());

    await apiGet('/api/v1/identity/me');

    expect(sentHeaders[0]).not.toHaveProperty('Authorization');
  });

  it('returns no token (and never loops) when interaction is required', async () => {
    const { pca } = makeFakePca({
      account,
      acquireTokenSilent: () =>
        Promise.reject(new InteractionRequiredAuthError('interaction_required', 'Interaction is required')),
    });
    installMsalTokenBridge(pca, config, new QueryClient());

    // The request still completes; the UI, not the transport layer, drives interactive sign-in.
    await expect(apiGet('/api/v1/identity/me')).resolves.toBeDefined();
    expect(sentHeaders[0]).not.toHaveProperty('Authorization');
  });

  it('on sign-out clears the token provider and invalidates identity', async () => {
    const queryClient = new QueryClient();
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    const { pca, fire } = makeFakePca({
      account,
      acquireTokenSilent: () =>
        Promise.resolve({ accessToken: 'silent-1' } as unknown as AuthenticationResult),
    });
    installMsalTokenBridge(pca, config, queryClient);

    fire(EventType.LOGOUT_SUCCESS);

    expect(invalidate).toHaveBeenCalledWith({ queryKey: identityKeys.me });

    // The provider was reset, so a request after sign-out carries no bearer.
    await apiGet('/api/v1/identity/me');
    expect(sentHeaders[0]).not.toHaveProperty('Authorization');
  });

  it('invalidates identity when an interactive sign-in succeeds', () => {
    const queryClient = new QueryClient();
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    const { pca, fire } = makeFakePca({ account });
    installMsalTokenBridge(pca, config, queryClient);

    fire(EventType.LOGIN_SUCCESS);

    expect(invalidate).toHaveBeenCalledWith({ queryKey: identityKeys.me });
  });
});

describe('activateRedirectAccount', () => {
  const bob: AccountInfo = {
    ...account,
    homeAccountId: 'home-2',
    localAccountId: 'local-2',
    username: 'bob@admc.example',
  };

  it('makes the redirect-returned account active even when another is already active (switch account)', async () => {
    // Alice is still the cached active account when Bob returns from the switch-account redirect.
    let active: AccountInfo | null = account;
    const pca = {
      handleRedirectPromise: () =>
        Promise.resolve({ account: bob } as unknown as AuthenticationResult),
      getActiveAccount: () => active,
      setActiveAccount: (next: AccountInfo) => {
        active = next;
      },
    } as unknown as IPublicClientApplication;

    await activateRedirectAccount(pca);

    // Without setting it from the result, Alice would stay active and every later bearer/decision
    // would be mis-attributed to her.
    expect(active).toBe(bob);
  });

  it('leaves the active account untouched when the redirect return carries no account', async () => {
    let active: AccountInfo | null = account;
    const pca = {
      handleRedirectPromise: () => Promise.resolve(null),
      getActiveAccount: () => active,
      setActiveAccount: (next: AccountInfo) => {
        active = next;
      },
    } as unknown as IPublicClientApplication;

    await activateRedirectAccount(pca);

    expect(active).toBe(account);
  });

  it('swallows a malformed redirect return so the app falls through to the sign-in gate', async () => {
    const setActiveAccount = vi.fn();
    const pca = {
      handleRedirectPromise: () => Promise.reject(new Error('bad redirect state')),
      setActiveAccount,
    } as unknown as IPublicClientApplication;

    await expect(activateRedirectAccount(pca)).resolves.toBeUndefined();
    expect(setActiveAccount).not.toHaveBeenCalled();
  });
});
