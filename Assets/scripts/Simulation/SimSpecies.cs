using System;

public enum ThermalVariant { Arctic, Common, Tropical }

/// <summary>
/// Species data class for simulation.
/// Population is stored as FLOAT for calculation precision.
/// This allows fractional accumulation (e.g., 2.196 → 2.411 → 2.647 → 3.191).
/// Species is considered extinct when Population < 1.0.
/// </summary>
[Serializable]
public class SimSpecies
{
    // Identity
    public string Name;
    public ThermalVariant Variant;
    public int Tier;  // 1 = Hexapod (producer), 2 = Sheplik (predator), 3+ = future

    // Current population (FLOAT for precision)
    public float Population;

    // Biological parameters (from SpeciesDatabase)
    public float EatingAmount;          // Prey demand per creature per step (Tier 1 = 0)
    public float ReproductionMultiplier;
    public float DeathThreshold;        // FinalPerf below this = death (default 0.3)
    public float DeathRate;             // Fraction dying per step when below threshold
    public float MinimumDeaths;         // Minimum deaths when dying (default 1)
    public float ReproThreshold;        // FinalPerf required to reproduce (default 0.25)

    // Thermal curve parameters (Kelvin)
    public float OptimalTempK;
    public float ArrhenBreadth;
    public float ArrhenLower;
    public float ArrhenUpper;
    public float LowerBoundK;
    public float UpperBoundK;

    // Runtime values (calculated each biology step)
    public float ThermalPerformance;    // From Arrhenius formula (0-1)
    public float FedRate = 1f;          // Feeding satisfaction (0-1), Tier 1 always 1.0
    public float FinalPerformance;      // ThermalPerf × FedRate

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

        // Clamp to [0, 1]
        return (float)Math.Max(0.0, Math.Min(1.0, perf));
    }

    // ========== FACTORY METHODS (for fallback/testing) ==========

    /// <summary>
    /// Create a Hexapod (Tier 1 producer) with default parameters
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
            MinimumDeaths = 1f,
            ReproThreshold = 0.25f,
            ArrhenBreadth = 5273.15f,
            ArrhenLower = 10273.15f,
            ArrhenUpper = 21273.15f
        };

        // Set temperature ranges based on variant
        switch (variant)
        {
            case ThermalVariant.Arctic:
                species.OptimalTempK = 278.15f;   // 5°C optimal
                species.LowerBoundK = 270.15f;    // -3°C
                species.UpperBoundK = 280.15f;    // 7°C
                break;
            case ThermalVariant.Common:
                species.OptimalTempK = 293.15f;   // 20°C optimal
                species.LowerBoundK = 285.15f;    // 12°C
                species.UpperBoundK = 295.15f;    // 22°C
                break;
            case ThermalVariant.Tropical:
                species.OptimalTempK = 308.65f;   // 35.5°C optimal
                species.LowerBoundK = 300.15f;    // 27°C
                species.UpperBoundK = 310.15f;    // 37°C
                break;
        }

        return species;
    }

    /// <summary>
    /// Create a Sheplik (Tier 2 predator) with default parameters
    /// </summary>
    public static SimSpecies CreateShelpik(ThermalVariant variant, float initialPopulation)
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
            MinimumDeaths = 1f,
            ReproThreshold = 0.25f,
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
                break;
            case ThermalVariant.Common:
                species.OptimalTempK = 293.15f;
                species.LowerBoundK = 285.15f;
                species.UpperBoundK = 295.15f;
                break;
            case ThermalVariant.Tropical:
                species.OptimalTempK = 308.65f;
                species.LowerBoundK = 300.15f;
                species.UpperBoundK = 310.15f;
                break;
        }

        return species;
    }
}
