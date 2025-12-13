using UnityEngine;

/// <summary>
/// ScriptableObject configuration for TinySea simulation v5.
/// All simulation parameters in one place for easy modification.
/// 
/// v5 CHANGES:
/// - Now uses RunSpeciesList instead of SpeciesDatabase
/// - RunSpeciesList is the runtime list that will actually be used for simulation
/// - SpeciesDatabase is only used for gathering defaults
/// </summary>
[CreateAssetMenu(fileName = "SimulationConfig", menuName = "TinySea/Simulation Config")]
public class SimulationConfig : ScriptableObject
{
    [Header("=== SIMULATION TIMING ===")]
    [Tooltip("Days between biology calculations. 1 = daily (most accurate), 5 = original game behavior")]
    [Range(1, 5)]
    public int BiologyStep = 1;

    [Tooltip("Number of years to simulate")]
    [Range(1, 500)]
    public int MaxYears = 1;

    // ==================== CARRYING CAPACITY (Soft Limit) ====================

    [Header("=== CARRYING CAPACITY (Soft Limit - Tier 1 Only) ===")]
    [Tooltip("Enable carrying capacity to slow Tier 1 population growth.\n\n" +
             "This is a SOFT LIMIT - it reduces birth rate as population approaches the limit.\n" +
             "It does NOT kill creatures, only slows reproduction.\n" +
             "ONLY applies to Tier 1 (prey).\n\n" +
             "Formula: births = rawBirths × (1 - tierPop/capacity)\n" +
             "At 50% capacity → 50% birth rate\n" +
             "At 100% capacity → 0% birth rate")]
    public bool UseCarryingCapacity = true;

    [Tooltip("Maximum sustainable population for Tier 1.\n\n" +
             "Represents the resource limit of the environment.\n" +
             "Recommended: 1000-10000 depending on desired ecosystem size.")]
    [Range(100, 100000)]
    public float CarryingCapacityPerTier = 5000f;

    // ==================== TEMPERATURE SETTINGS ====================

    [Header("=== TEMPERATURE: BASE ===")]
    [Tooltip("Base/mean temperature in °C")]
    public float BaseTemperature = 20f;

    [Header("=== TEMPERATURE: SEASONAL ===")]
    [Tooltip("Amplitude of seasonal variation (summer/winter swing)")]
    public float SeasonalAmplitude = 10f;

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

    // ==================== BATCH SIMULATION ====================

    [Header("=== BATCH SIMULATION ===")]
    [Tooltip("Number of runs per scenario (for batch simulations)")]
    public int RunsPerScenario = 10;

    [Tooltip("Random seed for reproducibility (-1 for random)")]
    public int RandomSeed = 12345;
}