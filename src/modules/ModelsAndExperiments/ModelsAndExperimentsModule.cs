using BeeEye.Modules.ModelsAndExperiments.Application;
using BeeEye.Modules.ModelsAndExperiments.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.ModelsAndExperiments;

/// <summary>Model registry, versions, experiments and publication lifecycle — surfaces the Lineage screen (V3-GOV-009).</summary>
public sealed class ModelsAndExperimentsModule : IModule
{
    public string Name => "Models and Experiments";
    public string RoutePrefix => "models";
    public string Description => "Model registry, versions, experiments and publication lifecycle.";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // The lineage catalogue is static declarative data — no services to register.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name).RequireReadPermission(Permissions.ModelView);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, Status));
        info.WithName("ModelsAndExperiments_Info");
        info.WithSummary("Models and Experiments module information");

        group.MapGet("/lineage", () => Results.Ok(LineageCatalog.Build()))
            .WithName("ModelsAndExperiments_Lineage")
            .WithSummary("Lineage: source-to-decision pipeline and per-metric provenance (V3-GOV-009)")
            .Produces<LineageResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
