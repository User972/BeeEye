using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.DataQuality;

/// <summary>Data-quality rules, issues and critical-quality gates.</summary>
public sealed class DataQualityModule : IModule
{
    public string Name => "Data Quality";
    public string RoutePrefix => "data-quality";
    public string Description => "Data-quality rules, issues and critical-quality gates.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Data Quality application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("DataQuality_Info");
        info.WithSummary("Data Quality module information");
    }
}
