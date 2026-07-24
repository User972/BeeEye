namespace BeeEye.Persistence.SyntheticData;

/// <summary>A fixed catalogue part (synthetic-demo). Empty <see cref="Models"/> means it fits every model.</summary>
public sealed record CatalogPart(
    string PartNumber,
    string Name,
    string Category,
    decimal UnitCost,
    int LeadTimeDays,
    int CurrentStock,
    int InboundStock,
    IReadOnlyList<string> Models,
    bool IsActive = true,
    string? SupersededByPartNumber = null);

/// <summary>A superseded → successor link with the date the successor takes over.</summary>
public sealed record SupersessionLink(string OldPartNumber, string NewPartNumber, DateOnly EffectiveDate);

/// <summary>A model-year recall campaign applied deterministically to every matching vehicle.</summary>
public sealed record RecallCampaign(string Model, int SaleYear, int MonthsAfterSale, string PartNumber, decimal LaborHours);

/// <summary>
/// The fixed synthetic spare-parts catalogue plus the deterministic service-type → parts consumption
/// rules. Model-specific oil filters and batteries make parts demand differ by model; rare drivetrain /
/// electrical parts and warranty/recall parts are intermittent; a couple of parts have little or no
/// usage on purpose so UC7's insufficient-data path is exercised.
/// </summary>
public static class PartsCatalog
{
    public const string Patrol = "Patrol";
    public const string Corolla = "Corolla";
    public const string HavalH9 = "Haval H9";
    public const string Camry = "Camry";
    public const string Es350 = "ES 350";

    private static readonly string[] All5 = [];         // empty => fits all models
    private static readonly string[] Suv = [Patrol, HavalH9];
    private static readonly string[] Sedans = [Corolla, Camry, Es350];

    /// <summary>The full catalogue. Order is stable so generation is deterministic.</summary>
    public static readonly IReadOnlyList<CatalogPart> Parts =
    [
        // ---- Filters (oil filter is model-specific -> per-model demand) ----
        new("FLT-OIL-PA", "Oil Filter (Patrol)", "Filters", 45.00m, 14, 40, 5, [Patrol]),
        new("FLT-OIL-CO", "Oil Filter (Corolla)", "Filters", 35.00m, 10, 60, 0, [Corolla]),
        new("FLT-OIL-HH", "Oil Filter (Haval H9)", "Filters", 48.00m, 21, 30, 10, [HavalH9]),
        new("FLT-OIL-CA", "Oil Filter (Camry)", "Filters", 38.00m, 12, 50, 0, [Camry]),
        new("FLT-OIL-ES", "Oil Filter (ES 350)", "Filters", 60.00m, 18, 20, 5, [Es350]),
        new("FLT-AIR-SUV", "Air Filter (SUV)", "Filters", 70.00m, 16, 25, 0, Suv),
        new("FLT-AIR-SDN", "Air Filter (Sedan)", "Filters", 55.00m, 14, 45, 0, Sedans),
        new("FLT-CAB", "Cabin Filter", "Filters", 40.00m, 12, 35, 0, All5),
        new("FLT-FUEL", "Fuel Filter", "Filters", 65.00m, 20, 15, 0, All5),

        // ---- Fluids ----
        new("OIL-5W30", "Engine Oil 5W-30 (1L)", "Fluids", 30.00m, 7, 200, 0, [Patrol, HavalH9, Es350]),
        new("OIL-0W20", "Engine Oil 0W-20 (1L)", "Fluids", 32.00m, 7, 220, 0, [Corolla, Camry]),
        new("FLU-COOL", "Coolant (1L)", "Fluids", 25.00m, 10, 120, 0, All5),
        new("FLU-BRAKE", "Brake Fluid (1L)", "Fluids", 22.00m, 10, 90, 0, All5),
        new("FLU-ATF", "Transmission Fluid (1L)", "Fluids", 45.00m, 14, 60, 0, All5),

        // ---- Brakes ----
        new("BRK-PAD-F", "Front Brake Pads", "Brakes", 220.00m, 21, 40, 10, All5),
        new("BRK-PAD-R", "Rear Brake Pads", "Brakes", 180.00m, 21, 30, 0, All5),
        new("BRK-DISC-F", "Front Brake Disc", "Brakes", 380.00m, 28, 18, 0, All5),
        new("BRK-DISC-R", "Rear Brake Disc", "Brakes", 340.00m, 28, 12, 0, All5),

        // ---- Wipers ----
        new("WIP-SET", "Wiper Blade Set", "Wipers", 90.00m, 7, 60, 0, All5),

        // ---- Batteries (size differs by segment) ----
        new("BAT-70AH", "12V Battery 70Ah", "Batteries", 420.00m, 10, 25, 5, Sedans),
        new("BAT-90AH", "12V Battery 90Ah", "Batteries", 520.00m, 12, 15, 5, Suv),

        // ---- Belts & ignition ----
        new("BLT-SERP", "Serpentine Belt", "Belts", 160.00m, 21, 20, 0, All5),
        new("BLT-TIMING", "Timing Belt", "Belts", 480.00m, 35, 8, 0, [HavalH9]),
        new("IGN-PLUG", "Spark Plug Set", "Ignition", 240.00m, 14, 30, 0, All5),

        // ---- Suspension ----
        new("SUS-SHOCK-F", "Front Shock Absorber", "Suspension", 360.00m, 28, 16, 0, All5),
        new("SUS-CTRLARM", "Control Arm", "Suspension", 420.00m, 30, 10, 0, All5),
        new("SUS-BEARING", "Wheel Bearing", "Suspension", 210.00m, 21, 20, 0, All5),

        // ---- Electrical (rare, long-lead) ----
        new("ELE-ALT", "Alternator", "Electrical", 980.00m, 35, 8, 2, All5),
        new("ELE-START", "Starter Motor", "Electrical", 760.00m, 35, 6, 0, All5),
        new("ELE-O2", "Oxygen Sensor", "Electrical", 340.00m, 28, 14, 0, All5),

        // ---- Cooling ----
        new("COOL-RAD", "Radiator", "Cooling", 690.00m, 30, 9, 0, All5),
        new("COOL-PUMP", "Water Pump", "Cooling", 420.00m, 28, 10, 0, All5),
        new("COOL-THERM", "Thermostat", "Cooling", 150.00m, 21, 18, 0, All5),

        // ---- Fuel & drivetrain ----
        new("FUE-PUMP", "Fuel Pump", "Fuel", 620.00m, 30, 8, 0, All5),
        new("DRV-CLUTCH", "Clutch Kit", "Drivetrain", 1450.00m, 42, 4, 0, [Corolla]),
        new("DRV-CVJOINT", "CV Joint", "Drivetrain", 380.00m, 28, 12, 0, Sedans),

        // ---- Warranty / recall specific ----
        new("WAR-ECU", "Engine Control Module", "Electronics", 1850.00m, 45, 5, 2, All5),
        new("RCL-HH-AIRBAG", "Airbag Inflator (recall)", "Safety", 640.00m, 40, 12, 20, [HavalH9]),
        new("RCL-PA-FUEL", "Fuel Line Kit (recall)", "Safety", 210.00m, 30, 15, 15, [Patrol]),

        // ---- Supersession predecessors (inactive; roll onto successors) ----
        new("FLT-OIL-CO-OLD", "Oil Filter (Corolla, superseded)", "Filters", 33.00m, 10, 0, 0, [Corolla], IsActive: false, SupersededByPartNumber: "FLT-OIL-CO"),
        new("BRK-PAD-F-OLD", "Front Brake Pads (superseded)", "Brakes", 200.00m, 21, 0, 0, All5, IsActive: false, SupersededByPartNumber: "BRK-PAD-F"),

        // ---- Deliberately sparse / new parts (exercise the insufficient-data flag) ----
        new("NEW-HH-SENSOR", "Lane Sensor (new)", "Electronics", 780.00m, 45, 6, 0, [HavalH9]),
        new("NIC-ES-TRIM", "Wood Trim Panel (niche)", "Interior", 350.00m, 30, 3, 0, [Es350]),
    ];

    /// <summary>Supersession links (successor takes over from the effective date).</summary>
    public static readonly IReadOnlyList<SupersessionLink> Supersessions =
    [
        new("FLT-OIL-CO-OLD", "FLT-OIL-CO", new DateOnly(2023, 7, 1)),
        new("BRK-PAD-F-OLD", "BRK-PAD-F", new DateOnly(2024, 1, 1)),
    ];

    /// <summary>Recall campaigns applied deterministically to every matching model-year vehicle.</summary>
    public static readonly IReadOnlyList<RecallCampaign> Recalls =
    [
        new(HavalH9, 2023, 12, "RCL-HH-AIRBAG", 1.0m),
        new(Patrol, 2022, 18, "RCL-PA-FUEL", 1.5m),
    ];

    // successor part number -> its predecessor + effective date (for date-based substitution)
    private static readonly Dictionary<string, SupersessionLink> BySuccessor =
        Supersessions.ToDictionary(s => s.NewPartNumber, s => s);

    // model -> repair-part pool (compatible category parts a repair can consume)
    private static readonly string[] RepairPool =
    [
        "BRK-PAD-F", "BRK-PAD-R", "BRK-DISC-F", "BRK-DISC-R", "BAT-70AH", "BAT-90AH", "SUS-SHOCK-F",
        "SUS-BEARING", "SUS-CTRLARM", "ELE-ALT", "ELE-START", "ELE-O2", "COOL-RAD", "COOL-PUMP",
        "COOL-THERM", "FUE-PUMP", "FLT-FUEL", "DRV-CVJOINT", "DRV-CLUTCH", "BLT-SERP", "BLT-TIMING", "IGN-PLUG",
    ];

    private static readonly Dictionary<string, CatalogPart> ByNumber = Parts.ToDictionary(p => p.PartNumber);

    /// <summary>The date the "new" lane sensor entered the catalogue — recent, so its history is short and sparse.</summary>
    private static readonly DateOnly NewSensorIntroduced = new(2026, 2, 1);

    private static bool FitsModel(string partNumber, string model)
        => ByNumber.TryGetValue(partNumber, out var p) && (p.Models.Count == 0 || p.Models.Contains(model));

    private static readonly string[] SuvModels = [Patrol, HavalH9];

    private static string OilFilterFor(string model) => model switch
    {
        Patrol => "FLT-OIL-PA",
        Corolla => "FLT-OIL-CO",
        HavalH9 => "FLT-OIL-HH",
        Camry => "FLT-OIL-CA",
        Es350 => "FLT-OIL-ES",
        _ => "FLT-OIL-CO",
    };

    private static string EngineOilFor(string model) => model is Corolla or Camry ? "OIL-0W20" : "OIL-5W30";

    private static string AirFilterFor(string model) => SuvModels.Contains(model) ? "FLT-AIR-SUV" : "FLT-AIR-SDN";

    /// <summary>
    /// Deterministic parts consumed by a service event. Applies date-based supersession substitution so a
    /// superseded part accrues historical usage that later rolls onto its successor.
    /// </summary>
    public static IEnumerable<(string PartNumber, int Quantity)> Consume(
        string model, string serviceType, int mileageKm, int routineIndex, DateOnly usageDate, DeterministicRandom rng)
    {
        switch (serviceType)
        {
            case "Routine":
                yield return (Substitute(EngineOilFor(model), usageDate), model is Patrol or HavalH9 ? 7 : 5);
                yield return (Substitute(OilFilterFor(model), usageDate), 1);
                if (routineIndex % 2 == 0)
                {
                    yield return (Substitute(AirFilterFor(model), usageDate), 1);
                }

                if (routineIndex % 3 == 0)
                {
                    yield return (Substitute("FLT-CAB", usageDate), 1);
                }

                if (routineIndex % 4 == 0)
                {
                    yield return (Substitute("WIP-SET", usageDate), 1);
                }

                // Intermittent fluid services so common fluids carry genuine (lumpy) demand.
                if (routineIndex % 4 == 1)
                {
                    yield return ("FLU-BRAKE", 1);
                }

                if (routineIndex % 5 == 2)
                {
                    yield return ("FLU-COOL", 2);
                }

                if (mileageKm >= 40000 && routineIndex % 4 == 3)
                {
                    yield return ("FLU-ATF", 3);
                }

                if (mileageKm >= 60000 && routineIndex % 6 == 0)
                {
                    yield return (Substitute("IGN-PLUG", usageDate), 1);
                }

                break;

            case "Repair":
            {
                var pick = ChooseRepairPart(model, rng);
                if (pick is not null)
                {
                    var qty = pick is "BRK-PAD-F" or "BRK-PAD-R" ? 2 : 1;
                    yield return (Substitute(pick, usageDate), qty);
                }

                break;
            }

            case "Warranty":
                // The lane sensor is a newly-introduced Haval H9 part — only fitted from late 2025, so it
                // has a short, sparse history that exercises UC7's low-data / insufficient-data path.
                if (model == HavalH9 && usageDate >= NewSensorIntroduced && rng.NextDouble() < 0.5)
                {
                    yield return ("NEW-HH-SENSOR", 1);
                }
                else
                {
                    yield return (Substitute("WAR-ECU", usageDate), 1);
                }

                break;

            case "Recall":
                var recall = Recalls.FirstOrDefault(r => r.Model == model);
                if (recall is not null)
                {
                    yield return (Substitute(recall.PartNumber, usageDate), 1);
                }

                break;
        }
    }

    private static string? ChooseRepairPart(string model, DeterministicRandom rng)
    {
        // Deterministic scan from a random offset so a compatible part is always found (or none).
        var start = rng.NextInt(0, RepairPool.Length);
        for (var i = 0; i < RepairPool.Length; i++)
        {
            var candidate = RepairPool[(start + i) % RepairPool.Length];
            if (FitsModel(candidate, model))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Substitutes a superseded predecessor part when the usage predates the supersession's effective date.</summary>
    private static string Substitute(string successorPartNumber, DateOnly usageDate)
        => BySuccessor.TryGetValue(successorPartNumber, out var link) && usageDate < link.EffectiveDate
            ? link.OldPartNumber
            : successorPartNumber;
}
