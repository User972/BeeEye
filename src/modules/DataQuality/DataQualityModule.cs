using BeeEye.Modules.DataQuality.Application;
using BeeEye.Modules.DataQuality.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using BeeEye.Shared.Security;
using BeeEye.Shared.Time;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.DataQuality;

/// <summary>Data-quality rules, issues and critical-quality gates — surfaces the Data Health screen (V3-GOV-008).</summary>
public sealed class DataQualityModule : IModule
{
    public string Name => "Data Quality";
    public string RoutePrefix => "data-quality";
    public string Description => "Data-quality rules, issues and critical-quality gates.";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DataHealthReadService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name).RequireReadPermission(Permissions.DataQualityView);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, Status));
        info.WithName("DataQuality_Info");
        info.WithSummary("Data Quality module information");

        group.MapGet("/health", async (DataHealthReadService svc, IClock clock, CancellationToken ct) =>
            {
                var health = await svc.ComputeAsync(clock.UtcNow, ct);
                return Results.Ok(health);
            })
            .WithName("DataQuality_Health")
            .WithSummary("Data Health: governed sources, data-quality score and issues (V3-GOV-008)")
            .Produces<DataHealthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
