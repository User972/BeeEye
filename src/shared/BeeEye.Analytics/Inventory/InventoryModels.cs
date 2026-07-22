namespace BeeEye.Analytics.Inventory;

/// <summary>A physical inventory unit — analytics input for UC5.</summary>
public sealed record InventoryUnit(
    string StockId,
    string ChassisNo,
    string Brand,
    string Model,
    string Variant,
    string Colour,
    string Interior,
    string Type,
    string Location,
    DateOnly DateOfPurchase,
    DateOnly DateOfManufacture,
    DateOnly? ServiceDate,
    decimal PurchasePrice,
    decimal HoldingCostPerDay,
    int LeadTimeDays);

/// <summary>Risk-factor weights (sum need not be 100 — the score is renormalised).</summary>
public sealed record RiskWeights(double Cover = 30, double Aging = 25, double Demand = 20, double Holding = 15, double Lead = 10);

/// <summary>Configurable risk-model settings. Defaults mirror the wireframe POC.</summary>
public sealed record RiskSettings
{
    public DateOnly AnalysisDate { get; init; } = new(2026, 6, 30);
    public int[] AgingBands { get; init; } = [30, 60, 90, 120];
    public int[] RiskBands { get; init; } = [34, 59, 79];
    public int TrailingMonths { get; init; } = 3;
    public double CoverMax { get; init; } = 6;
    public RiskWeights Weights { get; init; } = new();

    public static RiskSettings Default => new();
}

/// <summary>One additive contribution to the risk score, with a plain-language detail.</summary>
public sealed record RiskFactor(string Key, string Label, double Points, string Detail);

/// <summary>A decision-support recommendation for a unit — never an automated action.</summary>
public sealed record InventoryRecommendation(
    string Action,
    string Confidence,
    string Why,
    IReadOnlyList<string> Evidence,
    string Outcome,
    IReadOnlyList<string> Assumptions,
    string? Destination = null,
    int? DiscountPct = null);

/// <summary>Per-unit computed risk, its explainable breakdown and recommendation.</summary>
public sealed record InventoryUnitRisk(
    string StockId,
    string ChassisNo,
    string Brand,
    string Model,
    string Variant,
    string Colour,
    string Interior,
    string Type,
    string Location,
    DateOnly DateOfPurchase,
    DateOnly DateOfManufacture,
    DateOnly? ServiceDate,
    decimal PurchasePrice,
    decimal HoldingCostPerDay,
    int LeadTimeDays,
    int InventoryAgeDays,
    int ManufacturingAgeDays,
    decimal AccumulatedHoldingCost,
    string AgingBand,
    string ManufacturingBand,
    double Velocity,
    string DemandBasis,
    string DemandConfidence,
    string DemandDetail,
    double StockCover,
    int GroupStock,
    string TrendDirection,
    double TrendChangePct,
    int RiskScore,
    string RiskBand,
    IReadOnlyList<RiskFactor> Factors,
    InventoryRecommendation Recommendation);

/// <summary>Discount-responsiveness of a model-variant, for the recommendation engine.</summary>
public sealed record DiscountResponse(bool Responsive, int Suggest, string Range, double DiscountedAvg, double NonDiscountedAvg);
