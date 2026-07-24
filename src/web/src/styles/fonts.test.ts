import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

/**
 * V3-DS-003 — self-hosted fonts. Proves the app references no Google font host anywhere in its HTML or
 * CSS, that every @font-face is served from a local /fonts/*.woff2 that actually exists, and that the
 * icons-ready gate is preserved (Material Symbols uses font-display: block). A reintroduced CDN link, a
 * typo'd font path, or a missing binary fails here rather than as a silent external request in the browser.
 */
const read = (rel: string): string => readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8');
const exists = (rel: string): boolean => existsSync(fileURLToPath(new URL(rel, import.meta.url)));

const indexHtml = read('../../index.html');
const fontsCss = read('./fonts.css');
const mainTsx = read('../main.tsx');
const stylesheets: Record<string, string> = {
  'fonts.css': fontsCss,
  'global.css': read('./global.css'),
  'components.css': read('./components.css'),
  'tokens.css': read('./tokens.css'),
};

const GOOGLE_FONT_HOSTS = ['fonts.googleapis.com', 'fonts.gstatic.com'];
const fontUrls = [...fontsCss.matchAll(/url\((['"]?)([^'")]+)\1\)/g)]
  .map((m) => m[2])
  .filter((u): u is string => typeof u === 'string');

describe('self-hosted fonts (V3-DS-003)', () => {
  it('index.html loads no font from a Google host and preconnects to none', () => {
    for (const host of GOOGLE_FONT_HOSTS) {
      expect(indexHtml).not.toContain(host);
    }
    expect(indexHtml).not.toMatch(/preconnect/i);
  });

  it('no stylesheet references a Google font host', () => {
    for (const [name, css] of Object.entries(stylesheets)) {
      for (const host of GOOGLE_FONT_HOSTS) {
        expect(css, `${name} must not reference ${host}`).not.toContain(host);
      }
    }
  });

  it('main.tsx imports the self-hosted @font-face declarations', () => {
    expect(mainTsx).toContain('styles/fonts.css');
  });

  it('every @font-face src is a local /fonts/*.woff2 that exists on disk', () => {
    expect(fontUrls.length).toBeGreaterThan(0);
    for (const url of fontUrls) {
      expect(url.startsWith('/fonts/'), `${url} must be self-hosted`).toBe(true);
      expect(url.endsWith('.woff2'), `${url} must be woff2`).toBe(true);
      expect(exists(url.replace('/fonts/', '../../public/fonts/')), `${url} must exist`).toBe(true);
    }
  });

  it('declares all three families and keeps the icon font on font-display: block', () => {
    expect(fontsCss).toContain("font-family: 'IBM Plex Sans'");
    expect(fontsCss).toContain("font-family: 'IBM Plex Mono'");
    expect(fontsCss).toContain("font-family: 'Material Symbols Outlined'");

    // The Material Symbols face must use font-display: block so the icons-ready gate never flashes
    // raw ligature text (main.tsx + the .icons-ready rule in components.css depend on this).
    const symbolsFace = fontsCss.slice(fontsCss.indexOf("font-family: 'Material Symbols Outlined'"));
    expect(symbolsFace).toMatch(/font-display:\s*block/);
  });

  it('ships each font licence alongside the binaries', () => {
    expect(exists('../../public/fonts/LICENSE-IBM-Plex-OFL.txt')).toBe(true);
    expect(exists('../../public/fonts/LICENSE-Material-Symbols-Apache-2.0.txt')).toBe(true);
  });
});
