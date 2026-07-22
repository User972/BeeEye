using BeeEye.Persistence;
using BeeEye.Persistence.SampleData;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Api.Infrastructure;

/// <summary>Registers the operational database and, in development, migrates + seeds it.</summary>
public static class DatabaseStartup
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=beeeye;Username=beeeye;Password=beeeye_local_dev_only";

    public static IServiceCollection AddBeeEyeDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres") ?? DefaultConnection;
        services.AddDbContext<BeeEyeDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<SampleDataImporter>();
        return services;
    }

    /// <summary>
    /// Applies migrations and seeds the sample data (idempotent). Best-effort: a
    /// missing database logs a warning rather than blocking startup, so the API can
    /// still serve health checks while PostgreSQL comes up.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        var enabled = app.Configuration.GetValue("Database:AutoMigrateAndSeed", app.Environment.IsDevelopment());
        if (!enabled)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
            await db.Database.MigrateAsync();
            var importer = scope.ServiceProvider.GetRequiredService<SampleDataImporter>();
            var result = await importer.ImportAsync();
            logger.LogInformation(
                "Sample data ready — sales: {SalesStatus} ({SalesCount}), inventory: {InventoryStatus} ({InventoryCount}).",
                result.Sales.Status, result.Sales.Inserted, result.Inventory.Status, result.Inventory.Inserted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Database initialisation skipped — start PostgreSQL (docker compose up) to enable UC2/UC5 data.");
        }
    }
}
