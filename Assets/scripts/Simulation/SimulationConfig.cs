using UnityEngine;

/// <summary>
/// ScriptableObject configuration for TinySea simulation.
/// All simulation parameters in one place for easy modification.
/// 
/// POPULATION CONTROL FEATURES:
/// Without population limits, Tier 1 grows exponentially because:
/// - Reproduction scales with population (more creatures = more births)
/// - Predation only scales with predator count (much smaller)
/// - No resource competition or disease mechanics
/// 
/// Two mechanisms are provided to create realistic population dynamics:
/// 
/// 1. CARRYING CAPACITY (Logistic Growth Model)
///    - Reduces birth rate as population approaches environmental limit
///    - Formula: births = rawBirths × (1 - tierPop/carryingCapacity)
///    - At 50% capacity → 50% birth rate
///    - At 100% capacity → 0% birth rate
///    - Simulates: limited food, limited space, intra-species competition
/// 
/// 2. DENSITY-DEPENDENT MORTALITY
///    - Adds extra deaths when population exceeds comfortable threshold
///    - Applies even to thermally-healthy species
///    - Death rate scales with how far over threshold
///    - Simulates: disease spread, overcrowding stress, resource depletion
/// 
/// Both can be enabled independently or together for maximum realism.
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

    // ==================== POPULATION CONTROL SETTINGS ====================
    // These settings prevent unrealistic infinite population growth.

    [Header("=== CARRYING CAPACITY (Logistic Growth) ===")]
    [Tooltip("Enable carrying capacity to limit population growth.\n\n" +
             "Based on the logistic growth model where birth rate decreases " +
             "as population approaches environmental limits.\n\n" +
             "Simulates: limited food, space, and intra-species competition.\n\n" +
             "Without this, populations grow to infinity.")]
    public bool UseCarryingCapacity = true;

    [Tooltip("Maximum sustainable population PER TIER (all variants combined).\n\n" +
             "• At 0% of capacity: 100% birth rate (no reduction)\n" +
             "• At 50% of capacity: 50% birth rate\n" +
             "• At 100% of capacity: 0% birth rate (no new births)\n\n" +
             "Recommended: 1000-10000 depending on desired ecosystem size.")]
    [Range(100, 100000)]
    public float CarryingCapacityPerTier = 5000f;

    [Header("=== DENSITY-DEPENDENT MORTALITY ===")]
    [Tooltip("Enable extra deaths when population exceeds comfortable threshold.\n\n" +
             "Applies even to thermally-healthy species.\n\n" +
             "Simulates: disease spread, overcrowding stress, resource depletion.\n\n" +
             "This is separate from thermal death - it's purely population-based.")]
    public bool UseDensityDeath = true;

    [Tooltip("Population threshold (per tier) above which density deaths begin.\n\n" +
             "Below this threshold, no density deaths occur.\n" +
             "Above this threshold, deaths increase proportionally.\n\n" +
             "Recommended: 50-80% of CarryingCapacity for smooth transition.")]
    [Range(100, 100000)]
    public float DensityDeathThreshold = 4000f;

    [Tooltip("Maximum daily death rate from overcrowding (0.0 to 1.0).\n\n" +
             "Death rate scales linearly from 0 at threshold to this value.\n" +
             "• At 1× threshold: 0% density death rate\n" +
             "• At 2× threshold: this value (e.g., 10%)\n\n" +
             "Recommended: 0.05-0.15 (5-15% per day at extreme density).")]
    [Range(0f, 0.5f)]
    public float MaxDensityDeathRate = 0.1f;

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

    // ==================== DATA SETTINGS ====================

    [Header("=== SPECIES DATA ===")]
    [Tooltip("Reference to species database ScriptableObject")]
    public SpeciesDatabase Database;

    [Header("=== BATCH SIMULATION ===")]
    [Tooltip("Number of runs per scenario (for batch simulations)")]
    public int RunsPerScenario = 10;

    [Tooltip("Random seed for reproducibility (-1 for random)")]
    public int RandomSeed = 12345;
}
