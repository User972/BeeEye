namespace BeeEye.Shared.Security;

/// <summary>
/// The platform's permission catalogue — the single source of truth for what a caller may do.
/// <para>
/// Permissions are <c>resource.action</c> strings. Authorization is <b>permission-based, never
/// role-based</b>: handlers and policies always test a permission, so re-cutting ADMC's roles changes
/// only <see cref="RolePermissions"/> and touches no endpoint. See
/// <c>docs/adr/0008-authentication-and-authorization.md</c> and
/// <c>docs/architecture/security-threat-model.md</c> §3.
/// </para>
/// </summary>
public static class Permissions
{
    // ---- Executive -------------------------------------------------------
    public const string ExecutiveCockpitView = "executive-cockpit.view";
    public const string ReportExport = "report.export";

    // ---- Forecasting (UC2) -----------------------------------------------
    public const string ForecastView = "forecast.view";
    public const string ForecastRun = "forecast.run";
    public const string ForecastManage = "forecast.manage";
    public const string ForecastApprove = "forecast.approve";

    // ---- Inventory (UC5) -------------------------------------------------
    public const string InventoryRiskView = "inventory-risk.view";
    public const string InventoryRiskConfigure = "inventory-risk.configure";

    // ---- Recommendations & decisions (UC1, UC8) --------------------------
    public const string RecommendationReview = "recommendation.review";

    /// <summary>
    /// Run the ruleset and persist the resulting frozen recommendation records. The authoring side of
    /// the author/approve pair — deliberately not held by whoever approves.
    /// </summary>
    public const string RecommendationGenerate = "recommendation.generate";

    /// <summary>
    /// Approve, reject or modify a recommended action. Deliberately separate from
    /// <see cref="RecommendationReview"/> so no single role can both author and approve — the
    /// segregation-of-duties requirement in ADR 0006.
    /// </summary>
    public const string RecommendationApprove = "recommendation.approve";

    /// <summary>
    /// Record the realised outcome of an implemented decision (ADR 0006 §4's <c>ActionOutcome</c>).
    /// <para>
    /// Deliberately <b>not</b> part of the author/approve pair. Measuring what actually happened is
    /// observation, not approval — the person who decided may legitimately be the one who reports the
    /// result, and forcing a third party would simply mean outcomes never get recorded.
    /// </para>
    /// </summary>
    public const string DecisionOutcomeRecord = "decision-outcome.record";

    /// <summary>
    /// Record a verdict on an explanation ("Was this useful?", V3-DS-006).
    /// <para>
    /// State-changing, because it writes an attributed, append-only row — so it is enforced in every
    /// environment and can never be declared as a read. Deliberately <b>not</b> part of an
    /// author/approve pair: an opinion about whether an explanation was clear is not an approval of
    /// anything, it retrains nothing, and requiring separation of duties to say "this was confusing"
    /// would simply mean nobody ever says it.
    /// </para>
    /// </summary>
    public const string ExplanationFeedbackSubmit = "explanation-feedback.submit";

    // ---- Procurement (UC4) -----------------------------------------------
    public const string ProcurementView = "procurement.view";
    public const string ProcurementPropose = "procurement.propose";
    public const string ProcurementApprove = "procurement.approve";

    // ---- Sales actuals / configuration demand (UC3) ----------------------
    public const string SalesActualsView = "sales-actuals.view";

    // ---- After-sales & spare parts (UC6, UC7) ----------------------------
    public const string AfterSalesView = "after-sales.view";
    public const string SparePartsView = "spare-parts.view";

    // ---- Models & data ---------------------------------------------------
    public const string ModelView = "model.view";
    public const string ModelPublish = "model.publish";
    public const string DataQualityView = "data-quality.view";
    public const string DataQualityResolve = "data-quality.resolve";

    // ---- Governance & platform -------------------------------------------
    public const string AuditView = "audit.view";
    public const string IntegrationManage = "integration.manage";

    /// <summary>
    /// Read the platform's configuration — the risk weights, bands, horizon and analysis date — for the
    /// read-only Settings transparency screen (V3-GOV-010). Deliberately distinct from
    /// <see cref="SettingsManage"/>: editing configuration is state-changing and enforced in every
    /// environment, while merely seeing it is a read that may be relaxed in Development.
    /// </summary>
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string PlatformAdminister = "platform.administer";

    /// <summary>
    /// Every permission the platform recognises. A policy is registered for each at start-up, and a
    /// test asserts the two sets match — so a typo cannot produce an endpoint that authorises nothing.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ExecutiveCockpitView, ReportExport,
        ForecastView, ForecastRun, ForecastManage, ForecastApprove,
        InventoryRiskView, InventoryRiskConfigure,
        RecommendationReview, RecommendationGenerate, RecommendationApprove, DecisionOutcomeRecord,
        ExplanationFeedbackSubmit,
        ProcurementView, ProcurementPropose, ProcurementApprove,
        SalesActualsView,
        AfterSalesView, SparePartsView,
        ModelView, ModelPublish,
        DataQualityView, DataQualityResolve,
        AuditView, IntegrationManage, SettingsView, SettingsManage, PlatformAdminister,
    };

    /// <summary>
    /// Permissions that authorise a change of state or a binding approval. These are enforced in
    /// <b>every</b> environment: no configuration flag relaxes them (ADR 0008 §2.4).
    /// </summary>
    public static readonly IReadOnlySet<string> StateChanging = new HashSet<string>(StringComparer.Ordinal)
    {
        ForecastRun, ForecastManage, ForecastApprove,
        InventoryRiskConfigure,
        RecommendationGenerate, RecommendationApprove, DecisionOutcomeRecord,
        ExplanationFeedbackSubmit,
        ProcurementPropose, ProcurementApprove,
        ModelPublish,
        DataQualityResolve,
        IntegrationManage, SettingsManage, PlatformAdminister,
    };

    /// <summary>True when the permission authorises a state change or a binding approval.</summary>
    public static bool IsStateChanging(string permission) => StateChanging.Contains(permission);
}
