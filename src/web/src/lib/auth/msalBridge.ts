import { EventType, InteractionRequiredAuthError } from '@azure/msal-browser';
import type { AccountInfo, IPublicClientApplication } from '@azure/msal-browser';
import type { QueryClient } from '@tanstack/react-query';
import { resetAuthSeam, setAccessTokenProvider, setAccessTokenRefresher } from '@/lib/api/client';
import { identityKeys } from '@/lib/api/identity';
import type { EntraAuthConfig } from './config';

/**
 * Picks a deterministic active account so token acquisition is stable across reloads and tabs
 * (A.7.7). MSAL does not choose one for you when several are cached; without this the "active"
 * account would depend on call order.
 */
export function ensureActiveAccount(pca: IPublicClientApplication): AccountInfo | null {
  const active = pca.getActiveAccount();
  if (active) {
    return active;
  }
  const [first] = pca.getAllAccounts();
  if (first) {
    pca.setActiveAccount(first);
    return first;
  }
  return null;
}

/**
 * Acquires an access token silently, or returns `null` when interaction is required.
 *
 * The transport layer never navigates: on `InteractionRequiredAuthError` (or any failure) this
 * returns `null` and lets the UI drive interactive sign-in — a full-page redirect fired from inside a
 * background request's 401 handler would discard unsaved work and could race the replay. The common
 * "access token expired, refresh token still valid" case (A.7.5) is handled here without any
 * interaction, because MSAL refreshes silently.
 */
async function acquireSilent(
  pca: IPublicClientApplication,
  apiScope: string,
  forceRefresh: boolean,
): Promise<string | null> {
  const account = ensureActiveAccount(pca);
  if (!account) {
    return null;
  }
  try {
    const result = await pca.acquireTokenSilent({ scopes: [apiScope], account, forceRefresh });
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      return null;
    }
    // A network blip or an unexpected MSAL error surfaces to the caller as the original 401/failure,
    // never as an infinite refresh loop.
    return null;
  }
}

/**
 * Wires the MSAL instance into the framework-free transport seam and refreshes identity across
 * sign-in boundaries.
 *
 * Installed once at start-up, before any route or query runs, so the first `apiGet` in Entra mode
 * already carries a bearer (closes the "first query before MSAL init" race, A.7.13).
 */
export function installMsalTokenBridge(
  pca: IPublicClientApplication,
  config: EntraAuthConfig,
  queryClient: QueryClient,
): void {
  setAccessTokenProvider(() => acquireSilent(pca, config.apiScope, false));
  setAccessTokenRefresher(() => acquireSilent(pca, config.apiScope, true));

  // Roles and permissions can only change across a sign-in boundary, so re-fetch identity when one is
  // crossed — after an interactive sign-in, a sign-out, or an account switch (A.5).
  pca.addEventCallback((message) => {
    switch (message.eventType) {
      case EventType.LOGOUT_SUCCESS:
        // Drop the bearer immediately so nothing signed-out keeps sending a token (A.7.9), then
        // refresh identity so the app reflects the signed-out state.
        resetAuthSeam();
        void queryClient.invalidateQueries({ queryKey: identityKeys.me });
        break;
      case EventType.LOGIN_SUCCESS:
      case EventType.ACTIVE_ACCOUNT_CHANGED:
        // Not ACQUIRE_TOKEN_SUCCESS: MSAL fires it on every silent token acquisition — i.e. on every
        // `apiGet` in Entra mode, including cache hits and the hourly refresh — none of which is a
        // sign-in boundary. Invalidating here would refetch `/identity/me`, whose own request fires
        // the event again, into a self-sustaining loop (`useCurrentUser` stays mounted).
        void queryClient.invalidateQueries({ queryKey: identityKeys.me });
        break;
      default:
        break;
    }
  });
}
