/* ADMC Decision Intelligence — extended analytics engine (UC1/3/4/6/7/8 + governance).
   Pure, framework-free. Exposed as window.BIEngine2. Layers on top of window.BIEngine (engine.js).
   Every value is either derived from the supplied workbooks (via BIEngine) or generated from
   DETERMINISTIC, seeded synthetic fixtures (clearly labelled "Demo Data"). No live systems. */
(function () {
  "use strict";
  var SEED = 20260531; // fixed seed → identical results on every load / screen
  function E() { return window.BIEngine; }

  /* ---------------- deterministic RNG (mulberry32 + FNV key hashing) ---------------- */
  function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; var t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  function fnv(str) { var h = 2166136261; for (var i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
  function rng(key) { var r = mulberry32(fnv("ADMC|" + key) ^ SEED); return { f: r, i: function (lo, hi) { return lo + Math.floor(r() * (hi - lo + 1)); }, pick: function (a) { return a[Math.floor(r() * a.length)]; }, norm: function (m, s) { var u = Math.max(1e-9, r()), v = r(); return m + s * Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v); }, range: function (lo, hi) { return lo + r() * (hi - lo); } }; }

  /* ---------------- small utils ---------------- */
  function sum(a) { var s = 0; for (var i = 0; i < a.length; i++) s += a[i]; return s; }
  function mean(a) { return a.length ? sum(a) / a.length : 0; }
  function std(a) { if (a.length < 2) return 0; var m = mean(a); return Math.sqrt(mean(a.map(function (x) { return (x - m) * (x - m); }))); }
  function median(a) { if (!a.length) return 0; var s = a.slice().sort(function (x, y) { return x - y; }); var n = s.length; return n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2; }
  function clamp(x, lo, hi) { return Math.max(lo, Math.min(hi, x)); }
  function round(x, d) { var p = Math.pow(10, d || 0); return Math.round(x * p) / p; }
  function uniq(a) { return Array.from(new Set(a)); }
  function memo(fn) { var v, done = false; return function () { if (!done) { v = fn(); done = true; } return v; }; }
  function memoKey(fn) { var c = {}; return function (k) { if (!(k in c)) c[k] = fn(k); return c[k]; }; }
  var zForSL = function (sl) { var t = { 0.9: 1.28, 0.92: 1.41, 0.95: 1.645, 0.97: 1.88, 0.98: 2.05, 0.99: 2.33 }; return t[sl] || 1.645; };

  /* ---------------- AI output / status labels (shared vocabulary) ---------------- */
  var LABELS = {
    observed: { t: "Observed", c: "var(--text-muted)", bg: "var(--surface-3)", icon: "fact_check" },
    calculated: { t: "Calculated", c: "var(--primary-ink)", bg: "var(--primary-weak)", icon: "calculate" },
    forecast: { t: "Forecast", c: "var(--primary-2)", bg: "color-mix(in oklch, var(--primary-2) 15%, transparent)", icon: "insights" },
    recommendation: { t: "Recommendation", c: "var(--ai-1)", bg: "color-mix(in oklch, var(--ai-1) 14%, transparent)", icon: "auto_awesome" },
    simulation: { t: "Simulation", c: "var(--primary)", bg: "var(--primary-weak)", icon: "science" },
    demo: { t: "Demo Data", c: "oklch(0.5 0.16 300)", bg: "color-mix(in oklch, oklch(0.55 0.16 300) 13%, transparent)", icon: "biotech" },
    low: { t: "Low Confidence", c: "var(--risk-high)", bg: "color-mix(in oklch, var(--risk-high) 14%, transparent)", icon: "help" },
    dq: { t: "Data Quality", c: "var(--risk-med)", bg: "color-mix(in oklch, var(--risk-med) 16%, transparent)", icon: "rule" }
  };

  /* ---------------- base data access (memoized) ---------------- */
  var D = memo(function () { return E().dims(); });
  var lastMonth = memo(function () { return D().lastMonth; });
  var planningMonth = memo(function () { return E().addMonth(lastMonth(), 1); });
  function planningMonths(n) { var out = [], c = planningMonth(); for (var i = 0; i < n; i++) { out.push(c); c = E().addMonth(c, 1); } return out; }

  // seasonal index by calendar month (1..12), normalized to mean 1
  var seasonalIndex = memo(function () {
    var s = E().normSales(); var m = {}, cnt = {};
    s.forEach(function (r) { var mo = +r._mk.slice(5, 7); m[mo] = (m[mo] || 0) + r.units_sold; cnt[mo] = (cnt[mo] || 0) + 1; });
    var idx = {}, avgs = []; for (var i = 1; i <= 12; i++) { avgs.push(cnt[i] ? m[i] / cnt[i] : 0); }
    var base = mean(avgs.filter(function (x) { return x > 0; })) || 1;
    for (var j = 1; j <= 12; j++) idx[j] = cnt[j] ? clamp((m[j] / cnt[j]) / base, 0.7, 1.4) : 1;
    return idx;
  });
  function seasonalFor(mk) { return seasonalIndex()[+mk.slice(5, 7)] || 1; }

  // unit cost per model|variant (avg inventory purchase price → fallback sales price)
  var costMap = memo(function () {
    var inv = E().normInv(), s = E().normSales(); var m = {};
    inv.forEach(function (r) { var k = r.model + "|" + r.variant; (m[k] = m[k] || { inv: [], sale: [] }).inv.push(r.purchase_price); });
    s.forEach(function (r) { var k = r.model + "|" + r.variant; (m[k] = m[k] || { inv: [], sale: [] }).sale.push(r.unit_price); });
    var out = {}; Object.keys(m).forEach(function (k) { out[k] = Math.round(m[k].inv.length ? mean(m[k].inv) : (m[k].sale.length ? mean(m[k].sale) * 0.86 : 100000)); });
    return out;
  });
  function unitCost(model, variant) { var m = costMap(); return m[model + "|" + variant] || m[model + "|" + (D().modelMap[model] || { variants: [] }).variants[0]] || 120000; }

  // observed lead-time (days) stats per model, from inventory (manufacture→purchase age = supply age)
  var obsLead = memoKey(function (model) {
    var v = E().normInv().filter(function (r) { return r.model === model; }).map(function (r) { return r.lead_time_days; });
    if (!v.length) v = E().normInv().map(function (r) { return r.lead_time_days; });
    return { median: Math.round(median(v)), mean: Math.round(mean(v)), min: Math.min.apply(null, v), max: Math.max.apply(null, v), std: Math.round(std(v)), n: v.length, vals: v };
  });

  // national monthly demand per model|variant and per model|variant|location share
  var demBase = memo(function () {
    var s = E().normSales(), months = D().months.length; var mv = {}, mvl = {}, mvTot = {};
    s.forEach(function (r) { var k = r.model + "|" + r.variant; mv[k] = (mv[k] || 0) + r.units_sold; mvl[k + "|" + r.location] = (mvl[k + "|" + r.location] || 0) + r.units_sold; mvTot[k] = (mvTot[k] || 0) + r.units_sold; });
    return { mv: mv, mvl: mvl, mvTot: mvTot, months: months };
  });
  function nationalMonthly(model, variant) { var b = demBase(); return (b.mv[model + "|" + variant] || 0) / (b.months || 1); }

  /* =================================================================================
     SYNTHETIC FIXTURES (deterministic, seeded, clearly labelled Demo Data)
     ================================================================================= */
  var DELAY_REASONS = ["On schedule", "Port congestion at Jeddah Islamic Port", "Customs clearance hold", "Upstream production delay", "Shipping capacity shortage", "Documentation / LC hold"];
  var SUPPLIERS = memo(function () {
    var base = [
      { id: "SUP-A", name: "Supplier A", region: "East Asia", brands: ["Toyota", "Lexus"], tier: "Strategic", onTime: 0.94, avgDelay: 6, fill: 0.98, cancel: 0.02, leadVar: 0.12, leadBase: 62 },
      { id: "SUP-B", name: "Supplier B", region: "East Asia", brands: ["Nissan"], tier: "Core", onTime: 0.85, avgDelay: 15, fill: 0.93, cancel: 0.05, leadVar: 0.24, leadBase: 88 },
      { id: "SUP-OEM", name: "Regional OEM Hub", region: "GCC", brands: ["HAVAL"], tier: "Watch", onTime: 0.76, avgDelay: 24, fill: 0.90, cancel: 0.08, leadVar: 0.32, leadBase: 104 },
      { id: "SUP-IMP", name: "Central Import Partner", region: "Europe", brands: ["Toyota", "Nissan", "HAVAL", "Lexus"], tier: "Core", onTime: 0.90, avgDelay: 9, fill: 0.95, cancel: 0.03, leadVar: 0.16, leadBase: 74 },
      { id: "SUP-GLA", name: "Gulf Logistics Alliance", region: "GCC", brands: ["Toyota", "Nissan"], tier: "Watch", onTime: 0.82, avgDelay: 18, fill: 0.92, cancel: 0.06, leadVar: 0.26, leadBase: 95 }
    ];
    return base;
  });
  function supplierForBrand(brand) { var s = SUPPLIERS().filter(function (x) { return x.brands.indexOf(brand) >= 0; }); return s[0] || SUPPLIERS()[3]; }
  function altSupplierForBrand(brand) { var s = SUPPLIERS().filter(function (x) { return x.brands.indexOf(brand) >= 0; }); return s[1] || SUPPLIERS()[3]; }

  // Procurement / PO history — trailing 18 months, plus open (inbound) POs near planning
  var procurement = memo(function () {
    var out = [], D0 = D(), months = D0.months.slice(-18); var adMs = Date.parse(planningMonth() + "-01");
    D0.models.forEach(function (model) {
      var mm = D0.modelMap[model]; var brand = mm.brand;
      mm.variants.forEach(function (variant) {
        var natM = nationalMonthly(model, variant); if (natM < 0.2) return;
        var sup = supplierForBrand(brand); var r = rng("po|" + model + "|" + variant);
        var moq = [2, 3, 5, 5, 10][Math.floor(r.f() * 5)]; var mult = [1, 1, 2, 5][Math.floor(r.f() * 4)];
        var seq = 0;
        months.forEach(function (mk, mi) {
          if (mi % 2 !== 0 && r.f() > 0.5) return; // ~every 1-2 months
          seq++;
          var use = (mi % 3 === 0) ? sup : (r.f() < 0.25 ? altSupplierForBrand(brand) : sup);
          var lead = Math.round(use.leadBase * (1 + r.norm(0, use.leadVar)));
          lead = clamp(lead, 30, 180);
          var qty = Math.max(moq, Math.round(natM * (1.4 + r.range(-0.4, 0.9)) / mult) * mult);
          var orderMs = Date.parse(mk + "-15");
          var promisedMs = orderMs + lead * 86400000;
          var late = r.f() > use.onTime;
          var delay = late ? Math.round(use.avgDelay * r.range(0.6, 1.8)) : Math.round(r.range(-3, 1));
          var cancelled = r.f() < use.cancel;
          var actualMs = promisedMs + delay * 86400000;
          var open = actualMs > adMs; // still in transit / not yet received
          var recQty = cancelled ? 0 : Math.round(qty * (r.f() > use.fill ? r.range(0.8, 0.97) : 1));
          var pid = "PO-" + model.replace(/[^A-Za-z0-9]/g, "").slice(0, 3).toUpperCase() + variant + "-" + String(1000 + seq);
          out.push({
            po_id: pid, supplier_id: use.id, supplier_name: use.name, supplier_region: use.region,
            model: model, variant: variant, brand: brand, destination: r.pick(D0.invLocations.length ? D0.invLocations : D0.locations),
            order_date: mk + "-15", promised: new Date(promisedMs).toISOString().slice(0, 10), actual: cancelled ? null : new Date(actualMs).toISOString().slice(0, 10),
            qty_ordered: qty, qty_received: recQty, unit_cost: unitCost(model, variant), moq: moq, order_multiple: mult,
            lead_days: lead, delay_days: cancelled ? null : delay, expedited: !cancelled && late && r.f() < 0.4,
            cancelled: cancelled, delay_reason: cancelled ? "Order cancelled" : (delay > 3 ? r.pick(DELAY_REASONS.slice(1)) : "On schedule"),
            status: cancelled ? "Cancelled" : (open ? (r.f() < 0.5 ? "In transit" : "Confirmed") : "Received"), open: open && !cancelled,
            eta_days: open && !cancelled ? Math.max(1, Math.round((actualMs - adMs) / 86400000)) : null, source: "Synthetic demo fixture"
          });
        });
      });
    });
    return out;
  });
  // inbound qty by model|variant (open POs) — feeds UC1 "expected inbound"
  var inboundMap = memo(function () { var m = {}; procurement().forEach(function (p) { if (p.open) { var k = p.model + "|" + p.variant; m[k] = (m[k] || 0) + p.qty_ordered; } }); return m; });
  function inboundFor(model, variant) { return inboundMap()[model + "|" + variant] || 0; }

  // supplier performance recomputed from generated POs (internally consistent)
  var supplierPerf = memo(function () {
    var po = procurement(); var by = {};
    SUPPLIERS().forEach(function (s) { by[s.id] = { id: s.id, name: s.name, region: s.region, tier: s.tier, brands: s.brands, n: 0, onTime: 0, delaySum: 0, delayN: 0, cancel: 0, ordered: 0, received: 0, expedited: 0, leadVals: [] }; });
    po.forEach(function (p) { var b = by[p.supplier_id]; if (!b) return; b.n++; if (p.cancelled) b.cancel++; else { if (p.delay_days <= 2) b.onTime++; b.delaySum += Math.max(0, p.delay_days); b.delayN++; b.ordered += p.qty_ordered; b.received += p.qty_received; b.leadVals.push(p.lead_days); if (p.expedited) b.expedited++; } });
    return Object.keys(by).map(function (k) { var b = by[k]; var active = b.n - b.cancel || 1; return { id: b.id, name: b.name, region: b.region, tier: b.tier, brands: b.brands, orders: b.n, onTimePct: Math.round(b.onTime / active * 100), avgDelay: Math.round(b.delaySum / (b.delayN || 1)), fillPct: Math.round((b.received / (b.ordered || 1)) * 100), cancelPct: Math.round(b.cancel / b.n * 100), expeditedPct: Math.round(b.expedited / active * 100), leadMedian: Math.round(median(b.leadVals)), leadStd: Math.round(std(b.leadVals)) }; }).sort(function (a, b) { return b.orders - a.orders; });
  });

  /* ---------------- after-sales service (illustrative) ---------------- */
  // per-model service profile factors (relative service intensity), deterministic
  var MODEL_SVC = {
    "Haval H9": { factor: 1.42, ttfs: 4.6, repeat: 0.29, warranty: 0.34, labour: 2.6 },
    "Patrol": { factor: 1.14, ttfs: 5.4, repeat: 0.22, warranty: 0.19, labour: 2.2 },
    "Camry": { factor: 0.98, ttfs: 5.9, repeat: 0.17, warranty: 0.13, labour: 1.7 },
    "Corolla": { factor: 0.92, ttfs: 6.2, repeat: 0.15, warranty: 0.11, labour: 1.5 },
    "ES 350": { factor: 0.74, ttfs: 6.8, repeat: 0.12, warranty: 0.15, labour: 1.9 }
  };
  function svcProfile(model) { return MODEL_SVC[model] || { factor: 1, ttfs: 6, repeat: 0.18, warranty: 0.15, labour: 1.8 }; }
  // events per 100 vehicles by age bucket (within-window intensity), illustrative
  var AGE_BUCKETS = [{ k: "0–3 mo", lo: 0, hi: 3, per100: 9 }, { k: "4–6 mo", lo: 4, hi: 6, per100: 15 }, { k: "7–12 mo", lo: 7, hi: 12, per100: 28 }, { k: "13–24 mo", lo: 13, hi: 24, per100: 38 }, { k: "25–36 mo", lo: 25, hi: 36, per100: 24 }, { k: "36+ mo", lo: 37, hi: 60, per100: 17 }];
  var MILEAGE_BANDS = [{ k: "0–5k km", per100: 12, families: ["Consumables", "Filters"] }, { k: "5–10k km", per100: 19, families: ["Oil", "Filters"] }, { k: "10–20k km", per100: 27, families: ["Oil", "Brakes"] }, { k: "20–40k km", per100: 34, families: ["Brakes", "Tyres"] }, { k: "40–60k km", per100: 21, families: ["Tyres", "Cooling"] }, { k: "60k+ km", per100: 15, families: ["Electrical", "Engine"] }];

  // installed base per model (cumulative units sold up to planning)
  var installedBase = memo(function () {
    var s = E().normSales(); var m = {}; s.forEach(function (r) { m[r.model] = (m[r.model] || 0) + r.units_sold; }); return m;
  });
  // sales cohorts by quarter (for cohort matrix)
  function quarterOf(mk) { var y = mk.slice(0, 4), q = Math.floor((+mk.slice(5, 7) - 1) / 3) + 1; return y + "-Q" + q; }
  var cohorts = memo(function () {
    var s = E().normSales(); var m = {};
    s.forEach(function (r) { var q = quarterOf(r._mk); if (!m[q]) m[q] = { q: q, units: 0, byModel: {} }; m[q].units += r.units_sold; m[q].byModel[r.model] = (m[q].byModel[r.model] || 0) + r.units_sold; });
    return Object.keys(m).sort().map(function (k) { return m[k]; });
  });

  var serviceCorrelation = memo(function () {
    var D0 = D(), ib = installedBase(); var models = D0.models;
    // per-model aggregate service metrics
    var perModel = models.map(function (model) {
      var p = svcProfile(model); var base = ib[model] || 0;
      // annualised events/100 ~ weighted sum of bucket intensities scaled by factor (÷ representative window years)
      var per100Year = round(mean(AGE_BUCKETS.map(function (b) { return b.per100; })) * p.factor * 3.1, 0);
      var events = Math.round(base * per100Year / 100);
      var intensity = round(per100Year, 0);
      return { model: model, brand: D0.modelMap[model].brand, units: base, events: events, per100: intensity, ttfs: p.ttfs, repeat: Math.round(p.repeat * 100), warranty: Math.round(p.warranty * 100), labour: p.labour, factor: p.factor };
    }).sort(function (a, b) { return b.per100 - a.per100; });
    // service-intensity index normalized 0-100 against the max
    var maxP = Math.max.apply(null, perModel.map(function (x) { return x.per100; })) || 1;
    perModel.forEach(function (x) { x.index = Math.round(x.per100 / maxP * 100); });
    // cohort matrix: rows = last 10 quarters, cols = age buckets; populate only reachable buckets
    var chs = cohorts().slice(-10); var adMs = Date.parse(planningMonth() + "-01");
    var matrix = chs.map(function (c) {
      var qStartMs = Date.parse(c.q.slice(0, 4) + "-" + String(((+c.q.slice(6) - 1) * 3) + 1).padStart(2, "0") + "-01");
      var ageMo = Math.round((adMs - qStartMs) / 86400000 / 30.4);
      // weighted model factor for this cohort
      var wf = 0, tot = 0; Object.keys(c.byModel).forEach(function (m) { wf += svcProfile(m).factor * c.byModel[m]; tot += c.byModel[m]; });
      var f = tot ? wf / tot : 1;
      return { q: c.q, units: c.units, cells: AGE_BUCKETS.map(function (b) { return ageMo >= b.lo ? Math.round(b.per100 * f) : null; }) };
    });
    // mileage & time-since-sale distributions (total)
    var totalEvents = sum(perModel.map(function (x) { return x.events; }));
    var mileage = MILEAGE_BANDS.map(function (b) { return { k: b.k, events: Math.round(totalEvents * b.per100 / sum(MILEAGE_BANDS.map(function (x) { return x.per100; }))), families: b.families }; });
    var timeSince = AGE_BUCKETS.map(function (b) { return { k: b.k, per100: b.per100, events: Math.round(totalEvents * b.per100 / sum(AGE_BUCKETS.map(function (x) { return x.per100; }))) }; });
    // next-quarter expected visits + capacity by location (demo capacity assumption)
    var totalBase = sum(models.map(function (m) { return ib[m] || 0; }));
    var avgMonthlyPer100 = mean(perModel.map(function (x) { return x.per100; })) / 12;
    var nextQ = Math.round(totalBase * avgMonthlyPer100 / 100 * 3);
    var locShare = {}; E().normSales().forEach(function (r) { locShare[r.location] = (locShare[r.location] || 0) + r.units_sold; });
    var totShare = sum(Object.keys(locShare).map(function (k) { return locShare[k]; })) || 1;
    var capacity = Object.keys(locShare).sort(function (a, b) { return locShare[b] - locShare[a]; }).slice(0, 8).map(function (loc) {
      var r = rng("cap|" + loc); var expected = Math.round(nextQ * locShare[loc] / totShare);
      var cap = Math.round(expected * r.range(0.82, 1.35)); var util = Math.round(expected / (cap || 1) * 100);
      return { loc: loc, expected: expected, capacity: cap, util: util, overload: util > 100 };
    });
    var labourHours = Math.round(sum(perModel.map(function (x) { return x.events * x.labour; })) / 4);
    return { perModel: perModel, matrix: matrix, ageBuckets: AGE_BUCKETS.map(function (b) { return b.k; }), mileage: mileage, timeSince: timeSince, nextQ: nextQ, capacity: capacity, totalEvents: totalEvents, totalBase: totalBase, labourHoursQ: labourHours, per100All: round(totalEvents / (totalBase || 1) * 100, 1) };
  });

  /* ---------------- spare parts (illustrative) ---------------- */
  var PARTS = memo(function () {
    return [
      { pn: "DEMO-OIL-001", name: "Oil & filter service kit", family: "Oil", models: "All models", cost: 180, lead: 12, perEvent: 0.62 },
      { pn: "DEMO-BRK-001", name: "Front brake pad set", family: "Brakes", models: "All models", cost: 420, lead: 18, perEvent: 0.28 },
      { pn: "DEMO-BAT-001", name: "12V battery", family: "Electrical", models: "All models", cost: 520, lead: 25, perEvent: 0.11 },
      { pn: "DEMO-AIR-001", name: "Air & cabin filter", family: "Filters", models: "All models", cost: 95, lead: 10, perEvent: 0.48 },
      { pn: "DEMO-TYR-001", name: "Tyre (per unit)", family: "Tyres", models: "All models", cost: 640, lead: 30, perEvent: 0.34 },
      { pn: "DEMO-COO-001", name: "Coolant service kit", family: "Cooling", models: "All models", cost: 150, lead: 14, perEvent: 0.16 },
      { pn: "DEMO-SPK-001", name: "Spark / glow plug set", family: "Engine", models: "Corolla · Camry · ES 350 · Patrol", cost: 260, lead: 20, perEvent: 0.13 },
      { pn: "DEMO-SUS-001", name: "Suspension control arm", family: "Chassis", models: "Patrol · Haval H9", cost: 880, lead: 35, perEvent: 0.07 }
    ];
  });
  var partsDemand = memo(function () {
    var sc = serviceCorrelation(); var totalEvents = sc.totalEvents; var D0 = D();
    var locs = D0.invLocations.length ? D0.invLocations : D0.locations;
    var locShare = {}; E().normSales().forEach(function (r) { locShare[r.location] = (locShare[r.location] || 0) + r.units_sold; });
    var parts = PARTS().map(function (p) {
      // quarterly forecast demand nationally = events×perEvent, scaled to a quarter
      var natQ = Math.round(totalEvents * p.perEvent / 4);
      var byLoc = locs.map(function (loc) {
        var r = rng("part|" + p.pn + "|" + loc);
        var share = (locShare[loc] || 1); var totShare = sum(locs.map(function (l) { return locShare[l] || 1; }));
        var fc = Math.max(0, Math.round(natQ * share / totShare * r.range(0.8, 1.2)));
        var safety = Math.round(fc * r.range(0.25, 0.5));
        var reorder = safety + Math.round(fc / 3 * (p.lead / 30 + 0.5));
        var onHand = r.f() < 0.18 ? Math.round(safety * r.range(0.15, 0.7)) : Math.max(0, Math.round((reorder + safety) * r.range(0.55, 1.7)));
        var reserved = Math.round(onHand * r.range(0.05, 0.22));
        var inbound = r.f() < 0.35 ? Math.round(fc * r.range(0.2, 0.6)) : 0;
        var avail = Math.max(0, onHand - reserved);
        var emergencies = r.f() < 0.3 ? r.i(1, 5) : 0;
        var cover = fc > 0 ? (avail + inbound) / (fc / 3) : 99; // months of cover (quarter→month)
        var rec = recPart(avail, inbound, reorder, safety, fc, cover, emergencies);
        return { loc: loc, forecast: fc, safety: safety, reorder: reorder, onHand: onHand, reserved: reserved, avail: avail, inbound: inbound, cover: round(cover, 1), emergencies: emergencies, rec: rec, value: onHand * p.cost };
      });
      var totFc = sum(byLoc.map(function (x) { return x.forecast; }));
      var totAvail = sum(byLoc.map(function (x) { return x.avail; }));
      var totInbound = sum(byLoc.map(function (x) { return x.inbound; }));
      var stockoutLocs = byLoc.filter(function (x) { return x.rec.risk === "high"; }).length;
      var value = sum(byLoc.map(function (x) { return x.value; }));
      var emergencies = sum(byLoc.map(function (x) { return x.emergencies; }));
      return { pn: p.pn, name: p.name, family: p.family, models: p.models, cost: p.cost, lead: p.lead, natQ: natQ, byLoc: byLoc, totFc: totFc, totAvail: totAvail, totInbound: totInbound, stockoutLocs: stockoutLocs, value: value, emergencies: emergencies, cover: round((totAvail + totInbound) / (totFc / 3 || 1), 1) };
    });
    return { parts: parts, locs: locs };
  });
  function recPart(avail, inbound, reorder, safety, fc, cover, emerg) {
    if (fc <= 0) return { t: "Review compatibility", risk: "low", why: "No forecast demand for this location." };
    if (avail + inbound < safety) return { t: "Reorder now", risk: "high", why: "Position below safety stock; stockout likely within lead time." };
    if (avail + inbound < reorder) return { t: emerg > 1 ? "Increase safety stock" : "Reorder soon", risk: "med", why: "Approaching reorder point; replenish before depletion." };
    if (cover > 6) return { t: "Reduce safety stock", risk: "low", why: "Cover materially above target; capital tied up." };
    if (cover > 4) return { t: "Transfer from surplus", risk: "low", why: "Local surplus that could balance a short location." };
    return { t: "Maintain", risk: "low", why: "Position aligned with forecast demand." };
  }

  /* =================================================================================
     UC1 — MONTHLY ORDER OPTIMIZATION
     ================================================================================= */
  var _orderCache = {};
  function orderOpt(settings, filters, scenario) {
    var sig = JSON.stringify([filters || {}, scenario || {}]);
    if (_orderCache[sig]) return _orderCache[sig];
    var Eng = E(); var D0 = D(); var sc = Object.assign({ horizon: 1, serviceLevel: 0.95, coverMax: settings.coverMax || 6, budget: 0, momentum: 0.6, growth: 0, ramadan: false, includeLow: true, useInbound: true, moq: true }, scenario || {});
    var inv = Eng.computeInventory(Object.assign({}, settings), filters || {});
    // group current stock by model|variant|location
    var stockG = {}; inv.units.forEach(function (u) { var k = u.model + "|" + u.variant + "|" + u.location; stockG[k] = (stockG[k] || 0) + 1; });
    // candidate combos: model×variant×location that have stock OR national demand
    var combos = {};
    inv.units.forEach(function (u) { combos[u.model + "|" + u.variant + "|" + u.location] = { model: u.model, variant: u.variant, location: u.location, brand: u.brand, type: u.type }; });
    Eng.normSales().forEach(function (r) { var k = r.model + "|" + r.variant + "|" + r.location; if (!combos[k]) combos[k] = { model: r.model, variant: r.variant, location: r.location, brand: r.brand, type: r.type }; });
    // apply filters to combos
    var f = filters || {};
    function passes(c) { if (f.model && f.model.length && f.model.indexOf(c.model) < 0) return false; if (f.variant && f.variant.length && f.variant.indexOf(c.variant) < 0) return false; if (f.location && f.location.length && f.location.indexOf(c.location) < 0) return false; if (f.brand && f.brand.length && f.brand.indexOf(c.brand) < 0) return false; if (f.type && f.type.length && f.type.indexOf(c.type) < 0) return false; return true; }
    var pMonth = planningMonth(); var seas = seasonalFor(pMonth) * (sc.ramadan ? 1.06 : 1);
    var leadMonthsFor = memoKey(function (brand) { return supplierForBrand(brand).leadBase / 30; });
    var rows = Object.keys(combos).map(function (k) { return combos[k]; }).filter(passes).map(function (c) {
      var dem = Eng.demandVelocity(c.location, c.model, c.variant, settings.trailingMonths || 3);
      var stock = stockG[c.model + "|" + c.variant + "|" + c.location] || 0;
      var series = Eng.monthlySeries(Eng.normSales().filter(function (r) { return r.model === c.model && r.variant === c.variant && r.location === c.location; }), D0.months).map(function (x) { return x.units; });
      var recent = mean(series.slice(-3)), prior = mean(series.slice(-6, -3));
      var trendPct = prior ? (recent - prior) / prior * 100 : (recent > 0 ? 40 : 0);
      var vol = std(series.slice(-12));
      // recency-weighted demand blended with plain velocity per scenario momentum
      var recW = recent > 0 ? recent : dem.v; var demandMonthly = (sc.momentum * recW + (1 - sc.momentum) * dem.v) * seas * (1 + sc.growth / 100);
      var leadMo = leadMonthsFor(c.brand);
      var horizonMo = sc.horizon + leadMo;
      var fdemand = demandMonthly * horizonMo;
      var sigma = Math.max(vol, dem.v * 0.35, 0.5);
      var safety = zForSL(sc.serviceLevel) * sigma * Math.sqrt(horizonMo);
      var inbound = sc.useInbound ? Math.round(inboundFor(c.model, c.variant) * (stock ? 0.35 : 0.2)) : 0; // allocate a share to this location
      var raw = Math.max(0, fdemand + safety - stock - inbound);
      var sup = supplierForBrand(c.brand); var moq = sc.moq ? 2 : 1, mult = sc.moq ? 1 : 1;
      var base = raw > 0 ? Math.max(moq, Math.ceil(raw)) : 0;
      var band = zForSL(sc.serviceLevel) * sigma * Math.sqrt(horizonMo) * 0.6;
      var lo = Math.max(0, Math.round(base - band - demandMonthly * 0.4)), hi = Math.round(base + band + demandMonthly * 0.4);
      var cover = dem.v > 0 ? stock / dem.v : (stock > 0 ? 99 : 0);
      var projCover = demandMonthly > 0 ? (stock + inbound + base) / demandMonthly : 99;
      var conf = dem.conf === "High" && vol < Math.max(1, recent) * 0.6 ? "High" : (dem.conf === "Low" || dem.basis === "Insufficient demand history" || dem.basis === "Model-level fallback") ? "Low" : "Medium";
      if (!sc.includeLow && conf === "Low") return null;
      // status logic — consistent with overstock (UC5) & demand trend; not defaulted to Increase
      var status, why; var cmx = sc.coverMax;
      if (dem.basis === "Insufficient demand history" || dem.v <= 0) { status = "Insufficient data"; base = 0; lo = 0; hi = 0; why = "No reliable recent demand signal for this configuration at this location."; }
      else if (cover > cmx * 1.5 && trendPct < 5) { status = "Pause"; base = 0; lo = 0; hi = 0; why = "Stock cover is far above target with flat or falling demand — pause ordering and review."; }
      else if (cover > cmx && trendPct < 0) { status = "Reduce"; base = Math.min(base, Math.max(0, Math.round(demandMonthly * 0.5))); lo = 0; hi = base; why = "Cover already exceeds target and demand is declining — order below the model to draw stock down."; }
      else if (base === 0) { status = "Maintain"; why = "Current stock plus inbound already covers the planning and lead-time horizon."; }
      else if (conf === "Low") { status = "Review"; why = "A positive order is indicated but rests on a low-confidence demand basis — analyst review advised before ordering."; }
      else if ((trendPct > 12 || cover < 1.2) && base > demandMonthly) { status = "Increase"; why = "Recent demand momentum and/or thin local cover justify a larger order."; }
      else { status = "Maintain"; why = "Order aligns with expected demand over the planning and lead-time horizon."; }
      var cost = unitCost(c.model, c.variant);
      return {
        model: c.model, variant: c.variant, location: c.location, brand: c.brand, type: c.type,
        stock: stock, demandMonthly: round(demandMonthly, 1), velocity: round(dem.v, 1), basis: dem.basis, demandDetail: dem.detail, trendPct: round(trendPct, 0),
        fdemand: Math.round(fdemand), safety: Math.round(safety), inbound: inbound, leadDays: Math.round(leadMo * 30), leadMonths: round(leadMo, 1),
        base: base, lo: lo, hi: Math.max(hi, base), cover: round(cover, 1), projCover: round(projCover, 1),
        value: base * cost, valueLo: lo * cost, valueHi: Math.max(hi, base) * cost, cost: cost, status: status, why: why, conf: conf, sigma: round(sigma, 1), supplier: sup.name, series: series.slice(-12)
      };
    }).filter(Boolean).filter(function (r) { return r.stock > 0 || r.base > 0 || r.velocity > 0; });
    rows.sort(function (a, b) { return b.value - a.value; });
    // aggregates
    var totalUnits = sum(rows.map(function (r) { return r.base; }));
    var totalValue = sum(rows.map(function (r) { return r.value; }));
    var configs = rows.filter(function (r) { return r.base > 0; }).length;
    var review = rows.filter(function (r) { return r.status === "Review" || r.status === "Insufficient data"; }).length;
    var highRisk = rows.filter(function (r) { return r.status === "Increase" && r.cover < 1 || r.status === "Reduce" || r.status === "Pause"; }).length;
    var avgCover = mean(rows.filter(function (r) { return isFinite(r.projCover) && r.projCover < 90; }).map(function (r) { return r.projCover; }));
    // stockout exposure avoided ~ value of understock closed; overstock reduced ~ value of reduce/pause held back
    var understockVal = sum(rows.filter(function (r) { return r.status === "Increase"; }).map(function (r) { return (r.base) * r.cost * 0.18; }));
    var overstockVal = sum(rows.filter(function (r) { return r.status === "Reduce" || r.status === "Pause"; }).map(function (r) { return (r.demandMonthly * r.cost); }));
    var byStatus = ["Increase", "Maintain", "Reduce", "Pause", "Review", "Insufficient data"].map(function (s) { return { key: s, n: rows.filter(function (r) { return r.status === s; }).length, units: sum(rows.filter(function (r) { return r.status === s; }).map(function (x) { return x.base; })) }; });
    // order-mix by model|variant (for stacked visual)
    var mixMap = {}; rows.forEach(function (r) { var k = r.model + " " + r.variant; if (!mixMap[k]) mixMap[k] = { key: k, model: r.model, variant: r.variant, order: 0, stock: 0, demand: 0 }; mixMap[k].order += r.base; mixMap[k].stock += r.stock; mixMap[k].demand += r.demandMonthly; });
    var mix = Object.keys(mixMap).map(function (k) { return mixMap[k]; }).sort(function (a, b) { return b.order - a.order; });
    // regional matrix (model preference & imbalance per location)
    var regMap = {}; rows.forEach(function (r) { if (!regMap[r.location]) regMap[r.location] = { loc: r.location, order: 0, stock: 0, demand: 0, value: 0, topModel: {}, mismatch: 0 }; var g = regMap[r.location]; g.order += r.base; g.stock += r.stock; g.demand += r.demandMonthly; g.value += r.value; g.topModel[r.model] = (g.topModel[r.model] || 0) + r.demandMonthly; if (r.status === "Increase") g.mismatch++; });
    var regions = Object.keys(regMap).map(function (k) { var g = regMap[k]; g.top = Object.keys(g.topModel).sort(function (a, b) { return g.topModel[b] - g.topModel[a]; })[0] || "—"; g.cover = g.demand > 0 ? round(g.stock / g.demand, 1) : 99; return g; }).sort(function (a, b) { return b.order - a.order; });
    var out = { rows: rows, planningMonth: pMonth, planningLabel: Eng.monthLabel(pMonth), totalUnits: totalUnits, totalValue: totalValue, configs: configs, review: review, highRisk: highRisk, avgCover: round(avgCover, 1), understockVal: understockVal, overstockVal: overstockVal, byStatus: byStatus, mix: mix, regions: regions, scenario: sc };
    _orderCache[sig] = out; return out;
  }
  function orderScenarioCompare(settings, filters) {
    var presets = [
      { key: "Conservative", serviceLevel: 0.92, momentum: 0.4, growth: -3, coverMax: (settings.coverMax || 6) + 1 },
      { key: "Balanced", serviceLevel: 0.95, momentum: 0.6, growth: 0, coverMax: settings.coverMax || 6 },
      { key: "Growth", serviceLevel: 0.98, momentum: 0.75, growth: 8, coverMax: (settings.coverMax || 6) - 1 }
    ];
    return presets.map(function (p) { var o = orderOpt(settings, filters, p); return { key: p.key, units: o.totalUnits, value: o.totalValue, configs: o.configs, serviceLevel: Math.round(p.serviceLevel * 100), review: o.review, exceptions: o.highRisk, avgCover: o.avgCover }; });
  }

  /* =================================================================================
     UC3 — CONFIGURATION DEMAND INSIGHTS
     ================================================================================= */
  var _cfgCache = {};
  function configInsights(settings, filters) {
    var sig = JSON.stringify(filters || {});
    if (_cfgCache[sig]) return _cfgCache[sig];
    var Eng = E(); var D0 = D(); var f = filters || {};
    var sales = Eng.applySalesFilters(Eng.normSales(), f);
    var inv = Eng.computeInventory(settings, f);
    var invByCfg = {}; inv.units.forEach(function (u) { var k = u.model + "|" + u.variant + "|" + u.colour; if (!invByCfg[k]) invByCfg[k] = { units: 0, value: 0, cover: [] }; invByCfg[k].units++; invByCfg[k].value += u.purchase_price; if (isFinite(u.cover)) invByCfg[k].cover.push(u.cover); });
    // configuration = model|variant|colour (interior shown in drawer)
    var cfg = {};
    sales.forEach(function (r) {
      var k = r.model + "|" + r.variant + "|" + r.colour;
      if (!cfg[k]) cfg[k] = { model: r.model, variant: r.variant, colour: r.colour, brand: r.brand, type: r.type, units: 0, revenue: 0, disc: 0, discUnits: 0, locs: {}, series: {}, interiors: {} };
      var g = cfg[k]; g.units += r.units_sold; g.revenue += r.revenue; if (r._disc) { g.disc++; g.discUnits += r.units_sold; } g.locs[r.location] = (g.locs[r.location] || 0) + r.units_sold; g.series[r._mk] = (g.series[r._mk] || 0) + r.units_sold; g.interiors[r.interior] = (g.interiors[r.interior] || 0) + r.units_sold;
    });
    var months = D0.months;
    var list = Object.keys(cfg).map(function (k) {
      var g = cfg[k]; var s = months.map(function (mk) { return g.series[mk] || 0; });
      var recent = mean(s.slice(-3)), prior = mean(s.slice(-6, -3)), prior12 = mean(s.slice(-15, -3));
      var momentum = prior ? (recent - prior) / prior * 100 : (recent > 0 ? 50 : 0);
      var zeroMonths = s.slice(-6).filter(function (x) { return x === 0; }).length;
      var breadth = Object.keys(g.locs).length;
      var iv = invByCfg[k] || { units: 0, value: 0, cover: [] };
      var cover = iv.cover.length ? median(iv.cover) : (iv.units > 0 && recent <= 0 ? 99 : 0);
      var discShare = g.units ? g.discUnits / g.units * 100 : 0;
      // decay score 0..100 (higher = worse)
      var decay = clamp(
        (momentum < 0 ? Math.min(45, -momentum * 0.9) : 0) +
        zeroMonths * 6 +
        (cover > (settings.coverMax || 6) ? Math.min(20, (cover - (settings.coverMax || 6)) * 2) : 0) +
        (breadth <= 2 ? 10 : breadth <= 4 ? 5 : 0) +
        (discShare > 55 ? 10 : 0), 0, 100);
      var decayFactors = [
        { label: "Recent vs prior-quarter momentum", pts: momentum < 0 ? Math.min(45, -momentum * 0.9) : 0, detail: (momentum >= 0 ? "+" : "") + Math.round(momentum) + "%" },
        { label: "Months with zero sales (last 6)", pts: zeroMonths * 6, detail: zeroMonths + " of 6" },
        { label: "Stock cover above target", pts: cover > (settings.coverMax || 6) ? Math.min(20, (cover - (settings.coverMax || 6)) * 2) : 0, detail: isFinite(cover) && cover < 90 ? cover.toFixed(1) + " mo" : "no demand" },
        { label: "Narrow regional demand", pts: breadth <= 2 ? 10 : breadth <= 4 ? 5 : 0, detail: breadth + " locations" },
        { label: "Discount dependence", pts: discShare > 55 ? 10 : 0, detail: Math.round(discShare) + "% discounted" }
      ].sort(function (a, b) { return b.pts - a.pts; });
      return { key: g.model + " " + g.variant + " · " + g.colour, model: g.model, variant: g.variant, colour: g.colour, brand: g.brand, type: g.type, units: g.units, revenue: g.revenue, recent: round(recent, 1), prior: round(prior, 1), momentum: round(momentum, 0), zeroMonths: zeroMonths, breadth: breadth, invUnits: iv.units, invValue: iv.value, cover: round(cover, 1), discShare: round(discShare, 0), decay: Math.round(decay), decayFactors: decayFactors, series: s.slice(-12), locs: g.locs, interiors: g.interiors, prior12: round(prior12, 1) };
    });
    // clusters
    list.forEach(function (c) { c.cluster = clusterOf(c, settings); });
    // sort by opportunity/risk
    list.sort(function (a, b) { return b.units - a.units; });
    var clusterKeys = ["Star", "Emerging", "Core", "Promotion Candidate", "Declining", "Overstock Risk", "Dead-Stock Candidate", "Insufficient Evidence"];
    var clusterAgg = clusterKeys.map(function (k) { var g = list.filter(function (c) { return c.cluster === k; }); return { key: k, n: g.length, units: sum(g.map(function (x) { return x.units; })), invValue: sum(g.map(function (x) { return x.invValue; })) }; });
    // heatmap rows = model+variant, cols = colours
    var mvs = uniq(list.map(function (c) { return c.model + " " + c.variant; }));
    var colours = D0.colours;
    var heatUnits = mvs.map(function (mv) { return colours.map(function (col) { return sum(list.filter(function (c) { return (c.model + " " + c.variant) === mv && c.colour === col; }).map(function (x) { return x.recent; })); }); });
    var heatMomentum = mvs.map(function (mv) { return colours.map(function (col) { var g = list.filter(function (c) { return (c.model + " " + c.variant) === mv && c.colour === col; }); return g.length ? Math.round(mean(g.map(function (x) { return x.momentum; }))) : 0; }); });
    var heatDecay = mvs.map(function (mv) { return colours.map(function (col) { var g = list.filter(function (c) { return (c.model + " " + c.variant) === mv && c.colour === col; }); return g.length ? Math.round(mean(g.map(function (x) { return x.decay; }))) : 0; }); });
    // decay alerts — worst by inventory value at risk
    var alerts = list.filter(function (c) { return (c.cluster === "Declining" || c.cluster === "Overstock Risk" || c.cluster === "Dead-Stock Candidate") && c.invValue > 0; }).sort(function (a, b) { return b.invValue * b.decay - a.invValue * a.decay; }).slice(0, 6);
    var gaining = list.filter(function (c) { return c.momentum > 8 && c.recent > 0; }).length;
    var declining = list.filter(function (c) { return c.cluster === "Declining" || c.cluster === "Dead-Stock Candidate"; }).length;
    var atRiskValue = sum(list.filter(function (c) { return c.cluster === "Overstock Risk" || c.cluster === "Dead-Stock Candidate" || c.cluster === "Declining"; }).map(function (c) { return c.invValue; }));
    var oppValue = sum(list.filter(function (c) { return c.cluster === "Star" || c.cluster === "Emerging"; }).map(function (c) { return c.revenue; }));
    var out = { list: list, clusterAgg: clusterAgg, mvs: mvs, colours: colours, heatUnits: heatUnits, heatMomentum: heatMomentum, heatDecay: heatDecay, alerts: alerts, gaining: gaining, declining: declining, atRiskValue: atRiskValue, oppValue: oppValue };
    _cfgCache[sig] = out; return out;
  }
  function clusterOf(c, settings) {
    var cm = settings.coverMax || 6;
    if (c.units < 6 || c.zeroMonths >= 5) return "Insufficient Evidence";
    if (c.momentum > 12 && c.cover < cm && c.recent >= c.prior12) return c.units > 120 ? "Star" : "Emerging";
    if (c.cover > cm * 1.6 && (c.recent <= 0 || c.momentum < -15)) return "Dead-Stock Candidate";
    if (c.cover > cm && c.momentum < 0) return "Overstock Risk";
    if (c.momentum < -12) return "Declining";
    if (c.discShare > 55 && c.momentum > -5) return "Promotion Candidate";
    return "Core";
  }
  function clusterMeta(k) {
    return {
      "Star": { c: "var(--risk-low)", icon: "star", d: "High demand · healthy cover" },
      "Emerging": { c: "var(--primary-2)", icon: "trending_up", d: "Growing demand · monitor" },
      "Core": { c: "var(--primary)", icon: "check_circle", d: "Consistent, predictable demand" },
      "Promotion Candidate": { c: "var(--warn)", icon: "campaign", d: "Responds to discount periods" },
      "Declining": { c: "var(--risk-high)", icon: "trending_down", d: "Weakening across periods" },
      "Overstock Risk": { c: "var(--risk-high)", icon: "warehouse", d: "Low demand vs current stock" },
      "Dead-Stock Candidate": { c: "var(--risk-crit)", icon: "block", d: "Sustained low rotation" },
      "Insufficient Evidence": { c: "var(--text-muted)", icon: "help", d: "Not enough observations" }
    }[k] || { c: "var(--text-muted)", icon: "help", d: "" };
  }

  /* =================================================================================
     UC4 — PROCUREMENT QUANTITY OPTIMIZATION
     ================================================================================= */
  var _procCache = {};
  function procurementOpt(settings, filters, scenario) {
    var sig = JSON.stringify([filters || {}, scenario || {}]);
    if (_procCache[sig]) return _procCache[sig];
    var Eng = E(); var sc = Object.assign({ serviceLevel: 0.95, budget: 0, leadStress: 0, supplierDelay: 0, safety: 1, maxDays: (settings.coverMax || 6) * 30, moq: true, expedite: false, mode: "balanced" }, scenario || {});
    var order = orderOpt(settings, filters, { horizon: 1, serviceLevel: sc.serviceLevel, momentum: sc.mode === "aggressive" ? 0.75 : sc.mode === "conservative" ? 0.45 : 0.6, growth: sc.mode === "aggressive" ? 6 : sc.mode === "conservative" ? -2 : 0 });
    // aggregate order rows to model|variant (procurement is per configuration, not per branch)
    var mvMap = {};
    order.rows.forEach(function (r) {
      var k = r.model + "|" + r.variant; if (!mvMap[k]) mvMap[k] = { model: r.model, variant: r.variant, brand: r.brand, stock: 0, demand: 0, base: 0, inbound: 0, cost: r.cost, sigma: 0, cover: [], statuses: {} };
      var g = mvMap[k]; g.stock += r.stock; g.demand += r.demandMonthly; g.base += r.base; g.inbound += r.inbound; g.sigma += r.sigma; if (isFinite(r.cover) && r.cover < 90) g.cover.push(r.cover); g.statuses[r.status] = (g.statuses[r.status] || 0) + 1;
    });
    var pf = supplierPerf(); var pfById = {}; pf.forEach(function (s) { pfById[s.id] = s; });
    var rows = Object.keys(mvMap).map(function (k) {
      var g = mvMap[k]; var sup = supplierForBrand(g.brand); var perf = pfById[sup.id] || {};
      var leadBase = sup.leadBase * (1 + sc.leadStress / 100); var leadStd = leadBase * sup.leadVar * (1 + sc.supplierDelay / 100);
      var leadMo = leadBase / 30; var horizonMo = 1 + leadMo;
      var demand = g.demand; var fdemand = demand * horizonMo;
      var sigma = Math.max(g.sigma, demand * 0.3, 1);
      var safety = zForSL(sc.serviceLevel) * sigma * Math.sqrt(horizonMo) * sc.safety;
      var inbound = g.inbound; var stock = g.stock;
      var raw = Math.max(0, fdemand + safety - stock - inbound);
      var moq = sc.moq ? 3 : 1; var mult = sc.moq ? 2 : 1;
      var base = raw > 0 ? Math.max(moq, Math.ceil(raw / mult) * mult) : 0;
      var band = zForSL(sc.serviceLevel) * sigma * Math.sqrt(horizonMo);
      var lo = Math.max(0, Math.round(base - band)), hi = Math.round(base + band);
      var cover = g.cover.length ? median(g.cover) : (stock > 0 && demand <= 0 ? 99 : 0);
      var obs = obsLead(g.model);
      // status
      var status, why;
      var declining = (g.statuses["Reduce"] || 0) + (g.statuses["Pause"] || 0) > (g.statuses["Increase"] || 0);
      if (demand <= 0) { status = "Insufficient data"; base = 0; why = "No reliable demand signal for this configuration."; }
      else if (cover > (settings.coverMax || 6) * 1.4 && declining) { status = base === 0 ? "Pause procurement" : "Reduce quantity"; base = Math.min(base, Math.round(demand * 0.5)); why = "Stock materially above expected demand; reduce or pause to release working capital."; }
      else if (base === 0) { status = "Maintain plan"; why = "On-hand plus inbound covers the horizon; no new procurement needed now."; }
      else if (cover < 1.2 || (g.statuses["Increase"] || 0) > 1) { status = leadMo > 3 ? "Procure now" : "Procure within 14 days"; why = "Thin cover against a long replenishment lead time — order early to protect service level."; }
      else if (perf.onTimePct != null && perf.onTimePct < 82) { status = "Review supplier"; why = "Recommended quantity is sound but the assigned supplier shows elevated delay risk."; }
      else { status = "Procure within 14 days"; why = "Order the recommended quantity to hold the target service level."; }
      var slAchieved = clamp(Math.round((sc.serviceLevel * 100) - (perf.onTimePct != null ? (100 - perf.onTimePct) * 0.25 : 0) - sc.supplierDelay * 0.1), 60, 99);
      var conf = cover > 0 && demand > 2 ? (sigma < demand ? "High" : "Medium") : "Low";
      return { model: g.model, variant: g.variant, brand: g.brand, supplier: sup.name, supplierId: sup.id, stock: stock, demand: round(demand, 1), fdemand: Math.round(fdemand), inbound: inbound, leadMedian: Math.round(leadBase), leadStd: Math.round(leadStd), leadMo: round(leadMo, 1), obsLeadMedian: obs.median, safety: Math.round(safety), base: base, lo: lo, hi: Math.max(hi, base), cover: round(cover, 1), value: base * g.cost, valueLo: lo * g.cost, valueHi: Math.max(hi, base) * g.cost, cost: g.cost, status: status, why: why, conf: conf, slAchieved: slAchieved, onTime: perf.onTimePct, holdingImpact: Math.round(base * 8 * 30) };
    }).filter(function (r) { return r.stock > 0 || r.base > 0 || r.demand > 0; }).sort(function (a, b) { return b.value - a.value; });
    var totalQty = sum(rows.map(function (r) { return r.base; }));
    var totalValue = sum(rows.map(function (r) { return r.value; }));
    var totalLo = sum(rows.map(function (r) { return r.valueLo; })), totalHi = sum(rows.map(function (r) { return r.valueHi; }));
    var exceptions = rows.filter(function (r) { return r.status === "Review supplier" || r.status === "Procure now" || r.status === "Reduce quantity" || r.status === "Pause procurement"; }).length;
    var holding = sum(rows.map(function (r) { return r.holdingImpact; }));
    var slAvg = Math.round(mean(rows.filter(function (r) { return r.base > 0; }).map(function (r) { return r.slAchieved; })));
    var out = { rows: rows, totalQty: totalQty, totalValue: totalValue, totalLo: totalLo, totalHi: totalHi, exceptions: exceptions, holding: holding, slAvg: slAvg, suppliers: pf, scenario: sc, planningLabel: order.planningLabel };
    _procCache[sig] = out; return out;
  }

  /* =================================================================================
     UC8 — EXECUTIVE DECISION COCKPIT (consolidates the modules)
     ================================================================================= */
  function priorityScore(impact, urgency, confidence, controllability) {
    return Math.round(clamp(impact * urgency * confidence * controllability, 0, 1) * 100);
  }
  var _decCache = {};
  function decisionFeed(settings, filters) {
    var sig = JSON.stringify(filters || {});
    if (_decCache[sig]) return _decCache[sig];
    var Eng = E(); var f = filters || {};
    var order = orderOpt(settings, f, {}); var cfg = configInsights(settings, f); var proc = procurementOpt(settings, f, {});
    var inv = Eng.computeInventory(settings, f); var sc = serviceCorrelation(); var pd = partsDemand();
    var maxExposure = Math.max(inv.agg.value, 1);
    var decs = [];
    function conf01(c) { return c === "High" ? 0.9 : c === "Medium" ? 0.65 : 0.4; }
    // D1 — increase order allocation for high-demand, thin-stock config
    var inc = order.rows.filter(function (r) { return r.status === "Increase"; }).sort(function (a, b) { return b.value - a.value; })[0];
    if (inc) decs.push(mkDecision("D-ORD-1", "Increase order allocation — " + inc.model + " " + inc.variant + " (" + inc.location + ")", "Order Planning", "order", { model: [inc.model], variant: [inc.variant], location: [inc.location] }, "high", inc.value, "opportunity", conf01(inc.conf), "Recent demand momentum " + (inc.trendPct >= 0 ? "+" : "") + inc.trendPct + "% against " + (inc.cover > 0 ? inc.cover + " months" : "under 1 month") + " of local cover.", "Prepare an order proposal for ~" + inc.base + " units next month.", 3, "Sales Planning Manager", conf01(inc.conf) > 0.7 ? 0.85 : 0.6, 0.8));
    // D2 — reduce procurement for declining / over-covered configuration (UC3 alert → UC4/UC8)
    var alert = cfg.alerts[0];
    if (alert) decs.push(mkDecision("D-PRC-1", "Reduce procurement — " + alert.model + " " + alert.variant + " · " + alert.colour, "Procurement", "config", { model: [alert.model], variant: [alert.variant] }, "high", alert.invValue, "risk", alert.decay > 60 ? 0.7 : 0.6, alert.cluster + " · " + (alert.momentum >= 0 ? "+" : "") + alert.momentum + "% momentum with " + Eng.fmtSAR(alert.invValue) + " inventory exposure.", "Reduce next procurement and review promotion or redistribution.", 4, "Procurement Manager", 0.7, 0.9));
    // D3 — redistribute aging inventory (transfer)
    var t = inv.transfers[0];
    if (t) decs.push(mkDecision("D-INV-1", "Redistribute aging inventory — " + t.model + " " + t.variant, "Inventory", "inventory", { model: [t.model], variant: [t.variant], location: [t.src] }, "medium", t.avoidedHold * 12, "risk", 0.65, t.src + " holds " + t.srcCover.toFixed(1) + "m cover while " + t.dest + " shows live demand.", "Prepare transfer recommendation: ~" + t.units + " units " + t.src + " → " + t.dest + ".", 2, "Inventory Manager", 0.6, 0.85));
    // D4 — supplier delay exposure
    var badSup = proc.suppliers.filter(function (s) { return s.onTimePct < 82 && s.orders > 3; }).sort(function (a, b) { return a.onTimePct - b.onTimePct; })[0];
    if (badSup) { var exposure = sum(proc.rows.filter(function (r) { return r.supplierId === badSup.id; }).map(function (r) { return r.value; })); decs.push(mkDecision("D-SUP-1", "Review supplier delay exposure — " + badSup.name, "Procurement", "procurement", {}, "medium", exposure, "risk", 0.6, badSup.name + " on-time delivery is " + badSup.onTimePct + "% (avg delay " + badSup.avgDelay + " days).", "Assess alternative supplier / expedite critical lines.", badSup.orders, "Procurement Manager", 0.55, 0.7, true)); }
    // D5 — parts shortage
    var short = pd.parts.filter(function (p) { return p.stockoutLocs > 0; }).sort(function (a, b) { return b.stockoutLocs * b.value - a.stockoutLocs * a.value; })[0];
    if (short) decs.push(mkDecision("D-PRT-1", "Increase stock for " + short.family + " parts (" + short.pn + ")", "Parts", "parts", {}, "high", short.value, "risk", 0.55, short.stockoutLocs + " location(s) below safety stock on a fast-moving family.", "Reorder now and lift safety stock at exposed branches.", short.stockoutLocs, "Parts Manager", 0.6, 0.75, true));
    // D6 — workshop capacity
    var over = sc.capacity.filter(function (c) { return c.overload; }).sort(function (a, b) { return b.util - a.util; })[0];
    if (over) decs.push(mkDecision("D-SVC-1", "Prepare workshop capacity — " + over.loc, "After-Sales", "correlation", { location: [over.loc] }, "medium", over.expected * 900, "risk", 0.5, over.loc + " projected at " + over.util + "% of assumed service capacity next quarter.", "Extend appointment capacity / pre-position common parts.", over.expected, "After-Sales Manager", 0.5, 0.65, true));
    decs.sort(function (a, b) { return b.priority - a.priority; });
    // financials
    var oppValue = sum(decs.filter(function (d) { return d.kind === "opportunity"; }).map(function (d) { return d.impact; })) + cfg.oppValue * 0.02;
    var riskValue = sum(decs.filter(function (d) { return d.kind === "risk"; }).map(function (d) { return d.impact; }));
    var critical = decs.filter(function (d) { return d.severity === "high"; }).length;
    var lowConf = decs.filter(function (d) { return d.confidence01 < 0.5; }).length;
    var financial = {
      revenueRisk: Math.round(riskValue * 0.4), workingCapital: inv.agg.value, holdingAvoid: sum(inv.transfers.map(function (t) { return t.avoidedHold * 12; })),
      procurementInvest: proc.totalValue, stockoutExposure: order.understockVal, emergencyExposure: sum(pd.parts.map(function (p) { return p.emergencies * p.cost * 3; })), serviceImpact: sc.nextQ
    };
    var out = { decisions: decs, oppValue: oppValue, riskValue: riskValue, critical: critical, lowConf: lowConf, financial: financial, dueThisWeek: decs.filter(function (d) { return d.dueDays <= 7; }).length };
    _decCache[sig] = out; return out;
  }
  function mkDecision(id, title, area, screen, filter, severity, impact, kind, confidence01, whyNow, action, evidence, ownerRole, urgency, controllability, isDemo) {
    var impactF = clamp(impact / 5e6, 0.15, 1);
    var dueDays = severity === "high" ? 5 : severity === "medium" ? 12 : 20;
    return { id: id, title: title, area: area, screen: screen, filter: filter, severity: severity, impact: Math.round(impact), kind: kind, confidence01: confidence01, confidence: confidence01 > 0.75 ? "High" : confidence01 > 0.5 ? "Medium" : "Low", whyNow: whyNow, action: action, evidence: evidence, owner: ownerRole, dueDays: dueDays, priority: priorityScore(impactF, urgency, confidence01, controllability), urgency: urgency, controllability: controllability, isDemo: !!isDemo, factors: [{ k: "Business impact", v: Math.round(impactF * 100) }, { k: "Urgency", v: Math.round(urgency * 100) }, { k: "Confidence", v: Math.round(confidence01 * 100) }, { k: "Controllability", v: Math.round(controllability * 100) }] };
  }

  /* =================================================================================
     GOVERNANCE — Data Health & Lineage
     ================================================================================= */
  function dataHealth() {
    var Eng = E(); var dq = Eng.dataQuality(); var D0 = D();
    var sources = [
      { name: "Sales history", system: "Fusion Order Management", status: "Ready", rows: dq.salesRows, coverage: Eng.monthLabel(D0.firstMonth) + " → " + Eng.monthLabel(D0.lastMonth), note: "Supplied workbook — parsed & validated.", kind: "workbook" },
      { name: "Inventory on-hand", system: "Fusion Inventory Management", status: dq.mismatch.length ? "Ready with assumptions" : "Ready", rows: dq.invRows, coverage: "Snapshot @ " + planningMonth(), note: dq.mismatch.length ? dq.mismatch.join(", ") + " sell but hold no inventory snapshot." : "Supplied workbook — parsed & validated.", kind: "workbook" },
      { name: "Supplier master & PO history", system: "Fusion Procurement", status: "Demo data", rows: procurement().length, coverage: "Trailing 18 months (synthetic)", note: "Not supplied — deterministic synthetic fixture for the prototype.", kind: "demo" },
      { name: "Service / repair-order history", system: "Fusion Service", status: "Demo data", rows: serviceCorrelation().totalEvents, coverage: "Illustrative cohorts", note: "Not supplied — illustrative after-sales fixture.", kind: "demo" },
      { name: "Parts usage & parts inventory", system: "Fusion Inventory / Service", status: "Demo data", rows: partsDemand().parts.length + " SKUs", coverage: "Illustrative", note: "Not supplied — synthetic parts fixture linked to sales mix.", kind: "demo" },
      { name: "Vehicle mileage & warranty claims", system: "Fusion Service / CRM", status: "Blocked", rows: 0, coverage: "—", note: "Not available in sample; required for production after-sales modelling.", kind: "missing" },
      { name: "Open purchase orders / inbound", system: "Fusion Procurement", status: "Demo data", rows: procurement().filter(function (p) { return p.open; }).length, coverage: "Synthetic inbound", note: "Not supplied — inbound modelled from synthetic PO fixture.", kind: "demo" }
    ];
    return { sources: sources, dq: dq, score: dq.score, salesRows: dq.salesRows, invRows: dq.invRows, mismatch: dq.mismatch, coverage: Eng.monthLabel(D0.firstMonth) + " → " + Eng.monthLabel(D0.lastMonth), models: D0.models.length, locations: D0.locations.length, invLocations: D0.invLocations.length };
  }
  function lineage() {
    return {
      pipeline: [
        { t: "Oracle Fusion ERP / CRM", d: "System of record — Order Mgmt, Inventory, Procurement, Service, Financials, CRM", icon: "cloud", kind: "source" },
        { t: "Secure read-only integration", d: "Fusion REST / BICC governed extracts · no write-back in this phase", icon: "vpn_lock", kind: "integration" },
        { t: "Curated analytics layer", d: "Normalised sales, inventory, procurement, service & parts models", icon: "dataset", kind: "curated" },
        { t: "Forecast & decision models", d: "Demand, order, procurement-range, decay, service-intensity, parts & priority", icon: "model_training", kind: "model" },
        { t: "Explainability service", d: "Drivers, confidence, assumptions, evidence & data lineage per recommendation", icon: "psychology", kind: "explain" },
        { t: "Decision Intelligence application", d: "This experience — cockpit, modules, decision log & governance", icon: "insights", kind: "app" }
      ],
      metrics: [
        ["Recommended order mix", "Fusion Order Management", "Sales history + inventory snapshot", "confirmed"],
        ["Procurement range", "Fusion Procurement", "Synthetic supplier & PO fixture", "demo"],
        ["Inventory risk & aging", "Fusion Inventory Management", "Inventory workbook", "confirmed"],
        ["Sales forecast", "Fusion Order Management", "Sales history workbook", "confirmed"],
        ["Configuration demand", "Fusion Product Management", "Sales history workbook", "confirmed"],
        ["Service-intensity index", "Fusion Service", "Synthetic service fixture", "demo"],
        ["Spare-parts forecast", "Fusion Inventory / Service", "Synthetic parts fixture", "demo"],
        ["Executive priority score", "Fusion Financials (exposure)", "Derived across modules", "confirmed"]
      ].map(function (r) { return { metric: r[0], source: r[1], basis: r[2], state: r[3], sourceNote: r[3] === "demo" ? "Expected production source — confirm during discovery" : "Expected production source — confirm during discovery" }; })
    };
  }

  window.BIEngine2 = {
    LABELS: LABELS, SEED: SEED, planningMonth: planningMonth, planningMonths: planningMonths, seasonalFor: seasonalFor, unitCost: unitCost, obsLead: obsLead,
    suppliers: SUPPLIERS, supplierPerf: supplierPerf, procurement: procurement, inboundFor: inboundFor,
    orderOpt: orderOpt, orderScenarioCompare: orderScenarioCompare,
    configInsights: configInsights, clusterMeta: clusterMeta,
    procurementOpt: procurementOpt,
    serviceCorrelation: serviceCorrelation, parts: PARTS, partsDemand: partsDemand,
    decisionFeed: decisionFeed, priorityScore: priorityScore,
    dataHealth: dataHealth, lineage: lineage,
    installedBase: installedBase
  };
})();
