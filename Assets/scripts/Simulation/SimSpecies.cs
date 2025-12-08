using System;

public enum ThermalVariant { Arctic, Common, Tropical }

/// <summary>
/// Simple data class for a species.
/// Holds all parameters and current population.
/// </summary>
[Serializable]
public class SimSpecies
{
    // Identity
    public string Name;
    public ThermalVariant Variant;
    public int Tier;  // 1 = prey (Hexapod), 2 = predator (Shelpik)

    // Current population (changes during simulation)
    public float Population;

    // Biological parameters
    public float EatingAmount;          // How much this species eats (Tier 1 = 0)
    public float ReproductionMultiplier;
    public float DeathThreshold;        // Below this performance = death
    public float DeathRate;
    public float MinimumDeaths;
    public float ReproThreshold;        // Above this performance = reproduction

    // Thermal curve parameters (Kelvin)
    public float OptimalTempK;
    public float ArrhenBreadth;
    public float ArrhenLower;
    public float ArrhenUpper;
    public float LowerBoundK;
    public float UpperBoundK;

    // Runtime values (calculated each step)
    public float ThermalPerformance;
    public float FedRate = 1f;
    public float FinalPerformance;

    public string FullName => $"{Name}_{Variant}";

    /// <summary>
    /// Calculate thermal performance using Arrhenius formula
    /// </summary>
    public float CalculatePerformance(float temperatureCelsius)
    {
        float T = temperatureCelsius + 273.15f;
        float OT = OptimalTempK;
        float B = ArrhenBreadth;
        float L = ArrhenLower;
        float U = ArrhenUpper;
        float LB = LowerBoundK;
        float UB = UpperBoundK;

        double numerator = Math.Exp(B / OT - B / T) *
                          (1.0 + Math.Exp(L / OT - L / LB) + Math.Exp(U / UB - U / OT));
        double denominator = 1.0 + Math.Exp(L / T - L / LB) + Math.Exp(U / UB - U / T);

        double perf = numerator / denominator;
        return (float)Math.Max(0.0, Math.Min(1.0, perf));
    }

    // ========== FACTORY METHODS ==========

    public static SimSpecies CreateHexapod(ThermalVariant variant, float initialPopulation)
    {
        var species = new SimSpecies
        {
            Name = "Hexapod",
            Variant = variant,
            Tier = 1,
            Population = initialPopulation,
            EatingAmount = 0f,  // Tier 1 doesn't eat
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

    public static SimSpecies CreateShelpik(ThermalVariant variant, float initialPopulation)
    {
        var species = new SimSpecies
        {
            Name = "Shelpik",
            Variant = variant,
            Tier = 2,
            Population = initialPopulation,
            EatingAmount = 1.5f,  // Tier 2 eats Tier 1
            ReproductionMultiplier = 0.1f,
            DeathThreshold = 0.3f,
            DeathRate = 0.3f,
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
