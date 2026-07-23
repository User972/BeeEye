using BeeEye.Shared.Security;
using Xunit;

namespace BeeEye.UnitTests.Security;

/// <summary>
/// Tests for the permission catalogue and role → permission mapping (ADR 0008,
/// <c>docs/architecture/security-threat-model.md</c> §3). These are pure and framework-free, so the
/// authorization model is exhaustively testable without a host.
/// </summary>
public sealed class PermissionModelTests
{
    // ---------------------------------------------------------------- catalogue

    [Fact]
    public void All_contains_every_declared_permission_constant()
    {
        // Reflection over the constants, so adding a permission without registering it in All fails
        // here rather than silently producing an endpoint that authorises nothing.
        var declared = typeof(Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(declared);
        var missing = declared.Where(p => !Permissions.All.Contains(p)).ToList();
        Assert.True(missing.Count == 0, $"Not registered in Permissions.All: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_permission_uses_the_resource_action_shape()
    {
        foreach (var permission in Permissions.All)
        {
            var parts = permission.Split('.');
            Assert.True(parts.Length == 2, $"'{permission}' must be resource.action");
            Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p)));
            Assert.Equal(permission.ToLowerInvariant(), permission);
        }
    }

    [Fact]
    public void StateChanging_is_a_strict_subset_of_All()
    {
        Assert.ProperSubset(Permissions.All.ToHashSet(StringComparer.Ordinal), Permissions.StateChanging.ToHashSet(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(Permissions.RecommendationApprove)]
    [InlineData(Permissions.ProcurementApprove)]
    [InlineData(Permissions.ProcurementPropose)]
    [InlineData(Permissions.SettingsManage)]
    [InlineData(Permissions.PlatformAdminister)]
    [InlineData(Permissions.DataQualityResolve)]
    [InlineData(Permissions.ModelPublish)]
    [InlineData(Permissions.IntegrationManage)]
    [InlineData(Permissions.ForecastApprove)]
    [InlineData(Permissions.InventoryRiskConfigure)]
    public void Permissions_that_change_state_or_approve_are_marked_state_changing(string permission)
    {
        Assert.True(
            Permissions.IsStateChanging(permission),
            $"'{permission}' authorises a change or an approval and must be enforced in every environment");
    }

    [Theory]
    [InlineData(Permissions.ExecutiveCockpitView)]
    [InlineData(Permissions.ForecastView)]
    [InlineData(Permissions.InventoryRiskView)]
    [InlineData(Permissions.RecommendationReview)]
    [InlineData(Permissions.ProcurementView)]
    [InlineData(Permissions.SalesActualsView)]
    [InlineData(Permissions.AfterSalesView)]
    [InlineData(Permissions.SparePartsView)]
    [InlineData(Permissions.AuditView)]
    [InlineData(Permissions.ModelView)]
    [InlineData(Permissions.DataQualityView)]
    public void View_permissions_are_not_state_changing(string permission)
    {
        Assert.False(Permissions.IsStateChanging(permission));
    }

    [Fact]
    public void No_view_permission_is_marked_state_changing()
    {
        var wrong = Permissions.StateChanging.Where(p => p.EndsWith(".view", StringComparison.Ordinal)).ToList();
        Assert.True(wrong.Count == 0, $"View permissions must not be state-changing: {string.Join(", ", wrong)}");
    }

    // ---------------------------------------------------------------- roles

    [Fact]
    public void Every_role_grants_only_known_permissions()
    {
        foreach (var role in PlatformRoles.All)
        {
            var unknown = RolePermissions.ForRole(role).Where(p => !Permissions.All.Contains(p)).ToList();
            Assert.True(unknown.Count == 0, $"{role} grants unknown permissions: {string.Join(", ", unknown)}");
        }
    }

    [Fact]
    public void Every_role_grants_at_least_one_permission()
    {
        foreach (var role in PlatformRoles.All)
        {
            Assert.NotEmpty(RolePermissions.ForRole(role));
        }
    }

    [Fact]
    public void Every_permission_is_reachable_by_at_least_one_role()
    {
        // An unreachable permission is a dead endpoint: nobody could ever call it.
        var reachable = RolePermissions.ForRoles(PlatformRoles.All);
        var orphaned = Permissions.All.Where(p => !reachable.Contains(p)).ToList();
        Assert.True(orphaned.Count == 0, $"No role grants: {string.Join(", ", orphaned)}");
    }

    [Fact]
    public void An_unknown_role_grants_nothing()
    {
        Assert.Empty(RolePermissions.ForRole("Wizard"));
        Assert.Empty(RolePermissions.ForRole(string.Empty));
    }

    [Fact]
    public void Unknown_roles_are_ignored_rather_than_failing_the_whole_request()
    {
        var granted = RolePermissions.ForRoles([PlatformRoles.Executive, "GroupFromEntraWeDoNotMap"]);

        Assert.Contains(Permissions.ExecutiveCockpitView, granted);
        Assert.DoesNotContain(Permissions.PlatformAdminister, granted);
    }

    [Fact]
    public void ForRoles_unions_permissions_across_roles()
    {
        var granted = RolePermissions.ForRoles([PlatformRoles.Executive, PlatformRoles.ItAdmin]);

        Assert.Contains(Permissions.RecommendationApprove, granted);  // Executive
        Assert.Contains(Permissions.PlatformAdminister, granted);     // IT/Admin
    }

    [Fact]
    public void ForRoles_of_an_empty_set_grants_nothing()
    {
        Assert.Empty(RolePermissions.ForRoles([]));
    }

    [Fact]
    public void ForRoles_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => RolePermissions.ForRoles(null!));
    }

    // ---------------------------------------------------------------- segregation of duties

    [Fact]
    public void No_role_holds_both_sides_of_an_author_approve_pair()
    {
        foreach (var role in PlatformRoles.All)
        {
            var permissions = RolePermissions.ForRole(role);
            foreach (var (author, approver) in RolePermissions.AuthorApprovePairs)
            {
                Assert.False(
                    permissions.Contains(author) && permissions.Contains(approver),
                    $"{role} can both author ({author}) and approve ({approver}) — segregation of duties is broken");
            }
        }
    }

    [Fact]
    public void Approval_permissions_belong_to_the_executive_alone()
    {
        Assert.True(RolePermissions.Grants([PlatformRoles.Executive], Permissions.RecommendationApprove));
        Assert.False(RolePermissions.Grants([PlatformRoles.Analyst], Permissions.RecommendationApprove));
        Assert.False(RolePermissions.Grants([PlatformRoles.ItAdmin], Permissions.RecommendationApprove));

        Assert.True(RolePermissions.Grants([PlatformRoles.Executive], Permissions.ProcurementApprove));
        Assert.False(RolePermissions.Grants([PlatformRoles.Analyst], Permissions.ProcurementApprove));
    }

    [Fact]
    public void Authoring_permissions_belong_to_the_analyst_alone()
    {
        Assert.True(RolePermissions.Grants([PlatformRoles.Analyst], Permissions.ProcurementPropose));
        Assert.False(RolePermissions.Grants([PlatformRoles.Executive], Permissions.ProcurementPropose));
    }

    [Fact]
    public void Platform_administration_belongs_to_it_admin_alone()
    {
        Assert.True(RolePermissions.Grants([PlatformRoles.ItAdmin], Permissions.PlatformAdminister));
        Assert.False(RolePermissions.Grants([PlatformRoles.Executive], Permissions.PlatformAdminister));
        Assert.False(RolePermissions.Grants([PlatformRoles.Analyst], Permissions.PlatformAdminister));
    }

    [Fact]
    public void The_it_admin_holds_no_business_approval_authority()
    {
        var permissions = RolePermissions.ForRole(PlatformRoles.ItAdmin);

        Assert.DoesNotContain(Permissions.RecommendationApprove, permissions);
        Assert.DoesNotContain(Permissions.ProcurementApprove, permissions);
        Assert.DoesNotContain(Permissions.ForecastApprove, permissions);
    }

    // ---------------------------------------------------------------- mapping fidelity

    [Theory]
    // Executive
    [InlineData(PlatformRoles.Executive, Permissions.ExecutiveCockpitView, true)]
    [InlineData(PlatformRoles.Executive, Permissions.ForecastView, true)]
    [InlineData(PlatformRoles.Executive, Permissions.ForecastApprove, true)]
    [InlineData(PlatformRoles.Executive, Permissions.InventoryRiskView, true)]
    [InlineData(PlatformRoles.Executive, Permissions.AuditView, true)]
    [InlineData(PlatformRoles.Executive, Permissions.ReportExport, true)]
    [InlineData(PlatformRoles.Executive, Permissions.ForecastRun, false)]
    [InlineData(PlatformRoles.Executive, Permissions.InventoryRiskConfigure, false)]
    [InlineData(PlatformRoles.Executive, Permissions.ModelView, false)]
    [InlineData(PlatformRoles.Executive, Permissions.SettingsManage, false)]
    // Analyst
    [InlineData(PlatformRoles.Analyst, Permissions.ForecastRun, true)]
    [InlineData(PlatformRoles.Analyst, Permissions.ForecastManage, true)]
    [InlineData(PlatformRoles.Analyst, Permissions.InventoryRiskConfigure, true)]
    [InlineData(PlatformRoles.Analyst, Permissions.ProcurementPropose, true)]
    [InlineData(PlatformRoles.Analyst, Permissions.ModelPublish, true)]
    [InlineData(PlatformRoles.Analyst, Permissions.DataQualityResolve, true)]
    [InlineData(PlatformRoles.Analyst, Permissions.ForecastApprove, false)]
    [InlineData(PlatformRoles.Analyst, Permissions.AuditView, false)]
    [InlineData(PlatformRoles.Analyst, Permissions.SettingsManage, false)]
    // IT / Admin
    [InlineData(PlatformRoles.ItAdmin, Permissions.SettingsManage, true)]
    [InlineData(PlatformRoles.ItAdmin, Permissions.IntegrationManage, true)]
    [InlineData(PlatformRoles.ItAdmin, Permissions.AuditView, true)]
    [InlineData(PlatformRoles.ItAdmin, Permissions.DataQualityView, true)]
    [InlineData(PlatformRoles.ItAdmin, Permissions.ExecutiveCockpitView, false)]
    [InlineData(PlatformRoles.ItAdmin, Permissions.ForecastView, false)]
    [InlineData(PlatformRoles.ItAdmin, Permissions.ProcurementPropose, false)]
    public void Role_mapping_matches_the_threat_model(string role, string permission, bool granted)
    {
        Assert.Equal(granted, RolePermissions.Grants([role], permission));
    }
}
