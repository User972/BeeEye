import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
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
import { navGroups, navItems } from '@/config/navigation';

// The shell now integrates identity (AuthGate + the header account control both read
// `/identity/me`), so it needs a QueryClient. In local mode — the default when no AppAuthProvider is
// mounted — the gate renders children immediately and the header shows no account UI, so an anonymous
// identity keeps these navigation tests behaving exactly as before.
function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const anonymousIdentity = {
  isAuthenticated: false,
  subjectId: null,
  displayName: null,
  roles: [],
  permissions: [],
};

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
  fetchMock.mockImplementation((input: string) => {
    const url = String(input);
    if (url.includes('/identity/me')) {
      return Promise.resolve(json(anonymousIdentity));
    }
    return Promise.resolve(json({}));
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/**
 * Matches a group heading by its leading label. Anchored because several v3
 * group labels are substrings of one another ("Sales Intelligence" vs
 * "After-Sales Intelligence"), and headings also carry a trailing phase label.
 */
function groupName(label: string): RegExp {
  return new RegExp(`^${label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\b`);
}

/**
 * Renders the real application shell (RootLayout → NavRail + AppHeader) inside a
 * memory router whose routes come from the same registry the production router
 * uses, so the test exercises the actual navigation wiring.
 */
async function renderShell(initialPath = '/') {
  const rootRoute = createRootRoute({ component: RootLayout });
  const routes = navItems.map((item) =>
    createRoute({
      getParentRoute: () => rootRoute,
      path: item.path,
      component: () => <h1>{item.label} screen</h1>,
    }),
  );
  const router = createRouter({
    routeTree: rootRoute.addChildren(routes),
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  });

  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const result = render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  await screen.findByRole('navigation', { name: /primary navigation/i });
  return result;
}

describe('NavRail (V3-NAV-001)', () => {
  it('renders every non-empty v3 group as a labelled heading', async () => {
    await renderShell();
    const populated = navGroups.filter((g) => navItems.some((i) => i.group === g.id));
    for (const group of populated) {
      // Anchored: "Sales Intelligence" is a substring of "After-Sales Intelligence".
      expect(screen.getByRole('heading', { name: groupName(group.label) })).toBeInTheDocument();
    }
  });

  it('shows the delivery-phase label for groups that have one', async () => {
    await renderShell();
    const heading = screen.getByRole('heading', { name: groupName('Sales Intelligence') });
    expect(heading).toHaveTextContent('Phase 1');

    const governance = screen.getByRole('heading', { name: groupName('Governance') });
    expect(governance).not.toHaveTextContent(/Phase/);
  });

  it('renders one link per registry item, with no dead links', async () => {
    await renderShell();
    const nav = screen.getByRole('navigation', { name: /primary navigation/i });
    const links = within(nav).getAllByRole('link');
    expect(links).toHaveLength(navItems.length);
    for (const link of links) {
      expect(link.getAttribute('href')).toBeTruthy();
    }
  });

  it('groups links under the correct heading', async () => {
    await renderShell();
    const nav = screen.getByRole('navigation', { name: /primary navigation/i });
    const salesGroup = within(nav).getByRole('group', { name: groupName('Sales Intelligence') });
    const labels = within(salesGroup)
      .getAllByRole('link')
      .map((a) => a.textContent);
    expect(labels.join(' ')).toMatch(/Order Optimisation/);
    expect(labels.join(' ')).toMatch(/Forecast Accuracy/);
    expect(labels.join(' ')).toMatch(/Configuration Insights/);
  });

  it('marks the current screen with aria-current="page"', async () => {
    await renderShell('/inventory');
    const current = screen.getByRole('link', { current: 'page' });
    expect(current).toHaveTextContent(/Inventory Aging & Overstock/);
  });

  it('marks only the root link active at "/" (exact matching)', async () => {
    await renderShell('/');
    const current = screen.getAllByRole('link', { current: 'page' });
    expect(current).toHaveLength(1);
    expect(current[0]).toHaveTextContent(/Decision Cockpit/);
  });

  it('exposes the use-case code alongside the label', async () => {
    await renderShell();
    const link = screen.getByRole('link', { name: /Inventory Aging & Overstock/ });
    expect(link).toHaveTextContent('UC5');
  });
});

describe('application shell accessibility (V3-NAV-002)', () => {
  it('offers a skip link that targets the main region', async () => {
    await renderShell();
    const skip = screen.getByRole('link', { name: /skip to main content/i });
    expect(skip).toHaveAttribute('href', '#main-content');
    expect(document.getElementById('main-content')).toBeInTheDocument();
  });

  it('renders exactly one main landmark', async () => {
    await renderShell();
    expect(screen.getAllByRole('main')).toHaveLength(1);
  });

  it('names the navigation landmark', async () => {
    await renderShell();
    expect(screen.getByRole('navigation', { name: /primary navigation/i })).toBeInTheDocument();
  });
});

describe('mobile navigation (V3-NAV-003)', () => {
  it('starts collapsed and reports that via aria-expanded', async () => {
    await renderShell();
    const toggle = screen.getByRole('button', { name: /open navigation menu/i });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(toggle).toHaveAttribute('aria-controls', 'nav-rail');
  });

  it('expands and collapses when the toggle is used', async () => {
    await renderShell();

    fireEvent.click(screen.getByRole('button', { name: /open navigation menu/i }));
    const close = screen.getByRole('button', { name: /close navigation menu/i });
    expect(close).toHaveAttribute('aria-expanded', 'true');

    fireEvent.click(close);
    expect(screen.getByRole('button', { name: /open navigation menu/i })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });

  it('renders a dismissing scrim only while expanded', async () => {
    const { container } = await renderShell();
    expect(container.querySelector('.nav-scrim')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: /open navigation menu/i }));
    const scrim = container.querySelector('.nav-scrim');
    expect(scrim).not.toBeNull();

    fireEvent.click(scrim as Element);
    expect(screen.getByRole('button', { name: /open navigation menu/i })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });

  it('closes on Escape', async () => {
    await renderShell();

    fireEvent.click(screen.getByRole('button', { name: /open navigation menu/i }));
    fireEvent.keyDown(window, { key: 'Escape' });

    expect(screen.getByRole('button', { name: /open navigation menu/i })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });

  it('closes after following a link, so it never covers the new screen', async () => {
    await renderShell();

    fireEvent.click(screen.getByRole('button', { name: /open navigation menu/i }));
    expect(screen.getByRole('button', { name: /close navigation menu/i })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('link', { name: /Spare Parts Prediction/ }));

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /open navigation menu/i })).toHaveAttribute(
        'aria-expanded',
        'false',
      ),
    );
  });
});
