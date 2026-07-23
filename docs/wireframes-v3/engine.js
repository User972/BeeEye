/* Meridian BI — analytical engine (pure, framework-free). Exposed as window.BIEngine.
   All KPIs are calculated from window.BIDATA (the real uploaded workbooks). No hardcoded totals. */
(function () {
  "use strict";
  var RAW = window.BIDATA || { sales: [], inventory: [] };
  var MS = 86400000;

  /* ---------------- generic utils ---------------- */
  function sum(a) { var s = 0; for (var i = 0; i < a.length; i++) s += a[i]; return s; }
  function mean(a) { return a.length ? sum(a) / a.length : 0; }
  function std(a) { if (a.length < 2) return 0; var m = mean(a); return Math.sqrt(mean(a.map(function (x) { return (x - m) * (x - m); }))); }
  function clamp(x, lo, hi) { return Math.max(lo, Math.min(hi, x)); }
  function round(x, d) { var p = Math.pow(10, d || 0); return Math.round(x * p) / p; }
  function uniq(a) { return Array.from(new Set(a)); }
  function by(a, k) { var m = {}; a.forEach(function (r) { (m[r[k]] = m[r[k]] || []).push(r); }); return m; }
  function parseD(s) { return s ? Date.parse(s) : NaN; }
  function monthKey(iso) { return iso ? iso.slice(0, 7) : ""; }
  function addMonth(mk, n) { var y = +mk.slice(0, 4), m = +mk.slice(5, 7) - 1 + n; y += Math.floor(m / 12); m = ((m % 12) + 12) % 12; return y + "-" + String(m + 1).padStart(2, "0"); }
  function monthLabel(mk) { var names = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]; return names[+mk.slice(5, 7) - 1] + " " + mk.slice(2, 4); }
  function monthRange(a, b) { var out = [], c = a; while (c <= b) { out.push(c); c = addMonth(c, 1); } return out; }

  /* ---------------- formatting ---------------- */
  function fmtSAR(n, opt) {
    opt = opt || {}; if (n == null || isNaN(n)) return "—";
    var a = Math.abs(n), s;
    if (a >= 1e9) s = (n / 1e9).toFixed(2) + "B";
    else if (a >= 1e6) s = (n / 1e6).toFixed(2) + "M";
    else if (a >= 1e3) s = (n / 1e3).toFixed(opt.k2 ? 2 : 1) + "K";
    else s = Math.round(n).toLocaleString("en-US");
    return "SAR " + s + (opt.perDay ? "/day" : "");
  }
  function fmtInt(n) { return n == null || isNaN(n) ? "—" : Math.round(n).toLocaleString("en-US"); }
  function fmtNum(n, d) { return n == null || isNaN(n) ? "—" : (+n).toLocaleString("en-US", { minimumFractionDigits: d || 0, maximumFractionDigits: d || 0 }); }
  function fmtPct(n, d) { return n == null || isNaN(n) ? "—" : (n >= 0 ? "" : "") + n.toFixed(d == null ? 1 : d) + "%"; }
  function fmtSignPct(n, d) { return (n >= 0 ? "+" : "") + fmtPct(n, d); }

  /* ---------------- normalization ---------------- */
  var SALES = null, INV = null;
  function truthy(v) { var s = String(v).trim().toLowerCase(); return s === "true" || s === "yes" || s === "1" || v === true; }
  function normSales() {
    if (SALES) return SALES;
    SALES = RAW.sales.map(function (r, i) {
      var o = Object.assign({}, r);
      o._id = i; o._mk = monthKey(r.sale_date); o._ms = parseD(r.sale_date);
      o._ramadan = truthy(r.is_ramadan);
      o._disc = truthy(r.discount_applied) || (+r.discount_pct > 0);
      o.units_sold = +r.units_sold; o.unit_price = +r.unit_price; o.revenue = +r.revenue; o.discount_pct = +r.discount_pct;
      o._gross = o.units_sold * o.unit_price;
      return o;
    });
    return SALES;
  }
  function normInv() {
    if (INV) return INV;
    INV = RAW.inventory.map(function (r, i) {
      var o = Object.assign({}, r);
      o._id = i; o._dop = parseD(r.date_of_purchase); o._dom = parseD(r.date_of_manufacture); o._svc = parseD(r.service_date);
      o.purchase_price = +r.purchase_price; o.holding_cost_per_day = +r.holding_cost_per_day; o.lead_time_days = +r.lead_time_days;
      return o;
    });
    return INV;
  }

  /* ---------------- dimensions ---------------- */
  var DIMS = null;
  function dims() {
    if (DIMS) return DIMS;
    var s = normSales(), iv = normInv();
    var modelMap = {}, brandModels = {};
    s.forEach(function (r) {
      if (!modelMap[r.model]) modelMap[r.model] = { brand: r.brand, type: r.type, variants: new Set() };
      modelMap[r.model].variants.add(r.variant);
      (brandModels[r.brand] = brandModels[r.brand] || new Set()).add(r.model);
    });
    var months = monthRange(s.reduce(function (a, r) { return r._mk < a ? r._mk : a; }, "9999-99"), s.reduce(function (a, r) { return r._mk > a ? r._mk : a; }, "0000-00"));
    DIMS = {
      locations: uniq(s.map(function (r) { return r.location; })).sort(),
      invLocations: uniq(iv.map(function (r) { return r.location; })).sort(),
      models: uniq(s.map(function (r) { return r.model; })).sort(),
      variants: uniq(s.map(function (r) { return r.variant; })).sort(),
      brands: uniq(s.map(function (r) { return r.brand; })).sort(),
      types: uniq(s.map(function (r) { return r.type; })).sort(),
      colours: uniq(s.map(function (r) { return r.colour; })).sort(),
      interiors: uniq(s.map(function (r) { return r.interior; })).sort(),
      discountBands: [0, 5, 10, 15, 20],
      months: months, firstMonth: months[0], lastMonth: months[months.length - 1],
      modelMap: Object.keys(modelMap).reduce(function (acc, k) { acc[k] = { brand: modelMap[k].brand, type: modelMap[k].type, variants: Array.from(modelMap[k].variants).sort() }; return acc; }, {}),
      brandModels: Object.keys(brandModels).reduce(function (acc, k) { acc[k] = Array.from(brandModels[k]).sort(); return acc; }, {})
    };
    return DIMS;
  }

  /* ---------------- filtering ---------------- */
  function applySalesFilters(rows, f) {
    f = f || {};
    return rows.filter(function (r) {
      if (f.brand && f.brand.length && f.brand.indexOf(r.brand) < 0) return false;
      if (f.model && f.model.length && f.model.indexOf(r.model) < 0) return false;
      if (f.variant && f.variant.length && f.variant.indexOf(r.variant) < 0) return false;
      if (f.type && f.type.length && f.type.indexOf(r.type) < 0) return false;
      if (f.location && f.location.length && f.location.indexOf(r.location) < 0) return false;
      if (f.colour && f.colour.length && f.colour.indexOf(r.colour) < 0) return false;
      if (f.interior && f.interior.length && f.interior.indexOf(r.interior) < 0) return false;
      if (f.ramadan === "yes" && !r._ramadan) return false;
      if (f.ramadan === "no" && r._ramadan) return false;
      if (f.discountBand != null && f.discountBand !== "" && +r.discount_pct !== +f.discountBand) return false;
      if (f.dateFrom && r._mk < f.dateFrom) return false;
      if (f.dateTo && r._mk > f.dateTo) return false;
      return true;
    });
  }
  function applyInvFilters(rows, f) {
    f = f || {};
    return rows.filter(function (r) {
      if (f.brand && f.brand.length && f.brand.indexOf(r.brand) < 0) return false;
      if (f.model && f.model.length && f.model.indexOf(r.model) < 0) return false;
      if (f.variant && f.variant.length && f.variant.indexOf(r.variant) < 0) return false;
      if (f.type && f.type.length && f.type.indexOf(r.type) < 0) return false;
      if (f.location && f.location.length && f.location.indexOf(r.location) < 0) return false;
      if (f.colour && f.colour.length && f.colour.indexOf(r.colour) < 0) return false;
      if (f.interior && f.interior.length && f.interior.indexOf(r.interior) < 0) return false;
      return true;
    });
  }

  /* ---------------- sales KPIs & breakdowns ---------------- */
  function salesKpis(rows) {
    var units = sum(rows.map(function (r) { return r.units_sold; }));
    var rev = sum(rows.map(function (r) { return r.revenue; }));
    var gross = sum(rows.map(function (r) { return r._gross; }));
    var discTx = rows.filter(function (r) { return r._disc; });
    var discUnits = sum(discTx.map(function (r) { return r.units_sold; }));
    return {
      units: units, revenue: rev, gross: gross, discountValue: gross - rev,
      asp: units ? rev / units : 0, avgListPrice: units ? gross / units : 0,
      avgDiscountPct: gross ? (gross - rev) / gross * 100 : 0,
      discountedTx: discTx.length, txCount: rows.length,
      discountedTxPct: rows.length ? discTx.length / rows.length * 100 : 0,
      discountedUnitsPct: units ? discUnits / units * 100 : 0
    };
  }
  function monthlySeries(rows, months) {
    var mu = {}, mr = {};
    rows.forEach(function (r) { mu[r._mk] = (mu[r._mk] || 0) + r.units_sold; mr[r._mk] = (mr[r._mk] || 0) + r.revenue; });
    return months.map(function (mk) { return { month: mk, label: monthLabel(mk), units: mu[mk] || 0, revenue: mr[mk] || 0 }; });
  }
  function breakdown(rows, key) {
    var m = {};
    rows.forEach(function (r) { var k = r[key]; if (!m[k]) m[k] = { key: k, units: 0, revenue: 0, gross: 0, tx: 0 }; m[k].units += r.units_sold; m[k].revenue += r.revenue; m[k].gross += r._gross; m[k].tx++; });
    return Object.keys(m).map(function (k) { return m[k]; }).sort(function (a, b) { return b.units - a.units; });
  }
  function growth(series) {
    // series: monthly {units,revenue}. Compute YoY, MoM on last populated month, YTD, rolling sums.
    var n = series.length, last = series[n - 1] || { units: 0, revenue: 0 };
    var prev = series[n - 2] || { units: 0 }, yrAgo = series[n - 13] || { units: 0 };
    var lastYr = last.month ? last.month.slice(0, 4) : "";
    var ytd = sum(series.filter(function (s) { return s.month.slice(0, 4) === lastYr; }).map(function (s) { return s.units; }));
    var ytdRev = sum(series.filter(function (s) { return s.month.slice(0, 4) === lastYr; }).map(function (s) { return s.revenue; }));
    function roll(k) { return sum(series.slice(-k).map(function (s) { return s.units; })); }
    return {
      mom: prev.units ? (last.units - prev.units) / prev.units * 100 : null,
      yoy: yrAgo.units ? (last.units - yrAgo.units) / yrAgo.units * 100 : null,
      ytd: ytd, ytdRev: ytdRev, ytdYear: lastYr,
      roll3: roll(3), roll6: roll(6), roll12: roll(12), lastMonth: last
    };
  }
  function ramadanCompare(rows) {
    var r = rows.filter(function (x) { return x._ramadan; }), nr = rows.filter(function (x) { return !x._ramadan; });
    var rMonths = uniq(r.map(function (x) { return x._mk; })).length || 1, nMonths = uniq(nr.map(function (x) { return x._mk; })).length || 1;
    var ru = sum(r.map(function (x) { return x.units_sold; })), nu = sum(nr.map(function (x) { return x.units_sold; }));
    return {
      ramUnits: ru, nonUnits: nu, ramRev: sum(r.map(function (x) { return x.revenue; })), nonRev: sum(nr.map(function (x) { return x.revenue; })),
      ramMonths: rMonths, nonMonths: nMonths,
      ramAvgMonthly: ru / rMonths, nonAvgMonthly: nu / nMonths,
      lift: nu / nMonths ? (ru / rMonths - nu / nMonths) / (nu / nMonths) * 100 : null,
      ramDiscTxPct: r.length ? r.filter(function (x) { return x._disc; }).length / r.length * 100 : 0,
      nonDiscTxPct: nr.length ? nr.filter(function (x) { return x._disc; }).length / nr.length * 100 : 0
    };
  }
  function discountBands(rows) {
    var bands = [0, 5, 10, 15, 20];
    return bands.map(function (b) {
      var g = rows.filter(function (r) { return +r.discount_pct === b; });
      var months = uniq(g.map(function (r) { return r._mk; })).length || 1;
      var u = sum(g.map(function (r) { return r.units_sold; }));
      return { band: b, tx: g.length, units: u, revenue: sum(g.map(function (r) { return r.revenue; })), avgUnitsPerTx: g.length ? u / g.length : 0, asp: u ? sum(g.map(function (r) { return r.revenue; })) / u : 0 };
    });
  }

  /* ---------------- demand velocity (fallback hierarchy) ---------------- */
  var AGG = null;
  function aggMaps() {
    if (AGG) return AGG;
    var s = normSales();
    var lmv = {}, mv = {}, mdl = {}, mvTot = {}, lmvTot = {}, modelLocs = {};
    s.forEach(function (r) {
      var k1 = r.location + "|" + r.model + "|" + r.variant + "|" + r._mk;
      var k2 = r.model + "|" + r.variant + "|" + r._mk;
      var k3 = r.model + "|" + r._mk;
      lmv[k1] = (lmv[k1] || 0) + r.units_sold;
      mv[k2] = (mv[k2] || 0) + r.units_sold;
      mdl[k3] = (mdl[k3] || 0) + r.units_sold;
      mvTot[r.model + "|" + r.variant] = (mvTot[r.model + "|" + r.variant] || 0) + r.units_sold;
      lmvTot[r.location + "|" + r.model + "|" + r.variant] = (lmvTot[r.location + "|" + r.model + "|" + r.variant] || 0) + r.units_sold;
      (modelLocs[r.model] = modelLocs[r.model] || new Set()).add(r.location);
    });
    AGG = { lmv: lmv, mv: mv, mdl: mdl, mvTot: mvTot, lmvTot: lmvTot, modelLocs: modelLocs, lastMonth: dims().lastMonth };
    return AGG;
  }
  function trailingMonths(n, endMonth) { var out = [], c = endMonth || aggMaps().lastMonth; for (var i = 0; i < n; i++) { out.push(c); c = addMonth(c, -1); } return out; }
  function demandVelocity(loc, model, variant, nMonths, endMonth) {
    var A = aggMaps(); var w = trailingMonths(nMonths || 3, endMonth);
    var lmvVals = w.map(function (m) { return A.lmv[loc + "|" + model + "|" + variant + "|" + m] || 0; });
    if (sum(lmvVals) > 0) { var nz = lmvVals.filter(function (v) { return v > 0; }).length; return { v: mean(lmvVals), basis: "Location-model-variant demand", conf: nz >= 2 ? "High" : "Medium", detail: "Trailing " + (nMonths || 3) + "-month average at this location." }; }
    var mvVals = w.map(function (m) { return A.mv[model + "|" + variant + "|" + m] || 0; });
    var mvTot = A.mvTot[model + "|" + variant] || 0;
    var share = mvTot ? (A.lmvTot[loc + "|" + model + "|" + variant] || 0) / mvTot : 0;
    if (sum(mvVals) > 0 && share > 0) { return { v: mean(mvVals) * share, basis: "National model-variant fallback", conf: "Medium", detail: "National " + model + " " + variant + " demand scaled by this location's " + (share * 100).toFixed(1) + "% historical share." }; }
    var mdlVals = w.map(function (m) { return A.mdl[model + "|" + m] || 0; });
    var nLoc = (A.modelLocs[model] || new Set()).size || 1;
    if (sum(mdlVals) > 0) { return { v: mean(mdlVals) / nLoc, basis: "Model-level fallback", conf: "Low", detail: "National " + model + " demand divided across " + nLoc + " selling locations." }; }
    return { v: 0, basis: "Insufficient demand history", conf: "Low", detail: "No reliable recent demand signal for this combination." };
  }
  function demandTrend(loc, model, variant, useNational) {
    var A = aggMaps(); var recent = trailingMonths(3), prior = trailingMonths(3, addMonth(A.lastMonth, -3));
    function pull(mk) { if (useNational) return A.mv[model + "|" + variant + "|" + mk] || 0; return A.lmv[loc + "|" + model + "|" + variant + "|" + mk] || 0; }
    var r = mean(recent.map(pull)), p = mean(prior.map(pull));
    if (p === 0 && r === 0) { var nr = mean(recent.map(function (m) { return A.mv[model + "|" + variant + "|" + m] || 0; })), np = mean(prior.map(function (m) { return A.mv[model + "|" + variant + "|" + m] || 0; })); r = nr; p = np; }
    var chg = p ? (r - p) / p * 100 : (r > 0 ? 100 : 0);
    return { recent: r, prior: p, changePct: chg, dir: chg > 8 ? "increasing" : chg < -8 ? "declining" : "stable" };
  }

  /* ---------------- inventory metrics + risk + recommendation ---------------- */
  function percentileRanker(vals) { var s = vals.slice().sort(function (a, b) { return a - b; }); return function (x) { if (s.length < 2) return 50; var i = 0; while (i < s.length && s[i] < x) i++; return i / (s.length - 1) * 100; }; }
  function agingBandOf(age, t) { t = t || [30, 60, 90, 120]; if (age <= t[0]) return "New"; if (age <= t[1]) return "Healthy"; if (age <= t[2]) return "Watch"; if (age <= t[3]) return "High attention"; return "Critical aging"; }
  function mfgBandOf(age) { if (age <= 180) return "0–180 days"; if (age <= 270) return "181–270 days"; if (age <= 365) return "271–365 days"; return "365+ days"; }
  function riskBandOf(score, t) { t = t || [34, 59, 79]; if (score <= t[0]) return "Low"; if (score <= t[1]) return "Medium"; if (score <= t[2]) return "High"; return "Critical"; }

  function computeInventory(settings, filters) {
    var S = Object.assign({ analysisDate: "2026-06-30", agingBands: [30, 60, 90, 120], riskBands: [34, 59, 79], trailingMonths: 3, coverTarget: 2, coverMax: 6, weights: { cover: 30, aging: 25, demand: 20, holding: 15, lead: 10 } }, settings || {});
    var iv = applyInvFilters(normInv(), filters);
    var ad = parseD(S.analysisDate);
    var holdRanker = percentileRanker(normInv().map(function (r) { return Math.max(0, (ad - r._dop) / MS) * r.holding_cost_per_day; }));
    var leadRanker = percentileRanker(normInv().map(function (r) { return r.lead_time_days; }));
    // group stock counts across ALL inventory (not filtered) for accurate cover
    var groupStock = {}; normInv().forEach(function (r) { var k = r.location + "|" + r.model + "|" + r.variant; groupStock[k] = (groupStock[k] || 0) + 1; });
    var W = S.weights, wsum = W.cover + W.aging + W.demand + W.holding + W.lead || 100;

    var units = iv.map(function (r) {
      var invAge = Math.round((ad - r._dop) / MS);
      var mfgAge = Math.round((ad - r._dom) / MS);
      var accHold = Math.max(0, invAge) * r.holding_cost_per_day;
      var dem = demandVelocity(r.location, r.model, r.variant, S.trailingMonths);
      var trend = demandTrend(r.location, r.model, r.variant);
      var gStock = groupStock[r.location + "|" + r.model + "|" + r.variant] || 1;
      var cover = dem.v > 0 ? gStock / dem.v : (gStock > 0 ? 999 : 0);
      // subscores 0..100
      var coverSub = dem.v > 0 ? clamp((cover - 1) / (S.coverMax - 1) * 100, 0, 100) : 90;
      var agingSub = clamp(invAge / S.agingBands[3] * 100, 0, 100);
      var demandSub = trend.dir === "declining" ? clamp(60 + Math.min(40, Math.abs(trend.changePct)), 0, 100) : trend.dir === "stable" ? 35 : 10;
      var holdSub = holdRanker(accHold);
      var leadSub = leadRanker(r.lead_time_days);
      var factors = [
        { key: "cover", label: "estimated stock cover", pts: W.cover / 100 * coverSub, detail: dem.v > 0 ? cover.toFixed(1) + " months of cover" : "no reliable demand signal" },
        { key: "aging", label: "inventory holding age", pts: W.aging / 100 * agingSub, detail: invAge + " days in stock" },
        { key: "demand", label: "recent demand trend", pts: W.demand / 100 * demandSub, detail: trend.dir + " (" + fmtSignPct(trend.changePct) + " vs prior quarter)" },
        { key: "holding", label: "carrying-cost exposure", pts: W.holding / 100 * holdSub, detail: fmtSAR(accHold) + " accrued" },
        { key: "lead", label: "historical lead time", pts: W.lead / 100 * leadSub, detail: r.lead_time_days + " days" }
      ];
      var score = Math.round(factors.reduce(function (a, f) { return a + f.pts; }, 0) * (100 / wsum));
      score = clamp(score, 0, 100);
      var band = riskBandOf(score, S.riskBands);
      var rec = recommend({ r: r, invAge: invAge, cover: cover, dem: dem, trend: trend, band: band, gStock: gStock, accHold: accHold, settings: S });
      return {
        stock_id: r.stock_id, chassis_no: r.chassis_no, brand: r.brand, model: r.model, variant: r.variant, colour: r.colour, interior: r.interior, type: r.type,
        location: r.location, date_of_purchase: r.date_of_purchase, date_of_manufacture: r.date_of_manufacture, service_date: r.service_date,
        purchase_price: r.purchase_price, holding_cost_per_day: r.holding_cost_per_day, lead_time_days: r.lead_time_days,
        invAge: invAge, mfgAge: mfgAge, accHold: accHold, agingBand: agingBandOf(invAge, S.agingBands), mfgBand: mfgBandOf(mfgAge),
        velocity: dem.v, demandBasis: dem.basis, demandConf: dem.conf, demandDetail: dem.detail, cover: cover, groupStock: gStock,
        trend: trend.dir, trendPct: trend.changePct, risk: score, riskBand: band, factors: factors.sort(function (a, b) { return b.pts - a.pts; }), rec: rec
      };
    });

    // aggregates
    var val = sum(units.map(function (u) { return u.purchase_price; }));
    var accHoldTot = sum(units.map(function (u) { return u.accHold; }));
    var dailyHoldTot = sum(units.map(function (u) { return u.holding_cost_per_day; }));
    function bandAgg(field, keys) { var m = {}; keys.forEach(function (k) { m[k] = { key: k, units: 0, value: 0 }; }); units.forEach(function (u) { var k = u[field]; if (!m[k]) m[k] = { key: k, units: 0, value: 0 }; m[k].units++; m[k].value += u.purchase_price; }); return keys.map(function (k) { return m[k]; }); }
    function dimAgg(field) { var m = {}; units.forEach(function (u) { var k = u[field]; if (!m[k]) m[k] = { key: k, units: 0, value: 0, hold: 0 }; m[k].units++; m[k].value += u.purchase_price; m[k].hold += u.accHold; }); return Object.keys(m).map(function (k) { return m[k]; }).sort(function (a, b) { return b.value - a.value; }); }
    var riskKeys = ["Low", "Medium", "High", "Critical"];
    var agingKeys = ["New", "Healthy", "Watch", "High attention", "Critical aging"];
    var mfgKeys = ["0–180 days", "181–270 days", "271–365 days", "365+ days"];
    var agg = {
      count: units.length, value: val, accHold: accHoldTot, dailyHold: dailyHoldTot,
      avgInvAge: mean(units.map(function (u) { return u.invAge; })), avgMfgAge: mean(units.map(function (u) { return u.mfgAge; })), avgLead: mean(units.map(function (u) { return u.lead_time_days; })),
      highRiskValue: sum(units.filter(function (u) { return u.riskBand === "High" || u.riskBand === "Critical"; }).map(function (u) { return u.purchase_price; })),
      criticalValue: sum(units.filter(function (u) { return u.riskBand === "Critical"; }).map(function (u) { return u.purchase_price; })),
      criticalCount: units.filter(function (u) { return u.riskBand === "Critical"; }).length,
      highCount: units.filter(function (u) { return u.riskBand === "High"; }).length,
      decliningValue: sum(units.filter(function (u) { return u.trend === "declining"; }).map(function (u) { return u.purchase_price; })),
      transferCount: units.filter(function (u) { return u.rec.action === "Transfer stock"; }).length,
      promoCount: units.filter(function (u) { return u.rec.action === "Start targeted promotion"; }).length,
      discountCount: units.filter(function (u) { return u.rec.action === "Apply controlled discount"; }).length,
      pauseCount: units.filter(function (u) { return u.rec.action === "Pause / reduce procurement"; }).length,
      byRisk: riskKeys.map(function (k) { return { key: k, units: units.filter(function (u) { return u.riskBand === k; }).length, value: sum(units.filter(function (u) { return u.riskBand === k; }).map(function (u) { return u.purchase_price; })) }; }),
      byAging: agingKeys.map(function (k) { return { key: k, units: units.filter(function (u) { return u.agingBand === k; }).length, value: sum(units.filter(function (u) { return u.agingBand === k; }).map(function (u) { return u.purchase_price; })) }; }),
      byMfg: mfgKeys.map(function (k) { return { key: k, units: units.filter(function (u) { return u.mfgBand === k; }).length }; }),
      byLocation: dimAgg("location"), byModel: dimAgg("model"), byVariant: dimAgg("variant"), byBrand: dimAgg("brand"), byColour: dimAgg("colour"), byInterior: dimAgg("interior")
    };
    return { units: units, agg: agg, settings: S, transfers: transferOpportunities(units) };
  }

  function recommend(c) {
    var evid = [];
    var age = c.invAge, cover = c.cover, dem = c.dem, trend = c.trend, r = c.r;
    var mvResp = discountResponsive(r.model, r.variant);
    if (dem.v <= 0 && age > c.settings.agingBands[3]) {
      return { action: "Prioritise liquidation", conf: "Medium", why: "No reliable recent demand and the unit is beyond the critical aging threshold.", evidence: [age + " days in inventory", "Demand basis: " + dem.basis], outcome: "Recovers capital and stops holding-cost accrual on a non-moving unit.", assumptions: ["Assumes the absence of recent sales reflects genuinely weak demand, not a data gap."] };
    }
    if (dem.v <= 0) {
      return { action: "Investigate demand data", conf: "Low", why: "This location-model-variant has no reliable recent sales signal.", evidence: ["Demand basis: " + dem.basis, age + " days in inventory"], outcome: "Confirms whether the gap is a data issue or a true demand shortfall before acting.", assumptions: ["service_date meaning is unconfirmed and excluded from risk."] };
    }
    // transfer candidate
    var t = bestTransfer(r.model, r.variant, r.location);
    if (cover > c.settings.coverMax && t && t.destCover < cover * 0.6) {
      return { action: "Transfer stock", conf: t.conf, dest: t.dest, why: "This location holds elevated cover while " + t.dest + " shows stronger relative demand for the same model-variant.", evidence: [cover.toFixed(1) + " months cover here", t.dest + ": " + t.destCover.toFixed(1) + " months cover"], outcome: "Rebalances stock toward demand and lowers combined holding exposure.", assumptions: ["Transfer feasibility and logistics cost not modelled in the POC."] };
    }
    if (age > c.settings.agingBands[3] && mvResp.responsive) {
      return { action: "Apply controlled discount", conf: "Medium", pct: mvResp.suggest, why: "Unit is beyond the critical aging band and this model-variant has historically moved more volume in discounted periods.", evidence: [age + " days in inventory", fmtSAR(c.accHold) + " holding cost accrued", "Observed discount range: " + mvResp.range], outcome: "Accelerates sell-through within the historically observed discount range.", assumptions: ["Association only — historical discounts are correlated with, not proven to cause, higher volume."] };
    }
    if (age > c.settings.agingBands[2] && trend.dir !== "increasing") {
      return { action: "Start targeted promotion", conf: "Medium", why: "Inventory age is elevated and demand has softened but is not absent.", evidence: [age + " days in inventory", "Demand trend: " + trend.dir], outcome: "Lifts visibility for a slowing unit before deeper discounting is needed.", assumptions: ["Promotion response estimated from historical patterns."] };
    }
    if (cover > c.settings.coverMax && trend.dir !== "increasing") {
      return { action: "Pause / reduce procurement", conf: "Medium", why: "Stock cover is high and demand is flat or falling with no strong transfer destination.", evidence: [cover.toFixed(1) + " months cover", "Demand trend: " + trend.dir], outcome: "Prevents further build-up of an over-covered model-variant.", assumptions: ["Assumes current demand pattern persists."] };
    }
    return { action: "Retain", conf: "High", why: "Cover is within a healthy range, demand is holding and the unit is not aged.", evidence: [cover.toFixed(1) + " months cover", age + " days in inventory", "Demand trend: " + trend.dir], outcome: "No action required; continue to monitor.", assumptions: [] };
  }

  var DR = null;
  function discountResponsive(model, variant) {
    if (!DR) { DR = {}; }
    var k = model + "|" + variant; if (DR[k]) return DR[k];
    var s = normSales().filter(function (r) { return r.model === model && r.variant === variant; });
    var d = s.filter(function (r) { return r._disc; }), nd = s.filter(function (r) { return !r._disc; });
    var da = d.length ? mean(d.map(function (r) { return r.units_sold; })) : 0;
    var na = nd.length ? mean(nd.map(function (r) { return r.units_sold; })) : 0;
    var pcts = uniq(d.map(function (r) { return +r.discount_pct; })).filter(function (x) { return x > 0; }).sort(function (a, b) { return a - b; });
    var res = { responsive: da > na * 1.03 && d.length >= 5, suggest: pcts.length ? Math.min(15, pcts[Math.floor(pcts.length / 2)]) : 10, range: pcts.length ? pcts[0] + "%–" + pcts[pcts.length - 1] + "%" : "0%", discAvg: da, nonAvg: na };
    DR[k] = res; return res;
  }
  function bestTransfer(model, variant, srcLoc) {
    var locs = dims().invLocations; var best = null;
    var srcDem = demandVelocity(srcLoc, model, variant, 3);
    locs.forEach(function (L) {
      if (L === srcLoc) return;
      var gStock = normInv().filter(function (r) { return r.location === L && r.model === model && r.variant === variant; }).length;
      var d = demandVelocity(L, model, variant, 3);
      if (d.v <= 0) return;
      var cover = d.v > 0 ? gStock / d.v : 999;
      if (!best || cover < best.destCover) best = { dest: L, destCover: cover, destDem: d.v, conf: d.conf };
    });
    return best;
  }
  function transferOpportunities(units) {
    // group by model|variant; find source (high cover) -> dest (low cover, positive demand)
    var groups = {};
    units.forEach(function (u) { var k = u.model + "|" + u.variant; (groups[k] = groups[k] || []).push(u); });
    var out = [];
    Object.keys(groups).forEach(function (k) {
      var parts = k.split("|"), model = parts[0], variant = parts[1];
      var locs = {};
      dims().invLocations.forEach(function (L) {
        var st = normInv().filter(function (r) { return r.location === L && r.model === model && r.variant === variant; }).length;
        if (!st) return;
        var d = demandVelocity(L, model, variant, 3);
        locs[L] = { loc: L, stock: st, dem: d.v, cover: d.v > 0 ? st / d.v : 999, conf: d.conf };
      });
      var arr = Object.keys(locs).map(function (L) { return locs[L]; });
      if (arr.length < 2) return;
      arr.sort(function (a, b) { return b.cover - a.cover; });
      var src = arr[0], dst = arr[arr.length - 1];
      if (src.cover > 3 && dst.dem > 0 && src.cover > dst.cover * 1.5 && src.loc !== dst.loc) {
        var move = Math.max(1, Math.min(src.stock - 1, Math.round((src.cover - dst.cover) / 2 * dst.dem)));
        if (move < 1) return;
        var dailyHold = mean(normInv().filter(function (r) { return r.location === src.loc && r.model === model && r.variant === variant; }).map(function (r) { return r.holding_cost_per_day; }));
        out.push({
          model: model, variant: variant, src: src.loc, dest: dst.loc, units: move,
          srcCover: src.cover, destCover: dst.cover,
          srcCoverAfter: src.dem > 0 ? (src.stock - move) / src.dem : (src.stock - move > 0 ? 999 : 0),
          destCoverAfter: dst.dem > 0 ? (dst.stock + move) / dst.dem : 999,
          avoidedHold: Math.round(move * dailyHold * 30), conf: dst.conf,
          evidence: [src.loc + ": " + src.cover.toFixed(1) + "m cover, " + src.stock + " units", dst.loc + ": " + dst.cover.toFixed(1) + "m cover, ~" + dst.dem.toFixed(1) + "/mo demand"]
        });
      }
    });
    return out.sort(function (a, b) { return b.avoidedHold - a.avoidedHold; });
  }

  /* ---------------- forecasting ---------------- */
  function seasonalAt(Sh, idx, m, S0) { var i = idx; while (i >= Sh.length || Sh[i] == null) { i -= m; if (i < 0) return S0[((idx % m) + m) % m]; } return Sh[i]; }
  function holtWinters(y, m, h, alpha, beta, gamma) {
    var n = y.length;
    if (n < m + 2) return holtLinear(y, h, alpha, beta);
    var seasons = Math.floor(n / m);
    var level = mean(y.slice(0, m));
    var trend = (mean(y.slice(m, 2 * m)) - mean(y.slice(0, m))) / m; if (!isFinite(trend)) trend = 0;
    var S0 = [];
    for (var i = 0; i < m; i++) { var devs = []; for (var s = 0; s < seasons; s++) { var seg = y.slice(s * m, s * m + m); if (s * m + i < n) devs.push(y[s * m + i] - mean(seg)); } S0[i] = mean(devs); }
    var Sh = S0.slice(); var L = level, T = trend; var fitted = new Array(n).fill(null);
    for (var t = 0; t < n; t++) {
      var sv = seasonalAt(Sh, t, m, S0);
      fitted[t] = L + T + sv;
      var Ln = alpha * (y[t] - sv) + (1 - alpha) * (L + T);
      var Tn = beta * (Ln - L) + (1 - beta) * T;
      var Sn = gamma * (y[t] - Ln) + (1 - gamma) * sv;
      Sh[t + m] = Sn; L = Ln; T = Tn;
    }
    var fc = [];
    for (var k = 1; k <= h; k++) { fc.push(Math.max(0, L + k * T + seasonalAt(Sh, n + k - 1, m, S0))); }
    return { fitted: fitted, fc: fc, name: "Holt-Winters", L: L, T: T };
  }
  function holtLinear(y, h, alpha, beta) {
    alpha = alpha || 0.4; beta = beta || 0.1; var n = y.length; if (!n) return { fitted: [], fc: new Array(h).fill(0), name: "Holt" };
    var L = y[0], T = n > 1 ? y[1] - y[0] : 0; var fitted = [L];
    for (var t = 1; t < n; t++) { var f = L + T; fitted[t] = f; var Ln = alpha * y[t] + (1 - alpha) * (L + T); T = beta * (Ln - L) + (1 - beta) * T; L = Ln; }
    var fc = []; for (var k = 1; k <= h; k++) fc.push(Math.max(0, L + k * T)); return { fitted: fitted, fc: fc, name: "Holt", L: L, T: T };
  }
  function naive(y, h) { var v = y.length ? y[y.length - 1] : 0; return { fitted: y.map(function (_, i) { return i ? y[i - 1] : y[0]; }), fc: new Array(h).fill(v), name: "Naïve (last month)" }; }
  function movingAvg(y, h, k) { k = k || 3; var v = mean(y.slice(-k)); return { fitted: y.map(function (_, i) { return i < k ? mean(y.slice(0, i + 1)) : mean(y.slice(i - k, i)); }), fc: new Array(h).fill(Math.max(0, v)), name: k + "-month moving average" }; }
  function seasonalNaive(y, h, m) { m = m || 12; if (y.length < m) return naive(y, h); var fc = []; for (var k = 1; k <= h; k++) fc.push(Math.max(0, y[y.length - m + ((k - 1) % m)])); return { fitted: y.map(function (v, i) { return i >= m ? y[i - m] : v; }), fc: fc, name: "Seasonal naïve (last year)" }; }
  function metrics(actual, pred) {
    var n = actual.length, ae = [], se = [], diff = [], over = 0, under = 0, mape = [];
    for (var i = 0; i < n; i++) { var a = actual[i], f = pred[i], e = f - a; ae.push(Math.abs(e)); se.push(e * e); diff.push(e); if (f > a) over++; else if (f < a) under++; if (a !== 0) mape.push(Math.abs(e) / Math.abs(a)); }
    var sa = sum(actual);
    return { wmape: sa ? sum(ae) / sa * 100 : null, mae: mean(ae), rmse: Math.sqrt(mean(se)), bias: sa ? sum(diff) / sa * 100 : null, biasAbs: mean(diff), mape: mape.length ? mean(mape) * 100 : null, overPct: n ? over / n * 100 : 0, underPct: n ? under / n * 100 : 0, n: n };
  }
  function buildSeries(rows, months) { var s = monthlySeries(rows, months); return s.map(function (x) { return x.units; }); }
  var METHODS = {
    naive: naive, ma3: function (y, h) { return movingAvg(y, h, 3); }, snaive: function (y, h) { return seasonalNaive(y, h, 12); },
    hw: function (y, h) { return holtWinters(y, 12, h, 0.35, 0.08, 0.3); }
  };
  function forecast(rows, opts) {
    opts = opts || {}; var D = dims(); var months = D.months;
    var y = buildSeries(rows, months); var n = y.length;
    var H = opts.horizon || 6; var hold = Math.min(opts.holdout || 6, n - 12 > 0 ? n - 12 : 6);
    var trainY = y.slice(0, n - hold), holdY = y.slice(n - hold);
    var results = {}, keys = ["naive", "ma3", "snaive", "hw"];
    keys.forEach(function (key) { var m = METHODS[key](trainY, hold); results[key] = { name: m.name, pred: m.fc, metrics: metrics(holdY, m.fc) }; });
    // pick best by wmape (exclude null)
    var best = keys.filter(function (k) { return results[k].metrics.wmape != null; }).sort(function (a, b) { return results[a].metrics.wmape - results[b].metrics.wmape; })[0] || "hw";
    var chosen = opts.algo && METHODS[opts.algo] ? opts.algo : best;
    // refit chosen on full data for the future forecast
    var full = METHODS[chosen](y, H);
    var resid = []; for (var i = 0; i < n; i++) if (full.fitted[i] != null) resid.push(y[i] - full.fitted[i]);
    var sigma = std(resid);
    var z = opts.ci === 95 ? 1.96 : opts.ci === 90 ? 1.645 : 1.28;
    var futureMonths = []; for (var k = 1; k <= H; k++) futureMonths.push(addMonth(D.lastMonth, k));
    var future = full.fc.map(function (v, i) { var band = z * sigma * Math.sqrt(1 + 0.15 * i); return { month: futureMonths[i], label: monthLabel(futureMonths[i]), value: Math.max(0, v), lo: Math.max(0, v - band), hi: v + band }; });
    // backtest forecast (chosen trained on train) aligned to holdout months
    var btMethod = METHODS[chosen](trainY, hold);
    var holdMonths = months.slice(n - hold);
    var backtest = holdMonths.map(function (mk, i) { return { month: mk, label: monthLabel(mk), actual: holdY[i], forecast: Math.max(0, btMethod.fc[i]) }; });
    var hist = months.map(function (mk, i) { return { month: mk, label: monthLabel(mk), value: y[i], isHold: i >= n - hold }; });
    var expl = explainForecast(rows, y, future, chosen, results, opts);
    return {
      history: hist, backtest: backtest, future: future, futureSum: sum(future.map(function (f) { return f.value; })),
      chosen: chosen, chosenName: results[chosen] ? results[chosen].name : (full.name || chosen), best: best,
      methods: keys.map(function (key) { return { key: key, name: results[key].name, wmape: results[key].metrics.wmape, bias: results[key].metrics.bias, mae: results[key].metrics.mae, rmse: results[key].metrics.rmse, isBest: key === best, isChosen: key === chosen }; }),
      accuracy: results[chosen].metrics, sigma: sigma, holdout: hold, horizon: H, trainN: n - hold, totalN: n,
      histUnits: sum(y), lastMonth: D.lastMonth, explanation: expl, series: y, months: months
    };
  }
  function explainForecast(rows, y, future, chosen, results, opts) {
    var n = y.length, recent3 = mean(y.slice(-3)), prior12 = mean(y.slice(-15, -3)); var pts = [];
    var chg = prior12 ? (recent3 - prior12) / prior12 * 100 : 0;
    if (chg > 8) pts.push("Recent 3-month demand (" + recent3.toFixed(0) + "/mo) is above the prior 12-month average (" + prior12.toFixed(0) + "/mo).");
    else if (chg < -8) pts.push("Recent 3-month demand (" + recent3.toFixed(0) + "/mo) has slowed versus the prior 12-month average (" + prior12.toFixed(0) + "/mo).");
    else pts.push("Demand has been broadly stable versus the prior 12-month average.");
    // seasonality: Ramadan association
    var ram = ramadanCompare(rows); if (ram.lift != null && Math.abs(ram.lift) > 5) pts.push("Periods flagged as Ramadan show " + fmtSignPct(ram.lift, 0) + " monthly volume versus non-Ramadan periods (association, not proven cause).");
    var direction = sum(future.map(function (f) { return f.value; })) / future.length - recent3;
    pts.push("The " + (results[chosen] ? results[chosen].name : chosen) + " model projects a " + (direction >= 0 ? "continuation/uplift" : "moderation") + " over the next " + future.length + " months.");
    var wm = results[chosen] ? results[chosen].metrics.wmape : null;
    pts.push("Back-test WMAPE is " + (wm == null ? "n/a" : wm.toFixed(1) + "%") + "; confidence is " + (wm == null ? "low" : wm < 15 ? "high" : wm < 30 ? "medium" : "low") + ".");
    return { points: pts, recent3: recent3, prior12: prior12, changePct: chg };
  }

  /* ---------------- data quality ---------------- */
  function dataQuality() {
    var s = normSales(), iv = normInv(), issues = [];
    var dupStock = iv.length - uniq(iv.map(function (r) { return r.stock_id; })).length;
    var dupChassis = iv.length - uniq(iv.map(function (r) { return r.chassis_no; })).length;
    var negS = s.filter(function (r) { return r.units_sold < 0 || r.revenue < 0 || r.unit_price < 0; }).length;
    var negI = iv.filter(function (r) { return r.purchase_price < 0 || r.holding_cost_per_day < 0; }).length;
    var revBad = 0, revMax = 0; s.forEach(function (r) { var exp = r.units_sold * r.unit_price * (1 - r.discount_pct / 100); var e = Math.abs(exp - r.revenue); if (e > Math.max(1, Math.abs(exp) * 0.01)) revBad++; revMax = Math.max(revMax, e / Math.max(1, Math.abs(exp))); });
    var ltBad = 0; iv.forEach(function (r) { var d = (r._dop - r._dom) / MS; if (Math.abs(d - r.lead_time_days) > 2) ltBad++; });
    var invLoc = new Set(iv.map(function (r) { return r.location; })); var salesLoc = new Set(s.map(function (r) { return r.location; }));
    var mismatch = Array.from(salesLoc).filter(function (l) { return !invLoc.has(l); });
    var badDates = s.filter(function (r) { return isNaN(r._ms); }).length + iv.filter(function (r) { return isNaN(r._dop) || isNaN(r._dom); }).length;
    // sparse LMV segments
    var A = aggMaps(); var lmvKeys = uniq(normSales().map(function (r) { return r.location + "|" + r.model + "|" + r.variant; }));
    var sparse = 0; lmvKeys.forEach(function (k) { var w = trailingMonths(3); if (sum(w.map(function (m) { return A.lmv[k + "|" + m] || 0; })) === 0) sparse++; });
    issues.push({ id: "dup_stock", label: "Duplicate stock IDs", count: dupStock, severity: dupStock ? "high" : "ok", note: "All stock_id values are unique." });
    issues.push({ id: "dup_chassis", label: "Duplicate chassis numbers", count: dupChassis, severity: dupChassis ? "high" : "ok", note: "All chassis_no values are unique." });
    issues.push({ id: "rev", label: "Revenue reconciliation mismatches (>1%)", count: revBad, severity: revBad ? "medium" : "ok", note: "revenue = units × price × (1 − discount%) verified per row." });
    issues.push({ id: "lead", label: "Lead-time reconciliation mismatches (>2d)", count: ltBad, severity: ltBad ? "medium" : "ok", note: "lead_time_days = purchase − manufacture verified." });
    issues.push({ id: "neg", label: "Negative quantities / amounts", count: negS + negI, severity: (negS + negI) ? "high" : "ok", note: "No negative units, prices, revenue or holding costs." });
    issues.push({ id: "dates", label: "Invalid / unparseable dates", count: badDates, severity: badDates ? "high" : "ok", note: "All Excel serial dates parsed correctly." });
    issues.push({ id: "loc", label: "Sales locations absent from inventory", count: mismatch.length, severity: mismatch.length ? "medium" : "ok", note: mismatch.length ? mismatch.join(", ") + " sell but hold no stock." : "None." });
    issues.push({ id: "sparse", label: "Sparse location-model-variant segments (no trailing-3m sales)", count: sparse, severity: sparse ? "medium" : "ok", note: "Handled via the demand fallback hierarchy." });
    issues.push({ id: "svc", label: "Fields requiring business clarification", count: 1, severity: "medium", note: "service_date meaning unconfirmed — excluded from risk scoring." });
    var score = Math.round(100 - (dupStock + dupChassis) * 5 - revBad * 0.5 - (negS + negI) * 5 - badDates * 2 - mismatch.length * 1.5);
    return { issues: issues, score: clamp(score, 0, 100), salesRows: s.length, invRows: iv.length, revMaxErr: revMax, mismatch: mismatch, sparse: sparse };
  }

  /* ---------------- deterministic insight engine ---------------- */
  function ctxBuild(settings, filters) {
    var inv = computeInventory(settings, filters);
    var sales = applySalesFilters(normSales(), filters);
    var D = dims();
    return { inv: inv, sales: sales, months: D.months, settings: inv.settings, filters: filters || {} };
  }
  function topRiskUnits(inv, n) { return inv.units.slice().sort(function (a, b) { return b.risk - a.risk; }).slice(0, n || 5); }
  function execInsights(ctx) {
    var inv = ctx.inv, out = [];
    var top = topRiskUnits(inv, 1)[0];
    // highest stock-cover model-variant
    var mvCover = {}; inv.units.forEach(function (u) { var k = u.model + " " + u.variant; if (!mvCover[k] || u.cover > mvCover[k].cover) mvCover[k] = { k: k, cover: u.cover, model: u.model, variant: u.variant, value: 0 }; });
    inv.units.forEach(function (u) { var k = u.model + " " + u.variant; if (mvCover[k]) mvCover[k].value += u.purchase_price; });
    var mvArr = Object.keys(mvCover).map(function (k) { return mvCover[k]; }).filter(function (x) { return isFinite(x.cover); }).sort(function (a, b) { return b.cover - a.cover; });
    var topMV = mvArr[0];
    if (topMV) out.push({ icon: "risk", title: topMV.k + " carries the highest estimated stock-cover exposure at " + topMV.cover.toFixed(1) + " months.", target: { screen: "inventory", filter: { model: [topMV.model], variant: [topMV.variant] } } });
    // aging concentration
    var locHold = inv.agg.byLocation.slice().sort(function (a, b) { return b.hold - a.hold; }).slice(0, 2);
    if (locHold.length) out.push({ icon: "location", title: "Aging holding-cost exposure is concentrated in " + locHold.map(function (l) { return l.key; }).join(" and ") + " (" + fmtSAR(locHold[0].hold + (locHold[1] ? locHold[1].hold : 0)) + " accrued).", target: { screen: "inventory", filter: { location: locHold.map(function (l) { return l.key; }) } } });
    // strongest demand model vs stock
    var sBd = breakdown(ctx.sales, "model");
    if (sBd.length) out.push({ icon: "sales", title: sBd[0].key + " shows the strongest recent sales volume (" + fmtInt(sBd[0].units) + " units); confirm inventory keeps pace with demand.", target: { screen: "forecast", filter: { model: [sBd[0].key] } } });
    // forecast bias
    var fc = forecast(ctx.sales, { holdout: ctx.settings.holdout || 6, horizon: 3, ci: 80 });
    var biasDir = fc.accuracy.bias == null ? "n/a" : fc.accuracy.bias > 3 ? "high (over-forecasting)" : fc.accuracy.bias < -3 ? "low (under-forecasting)" : "broadly balanced";
    out.push({ icon: "forecast", title: "Total-business forecast bias is " + biasDir + " (" + (fc.accuracy.bias == null ? "n/a" : fmtSignPct(fc.accuracy.bias, 1)) + ") at back-test WMAPE " + (fc.accuracy.wmape == null ? "n/a" : fc.accuracy.wmape.toFixed(1) + "%") + ".", target: { screen: "forecast" } });
    // recommended action
    var t = inv.transfers[0];
    if (t) out.push({ icon: "action", title: "Recommended: transfer ~" + t.units + " × " + t.model + " " + t.variant + " from " + t.src + " to " + t.dest + " to cut ~" + fmtSAR(t.avoidedHold) + " of monthly holding exposure.", target: { screen: "actions" } });
    return { insights: out, forecast: fc, topMV: topMV };
  }

  function answer(q, ctx) {
    q = (q || "").toLowerCase();
    var inv = ctx.inv, D = dims();
    function resp(o) { return Object.assign({ answer: "", findings: [], metrics: [], filters: ctx.filters, period: D.firstMonth + " → " + D.lastMonth, confidence: "Medium", assumptions: ["Sample/demo data; not live Oracle Fusion.", "Inventory metrics as of " + ctx.settings.analysisDate + " (POC assumption)."], actions: [], targets: [] }, o); }
    var top = topRiskUnits(inv, 5);
    // routing
    if (/inventory value|total inventory|current inventory/.test(q) && !/risk/.test(q))
      return resp({ answer: "Total inventory purchase value is " + fmtSAR(inv.agg.value) + " across " + inv.agg.count + " stock units.", findings: ["Accumulated holding cost to date: " + fmtSAR(inv.agg.accHold) + ".", "Aggregate daily holding cost: " + fmtSAR(inv.agg.dailyHold, { perDay: true, k2: true }) + "."], metrics: [{ k: "Inventory value", v: fmtSAR(inv.agg.value) }, { k: "Units", v: fmtInt(inv.agg.count) }], targets: [{ label: "Open Inventory Intelligence", screen: "inventory" }], confidence: "High" });
    if (/highest stock value|model.*highest.*value|which model.*value/.test(q)) {
      var m = inv.agg.byModel[0]; return resp({ answer: m.key + " holds the highest inventory value at " + fmtSAR(m.value) + " (" + m.units + " units).", findings: inv.agg.byModel.slice(0, 3).map(function (x) { return x.key + ": " + fmtSAR(x.value) + " (" + x.units + " units)"; }), metrics: [{ k: "Top model value", v: fmtSAR(m.value) }], targets: [{ label: "View in Inventory", screen: "inventory", filter: { model: [m.key] } }], confidence: "High" });
    }
    if (/highest.*stock cover|highest.*overstock|overstock risk|estimated stock cover/.test(q)) {
      var r = execInsights(ctx).topMV; return resp({ answer: (r ? r.k + " has the highest estimated stock cover at " + r.cover.toFixed(1) + " months." : "No cover estimate available."), findings: top.slice(0, 3).map(function (u) { return u.model + " " + u.variant + " @ " + u.location + ": " + (isFinite(u.cover) ? u.cover.toFixed(1) + "m cover" : "no demand") + ", risk " + u.risk; }), metrics: [{ k: "Cover", v: r ? r.cover.toFixed(1) + " mo" : "—" }], actions: ["Review transfer opportunities before discounting."], targets: [{ label: "Open risk quadrant", screen: "inventory", filter: r ? { model: [r.model], variant: [r.variant] } : {} }], confidence: "Medium" });
    }
    if (/held.*90|more than 90|over 90|aged/.test(q)) {
      var aged = inv.units.filter(function (u) { return u.invAge > 90; }); return resp({ answer: aged.length + " units have been held longer than 90 days (" + fmtSAR(sum(aged.map(function (u) { return u.purchase_price; }))) + " purchase value).", findings: [fmtSAR(sum(aged.map(function (u) { return u.accHold; }))) + " of holding cost accrued on these units.", "Oldest: " + (aged.sort(function (a, b) { return b.invAge - a.invAge; })[0] || {}).invAge + " days."], metrics: [{ k: "Aged units", v: aged.length }], targets: [{ label: "Filter aging ≥ 90d", screen: "inventory" }], confidence: "High" });
    }
    if (/holding.cost|carrying.cost|accumulated/.test(q))
      return resp({ answer: "Accumulated holding-cost exposure is " + fmtSAR(inv.agg.accHold) + ", accruing at " + fmtSAR(inv.agg.dailyHold, { perDay: true, k2: true }) + ".", findings: inv.agg.byModel.slice(0, 3).map(function (x) { return x.key + ": " + fmtSAR(x.hold) + " accrued"; }), metrics: [{ k: "Accrued", v: fmtSAR(inv.agg.accHold) }, { k: "Daily", v: fmtSAR(inv.agg.dailyHold, { perDay: true, k2: true }) }], targets: [{ label: "Holding-cost view", screen: "inventory" }], confidence: "High" });
    if (/inconsistent|mismatch|inconsistent with.*demand|inventory.*demand/.test(q)) {
      var t = inv.transfers.slice(0, 3); return resp({ answer: t.length + " location imbalances found where stock is misaligned with recent demand.", findings: t.map(function (x) { return x.src + " (over-covered) vs " + x.dest + " (demand) for " + x.model + " " + x.variant; }), metrics: [{ k: "Imbalances", v: inv.transfers.length }], actions: t.map(function (x) { return "Transfer ~" + x.units + " " + x.model + " " + x.variant + ": " + x.src + " → " + x.dest; }), targets: [{ label: "Location mismatch matrix", screen: "inventory" }], confidence: "Medium" });
    }
    if (/transfer/.test(q)) {
      var tt = inv.transfers.slice(0, 4); return resp({ answer: tt.length ? "Top transfer opportunity: move ~" + tt[0].units + " × " + tt[0].model + " " + tt[0].variant + " from " + tt[0].src + " to " + tt[0].dest + "." : "No clear transfer opportunities under current settings.", findings: tt.map(function (x) { return x.src + " → " + x.dest + ": " + x.units + " × " + x.model + " " + x.variant + ", saves ~" + fmtSAR(x.avoidedHold) + "/mo"; }), metrics: [{ k: "Opportunities", v: inv.transfers.length }], actions: tt.map(function (x) { return "Create transfer action: " + x.src + " → " + x.dest; }), targets: [{ label: "Open transfer simulator", screen: "inventory" }], confidence: "Medium" });
    }
    if (/promot/.test(q)) {
      var promo = inv.units.filter(function (u) { return u.rec.action === "Start targeted promotion" || u.rec.action === "Apply controlled discount"; }); var byMV = {}; promo.forEach(function (u) { var k = u.model + " " + u.variant; byMV[k] = (byMV[k] || 0) + 1; });
      return resp({ answer: promo.length + " units are candidates for promotion or a controlled discount.", findings: Object.keys(byMV).sort(function (a, b) { return byMV[b] - byMV[a]; }).slice(0, 4).map(function (k) { return k + ": " + byMV[k] + " units"; }), metrics: [{ k: "Promotion candidates", v: promo.length }], actions: ["Apply discounts only within the historically observed 0–20% range."], targets: [{ label: "Promotion candidates", screen: "inventory" }], confidence: "Medium" });
    }
    if (/strongest.*sales|strongest.*trend|best.*trend|growing/.test(q)) {
      var g = {}; D.models.forEach(function (mo) { var rows = ctx.sales.filter(function (r) { return r.model === mo; }); var s = monthlySeries(rows, D.months).map(function (x) { return x.units; }); g[mo] = { recent: mean(s.slice(-3)), prior: mean(s.slice(-15, -3)) }; g[mo].chg = g[mo].prior ? (g[mo].recent - g[mo].prior) / g[mo].prior * 100 : 0; });
      var arr = Object.keys(g).map(function (k) { return { k: k, chg: g[k].chg, recent: g[k].recent }; }).sort(function (a, b) { return b.chg - a.chg; });
      return resp({ answer: arr[0].k + " shows the strongest recent sales trend (" + fmtSignPct(arr[0].chg, 0) + " vs prior year, ~" + arr[0].recent.toFixed(0) + " units/mo).", findings: arr.slice(0, 4).map(function (x) { return x.k + ": " + fmtSignPct(x.chg, 0) + " recent trend"; }), metrics: [{ k: "Top model trend", v: fmtSignPct(arr[0].chg, 0) }], targets: [{ label: "Open forecasting", screen: "forecast", filter: { model: [arr[0].k] } }], confidence: "Medium" });
    }
    if (/next quarter|forecast.*quarter|forecast for the next|future forecast|projection/.test(q)) {
      var fc = forecast(ctx.sales, { holdout: ctx.settings.holdout || 6, horizon: 3, ci: 80 });
      return resp({ answer: "Next-quarter total forecast is ~" + fmtInt(fc.futureSum) + " units (" + fc.chosenName + ").", findings: fc.future.map(function (f) { return f.label + ": ~" + f.value.toFixed(0) + " units (" + f.lo.toFixed(0) + "–" + f.hi.toFixed(0) + ")"; }).concat(fc.explanation.points.slice(0, 2)), metrics: [{ k: "Next 3 mo", v: fmtInt(fc.futureSum) }, { k: "WMAPE", v: fc.accuracy.wmape == null ? "—" : fc.accuracy.wmape.toFixed(1) + "%" }], targets: [{ label: "Open forecasting", screen: "forecast" }], confidence: fc.accuracy.wmape < 15 ? "High" : "Medium" });
    }
    if (/highest error|worst forecast|hardest.*forecast|difficult.*forecast|lowest confidence/.test(q)) {
      var errs = D.models.map(function (mo) { var f = forecast(ctx.sales.filter(function (r) { return r.model === mo; }), { holdout: 6, horizon: 3 }); return { k: mo, wmape: f.accuracy.wmape }; }).filter(function (x) { return x.wmape != null; }).sort(function (a, b) { return b.wmape - a.wmape; });
      return resp({ answer: errs.length ? errs[0].k + " is the hardest to forecast (WMAPE " + errs[0].wmape.toFixed(1) + "%)." : "n/a", findings: errs.slice(0, 5).map(function (x) { return x.k + ": WMAPE " + x.wmape.toFixed(1) + "%"; }), metrics: [{ k: "Worst WMAPE", v: errs.length ? errs[0].wmape.toFixed(1) + "%" : "—" }], targets: [{ label: "Forecast accuracy", screen: "forecast", filter: errs.length ? { model: [errs[0].k] } : {} }], confidence: "Medium" });
    }
    if (/bias|over.forecast|under.forecast/.test(q)) {
      var fcb = forecast(ctx.sales, { holdout: 6, horizon: 3 }); var b = fcb.accuracy.bias;
      return resp({ answer: "Total-business forecast bias is " + (b == null ? "n/a" : fmtSignPct(b, 1)) + " — " + (b > 3 ? "generally over-forecast" : b < -3 ? "generally under-forecast" : "broadly balanced") + ".", findings: ["Over-forecast in " + fcb.accuracy.overPct.toFixed(0) + "% of holdout months, under in " + fcb.accuracy.underPct.toFixed(0) + "%.", "MAE " + fcb.accuracy.mae.toFixed(1) + " units, RMSE " + fcb.accuracy.rmse.toFixed(1) + "."], metrics: [{ k: "Bias", v: b == null ? "—" : fmtSignPct(b, 1) }], targets: [{ label: "Bias analysis", screen: "forecast" }], confidence: "Medium" });
    }
    if (/ramadan/.test(q)) {
      var rc = ramadanCompare(ctx.sales); return resp({ answer: "Ramadan-flagged months averaged " + rc.ramAvgMonthly.toFixed(0) + " units/mo vs " + rc.nonAvgMonthly.toFixed(0) + " in other months (" + (rc.lift == null ? "n/a" : fmtSignPct(rc.lift, 0)) + ").", findings: ["Ramadan revenue: " + fmtSAR(rc.ramRev) + " across " + rc.ramMonths + " flagged months.", "Discount frequency in Ramadan: " + rc.ramDiscTxPct.toFixed(0) + "% of transactions vs " + rc.nonDiscTxPct.toFixed(0) + "% otherwise."], metrics: [{ k: "Ramadan lift", v: rc.lift == null ? "—" : fmtSignPct(rc.lift, 0) }], assumptions: ["Association only — not a proven causal effect."], targets: [{ label: "Ramadan & discount analysis", screen: "forecast" }], confidence: "Medium" });
    }
    if (/discount/.test(q)) {
      var db = discountBands(ctx.sales); return resp({ answer: "Discounts of 0–20% appear in the data; higher-discount transactions tend to carry more units per transaction.", findings: db.map(function (x) { return x.band + "%: " + fmtInt(x.units) + " units, " + x.avgUnitsPerTx.toFixed(1) + " units/txn, ASP " + fmtSAR(x.asp); }), metrics: [{ k: "Discount range", v: "0–20%" }], assumptions: ["Association only — discounts correlate with, but are not proven to cause, higher volume."], targets: [{ label: "Discount analysis", screen: "forecast" }], confidence: "Medium" });
    }
    if (/top.*action|management action|five action|5 action|what should/.test(q)) {
      var ai = execInsights(ctx); return resp({ answer: "Top management actions from current analysis:", findings: ai.insights.map(function (x) { return x.title; }), metrics: [{ k: "Critical units", v: inv.agg.criticalCount }, { k: "High-risk value", v: fmtSAR(inv.agg.highRiskValue) }], actions: inv.transfers.slice(0, 2).map(function (x) { return "Transfer " + x.units + " " + x.model + " " + x.variant + ": " + x.src + " → " + x.dest; }).concat(["Review procurement for over-covered variants."]), targets: [{ label: "Management Actions", screen: "actions" }], confidence: "Medium" });
    }
    if (/assumption/.test(q))
      return resp({ answer: "Key POC assumptions currently applied:", findings: ["Inventory analysis date: " + ctx.settings.analysisDate + " (configurable).", "No historical business forecasts supplied — accuracy shown via back-testing.", "service_date meaning unconfirmed; excluded from risk.", "Sparse location-model-variant demand uses a documented fallback hierarchy.", "Risk weights: cover " + ctx.settings.weights.cover + " / aging " + ctx.settings.weights.aging + " / demand " + ctx.settings.weights.demand + " / holding " + ctx.settings.weights.holding + " / lead " + ctx.settings.weights.lead + "."], targets: [{ label: "Methodology & Assumptions", screen: "methodology" }], confidence: "High" });
    if (/data quality|quality issue|data issue/.test(q)) {
      var dq = dataQuality(); return resp({ answer: "Data-quality score is " + dq.score + "/100. Key items to note:", findings: dq.issues.filter(function (i) { return i.severity !== "ok"; }).map(function (i) { return i.label + ": " + i.count + " — " + i.note; }), metrics: [{ k: "Quality score", v: dq.score + "/100" }], targets: [{ label: "Data Management", screen: "data" }], confidence: "High" });
    }
    if (/risk score|explain.*risk|why.*high risk/.test(q)) {
      var u = top[0]; return resp({ answer: u.stock_id + " (" + u.model + " " + u.variant + " @ " + u.location + ") scores " + u.risk + " — " + u.riskBand + ".", findings: u.factors.map(function (f) { return "+" + f.pts.toFixed(0) + " pts: " + f.label + " — " + f.detail; }), metrics: [{ k: "Risk score", v: u.risk + " (" + u.riskBand + ")" }], actions: [u.rec.action + " — " + u.rec.why], targets: [{ label: "Open vehicle detail", screen: "inventory" }], confidence: "Medium" });
    }
    // fallback
    var ai2 = execInsights(ctx);
    return resp({ answer: "Here is what stands out across inventory and sales right now:", findings: ai2.insights.map(function (x) { return x.title; }), metrics: [{ k: "Inventory value", v: fmtSAR(inv.agg.value) }, { k: "High-risk value", v: fmtSAR(inv.agg.highRiskValue) }, { k: "Critical units", v: inv.agg.criticalCount }], targets: [{ label: "Executive Cockpit", screen: "exec" }], confidence: "Medium" });
  }

  window.BIEngine = {
    fmtSAR: fmtSAR, fmtInt: fmtInt, fmtNum: fmtNum, fmtPct: fmtPct, fmtSignPct: fmtSignPct, monthLabel: monthLabel, addMonth: addMonth,
    dims: dims, applySalesFilters: applySalesFilters, applyInvFilters: applyInvFilters,
    salesKpis: salesKpis, monthlySeries: monthlySeries, breakdown: breakdown, growth: growth, ramadanCompare: ramadanCompare, discountBands: discountBands,
    computeInventory: computeInventory, forecast: forecast, demandVelocity: demandVelocity, dataQuality: dataQuality,
    ctxBuild: ctxBuild, execInsights: execInsights, answer: answer, topRiskUnits: topRiskUnits,
    normSales: normSales, normInv: normInv, raw: RAW
  };
})();
