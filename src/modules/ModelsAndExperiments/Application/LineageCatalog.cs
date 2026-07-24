using BeeEye.Modules.ModelsAndExperiments.Contracts;

namespace BeeEye.Modules.ModelsAndExperiments.Application;

/// <summary>
/// The declarative source-of-truth for the Lineage screen (V3-GOV-009): the six-stage source-to-decision
/// pipeline and the eight decision metrics with their source and basis. Ported verbatim from
/// <c>engine2.js</c> <c>lineage()</c> (L577–598).
/// <para>
/// Stage 2's "no write-back" claim must stay literally true — BeeEye never writes to Oracle Fusion
/// (CLAUDE.md). Do not add anything to the pipeline that contradicts it.
/// </para>
/// </summary>
public static class LineageCatalog
{
    public static readonly IReadOnlyList<PipelineStageDto> Pipeline =
    [
        new("Oracle Fusion ERP / CRM",
            "System of record — Order Mgmt, Inventory, Procurement, Service, Financials, CRM",
            "cloud", "source"),
        new("Secure read-only integration",
            "Fusion REST / BICC governed extracts · no write-back in this phase",
            "vpn_lock", "integration"),
        new("Curated analytics layer",
            "Normalised sales, inventory, procurement, service & parts models",
            "dataset", "curated"),
        new("Forecast & decision models",
            "Demand, order, procurement-range, decay, service-intensity, parts & priority",
            "model_training", "model"),
        new("Explainability service",
            "Drivers, confidence, assumptions, evidence & data lineage per recommendation",
            "psychology", "explain"),
        new("Decision Intelligence application",
            "This experience — cockpit, modules, decision log & governance",
            "insights", "app"),
    ];

    // (metric, source, basis). The confirmed/demo state is DERIVED from the basis below, not stored a
    // second time — so a metric can never disagree with the provenance printed beside it.
    private static readonly (string Metric, string Source, string Basis)[] MetricDefinitions =
    [
        ("Recommended order mix", "Fusion Order Management", "Sales history + inventory snapshot"),
        ("Procurement range", "Fusion Procurement", "Synthetic supplier & PO fixture"),
        ("Inventory risk & aging", "Fusion Inventory Management", "Inventory workbook"),
        ("Sales forecast", "Fusion Order Management", "Sales history workbook"),
        ("Configuration demand", "Fusion Product Management", "Sales history workbook"),
        ("Service-intensity index", "Fusion Service", "Synthetic service fixture"),
        ("Spare-parts forecast", "Fusion Inventory / Service", "Synthetic parts fixture"),
        ("Executive priority score", "Fusion Financials (exposure)", "Derived across modules"),
    ];

    public static readonly IReadOnlyList<LineageMetricDto> Metrics =
        [.. MetricDefinitions.Select(m => new LineageMetricDto(m.Metric, m.Source, m.Basis, StateOf(m.Basis)))];

    /// <summary>
    /// A metric is <c>demo</c> exactly when its basis is a synthetic fixture. Deriving the flag from the
    /// basis single-sources the confirmed/demo state, so it cannot drift from the provenance it labels.
    /// This yields the platform's synthetic-demo set (UC4 Procurement, UC6 Service-intensity, UC7 Spare
    /// parts) — the same three metrics the v3 wireframe tags demo.
    /// </summary>
    public static string StateOf(string basis) =>
        basis.Contains("Synthetic", StringComparison.OrdinalIgnoreCase) ? "demo" : "confirmed";

    public static LineageResponse Build() => new(Pipeline, Metrics);
}
