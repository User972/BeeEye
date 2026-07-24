import { fireEvent, render, screen, within } from '@testing-library/react';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RootLayout } from './RootLayout';
import { AuthContext, LOCAL_AUTH } from '@/lib/auth/context';
import type { AppAuth } from '@/lib/auth/context';
import type { CurrentUser } from '@/lib/api/identity';

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const signedIn: CurrentUser = {
  isAuthenticated: true,
  subjectId: 'exec-1',
  displayName: 'An Executive',
  roles: ['Executive'],
  permissions: ['recommendation.review'],
};

const anonymous: CurrentUser = {
  isAuthenticated: false,
  subjectId: null,
  displayName: null,
  roles: [],
  permissions: [],
};

function entraAuth(over: Partial<AppAuth> = {}): AppAuth {
  return {
    mode: 'entra',
    account: null,
    isAuthenticated: true,
    signIn: vi.fn(),
    signOut: vi.fn(),
    switchAccount: vi.fn(),
    ...over,
  };
}

const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

interface ShellOptions {
  auth: AppAuth;
  identity: CurrentUser;
  initialPath?: string;
  identityPending?: boolean;
}

function renderShell({ auth, identity, initialPath = '/', identityPending = false }: ShellOptions) {
  fetchMock.mockImplementation((input: RequestInfo | URL) => {
    const url = input instanceof Request ? input.url : String(input);
    if (url.includes('/identity/me')) {
      if (identityPending) return new Promise<Response>(() => {});
      return Promise.resolve(json(identity));
    }
    return Promise.resolve(json({}));
  });

  const rootRoute = createRootRoute({ component: RootLayout });
  const index = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: () => <h1>Cockpit screen</h1>,
  });
  const decisions = createRoute({
    getParentRoute: () => rootRoute,
    path: '/decisions',
    component: () => <h1>Decision Log screen</h1>,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([index, decisions]),
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });

  return render(
    <QueryClientProvider client={client}>
      <AuthContext.Provider value={auth}>
        <RouterProvider router={router} />
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

describe('AppHeader account controls (A.6)', () => {
  it('shows the signed-in name (from /identity/me) and a sign-out control in entra mode', async () => {
    const auth = entraAuth();
    renderShell({ auth, identity: signedIn });

    expect(await screen.findByText('An Executive')).toBeInTheDocument();
    const signOut = screen.getByRole('button', { name: /sign out/i });
    fireEvent.click(signOut);
    expect(auth.signOut).toHaveBeenCalledTimes(1);
  });

  it('offers a switch-account control that re-runs interactive login', async () => {
    const auth = entraAuth();
    renderShell({ auth, identity: signedIn });

    fireEvent.click(await screen.findByRole('button', { name: /switch account/i }));
    expect(auth.switchAccount).toHaveBeenCalledTimes(1);
  });

  it('shows a sign-in control when anonymous in entra mode', async () => {
    const auth = entraAuth({ isAuthenticated: false });
    renderShell({ auth, identity: anonymous });

    // The gate and the header both offer sign-in; at least one control is present.
    expect(await screen.findByText(/sign in to continue/i)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /sign in/i }).length).toBeGreaterThan(0);
  });

  it('renders no account UI at all in local mode (parity with before sign-in existed, A.7.14)', async () => {
    renderShell({ auth: LOCAL_AUTH, identity: anonymous });

    expect(await screen.findByText('Cockpit screen')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /sign in/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /sign out/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/sign in to continue/i)).not.toBeInTheDocument();
  });
});

describe('AuthGate route gating (A.6)', () => {
  it('renders the screen when the server says the caller is authenticated', async () => {
    renderShell({ auth: entraAuth(), identity: signedIn });
    expect(await screen.findByText('Cockpit screen')).toBeInTheDocument();
  });

  it('routes an anonymous user to sign-in and does not render the screen', async () => {
    renderShell({ auth: entraAuth({ isAuthenticated: false }), identity: anonymous });

    expect(await screen.findByText(/sign in to continue/i)).toBeInTheDocument();
    expect(screen.queryByText('Decision Log screen')).not.toBeInTheDocument();
  });

  it('preserves the deep-link returnTo when signing in from the gate', async () => {
    const auth = entraAuth({ isAuthenticated: false });
    renderShell({ auth, identity: anonymous, initialPath: '/decisions' });

    const gate = (await screen.findByText(/sign in to continue/i)).closest('.signin');
    fireEvent.click(within(gate as HTMLElement).getByRole('button', { name: /sign in/i }));

    expect(auth.signIn).toHaveBeenCalledWith(expect.stringContaining('/decisions'));
  });

  it('holds a LoadingState while identity resolves, so no control flashes in then out', async () => {
    renderShell({ auth: entraAuth(), identity: signedIn, identityPending: true });

    expect(await screen.findByText('Checking your access…')).toBeInTheDocument();
    expect(screen.queryByText('Cockpit screen')).not.toBeInTheDocument();
  });

  it('does not gate in local mode — the screen renders anonymously', async () => {
    renderShell({ auth: LOCAL_AUTH, identity: anonymous });
    expect(await screen.findByText('Cockpit screen')).toBeInTheDocument();
    expect(screen.queryByText(/sign in to continue/i)).not.toBeInTheDocument();
  });
});
