import { Suspense, useCallback, useEffect, useState } from 'react';
import { Outlet, useRouterState } from '@tanstack/react-router';
import { NavRail } from './NavRail';
import { AppHeader } from './AppHeader';
import { LoadingState } from '@/components/ui/states';

/** Root route shell: dark nav rail + main column. A single Suspense boundary
 *  covers the lazily-loaded use-case pages.
 *
 *  At narrow widths the rail collapses behind a header toggle. The v3 designs
 *  are desktop-only and define no mobile navigation pattern — see
 *  `docs/implementation/v3-design-inventory.md` finding V3-RESP-1. */
export function RootLayout() {
  const [navOpen, setNavOpen] = useState(false);
  const pathname = useRouterState({ select: (s) => s.location.pathname });

  const closeNav = useCallback(() => setNavOpen(false), []);
  const toggleNav = useCallback(() => setNavOpen((v) => !v), []);

  // Close the mobile rail on route change, so following a link never leaves it
  // open over the screen it navigated to.
  useEffect(() => {
    setNavOpen(false);
  }, [pathname]);

  // Escape closes the mobile rail.
  useEffect(() => {
    if (!navOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setNavOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [navOpen]);

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <NavRail open={navOpen} onNavigate={closeNav} />
      {navOpen ? <div className="nav-scrim" onClick={closeNav} aria-hidden="true" /> : null}
      <div className="app-main">
        <AppHeader navOpen={navOpen} onToggleNav={toggleNav} />
        <main className="app-content" id="main-content" tabIndex={-1}>
          <Suspense fallback={<LoadingState label="Loading screen…" />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
    </div>
  );
}
