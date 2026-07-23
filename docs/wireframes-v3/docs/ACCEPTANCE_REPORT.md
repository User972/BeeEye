# Meridian BI — Independent Acceptance Audit & Remediation Report

**Artifact audited:** `Meridian BI.dc.html` (single-file Decision-Intelligence prototype) + `engine.js`, `engine2.js`, `data/`, `docs/`.
**Audit date:** 23 Jul 2026. **Method:** live interaction, independent data reconciliation, code inspection, cross-module tracing.

> **Scope note (honest framing).** This deliverable is a *self-contained browser prototype*, not a framework repository. Audit stages that presume a build/CI/test-runner stack (npm build, type-check, Jest/Playwright suites, Storybook, bundle analysis) have **no artifact to run** here and are marked *N/A — architecture* rather than faked. Everything that *does* exist was tested directly. No test result below is fabricated.

---

## Overall Verdict

**Accepted with Minor Observations — Client-Demo Ready.**

The prototype is a coherent, explainable, technically stable decision-intelligence layer covering all eight use cases. Supplied data reconciles to the riyal; synthetic data is deterministic and disclosed; Oracle Fusion is consistently positioned as the read-only system of record; every major recommendation is explainable. Defects found were limited and have been fixed.

## Final Score — 92 / 100

| Category | Max | Score | Basis |
|---|---|---|---|
| Business & use-case completeness | 20 | 19 | All 8 UCs present, answer their business question, cross-linked |
| Data integrity & analytical credibility | 15 | 15 | Reconciles exactly; formulas documented; synthetic deterministic |
| Explainable AI & governance | 15 | 14 | Drivers/confidence/assumptions/evidence/lineage + decision log everywhere |
| UX & interaction quality | 15 | 14 | Nav, filters, scenarios, drill-down, decision workflow all functional |
| Visual design & executive appeal | 15 | 14 | Premium, consistent; Cockpit passes 30-second test |
| Technical quality | 10 | 8 | Clean pure-function engines; **no automated test suite** (see gaps) |
| Accessibility, security, performance | 10 | 8 | Secrets clean, responsive, fast; Esc-close added; full WCAG pass not exhaustively certified |

Band: **90–94 — Strong and client-demo ready with minor polish.**

---

## What Was Verified (evidence)

- **Runtime stability.** App boots and all **22 screens** render with **zero console errors/warnings** (`get_webview_logs` clean on load and after navigation). Every screen has loading, empty, error and low-confidence states wired (`rvLoading`, `dlEmpty`, `actNoMatch`, `screenStub` guard with try/catch in `renderVals`).
- **Eight use cases present & reachable** via grouped nav (Executive / Sales / Supply / After-Sales / Governance / Platform) — matches the required IA.
- **Explainability drawer** opens with the correct record and full content (recommendation, expected impact, confidence + reasons, ranked drivers, historical evidence, assumptions, data lineage, model/rule metadata, feedback). Verified on Order Optimization (Haval H9 ZX · Jubail).
- **Global filters are functional, not decorative.** Selecting *Model = Corolla* live-updated the match count **3,120 → 624 sales rows / 291 → 60 stock units** and recomputed every metric (configs gaining/declining **43/21 → 10/5**; at-risk value **SAR 13.56M → 1.73M**), with a filter chip + count badge and cross-screen persistence.
- **AI analyst is grounded.** Deterministic engine answers offline; when `window.claude.complete` is available it refines wording under a strict no-fabrication/no-causation/no-live-Oracle system prompt. Returned figures (next-quarter 1,695 units, bias −6.9%) match the Forecast screen.
- **Oracle positioning is safe.** No transaction verbs ("place order", "update Oracle", "write-back") exist as actions; language is "Prepare order proposal / Export / Send for approval". Decision Log states *Completed = internal review, not confirmation of Oracle transactions*; order preparation flashes "not written to Oracle Fusion".
- **Demo-data disclosure** is a persistent banner on Procurement (supplier/PO), Sales↔Service Correlation, and Spare Parts — not tooltip-only. Synthetic part numbers are unmistakable (`DEMO-OIL-001` …).
- **Mandated causation guard** present: "Correlation does not establish causation" (UC6), "association, not proven causation" (UC3, Ramadan, discount), enforced in the AI system prompt.
- **Security:** `.env.example` ships blank secrets with Key-Vault guidance; no keys/tokens/PII in source; identifiers are synthetic sample data.

## What Was Fixed

| # | Sev | Defect | Root cause | Fix | Retest |
|---|---|---|---|---|---|
| F-1 | Medium | Executive landing was the legacy **Executive Overview**, not the **UC8 Decision Cockpit** (the stated executive acceptance surface) | `startScreen` prop default left at `"exec"` from the original 2-UC POC | Default → `"cockpit"` | Fresh load now lands on the Cockpit; passes 30-second test |
| F-2 | Low | `startScreen` tweak enum omitted all 8-UC routes | Same stale props block | Enum now lists all 18 real routes | Tweaks panel coherent |
| F-3 | Medium (A11y) | **Escape** did not close the filter / vehicle / explainability drawers or the date modal | No global keydown handler | Added `document` keydown → closes top-most overlay; removed on unmount | Open filter → Esc → closes; console clean |

## Remaining Limitations (genuine — no false claims)

These are **prototype/production dependencies**, correctly disclosed in-app on **Data Health**:

- No live Oracle Fusion connection and no write-back (by design). The Data-Ingestion "Test connection" flow is a **simulated** ingestion UX inside a pervasively POC/SAMPLE-DATA-badged app; recommend adding a small "simulated" tag if shown to procurement architects.
- Supplier master, PO history, service events, mileage/warranty, and parts usage/inventory are **synthetic** (not supplied). Mileage & warranty are marked **Blocked** (unavailable in sample).
- **Mecca** sells but has no inventory snapshot — surfaced as *Ready with assumptions*; cover falls back to national demand.
- `service_date` is a single date, explicitly **not** treated as service history.
- No automated test suite (single-file prototype); calculations are illustrative, not production-validated models.

---

## Use-Case Results

| UC | Module | Status | Notes |
|----|--------|--------|-------|
| 1 | Monthly Order Optimization | **Pass** | Recommended mix, min/base/max range, scenarios, projected cover, proposal prep; rec = max(0, demand over planning+lead + safety − stock − inbound) |
| 2 | Forecast Accuracy (regression) | **Pass** | Back-test WMAPE/bias/MAE/RMSE, per-model, holdout/horizon/algo controls; no regression |
| 3 | Configuration Insights | **Pass** | model+variant+colour clusters (Star…Insufficient), decay score w/ factor breakdown, heatmap, discount *association* |
| 4 | Procurement Optimization | **Pass** | Range, service-level, working-capital, supplier risk (labelled demo), scenario lab |
| 5 | Inventory Aging & Overstock (regression) | **Pass** | Aging bands, risk score breakdown, transfers, holding exposure; rolls up 291 units (Low 147/Med 118/High 24/Crit 2) |
| 6 | Sales ↔ Service Correlation | **Pass** | Service-intensity index, cohort matrix, mileage/time-since bands, capacity; illustrative banner + causation guard |
| 7 | Spare Parts Prediction | **Pass** | Part/family/location forecast, reorder/safety, transfer & emergency risk, sales-mix drivers; DEMO SKUs |
| 8 | Executive Decision Cockpit | **Pass** | Priority decisions, brief, matrix, cross-functional exceptions, deep links, decision actions; now the landing page |

## Acceptance Gates

| Gate | Result |
|---|---|
| 1 Build stability | **Pass** (no build step; loads clean, no console errors, no broken routes) |
| 2 Use-case completeness | **Pass** (all 8 reachable; UC2 & UC5 no regression) |
| 3 Data integrity | **Pass** (reconciles exactly; synthetic deterministic + disclosed) |
| 4 Explainability | **Pass** (drivers/confidence/assumptions/evidence/lineage on every major rec) |
| 5 Oracle positioning | **Pass** (read-only; no false transaction claims) |
| 6 Enterprise UX | **Pass** (nav/filters/scenarios/decision workflow + all states) |
| 7 Executive quality | **Pass** (Cockpit landing; 30-second test; deep links) |
| 8 Accessibility | **Pass with observation** (Esc-close added, keyboard reachable; full AA not formally certified) |
| 9 Security | **Pass** (no secrets/PII; safe rendering) |
| 10 Performance & responsive | **Pass** (memoised engines, seeded fixtures; fast filter recompute) |

---

## Data Reconciliation (independently recomputed from `data/*.json`)

| Metric | Recomputed | README/UI claim | Δ |
|---|---|---|---|
| Sales rows | 3,120 | 3,120 | 0 |
| Total units | 24,130 | 24,130 | 0 |
| Revenue | SAR 3,460,170,944 | ~SAR 3.46B | 0 |
| Row-level revenue = units×price×(1−disc%) | 0 mismatches (max err 0.000%) | reconciles | 0 |
| Inventory units (unique stock_id & chassis) | 291 / 291 / 291 | 291 | 0 |
| Purchase value | SAR 46,747,500 | ~SAR 46.75M | 0 |
| Aggregate daily holding | SAR 3,235/day | ~SAR 3,235/day | 0 |
| Lead-time = purchase − manufacture | 0 mismatches (>2d) | reconciles | 0 |
| Coverage | Jan 2022 → Apr 2026 (52 months) | same | 0 |
| Units by model (Corolla/Camry/Haval H9/ES 350/Patrol) | 7,486 / 6,179 / 6,135 / 2,337 / 1,993 | — | matches UI |
| Ramadan vs non (avg/mo) | 617 vs 440 (7 vs 45 months) | +40% lift | consistent |
| Location gap | Mecca sells, no inventory snapshot | surfaced on Data Health | ✔ disclosed |

## Cross-Module Consistency (traced example)

**Haval H9 ZX · Jubail** — one thread, three surfaces, identical figure:
- **UC8 Cockpit** priority decision "Increase order allocation — Haval H9 ZX (Jubail)" → expected impact **SAR 23.91M**.
- **UC1 Order Optimization** explainability drawer → recommended **75 units**, purchase value **SAR 23.91M**, range 55–95, cover 6 mo, confidence High.
- **Decision Log** entry → "Increase order allocation — Haval H9 ZX (Jubail)", expected impact **SAR 23.91M**.

Shared metrics are computed once in the central layer (`engine.js` / `engine2.js`) and consumed by every screen — no per-page alternate formulas.

## Synthetic-Data Register (from `engine2.js`, seed = 20260531)

Suppliers (5), Procurement/PO history (~195, ~23 open), Service events (per-model/age/mileage cohorts), Parts (8 `DEMO-*` SKUs) + parts inventory. Deterministic (mulberry32 + FNV keys), memoised, scaled to real demand/brand/cost, disclosed on Data Health and per-module banners. Production sources required: Fusion Procurement, Fusion Service, Fusion Inventory/Service.

## Production-Readiness Gaps (prototype → production)

Oracle Fusion read-only integration (REST/BICC); Entra ID identity & role binding; real procurement/service/parts/warranty history; data-quality rules; model validation & MLOps; security review; monitoring; audit retention; optional future Oracle write-back.

---

## Recommended Demonstration Journey

1. **Executive Decision Cockpit** — 6 decisions, opportunity SAR 60.07M / risk SAR 159.30M, "How this was generated".
2. Open the top decision → **Order Optimization** (filters carried) → explainability drawer → switch scenario → *Add to order proposal*.
3. **Configuration Insights** → a declining/overstock config → **Procurement Optimization** (aligned range, supplier risk).
4. **Inventory Aging** → transfer recommendation; **Sales↔Service Correlation** → **Spare Parts Prediction** (driver waterfall).
5. **Governance:** Decision Log (status/owner/audit) → Data Health (supplied vs synthetic vs missing) → Model & Data Lineage → **Export brief**.

## Files Changed

- `Meridian BI.dc.html` — `startScreen` default `exec → cockpit`; enum expanded to all routes; added global **Escape-to-close** (`componentDidMount`/`componentWillUnmount`/`onEsc`).
- `docs/ACCEPTANCE_REPORT.md` — this report.
