namespace BeeEye.Persistence.SampleData;

/// <summary>JSON shape of a sales-history row (snake_case via the serializer policy).</summary>
internal sealed class SalesRecordDto
{
    public string SaleDate { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Revenue { get; set; }
    public string Currency { get; set; } = "SAR";
    public string Colour { get; set; } = string.Empty;
    public string DateOfManufacture { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Interior { get; set; } = string.Empty;
    public string DiscountApplied { get; set; } = "No";
    public int DiscountPct { get; set; }
    public string IsRamadan { get; set; } = "False";
}

/// <summary>JSON shape of an inventory-stock row.</summary>
internal sealed class InventoryRecordDto
{
    public string StockId { get; set; } = string.Empty;
    public string ChassisNo { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DateOfPurchase { get; set; } = string.Empty;
    public string DateOfManufacture { get; set; } = string.Empty;
    public string? ServiceDate { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal HoldingCostPerDay { get; set; }
    public string Currency { get; set; } = "SAR";
    public string Brand { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Interior { get; set; } = string.Empty;
}

/// <summary>Per-object outcome of a sample-data import.</summary>
public sealed record ImportObjectResult(string Object, string Status, int Inserted, int Total, string Checksum);

/// <summary>Outcome of importing both sample objects.</summary>
public sealed record ImportResult(ImportObjectResult Sales, ImportObjectResult Inventory);
