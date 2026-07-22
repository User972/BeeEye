import { Suspense } from 'react';
import { Outlet } from '@tanstack/react-router';
import { NavRail } from './NavRail';
import { AppHeader } from './AppHeader';
import { LoadingState } from '@/components/ui/states';

/** Root route shell: dark nav rail + main column. A single Suspense boundary
 *  covers the lazily-loaded use-case pages. */
export function RootLayout() {
  return (
    <div className="app-shell">
      <NavRail />
      <div className="app-main">
        <AppHeader />
        <main className="app-content">
          <Suspense fallback={<LoadingState label="Loading screen…" />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
    </div>
  );
}
