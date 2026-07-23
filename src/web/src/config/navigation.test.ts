import { describe, expect, it } from 'vitest';
import { navGroups, navItems, navItemsInGroup, navItemById } from './navigation';
import type { NavGroupId } from './navigation';

describe('navigation registry', () => {
  it('has unique ids and paths', () => {
    const ids = navItems.map((i) => i.id);
    const paths = navItems.map((i) => i.path);
    expect(new Set(ids).size).toBe(ids.length);
    expect(new Set(paths).size).toBe(paths.length);
  });

  it('covers all eight business use cases', () => {
    const useCases = new Set(navItems.map((i) => i.useCase).filter(Boolean));
    expect(useCases).toEqual(new Set(['UC1', 'UC2', 'UC3', 'UC4', 'UC5', 'UC6', 'UC7', 'UC8']));
  });

  it('gives every screen at least one capability', () => {
    for (const item of navItems) {
      expect(item.capabilities.length).toBeGreaterThan(0);
    }
  });

  it('has exactly one route at the app root', () => {
    expect(navItems.filter((i) => i.path === '/')).toHaveLength(1);
  });
});

describe('v3 navigation groups (V3-NAV-001)', () => {
  it('declares the six v3 groups in v3 order', () => {
    expect(navGroups.map((g) => g.id)).toEqual([
      'executive',
      'sales',
      'supply',
      'after-sales',
      'governance',
      'platform',
    ]);
  });

  it('labels groups exactly as the v3 designs do', () => {
    expect(navGroups.map((g) => g.label)).toEqual([
      'Executive',
      'Sales Intelligence',
      'Supply Intelligence',
      'After-Sales Intelligence',
      'Governance',
      'Platform',
    ]);
  });

  it('carries the v3 delivery-phase labels, and none on Governance or Platform', () => {
    const phases = Object.fromEntries(navGroups.map((g) => [g.id, g.phase]));
    expect(phases['executive']).toBe('Phase 5');
    expect(phases['sales']).toBe('Phase 1');
    expect(phases['supply']).toBe('Phase 2–3');
    expect(phases['after-sales']).toBe('Phase 4');
    expect(phases['governance']).toBeUndefined();
    expect(phases['platform']).toBeUndefined();
  });

  it('has unique group ids', () => {
    const ids = navGroups.map((g) => g.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('assigns every nav item to a declared group', () => {
    const declared = new Set<NavGroupId>(navGroups.map((g) => g.id));
    for (const item of navItems) {
      expect(declared.has(item.group)).toBe(true);
    }
  });

  it('places each use case in the v3 group its module belongs to', () => {
    const groupOf = (useCase: string) => navItems.find((i) => i.useCase === useCase)?.group;
    expect(groupOf('UC8')).toBe('executive');
    expect(groupOf('UC1')).toBe('sales');
    expect(groupOf('UC2')).toBe('sales');
    expect(groupOf('UC3')).toBe('sales');
    expect(groupOf('UC4')).toBe('supply');
    expect(groupOf('UC5')).toBe('supply');
    expect(groupOf('UC6')).toBe('after-sales');
    expect(groupOf('UC7')).toBe('after-sales');
  });

  it('orders sales screens Order → Forecast → Configuration, as v3 does', () => {
    expect(navItemsInGroup('sales').map((i) => i.useCase)).toEqual(['UC1', 'UC2', 'UC3']);
  });

  it('orders supply screens Procurement → Inventory, as v3 does', () => {
    expect(navItemsInGroup('supply').map((i) => i.useCase)).toEqual(['UC4', 'UC5']);
  });

  it('orders after-sales screens Correlation → Spare Parts, as v3 does', () => {
    expect(navItemsInGroup('after-sales').map((i) => i.useCase)).toEqual(['UC6', 'UC7']);
  });

  it('puts Settings under Governance and Data Management under Platform', () => {
    expect(navItemById('platform-settings')?.group).toBe('governance');
    expect(navItemById('data-management')?.group).toBe('platform');
  });

  it('uses the v3 icon for every screen that v3 also has', () => {
    const expected: Record<string, string> = {
      'executive-cockpit': 'dashboard',
      'order-optimisation': 'shopping_cart_checkout',
      'sales-forecasting': 'trending_up',
      'configuration-demand': 'grid_view',
      procurement: 'local_shipping',
      'inventory-intelligence': 'inventory_2',
      'after-sales': 'handyman',
      'spare-parts': 'settings_suggest',
      'data-management': 'database',
      'platform-settings': 'settings',
    };
    for (const [id, icon] of Object.entries(expected)) {
      expect(navItemById(id)?.icon).toBe(icon);
    }
  });

  it('never lists an item without a route (no dead links in the rail)', () => {
    for (const item of navItems) {
      expect(item.path.startsWith('/')).toBe(true);
    }
  });

  it('navItemsInGroup partitions navItems exactly', () => {
    const total = navGroups.reduce((n, g) => n + navItemsInGroup(g.id).length, 0);
    expect(total).toBe(navItems.length);
  });
});
