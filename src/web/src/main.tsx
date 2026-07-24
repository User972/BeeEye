import { StrictMode } from 'react';
import type { ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { PublicClientApplication } from '@azure/msal-browser';
import { router } from './router';
import { resolveAuthConfig, msalConfiguration } from '@/lib/auth/config';
import { AppAuthProvider } from '@/lib/auth/AuthProvider';
import {
  activateRedirectAccount,
  ensureActiveAccount,
  installMsalTokenBridge,
} from '@/lib/auth/msalBridge';
import '@/styles/fonts.css';
import '@/styles/global.css';
import '@/styles/components.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
});

// Reveal icons only once the icon font is actually usable — otherwise every icon renders
// as its raw ligature name. See the .icons-ready rule in components.css.
if ('fonts' in document) {
  document.fonts
    .load('24px "Material Symbols Outlined"')
    .then((loaded) => {
      if (loaded.length > 0) {
        document.documentElement.classList.add('icons-ready');
      }
    })
    .catch(() => {
      /* offline: icons stay hidden, text labels carry the meaning */
    });
}

function mount(tree: ReactNode): void {
  const rootElement = document.getElementById('root');
  if (!rootElement) {
    throw new Error('Root element #root not found.');
  }
  createRoot(rootElement).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>{tree}</QueryClientProvider>
    </StrictMode>,
  );
}

/**
 * Boots the SPA.
 *
 * In `entra` mode MSAL is initialised, the redirect return is processed **exactly once** (here, in
 * imperative bootstrap rather than an effect, so React 19 StrictMode's double-invoked effects cannot
 * double-handle it — A.7.12), and the token bridge is installed *before* the router renders, so the
 * first `apiGet` already carries a bearer (A.7.13). In `local` mode none of this runs and the app
 * renders anonymously exactly as today.
 */
async function bootstrap(): Promise<void> {
  const config = resolveAuthConfig(
    {
      VITE_AUTH_MODE: import.meta.env.VITE_AUTH_MODE,
      VITE_AAD_CLIENT_ID: import.meta.env.VITE_AAD_CLIENT_ID,
      VITE_AAD_AUTHORITY: import.meta.env.VITE_AAD_AUTHORITY,
      VITE_AAD_API_SCOPE: import.meta.env.VITE_AAD_API_SCOPE,
      VITE_AAD_REDIRECT_URI: import.meta.env.VITE_AAD_REDIRECT_URI,
    },
    window.location.origin,
  );

  if (config.mode === 'local') {
    mount(
      <AppAuthProvider mode="local">
        <RouterProvider router={router} />
      </AppAuthProvider>,
    );
    return;
  }

  const pca = new PublicClientApplication(msalConfiguration(config));
  await pca.initialize();
  // Process the redirect return exactly once and make the returned account active — the switch-account
  // flow depends on this, and a malformed return falls through to the sign-in gate (A.7.2, A.7.6, A.7.7).
  await activateRedirectAccount(pca);
  ensureActiveAccount(pca);
  installMsalTokenBridge(pca, config, queryClient);

  mount(
    <AppAuthProvider mode="entra" pca={pca} config={config}>
      <RouterProvider router={router} />
    </AppAuthProvider>,
  );
}

void bootstrap().catch((error: unknown) => {
  // A misconfigured *deployed* build fails loud rather than booting anonymously (A.8). Surface a plain
  // message instead of a silent white screen; never print token material.
  const rootElement = document.getElementById('root');
  if (rootElement) {
    rootElement.textContent =
      'BeeEye could not start: authentication is misconfigured. See the console for details.';
  }
  console.error('BeeEye bootstrap failed:', error instanceof Error ? error.message : error);
});
