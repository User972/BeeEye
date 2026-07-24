using BeeEye.Persistence;
using BeeEye.Persistence.SyntheticData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class SyntheticGeneratorTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Synthetic_dataset_is_present_and_labelled_synthetic_demo()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var batch = await db.IngestionBatches.AsNoTracking()
            .SingleAsync(b => b.SourceObject == "after-sales-parts");
        Assert.Equal(SyntheticAfterSalesImporter.SourceSystem, batch.SourceSystem);
        Assert.Equal("synthetic-demo", batch.SourceSystem);

        Assert.True(await db.VehicleSales.CountAsync() > 0);
        Assert.True(await db.ServiceEvents.CountAsync() > 0);
        Assert.True(await db.PartUsages.CountAsync() > 0);
        Assert.Equal(PartsCatalog.Parts.Count, await db.Parts.CountAsync());
    }

    [Fact]
    public async Task Vins_are_synthetic_and_unique_with_no_pii()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var total = await db.VehicleSales.CountAsync();
        var distinct = await db.VehicleSales.Select(v => v.Vin).Distinct().CountAsync();
        Assert.Equal(total, distinct); // unique

        // Every VIN is a synthetic surrogate (prefixed SYN) — never a real VIN or personal identifier.
        Assert.False(await db.VehicleSales.AnyAsync(v => !v.Vin.StartsWith("SYN")));
    }

    [Fact]
    public async Task Re_running_the_generator_is_idempotent()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var importer = scope.ServiceProvider.GetRequiredService<SyntheticAfterSalesImporter>();

        var vehiclesBefore = await db.VehicleSales.CountAsync();
        var eventsBefore = await db.ServiceEvents.CountAsync();
        var usagesBefore = await db.PartUsages.CountAsync();
        var batchesBefore = await db.IngestionBatches.CountAsync();

        // Same settings the startup used (density comes from configuration) -> same checksum -> skipped.
        var settings = SyntheticGenerationSettings.Default with { Density = config.GetValue("Synthetic:Density", 1.0) };
        var result = await importer.ImportAsync(settings);

        Assert.Equal("skipped", result.Status);
        Assert.Equal(vehiclesBefore, await db.VehicleSales.CountAsync());
        Assert.Equal(eventsBefore, await db.ServiceEvents.CountAsync());
        Assert.Equal(usagesBefore, await db.PartUsages.CountAsync());
        Assert.Equal(batchesBefore, await db.IngestionBatches.CountAsync()); // no duplicate batch
    }

    [Fact]
    public async Task Supersession_predecessor_usage_precedes_the_successor()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var oldPart = await db.Parts.SingleAsync(p => p.PartNumber == "FLT-OIL-CO-OLD");
        var newPart = await db.Parts.SingleAsync(p => p.PartNumber == "FLT-OIL-CO");

        var oldLast = await db.PartUsages.Where(u => u.PartId == oldPart.Id).MaxAsync(u => (DateOnly?)u.UsageDate);
        var newFirst = await db.PartUsages.Where(u => u.PartId == newPart.Id).MinAsync(u => (DateOnly?)u.UsageDate);

        // The superseded part's demand tails off before the successor takes over.
        Assert.NotNull(oldLast);
        Assert.NotNull(newFirst);
        Assert.True(newFirst!.Value >= oldLast!.Value.AddMonths(-1));
        Assert.Equal(newPart.Id, oldPart.SupersededByPartId);
    }
}
