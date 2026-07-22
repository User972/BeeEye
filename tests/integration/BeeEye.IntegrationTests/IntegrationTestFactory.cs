using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// Boots the full API against a real, disposable PostgreSQL container (Testcontainers)
/// — not an in-memory substitute — and lets the app's startup migrate + seed it.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Set BEEEYE_TEST_POSTGRES to run against an already-running PostgreSQL (e.g. where Docker image
    // pulls are unavailable); otherwise a disposable Testcontainers postgres:16 is used, as in CI.
    private readonly string? _external = Environment.GetEnvironmentVariable("BEEEYE_TEST_POSTGRES");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("beeeye_test")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _external ?? _postgres.GetConnectionString());
        builder.UseSetting("Database:AutoMigrateAndSeed", "true");
        // Keep the synthetic after-sales/parts seed small so integration runs stay fast.
        builder.UseSetting("Synthetic:Density", "0.2");
    }

    public async Task InitializeAsync()
    {
        if (_external is null)
        {
            await _postgres.StartAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_external is null)
        {
            await _postgres.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationTestFactory>
{
    public const string Name = "integration";
}
