import { createContext, useContext } from 'react';
import type { AccountInfo } from '@azure/msal-browser';
import type { AuthMode } from './config';

/**
 * The auth surface the UI consumes — mode plus the three interactive actions.
 *
 * Whether a control is *shown* may read `isAuthenticated` here, but what the server *accepts* is never
 * decided from this object: permissions come from `GET /api/v1/identity/me`, and the display name too.
 * This carries only what the browser legitimately knows about its own MSAL session.
 */
export interface AppAuth {
  mode: AuthMode;
  /** The active MSAL account, or `null` when anonymous / in local mode. */
  account: AccountInfo | null;
  /** Whether MSAL holds an account. The *server's* word (`/identity/me`) still gates the app. */
  isAuthenticated: boolean;
  /** Starts interactive sign-in, returning the user to `returnTo` afterwards. No-op in local mode. */
  signIn: (returnTo?: string) => void;
  /** Signs out and lands on the signed-out state. No-op in local mode. */
  signOut: () => void;
  /** Re-runs interactive login to pick a different account (A.7.7). No-op in local mode. */
  switchAccount: () => void;
}

/**
 * The default: local mode, nobody signed in, every action a no-op. This is what `useAppAuth` returns
 * when no provider is mounted, so a component rendered bare behaves exactly as it did before sign-in
 * existed rather than throwing.
 */
export const LOCAL_AUTH: AppAuth = {
  mode: 'local',
  account: null,
  isAuthenticated: false,
  signIn: () => {},
  signOut: () => {},
  switchAccount: () => {},
};

export const AuthContext = createContext<AppAuth>(LOCAL_AUTH);

/** The current auth surface. Defaults to local mode outside an `AppAuthProvider`. */
export function useAppAuth(): AppAuth {
  return useContext(AuthContext);
}
