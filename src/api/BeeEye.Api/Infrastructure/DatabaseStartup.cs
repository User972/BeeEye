using BeeEye.Persistence;
using BeeEye.Persistence.SampleData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BeeEye.Api.Infrastructure;

/// <summary>Registers the operational database and, in development, migrates + seeds it.</summary>
public static class DatabaseStartup
{
    public static IServiceCollection AddBeeEyeDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres") ?? BuildLocalConnectionString(configuration);
        services.AddDbContext<BeeEyeDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<SampleDataImporter>();

        // Readiness reflects actual database connectivity — /health/ready lies otherwise, because
        // InitialiseDatabaseAsync swallows a missing database so the process can still start.
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }

    /// <summary>
    /// Builds the local connection string from the documented <c>POSTGRES_*</c> environment
    /// variables (matching <c>.env.example</c> / <c>docker-compose.yml</c>) when no explicit
    /// <c>ConnectionStrings:Postgres</c> is configured, so customised local credentials take effect.
    /// </summary>
    private static string BuildLocalConnectionString(IConfiguration configuration)
    {
        var host = configuration["POSTGRES_HOST"] ?? "localhost";
        var port = configuration["POSTGRES_PORT"] ?? "5432";
        var database = configuration["POSTGRES_DB"] ?? "beeeye";
        var user = configuration["POSTGRES_USER"] ?? "beeeye";
        var password = configuration["POSTGRES_PASSWORD"] ?? "beeeye_local_dev_only";
        return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
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

/// <summary>Readiness probe: reports Unhealthy when PostgreSQL is unreachable.</summary>
internal sealed class DatabaseHealthCheck(BeeEyeDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("PostgreSQL reachable.")
            : HealthCheckResult.Unhealthy("PostgreSQL unreachable.");
}
