using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.SpareParts;

/// <summary>Spare-parts demand prediction and stocking recommendations (UC7).</summary>
public sealed class SparePartsModule : IModule
{
    public string Name => "Spare Parts";
    public string RoutePrefix => "spare-parts";
    public string Description => "Spare-parts demand prediction and stocking recommendations (UC7).";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Spare Parts application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("SpareParts_Info");
        info.WithSummary("Spare Parts module information");
    }
}
