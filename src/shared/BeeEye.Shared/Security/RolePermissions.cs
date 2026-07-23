namespace BeeEye.Shared.Security;

/// <summary>
/// The platform's role names. Roles arrive as claims from the identity provider and exist only to
/// bundle permissions; nothing in the application branches on a role directly.
/// </summary>
public static class PlatformRoles
{
    public const string Executive = "Executive";
    public const string Analyst = "Analyst";

    /// <summary>IT / Data Steward. Administers the platform; holds no business-approval authority.</summary>
    public const string ItAdmin = "ItAdmin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Executive, Analyst, ItAdmin,
    };
}

/// <summary>
/// The role → permission mapping from <c>docs/architecture/security-threat-model.md</c> §3.2,
/// ratified by <c>docs/adr/0008-authentication-and-authorization.md</c>.
/// <para>
/// This is the <b>only</b> place roles are interpreted. When ADMC re-cuts its roles, this table
/// changes and no endpoint does.
/// </para>
/// <para>
/// <b>Segregation of duties.</b> Approval-bearing permissions are held by Executive alone, while the
/// authoring permissions that produce the things being approved are held by Analyst alone. No role
/// holds both sides of any author/approve pair — <see cref="AuthorApprovePairs"/> names them, and a
/// test asserts the property holds for every role.
/// </para>
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [PlatformRoles.Executive] = new HashSet<string>(StringComparer.Ordinal)
            {
                Permissions.ExecutiveCockpitView,
                Permissions.ReportExport,
                Permissions.ForecastView,
                Permissions.ForecastApprove,
                Permissions.InventoryRiskView,
                Permissions.RecommendationReview,
                Permissions.RecommendationApprove,
                Permissions.DecisionOutcomeRecord,
                Permissions.ProcurementView,
                Permissions.ProcurementApprove,
                Permissions.SalesActualsView,
                Permissions.AfterSalesView,
                Permissions.SparePartsView,
                Permissions.AuditView,
            },

            [PlatformRoles.Analyst] = new HashSet<string>(StringComparer.Ordinal)
            {
                Permissions.ExecutiveCockpitView,
                Permissions.ReportExport,
                Permissions.ForecastView,
                Permissions.ForecastRun,
                Permissions.ForecastManage,
                Permissions.InventoryRiskView,
                Permissions.InventoryRiskConfigure,
                Permissions.RecommendationReview,
                Permissions.RecommendationGenerate,
                // Measuring a realised outcome is observation, not approval, so the Analyst holding
                // it does not breach the author/approve separation asserted below.
                Permissions.DecisionOutcomeRecord,
                Permissions.ProcurementView,
                Permissions.ProcurementPropose,
                Permissions.SalesActualsView,
                Permissions.AfterSalesView,
                Permissions.SparePartsView,
                Permissions.ModelView,
                Permissions.ModelPublish,
                Permissions.DataQualityView,
                Permissions.DataQualityResolve,
            },

            [PlatformRoles.ItAdmin] = new HashSet<string>(StringComparer.Ordinal)
            {
                Permissions.InventoryRiskConfigure,
                Permissions.ModelView,
                Permissions.DataQualityView,
                Permissions.DataQualityResolve,
                Permissions.AuditView,
                Permissions.IntegrationManage,
                Permissions.SettingsManage,
                Permissions.PlatformAdminister,
            },
        };

    /// <summary>
    /// Author/approve pairs that must never be held by the same role. Enforced by test, not by
    /// convention, because segregation of duties silently decays otherwise.
    /// </summary>
    public static readonly IReadOnlyList<(string Author, string Approver)> AuthorApprovePairs =
    [
        (Permissions.RecommendationGenerate, Permissions.RecommendationApprove),
        (Permissions.ProcurementPropose, Permissions.ProcurementApprove),
        (Permissions.ForecastManage, Permissions.ForecastApprove),
    ];

    /// <summary>The permissions granted by a single role. Unknown roles grant nothing.</summary>
    public static IReadOnlySet<string> ForRole(string role) =>
        Map.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The union of permissions granted by a set of roles. Unknown roles are ignored rather than
    /// rejected, so an unrecognised group claim from the identity provider grants nothing instead of
    /// failing the whole request.
    /// </summary>
    public static IReadOnlySet<string> ForRoles(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            granted.UnionWith(ForRole(role));
        }

        return granted;
    }

    /// <summary>True when any of the given roles grants the permission.</summary>
    public static bool Grants(IEnumerable<string> roles, string permission) =>
        ForRoles(roles).Contains(permission);
}
