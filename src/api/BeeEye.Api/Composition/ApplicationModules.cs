using BeeEye.Shared.Modularity;

namespace BeeEye.Api.Composition;

/// <summary>
/// The ordered set of bounded-context modules composed into the API host.
/// This is the single place in the codebase that knows every module — the
/// composition root. Adding a module means adding one line here and one
/// <c>ProjectReference</c> in the host project.
/// </summary>
public static class ApplicationModules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new BeeEye.Modules.Identity.IdentityModule(),
        new BeeEye.Modules.Organisation.OrganisationModule(),
        new BeeEye.Modules.MasterData.MasterDataModule(),
        new BeeEye.Modules.Integration.IntegrationModule(),
        new BeeEye.Modules.DataQuality.DataQualityModule(),
        new BeeEye.Modules.SalesActuals.SalesActualsModule(),
        new BeeEye.Modules.Forecasting.ForecastingModule(),
        new BeeEye.Modules.Inventory.InventoryModule(),
        new BeeEye.Modules.Procurement.ProcurementModule(),
        new BeeEye.Modules.AfterSales.AfterSalesModule(),
        new BeeEye.Modules.SpareParts.SparePartsModule(),
        new BeeEye.Modules.ModelsAndExperiments.ModelsAndExperimentsModule(),
        new BeeEye.Modules.Predictions.PredictionsModule(),
        new BeeEye.Modules.Recommendations.RecommendationsModule(),
        new BeeEye.Modules.DecisionsAndOutcomes.DecisionsAndOutcomesModule(),
        new BeeEye.Modules.ExecutiveInsights.ExecutiveInsightsModule(),
        new BeeEye.Modules.Notifications.NotificationsModule(),
        new BeeEye.Modules.Audit.AuditModule(),
        new BeeEye.Modules.PlatformAdministration.PlatformAdministrationModule(),
    ];
}
