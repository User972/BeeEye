import { lazy } from 'react';
import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import { RootLayout } from '@/components/layout/RootLayout';
import { navItems } from '@/config/navigation';

const rootRoute = createRootRoute({ component: RootLayout });

/**
 * Explicit lazy imports (one per screen) so Vite can code-split each use-case
 * page into its own chunk. Keyed by nav item id.
 */
const pages: Record<string, ReturnType<typeof lazy>> = {
  'executive-cockpit': lazy(() => import('@/pages/executive-cockpit')),
  'sales-forecasting': lazy(() => import('@/pages/sales-forecasting')),
  'inventory-intelligence': lazy(() => import('@/pages/inventory-intelligence')),
  'order-optimisation': lazy(() => import('@/pages/order-optimisation')),
  'configuration-demand': lazy(() => import('@/pages/configuration-demand')),
  procurement: lazy(() => import('@/pages/procurement')),
  'after-sales': lazy(() => import('@/pages/after-sales')),
  'spare-parts': lazy(() => import('@/pages/spare-parts')),
  'data-management': lazy(() => import('@/pages/data-management')),
  'platform-settings': lazy(() => import('@/pages/platform-settings')),
};

const routes = navItems.flatMap((item) => {
  const component = pages[item.id];
  if (!component) return [];
  return [
    createRoute({
      getParentRoute: () => rootRoute,
      path: item.path,
      component,
    }),
  ];
});

const routeTree = rootRoute.addChildren(routes);

export const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
  defaultPreloadStaleTime: 0,
});
