using System;

public enum ThermalVariant { Arctic, Common, Tropical, Custom }

/// <summary>
/// Species data class for simulation.
/// Population is stored as FLOAT for calculation precision.
/// Species is considered extinct when Population == 0 (after rounding).
/// </summary>
[Serializable]
public class SimSpecies
{
    // ==================== IDENTITY ====================
    public string Name;
    public ThermalVariant Variant;
    public int Tier;  // 1 = Hexapod (prey), 2 = Sheplik (predator)

    // ==================== POPULATION ====================
    public float Population;

    // ==================== BIOLOGICAL PARAMETERS ====================
    public float EatingAmount;              // Prey consumed per creature per step (Tier 1 = 0)
    public float ReproductionMultiplier;    // Birth rate multiplier
    public float DeathThreshold;            // FinalPerf below this triggers thermal death (default 0.3)
    public float DeathRate;                 // Fraction dying when thermal death triggers
    public float ReproThreshold;            // Condition inflection point: above = healthy reproduction ramp, below = struggling but non-zero (default 0.25)

    // ==================== NATURAL MORTALITY ====================
    public float NaturalDeathRate = 0.02f;      // Base natural death rate (all species: 2%)
    public float NaturalDeathVariance = 0.01f;  // Random variance range (±1%)

    // ==================== HUNTING EFFICIENCY (Tier 2 only) ====================
    public float HuntingEfficiency = 0.75f;     // Base hunting success rate (75%)
    public float HuntingVariance = 0.15f;       // Random variance range (±15%)

    // ==================== CONSTANTS ====================
    public const float NO_PREDATOR_PENALTY = 0.85f;           // 15% birth reduction when no predators
    public const float MIN_FINAL_PERF_FOR_NATURAL_DEATH = 0.1f; // Floor to prevent division by zero
    private const float LETHAL_TRANSITION_WIDTH = 2.0f; // Smooth fade width in degrees Celsius

    // ==================== THERMAL CURVE PARAMETERS (Kelvin) ====================
    public float OptimalTempK;
    public float ArrhenBreadth;
    public float ArrhenLower;
    public float ArrhenUpper;
    public float LowerBoundK;
    public float UpperBoundK;

    // ==================== PEAK HEIGHT & LETHAL LIMITS ====================
    public float Pmax = 1.0f;          // Maximum performance at optimal temperature (0-1)
    public float CTminC = -5.0f;       // Critical thermal minimum (Celsius) — below this, performance = 0
    public float CTmaxC = 40.0f;       // Critical thermal maximum (Celsius) — above this, performance = 0
    public float TemperatureDebuff = 0f;  // Per-species temperature offset (shifts experienced temp)

    // ==================== RUNTIME VALUES (calculated each step) ====================
    public float RawThermalPerformance;     // Arrhenius + CTmin/CTmax fade, WITHOUT Pmax
    public float ThermalPerformance;        // RawThermalPerformance × Pmax (used for reproduction)
    public float FedRate = 1f;              // Feeding satisfaction (0-1), Tier 1 always 1.0
    public float RawFinalPerformance;       // RawThermalPerformance × FedRate — used for death checks
    public float FinalPerformance;          // ThermalPerf x FedRate (repro threshold + birth count; Condition NOT involved)
    public float CurrentHuntingSuccess;     // This step's hunting success (for tracking)
    public float Condition = 1.0f;          // Health/energy reserves [0-1], starts at 1.0

    /// <summary>
    /// Full name for display (e.g., "Hexapod_Arctic")
    /// </summary>
    public string FullName => $"{Name}_{Variant}";

    /// <summary>
    /// Calculate thermal performance using Arrhenius formula.
    /// Result is clamped to [0, 1].
    /// </summary>
    public float CalculatePerformance(float temperatureCelsius)
    {
        temperatureCelsius += TemperatureDebuff;

        // Smooth lethal fade (cosine transition over LETHAL_TRANSITION_WIDTH degrees)
        float halfRange = (CTmaxC - CTminC) / 2f;
        float tw = (float)Math.Min(LETHAL_TRANSITION_WIDTH, halfRange);

        float fadeFactor = 1f;
        if (temperatureCelsius <= CTminC)
            fadeFactor = 0f;
        else if (temperatureCelsius < CTminC + tw)
            fadeFactor = 0.5f * (1f + (float)Math.Cos(Math.PI * (CTminC + tw - temperatureCelsius) / tw));

        if (temperatureCelsius >= CTmaxC)
            fadeFactor = 0f;
        else if (temperatureCelsius > CTmaxC - tw)
            fadeFactor *= 0.5f * (1f + (float)Math.Cos(Math.PI * (temperatureCelsius - (CTmaxC - tw)) / tw));

        if (fadeFactor <= 0f) return 0f;

        float T = temperatureCelsius + 273.15f;  // Convert to Kelvin
        float OT = OptimalTempK;
        float B = ArrhenBreadth;
        float L = ArrhenLower;
        float U = ArrhenUpper;
        float LB = LowerBoundK;
        float UB = UpperBoundK;

        // Arrhenius formula
        double numerator = Math.Exp(B / OT - B / T) *
                          (1.0 + Math.Exp(L / OT - L / LB) + Math.Exp(U / UB - U / OT));
        double denominator = 1.0 + Math.Exp(L / T - L / LB) + Math.Exp(U / UB - U / T);

        double perf = numerator / denominator;

        // Clamp to [0, 1] with CTmin/CTmax fade — Pmax is applied externally
        return (float)Math.Max(0.0, Math.Min(1.0, perf)) * fadeFactor;
    }

    // ==================== FACTORY METHODS (for fallback/testing) ====================

    /// <summary>
    /// Create a Hexapod (Tier 1 prey) with default parameters
    /// </summary>
    public static SimSpecies CreateHexapod(ThermalVariant variant, float initialPopulation)
    {
        var species = new SimSpecies
        {
            Name = "Hexapod",
            Variant = variant,
            Tier = 1,
            Population = initialPopulation,
            EatingAmount = 0f,              // Tier 1 doesn't eat
            ReproductionMultiplier = 0.45f,
            DeathThreshold = 0.3f,
            DeathRate = 0.6f,
            ReproThreshold = 0.25f,
            NaturalDeathRate = 0.02f,       // 2% base
            NaturalDeathVariance = 0.01f,   // ±1%
            HuntingEfficiency = 1.0f,       // Ignored for Tier 1
            HuntingVariance = 0f,
            ArrhenBreadth = 5273.15f,
            ArrhenLower = 10273.15f,
            ArrhenUpper = 21273.15f
        };

        // Set temperature ranges and thermal limits based on variant
        switch (variant)
        {
            case ThermalVariant.Arctic:
                species.OptimalTempK = 278.15f;   // 5°C optimal
                species.LowerBoundK = 270.15f;    // -3°C
                species.UpperBoundK = 280.15f;    // 7°C
                species.Pmax = 1.0f;
                species.CTminC = -30f;
                species.CTmaxC = 20f;
                break;
            case ThermalVariant.Common:
                species.OptimalTempK = 293.15f;   // 20°C optimal
                species.LowerBoundK = 285.15f;    // 12°C
                species.UpperBoundK = 295.15f;    // 22°C
                species.Pmax = 0.9f;
                species.CTminC = -5f;
                species.CTmaxC = 40f;
                break;
            case ThermalVariant.Tropical:
                species.OptimalTempK = 308.65f;   // 35.5°C optimal
                species.LowerBoundK = 300.15f;    // 27°C
                species.UpperBoundK = 310.15f;    // 37°C
                species.Pmax = 1.0f;
                species.CTminC = 0f;
                species.CTmaxC = 80f;
                break;
        }

        return species;
    }

    /// <summary>
    /// Create a Sheplik (Tier 2 predator) with default parameters
    /// </summary>
    public static SimSpecies CreateSheplik(ThermalVariant variant, float initialPopulation)
    {
        var species = new SimSpecies
        {
            Name = "Sheplik",
            Variant = variant,
            Tier = 2,
            Population = initialPopulation,
            EatingAmount = 1.5f,            // Tier 2 eats Tier 1
            ReproductionMultiplier = 0.1f,  // 4.5x slower than Tier 1
            DeathThreshold = 0.3f,
            DeathRate = 0.3f,               // Lower death rate than Tier 1
            ReproThreshold = 0.25f,
            NaturalDeathRate = 0.01f,       // 1% base (allometric: larger predators have lower background mortality)
            NaturalDeathVariance = 0.005f,  // ±0.5%
            HuntingEfficiency = 0.75f,      // 75% base success
            HuntingVariance = 0.15f,        // ±15% variance
            ArrhenBreadth = 5273.15f,
            ArrhenLower = 10273.15f,
            ArrhenUpper = 21273.15f
        };

        switch (variant)
        {
            case ThermalVariant.Arctic:
                species.OptimalTempK = 278.15f;
                species.LowerBoundK = 270.15f;
                species.UpperBoundK = 280.15f;
                species.Pmax = 1.0f;
                species.CTminC = -30f;
                species.CTmaxC = 20f;
                break;
            case ThermalVariant.Common:
                species.OptimalTempK = 293.15f;
                species.LowerBoundK = 285.15f;
                species.UpperBoundK = 295.15f;
                species.Pmax = 0.9f;
                species.CTminC = -5f;
                species.CTmaxC = 40f;
                break;
            case ThermalVariant.Tropical:
                species.OptimalTempK = 308.65f;
                species.LowerBoundK = 300.15f;
                species.UpperBoundK = 310.15f;
                species.Pmax = 1.0f;
                species.CTminC = 0f;
                species.CTmaxC = 80f;
                break;
        }

        return species;
    }
}
