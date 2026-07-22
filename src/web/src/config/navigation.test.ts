import { describe, expect, it } from 'vitest';
import { navItems } from './navigation';

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
