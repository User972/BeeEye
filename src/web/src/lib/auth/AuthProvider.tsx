import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { MsalProvider, useMsal } from '@azure/msal-react';
import { EventType } from '@azure/msal-browser';
import type {
  AccountInfo,
  AuthenticationResult,
  IPublicClientApplication,
} from '@azure/msal-browser';
import { AuthContext, LOCAL_AUTH } from './context';
import type { AppAuth } from './context';
import type { AuthMode, EntraAuthConfig } from './config';

/** Resolves a router href (`/decisions?x=1`) or a full URL to an absolute URL for `redirectStartPage`. */
function absoluteUrl(pathOrUrl: string): string {
  try {
    return new URL(pathOrUrl, window.location.origin).toString();
  } catch {
    return window.location.origin;
  }
}

function EntraAuthInner({ config, children }: { config: EntraAuthConfig; children: ReactNode }) {
  const { instance } = useMsal();
  const [account, setAccount] = useState<AccountInfo | null>(
    () => instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null,
  );

  useEffect(() => {
    const syncAccount = () =>
      setAccount(instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null);

    const callbackId = instance.addEventCallback((message) => {
      // A fresh interactive sign-in names the account to make active; without this a second account
      // would sign in but the app would keep acting as the first (A.7.7).
      if (message.eventType === EventType.LOGIN_SUCCESS) {
        const payload = message.payload as AuthenticationResult | null;
        if (payload?.account) {
          instance.setActiveAccount(payload.account);
        }
      }
      syncAccount();
    });

    // A sign-in that completed during the pre-render redirect handling has already landed; reflect it.
    syncAccount();

    return () => {
      if (callbackId) {
        instance.removeEventCallback(callbackId);
      }
    };
  }, [instance]);

  const value = useMemo<AppAuth>(
    () => ({
      mode: 'entra',
      account,
      isAuthenticated: account !== null,
      signIn: (returnTo?: string) => {
        void instance.loginRedirect({
          scopes: [config.apiScope],
          ...(returnTo ? { redirectStartPage: absoluteUrl(returnTo) } : {}),
        });
      },
      signOut: () => {
        void instance.logoutRedirect({ ...(account ? { account } : {}) });
      },
      switchAccount: () => {
        void instance.loginRedirect({ scopes: [config.apiScope], prompt: 'select_account' });
      },
    }),
    [account, config.apiScope, instance],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

interface AppAuthProviderProps {
  mode: AuthMode;
  /** Required in `entra` mode. Omitted in `local` mode, which renders children with no MSAL. */
  pca?: IPublicClientApplication;
  config?: EntraAuthConfig;
  children: ReactNode;
}

/**
 * Provides the auth surface to the tree.
 *
 * In `entra` mode it wraps the app in `<MsalProvider>` and exposes real sign-in/out actions; in
 * `local` mode it provides the no-op `LOCAL_AUTH`, so every screen renders anonymously exactly as it
 * did before sign-in existed (A.7.14).
 */
export function AppAuthProvider({ mode, pca, config, children }: AppAuthProviderProps) {
  if (mode === 'entra' && pca && config) {
    return (
      <MsalProvider instance={pca}>
        <EntraAuthInner config={config}>{children}</EntraAuthInner>
      </MsalProvider>
    );
  }
  return <AuthContext.Provider value={LOCAL_AUTH}>{children}</AuthContext.Provider>;
}
