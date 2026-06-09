using UnityEngine;

/// <summary>
/// ScriptableObject configuration for the TinySea simulation.
/// All simulation parameters in one place for easy modification.
///
/// v6 CHANGES:
/// - Replaced MaxYears with DaysPerScenario (direct day control)
/// - Clarified NumberOfScenarios (how many times to run the same scenario)
///
/// v10 CHANGES:
/// - Carrying capacity is now a shared food/resource pool (drives Tier 1 FedRate),
///   not a soft cap on births. Tooltips on UseCarryingCapacity / CarryingCapacityTier1
///   updated to reflect the new semantic.
/// - IsValid emits a Debug.LogWarning (non-fatal) when initial Tier 1 pop exceeds
///   the cap — the simulation handles it, but unintentional over-seeding is a
///   common mistake worth flagging.
///
/// v11.1 CHANGES:
/// - UseCarryingCapacity field removed. Carrying capacity is always on.
///   CarryingCapacityTier1 must be > 0 (validated in IsValid).
/// </summary>
[CreateAssetMenu(fileName = "SimulationConfig", menuName = "TinySea/Simulation Config")]
public class SimulationConfig : ScriptableObject
{
    [Header("=== SIMULATION TIMING ===")]
    [Tooltip("Days between biology calculations. 1 = daily (most accurate), 5 = original game behavior")]
    [Range(1, 5)]
    public int BiologyStep = 1;

    [Tooltip("Number of days per scenario.\n\n" +
             "Examples:\n" +
             "- 35 days for quick tests\n" +
             "- 365 days for 1 year\n" +
             "- 3650 days for 10 years")]
    [Range(1, 182500)]  // Max ~500 years
    public int DaysPerScenario = 365;

    [Tooltip("Number of times to run the scenario.\n\n" +
             "Since the simulation has randomness (temperature variation, hunting efficiency, etc.),\n" +
             "running multiple scenarios allows for statistical analysis.\n\n" +
             "Each scenario runs for DaysPerScenario days with a different random seed.")]
    [Range(1, 100)]
    public int NumberOfScenarios = 5;

    // ==================== CARRYING CAPACITY (Shared Resource Pool, v10) ====================

    [Header("=== CARRYING CAPACITY (Shared Resource Pool — Tier 1 Only) ===")]
    [Tooltip("Tier 1 shared resource pool capacity.\n\n" +
             "Represents the environmental food/resource pool that all Tier 1 species draw from.\n" +
             "Feeds the FedRate calculation in Step 2: food_density = max(0, 1 − tier1Pop/capacity).\n\n" +
             "Always ON as of v11.1 — Tier 1 species without a resource ceiling grow without\n" +
             "bound, which is biologically meaningless. The previous UseCarryingCapacity toggle\n" +
             "was removed.\n\n" +
             "NOTE: equilibrium populations under v10 may oscillate around 80–95% of this value\n" +
             "(logistic-overshoot dynamics) rather than sitting smoothly at it.\n" +
             "Recommended: 1000–10000 depending on desired ecosystem size.")]
    [Range(100, 100000)]
    public float CarryingCapacityTier1 = 5000f;

    // ==================== CONDITION (HEALTH) SYSTEM ====================

    [Header("=== CONDITION (HEALTH) SYSTEM ===")]
    [Tooltip("How fast Condition drains toward poor performance.\n" +
             "0.15 = ~8 days from full health to death threshold at suboptimal temps.\n" +
             "Drain accelerates up to 2x near lethal limits.")]
    [Range(0.01f, 1.0f)]
    public float ConditionDrainRate = 0.15f;

    [Tooltip("How fast Condition recovers toward good performance.\n" +
             "Slower than drain (asymmetric recovery).\n" +
             "0.10 = ~10 good days to fully recover.")]
    [Range(0.01f, 1.0f)]
    public float ConditionRecoveryRate = 0.10f;

    // ==================== TEMPERATURE SETTINGS ====================

    [Header("=== TEMPERATURE: BASE ===")]
    [Tooltip("Base/mean temperature in °C")]
    public float BaseTemperature = 20f;

    [Header("=== TEMPERATURE: SEASONAL ===")]
    [Tooltip("Amplitude of seasonal variation (summer/winter swing)")]
    public float SeasonalAmplitude = 5f;

    [Header("=== TEMPERATURE: CLIMATE TREND ===")]
    [Tooltip("°C warming per year (climate change)")]
    public float ClimateTrend = 1f;

    [Tooltip("Enable year-to-year variation")]
    public bool InterannualVariation = true;

    [Header("=== TEMPERATURE: INTERANNUAL VARIATION ===")]
    [Tooltip("Magnitude of year-to-year temperature variation")]
    public float VariabilityMagnitude = 2f;

    [Tooltip("Bias towards warmer years (positive skew)")]
    public float WarmingBias = 1.5f;

    [Header("=== TEMPERATURE: DAILY VARIATION ===")]
    [Tooltip("Enable autocorrelated (smooth) daily variation")]
    public bool Autocorrelated = true;

    [Tooltip("Base daily random variation range")]
    public float DailyVariationRange = 5f;

    [Tooltip("How much daily randomness increases per year")]
    public float RandomnessGrowthRate = 0.5f;

    [Header("=== TEMPERATURE: BOUNDS ===")]
    [Tooltip("Minimum possible temperature")]
    public float TemperatureBoundsMin = -5f;

    [Tooltip("Maximum possible temperature")]
    public float TemperatureBoundsMax = 50f;

    // ==================== SPECIES DATA ====================

    [Header("=== SPECIES DATA ===")]
    [Tooltip("Runtime species list for simulation.\n\n" +
             "This is the actual list of species that will be used in the simulation.\n" +
             "Use this instead of SpeciesDatabase for runtime configuration.")]
    public RunSpeciesList RunSpecies;

    // ==================== TIER 2 (PREDATOR) — disabled (Tier-1-only build) ====================
    [Header("=== TIER 2 (PREDATOR) — disabled ===")]
    [Tooltip("Tier-1-only simulator: Tier 2 (predator) is OFF by default. Tier-2 species are " +
             "excluded before the sim and Tier-2 output columns are suppressed; CSV input with " +
             "tier != 0 is rejected at parse. Tier-2 engine code stays intact behind this flag.")]
    public bool Tier2Enabled = false;

    // ==================== RANDOM SEED ====================

    [Header("=== RANDOMNESS ===")]
    [Tooltip("Base random seed for reproducibility.\n" +
             "-1 = use system time (different each run)\n" +
             "Any other value = reproducible results\n\n" +
             "Each scenario will use: BaseSeed + ScenarioIndex")]
    public int RandomSeed = 12345;

    // ==================== VALIDATION ====================

    /// <summary>
    /// Validate configuration values
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        if (DaysPerScenario < 1)
        {
            errorMessage = "Days per scenario must be at least 1";
            return false;
        }

        if (NumberOfScenarios < 1)
        {
            errorMessage = "Number of scenarios must be at least 1";
            return false;
        }

        if (RunSpecies == null || RunSpecies.speciesList == null || RunSpecies.speciesList.Count == 0)
        {
            errorMessage = "No species configured. Please add species to RunSpeciesList.";
            return false;
        }

        // v11.1 / F14: carrying capacity is always on, and the inspector restricts it to
        // [Range(100, 100000)]. Nothing enforced that floor at runtime, so a UI- or
        // asset-supplied value below 100 (or at/below 0) slipped through. Enforce the floor.
        if (CarryingCapacityTier1 < 100f)
        {
            errorMessage = "CarryingCapacityTier1 must be at least 100 (carrying capacity is always on).";
            return false;
        }

        // F14: temperature bounds must be ordered. Nothing checked min < max, so an inverted
        // pair was silently accepted and produced a meaningless clamp range.
        if (TemperatureBoundsMin >= TemperatureBoundsMax)
        {
            errorMessage = $"TemperatureBoundsMin ({TemperatureBoundsMin}) must be less than TemperatureBoundsMax ({TemperatureBoundsMax}).";
            return false;
        }

        // Warn (don't fail) if initial Tier 1 population exceeds the food-pool cap.
        // Over-cap starts are valid for studying crash dynamics; the Condition system
        // handles graceful decline over ~8-10 days. But it's a common mis-configuration
        // to forget the cap when seeding a high initial population, so log a heads-up.
        int tier1InitialPop = 0;
        foreach (var sp in RunSpecies.speciesList)
        {
            // SpeciesData.tier is 0-based: 0 = Tier 1 prey, 1 = Tier 2 predator.
            if (sp.tier == 0) tier1InitialPop += sp.count;
        }
        if (tier1InitialPop > CarryingCapacityTier1)
        {
            Debug.LogWarning(
                $"[SimulationConfig] Initial Tier 1 population ({tier1InitialPop}) exceeds " +
                $"CarryingCapacityTier1 ({CarryingCapacityTier1:F0}). " +
                "This is a valid scenario (the Condition system will produce a graceful " +
                "decline over ~8-10 days), but if it's unintentional, lower initial " +
                "populations or raise the capacity.");
        }

        errorMessage = null;
        return true;
    }
}
