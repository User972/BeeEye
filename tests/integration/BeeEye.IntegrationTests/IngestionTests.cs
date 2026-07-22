using BeeEye.Persistence;
using BeeEye.Persistence.SampleData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class IngestionTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Startup_seed_loads_the_documented_dataset_profile()
    {
        // The factory started the host, which migrated + seeded the container.
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        Assert.Equal(291, await db.InventoryItems.CountAsync());
        Assert.Equal(3120, await db.SalesFacts.CountAsync());
        Assert.Equal(2, await db.IngestionBatches.CountAsync());
    }

    [Fact]
    public async Task Re_importing_the_same_extract_is_idempotent()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        var importer = scope.ServiceProvider.GetRequiredService<SampleDataImporter>();

        var invBefore = await db.InventoryItems.CountAsync();
        var salesBefore = await db.SalesFacts.CountAsync();

        var result = await importer.ImportAsync();

        Assert.Equal("skipped", result.Sales.Status);
        Assert.Equal("skipped", result.Inventory.Status);
        Assert.Equal(invBefore, await db.InventoryItems.CountAsync());
        Assert.Equal(salesBefore, await db.SalesFacts.CountAsync());
        Assert.Equal(2, await db.IngestionBatches.CountAsync()); // no duplicate batch
    }
}
