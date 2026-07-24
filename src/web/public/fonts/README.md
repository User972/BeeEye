# Vendored web fonts (self-hosted)

These fonts are redistributed with the app so it makes **zero external font requests** (V3-DS-003 /
V3-CONFLICT-3). They replace the Google Fonts CDN that `index.html` previously loaded. `@font-face`
declarations live in [`../../src/styles/fonts.css`](../../src/styles/fonts.css); the files are served from
`/fonts/` (Vite copies `public/` to the dist root, so the path is stable in dev, `preview` and prod).

| Family | Files | Weights | Source | Version | Licence |
|--------|-------|---------|--------|---------|---------|
| IBM Plex Sans | `ibm-plex-sans-{latin,latin-ext}.woff2` | variable 400–700 (one file per subset) | Google Fonts (fonts.google.com) | v23 | SIL Open Font License 1.1 |
| IBM Plex Mono | `ibm-plex-mono-{400,600}-{latin,latin-ext}.woff2` | 400, 600 | Google Fonts | v20 | SIL Open Font License 1.1 |
| Material Symbols Outlined | `material-symbols-outlined.woff2` | 400 (static instance at `opsz,wght,FILL,GRAD@20..24,400,0,0`) | Google Fonts | v362 | Apache License 2.0 |

Vendored from the Google Fonts distribution; the request hosts that previously served them at runtime
(the `googleapis`/`gstatic` domains) are no longer contacted by the app.

## Notes

- **Material Symbols is the full icon set, not glyph-subset** — subsetting would let a newly-referenced
  icon silently break. It is the single large asset (~713 KB woff2).
- **Material Symbols is Apache-2.0, not OFL.** The S8 brief described both families as OFL-1.1; the icon
  font is in fact Apache-2.0, so its own licence text ships beside it rather than an OFL copy.
- The `unicode-range` on the IBM Plex faces mirrors Google's `latin` / `latin-ext` split, so only the
  subset a page actually needs is fetched. Non-Latin characters fall back to the system stack per-glyph.

## Licences (shipped alongside the fonts)

- [`LICENSE-IBM-Plex-OFL.txt`](LICENSE-IBM-Plex-OFL.txt) — SIL OFL 1.1, © 2017 IBM Corp., Reserved Font Name "Plex".
- [`LICENSE-Material-Symbols-Apache-2.0.txt`](LICENSE-Material-Symbols-Apache-2.0.txt) — Apache License 2.0, © Google LLC.
