using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TinySea Ecosystem Simulator v10
///
/// BIOLOGY SEQUENCE (10 steps):
/// 1. Thermal Performance - Arrhenius formula
/// 2. Feeding/Predation - Tier 1 FedRate from shared food-pool density (linear) +
///                        Tier 2 Holling Type II functional response + PREDATION ACCUMULATOR
/// 3. Raw Final Performance - RawThermalPerf x FedRate (Condition drain target)
/// 4. Update Condition - Drain/recover toward RawFinalPerformance; rates scaled by Pmax
///                       (Tier 1 target now varies with food density too)
/// 5. Final Performance - ThermalPerf x FedRate (computed for logging; not a biology input)
/// 6. Thermal Death - INSTANT kill at lethal limits (RawThermalPerf == 0)
/// 7. Condition Death - GRADUATED: severity scales with how far below threshold + survivor fitness boost
/// 8. Reproduction - CONDITION-BASED GRADUATED SCALE x Pmax + BIRTH ACCUMULATOR + Tier 1 penalty
///                   (no more soft-cap-on-births — throttling is via Condition pathway in v10)
///                   (newborns inherit parent group Condition — no fixed constant)
/// 9. Natural Death - FLAT RATE + NATURAL DEATH ACCUMULATOR
/// 10. Population Rounding - All populations become integers
///
/// DEATH TYPES:
/// - Thermal: Instant kill when beyond CTmin/CTmax (RawThermalPerf == 0)
/// - Condition: GRADUATED — severity proportional to (threshold - condition) / threshold, survivors get fitness boost
/// - Natural: Flat 2% rate — old age, disease, accidents (no performance scaling)
/// - Predation: Tier 2 eats Tier 1 (unchanged)
///
/// CONDITION SYSTEM:
/// - Per-species health value [0-1], starts at 1.0
/// - Drains toward RawFinalPerformance (asymmetric: drains faster than recovers)
/// - Drain accelerates up to 2x near lethal temperatures (continuous quadratic)
/// - Pmax scales the rates: drain /= Pmax, recovery *= Pmax
///   (specialists drain slower and recover faster than generalists)
/// - In v10, Tier 1 target depends on BOTH temperature AND food density (via FedRate),
///   so Tier 1 Condition no longer plateaus at 1.0 even at perfect temperature when
///   the shared resource pool is depleted. Tier 2 target depends on temperature AND
///   hunting success (FedRate from Holling II) as before.
/// - Global drain/recovery rates on SimulationConfig
///
/// ACCUMULATORS:
/// - Birth: Fractional births carry over
/// - Predation: Fractional prey deaths carry over
/// - Natural Death: Fractional deaths carry over
/// - Condition Death: Fractional condition deaths carry over
///
/// v6 CHANGES:
/// - Added Condition (health) system with drain/recovery
/// - Split thermal death into instant (lethal) + condition (chronic)
/// - Decoupled natural death from performance (flat rate)
/// - HasCrashed() now checks total population == 0 (not single tier)
///
/// v7 CHANGES:
/// - Replaced dual hunting system (hunting bonus + scarcity multiplier) with
///   single Holling Type II Functional Response (Holling 1959)
/// - Hunting efficiency now scales naturally with prey:predator ratio
/// - No FedRate floor — zero prey = zero hunting efficiency = true starvation
///
/// v8 CHANGES:
/// - Switched reproduction from FinalPerformance-driven to Condition-driven
/// - Reproduction now uses species health (Condition) as the scale factor
/// - No hard cliff: below ReproThreshold gives diminished but non-zero reproduction
/// - Continuous piecewise formula joined at STRUGGLING_REPRO_RATE (0.10)
/// - Ecologically: animals with energy reserves reproduce in all seasons, just less in harsh conditions
/// - Side effect (fixed in v9): Pmax dropped out of the reproduction/survival pathway
///
/// v9 CHANGES:
/// - Wired Pmax back into the Condition pathway AFTER Condition is computed
///   (rather than into Condition's drain target), so Condition semantics and 0-1
///   scale are preserved and ReproThreshold/DeathThreshold need no retuning.
/// - Reproduction: births *= Pmax (specialists convert health to offspring more efficiently)
/// - Condition drain: /= Pmax (specialists resist chronic stress)
/// - Condition recovery: *= Pmax (specialists rebound faster)
/// - Biological framing: Condition = health (species-agnostic),
///   Pmax = peak metabolic capacity (scales reproductive output and stress dynamics).
///
/// v10 CHANGES:
/// - Reframed carrying capacity as a SHARED FOOD/RESOURCE POOL that drives Tier 1
///   FedRate directly. The pool is the same as before (CarryingCapacityPerTier);
///   the change is what it does: it now feeds the FedRate calculation in Step 2
///   instead of multiplying births in Step 8.
///   - Tier 1 FedRate = min(1, HuntingEfficiency × food_density)  [linear, not Holling II]
///     (Plankton-style passive extractors don't have search/handling phases.)
///   - food_density = max(0, 1 - tier1Pop / CarryingCapacityPerTier).
///   - When UseCarryingCapacity is false, food_density = 1.0 (legacy behaviour).
/// - DELETED the soft-cap-on-births block in ApplyReproduction. Tier 1 reproduction
///   now throttles indirectly via Condition: high pop → low food density → low
///   FedRate → low RawFinalPerformance target → Condition drains → reproScale
///   shrinks AND condition deaths fire. Logistic-overshoot dynamics emerge naturally.
/// - PROCESSING-ORDER BUG (Brian Mail 8) ELIMINATED as a side effect of the above:
///   the live-tierPop read inside the per-species reproduction loop is gone.
/// - HuntingEfficiency for Tier 1 is now meaningful — semantically "resource
///   extraction efficiency". Default 1.0 = perfect plankton-style extraction.
/// - NEWBORN_CONDITION constant REMOVED. Newborns inherit the species' current
///   group Condition (parent's Condition already encodes recent provisioning
///   capacity via lagged drain dynamics; multiplying by today's FedRate would
///   double-count). Same logic for Tier 1 and Tier 2.
/// - New CSV columns: FedRateT1, FoodDensityT1. New #config: line: model_version,v10-food-pool.
/// - Pooled FedRate bug for Tier 2 predators (review item A1) is NOT addressed in v10
///   — kept for v11 to keep the validation surface tractable.
/// </summary>
public class EcosystemSimulator
{
    public List<SimSpecies> Species { get; private set; }
    public int BiologyStep { get; set; } = 1;

    // Random number generator
    private System.Random _rng;

    // Track whether tiers were populated at initialization
    private bool _tier1WasPopulated = false;
    private bool _tier2WasPopulated = false;

    // ==================== ACCUMULATORS ====================
    private Dictionary<string, float> _birthAccumulators = new Dictionary<string, float>();
    private Dictionary<string, float> _naturalDeathAccumulators = new Dictionary<string, float>();
    private Dictionary<string, float> _predationAccumulators = new Dictionary<string, float>();
    private Dictionary<string, float> _thermalDeathAccumulators = new Dictionary<string, float>();
    private Dictionary<string, float> _conditionDeathAccumulators = new Dictionary<string, float>();

    // ==================== POPULATION TRACKING ====================
    public float StartPopT1 { get; private set; } = 0f;
    public float StartPopT2 { get; private set; } = 0f;
    public float EndPopT1 { get; private set; } = 0f;
    public float EndPopT2 { get; private set; } = 0f;

    // ==================== DEATH/BIRTH TRACKING ====================
    // Tier 1
    public float LastEatenT1 { get; private set; } = 0f;
    public float LastTempDeathsT1 { get; private set; } = 0f;
    public float LastNaturalDeathsT1 { get; private set; } = 0f;
    public float LastBirthsT1 { get; private set; } = 0f;

    public float LastConditionDeathsT1 { get; private set; } = 0f;

    // Tier 2
    public float LastTempDeathsT2 { get; private set; } = 0f;
    public float LastConditionDeathsT2 { get; private set; } = 0f;
    public float LastNaturalDeathsT2 { get; private set; } = 0f;
    public float LastBirthsT2 { get; private set; } = 0f;
    public float LastFedRateT2 { get; private set; } = 1f;
    public float LastAvgHuntingEfficiency { get; private set; } = 1f;

    // Tier 1 feeding (v10): population-weighted average FedRate and the daily
    // shared-resource food density that drove it. Both written to scenario CSV.
    public float LastFedRateT1 { get; private set; } = 1f;
    public float LastFoodDensityT1 { get; private set; } = 1f;

    // Reproduction scale tracking (graduated reproduction)
    public float LastReproScaleT1 { get; private set; } = 0f;
    public float LastReproScaleT2 { get; private set; } = 0f;

    // Combined
    public float LastTotalDeaths => LastEatenT1 + LastTempDeathsT1 + LastTempDeathsT2 +
                                    LastConditionDeathsT1 + LastConditionDeathsT2 +
                                    LastNaturalDeathsT1 + LastNaturalDeathsT2;
    public float LastTotalBirths => LastBirthsT1 + LastBirthsT2;

    // ==================== ACCUMULATOR TOTALS (for CSV output) ====================
    public float BirthAccumT1 { get; private set; } = 0f;
    public float BirthAccumT2 { get; private set; } = 0f;
    public float NaturalDeathAccumT1 { get; private set; } = 0f;
    public float NaturalDeathAccumT2 { get; private set; } = 0f;
    public float PredationAccumT1 { get; private set; } = 0f;
    public float ConditionDeathAccumT1 { get; private set; } = 0f;
    public float ConditionDeathAccumT2 { get; private set; } = 0f;

    // ==================== AVERAGE CONDITION (for CSV output) ====================
    public float AvgConditionT1 { get; private set; } = 1f;
    public float AvgConditionT2 { get; private set; } = 1f;

    // ==================== CARRYING CAPACITY (Soft Limit - Tier 1 Only) ====================
    public bool UseCarryingCapacity { get; set; } = true;
    public float CarryingCapacityPerTier { get; set; } = 5000f;

    // ==================== CONDITION (HEALTH) SYSTEM ====================
    public float ConditionDrainRate { get; set; } = 0.15f;
    public float ConditionRecoveryRate { get; set; } = 0.10f;

    // ==================== CONSTANTS ====================
    private const float MIN_ALIVE_POP = 1.0f;
    // NEWBORN_CONDITION constant removed in v10: newborns inherit the species'
    // current group Condition rather than entering at a fixed value. See the
    // ApplyReproduction docblock and the comment in the births-application
    // block for rationale.

    // --- Holling Type II Functional Response (Holling 1959) ---
    // "The Components of Predation as Revealed by a Study of Small-Mammal Predation
    //  of the European Pine Sawfly" — Canadian Entomologist 91(5):293-320.
    //
    // Models how predator hunting success scales with prey availability:
    //   efficiency = ratio / (ratio + halfSaturation)
    // where halfSaturation = NORMAL_PREY_RATIO × (1 - baseEff) / baseEff
    //
    // At high prey density, search time is negligible → efficiency approaches 1.0.
    // At NORMAL_PREY_RATIO, efficiency equals the species' base hunting efficiency.
    // At low prey density, search time dominates → efficiency drops toward 0.0.
    //
    // This replaces the previous dual system (hunting bonus + scarcity multiplier)
    // with a single, scientifically-grounded curve. The half-saturation constant is
    // derived from each species' base efficiency, so no arbitrary tuning is needed.
    private const float NORMAL_PREY_RATIO = 20f;            // Prey:predator ratio where base efficiency applies
    private const float MIN_HUNTING_SUCCESS = 0.0f;         // Floor for hunting success (0 = nothing to hunt)
    private const float MAX_HUNTING_SUCCESS = 1.0f;         // Ceiling for hunting success

    // --- Reproduction ---
    private const float MIN_POPULATION_FOR_REPRODUCTION = 2f; // Need at least 2 to reproduce

    // --- Condition-Based Reproduction (v8) ---
    // Below ReproThreshold, reproduction is diminished but non-zero.
    // This constant sets the maximum reproScale when Condition equals ReproThreshold.
    // Above threshold: reproScale ramps from this value to 1.0.
    // Below threshold: reproScale ramps from 0 to this value.
    // The two regions meet at this value, ensuring continuity (no cliff).
    private const float STRUGGLING_REPRO_RATE = 0.10f;        // 10% max reproduction when below threshold

    /// <summary>
    /// Verbose simulation log. Stripped from ALL builds AND the Editor by default.
    /// To enable: add TINYSEA_SIM_LOG to Player Settings > Scripting Define Symbols.
    /// Uses [Conditional] so string interpolation in callers is also removed at compile time,
    /// preventing ~900,000 string allocations per 50-year scenario.
    /// </summary>
    [System.Diagnostics.Conditional("TINYSEA_SIM_LOG")]
    private static void SimLog(string message) => Debug.Log(message);

    public EcosystemSimulator(int seed = -1)
    {
        Species = new List<SimSpecies>();
        _rng = seed < 0 ? new System.Random() : new System.Random(seed);
    }

    /// <summary>
    /// Set random seed for reproducibility
    /// </summary>
    public void SetSeed(int seed)
    {
        _rng = seed < 0 ? new System.Random() : new System.Random(seed);
    }

    /// <summary>
    /// Initialize species from RunSpeciesList (PRIMARY method)
    /// This is the runtime list that will be used for simulation.
    /// </summary>
    public void InitializeFromRunSpeciesList(RunSpeciesList runSpecies)
    {
        Species.Clear();
        ClearAccumulators();

        if (runSpecies == null || runSpecies.speciesList == null || runSpecies.speciesList.Count == 0)
        {
            Debug.LogError("RunSpeciesList is null or empty!");
            return;
        }

        SimLog($"Initializing from RunSpeciesList: {runSpecies.name}");

        foreach (var data in runSpecies.speciesList)
        {
            var simSpecies = new SimSpecies
            {
                Name = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.speciesName.ToString(),
                Variant = ConvertVariant(data.variant),
                Tier = data.tier + 1,  // Database uses 0-based, we use 1-based
                Population = data.count,
                EatingAmount = data.eatingAmount,
                ReproductionMultiplier = data.reproductionMultiplier,
                DeathThreshold = data.deathThreshold,
                DeathRate = data.deathRate,
                ReproThreshold = data.reproThreshold,
                NaturalDeathRate = data.naturalDeathRate,
                NaturalDeathVariance = data.naturalDeathVariance,
                HuntingEfficiency = data.huntingEfficiency,
                HuntingVariance = data.huntingVariance,
                OptimalTempK = data.optimalTempK,
                ArrhenBreadth = data.arrhenBreadth,
                ArrhenLower = data.arrhenLower,
                ArrhenUpper = data.arrhenUpper,
                LowerBoundK = data.lowerBoundK,
                UpperBoundK = data.upperBoundK,
                Pmax = data.pmax,
                CTminC = data.ctMinC,
                CTmaxC = data.ctMaxC,
                TemperatureDebuff = data.TemperatureDebuff,
                Condition = 1.0f
            };

            Species.Add(simSpecies);
            InitializeAccumulators(simSpecies.FullName);

            SimLog($"Loaded: {simSpecies.FullName} (Tier {simSpecies.Tier}) - Pop: {simSpecies.Population}");
        }

        SimLog($"Total species loaded from RunSpeciesList: {Species.Count}");

        _tier1WasPopulated = GetTier1Population() > 0;
        _tier2WasPopulated = GetTier2Population() > 0;
    }

    /// <summary>
    /// Initialize species from SpeciesDatabase (LEGACY - for backwards compatibility)
    /// Prefer InitializeFromRunSpeciesList for new code.
    /// </summary>
    public void InitializeFromDatabase(SpeciesDatabase database)
    {
        Species.Clear();
        ClearAccumulators();

        if (database == null || database.speciesList == null)
        {
            Debug.LogError("SpeciesDatabase is null or empty!");
            return;
        }

        SimLog($"Initializing from SpeciesDatabase (legacy): {database.name}");

        foreach (var data in database.speciesList)
        {
            var simSpecies = new SimSpecies
            {
                Name = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.speciesName.ToString(),
                Variant = ConvertVariant(data.variant),
                Tier = data.tier + 1,  // Database uses 0-based, we use 1-based
                Population = data.count,
                EatingAmount = data.eatingAmount,
                ReproductionMultiplier = data.reproductionMultiplier,
                DeathThreshold = data.deathThreshold,
                DeathRate = data.deathRate,
                ReproThreshold = data.reproThreshold,
                NaturalDeathRate = data.naturalDeathRate,
                NaturalDeathVariance = data.naturalDeathVariance,
                HuntingEfficiency = data.huntingEfficiency,
                HuntingVariance = data.huntingVariance,
                OptimalTempK = data.optimalTempK,
                ArrhenBreadth = data.arrhenBreadth,
                ArrhenLower = data.arrhenLower,
                ArrhenUpper = data.arrhenUpper,
                LowerBoundK = data.lowerBoundK,
                UpperBoundK = data.upperBoundK,
                Pmax = data.pmax,
                CTminC = data.ctMinC,
                CTmaxC = data.ctMaxC,
                TemperatureDebuff = data.TemperatureDebuff,
                Condition = 1.0f
            };

            Species.Add(simSpecies);
            InitializeAccumulators(simSpecies.FullName);

            SimLog($"Loaded: {simSpecies.FullName} (Tier {simSpecies.Tier}) - Pop: {simSpecies.Population}");
        }

        SimLog($"Total species loaded: {Species.Count}");

        _tier1WasPopulated = GetTier1Population() > 0;
        _tier2WasPopulated = GetTier2Population() > 0;
    }

    private ThermalVariant ConvertVariant(SpeciesVariant variant)
    {
        switch (variant)
        {
            case SpeciesVariant.Arctic: return ThermalVariant.Arctic;
            case SpeciesVariant.Common: return ThermalVariant.Common;
            case SpeciesVariant.Tropical: return ThermalVariant.Tropical;
            case SpeciesVariant.Custom: return ThermalVariant.Custom;
            default: return ThermalVariant.Common;
        }
    }

    private void ClearAccumulators()
    {
        _birthAccumulators.Clear();
        _naturalDeathAccumulators.Clear();
        _predationAccumulators.Clear();
        _thermalDeathAccumulators.Clear();
        _conditionDeathAccumulators.Clear();
    }

    private void InitializeAccumulators(string fullName)
    {
        _birthAccumulators[fullName] = 0f;
        _naturalDeathAccumulators[fullName] = 0f;
        _predationAccumulators[fullName] = 0f;
        _thermalDeathAccumulators[fullName] = 0f;
        _conditionDeathAccumulators[fullName] = 0f;
    }

    /// <summary>
    /// Initialize with default species (fallback only)
    /// </summary>
    public void InitializeDefaultSpecies()
    {
        Species.Clear();
        ClearAccumulators();

        // Tier 1: Hexapod (20 of each variant)
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Arctic, 20f));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Common, 20f));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Tropical, 20f));

        // Tier 2: Sheplik (4 of each variant)
        Species.Add(SimSpecies.CreateSheplik(ThermalVariant.Arctic, 4f));
        Species.Add(SimSpecies.CreateSheplik(ThermalVariant.Common, 4f));
        Species.Add(SimSpecies.CreateSheplik(ThermalVariant.Tropical, 4f));

        foreach (var sp in Species)
        {
            InitializeAccumulators(sp.FullName);
        }

        _tier1WasPopulated = GetTier1Population() > 0;
        _tier2WasPopulated = GetTier2Population() > 0;
    }

    /// <summary>
    /// Run one biology step at the given temperature.
    ///
    /// BIOLOGY SEQUENCE (10 steps):
    /// 1. Thermal Performance - Arrhenius formula
    /// 2. Feeding/Predation - Tier 1 FedRate from food-pool density (linear) +
    ///                        Tier 2 Holling II + PREDATION ACCUMULATOR (v10)
    /// 3. Raw Final Performance - RawThermalPerf x FedRate (Condition drain target)
    /// 4. Update Condition - Drain/recover toward RawFinalPerformance; rates scaled by Pmax
    /// 5. Final Performance - ThermalPerf x FedRate (computed for logging; not a biology input)
    /// 6. Thermal Death - INSTANT kill at lethal limits (RawThermalPerf == 0)
    /// 7. Condition Death - GRADUATED: severity scales with how far below threshold
    /// 8. Reproduction - CONDITION-BASED GRADUATED SCALE x Pmax + BIRTH ACCUMULATOR + Tier 1 penalty
    ///                   (no soft-cap-on-births; throttling via Condition pathway, v10)
    /// 9. Natural Death - FLAT RATE + NATURAL DEATH ACCUMULATOR
    /// 10. Population Rounding - All populations become integers
    /// </summary>
    public void ProcessBiologyStep(float temperature)
    {
        // Record start populations
        StartPopT1 = GetTier1Population();
        StartPopT2 = GetTier2Population();

        // Reset tracking
        LastEatenT1 = 0f;
        LastTempDeathsT1 = 0f;
        LastTempDeathsT2 = 0f;
        LastConditionDeathsT1 = 0f;
        LastConditionDeathsT2 = 0f;
        LastNaturalDeathsT1 = 0f;
        LastNaturalDeathsT2 = 0f;
        LastBirthsT1 = 0f;
        LastBirthsT2 = 0f;
        LastFedRateT2 = 1f;
        LastAvgHuntingEfficiency = 1f;
        LastReproScaleT1 = 0f;
        LastReproScaleT2 = 0f;

        SimLog($"=== Biology Step at {temperature:F2}°C (BiologyStep={BiologyStep}) ===");
        SimLog($"  START: T1={StartPopT1:F0}, T2={StartPopT2:F0}");

        // ========== STEP 1: THERMAL PERFORMANCE ==========
        SimLog("--- Step 1: Thermal Performance ---");
        foreach (var sp in Species)
        {
            sp.RawThermalPerformance = sp.CalculatePerformance(temperature);
            sp.ThermalPerformance = sp.RawThermalPerformance * sp.Pmax;
            sp.FedRate = 1f;
            sp.CurrentHuntingSuccess = 1f;
            SimLog($"  {sp.FullName}: Pop={sp.Population:F0}, RawPerf={sp.RawThermalPerformance:F3}, ThermalPerf={sp.ThermalPerformance:F3} (Pmax={sp.Pmax:F2})");
        }

        // ========== STEP 2: FEEDING (with predation accumulator) ==========
        SimLog("--- Step 2: Feeding/Predation ---");
        ProcessFeedingWithAccumulator();

        // ========== STEP 3: RAW FINAL PERFORMANCE (Condition drain target) ==========
        SimLog("--- Step 3: Raw Final Performance ---");
        foreach (var sp in Species)
        {
            sp.RawFinalPerformance = sp.RawThermalPerformance * sp.FedRate;
            SimLog($"  {sp.FullName}: RawFinalPerf={sp.RawFinalPerformance:F3} (RawThermal={sp.RawThermalPerformance:F3} x Fed={sp.FedRate:F3})");
        }

        // ========== STEP 4: UPDATE CONDITION ==========
        SimLog("--- Step 4: Update Condition ---");
        foreach (var sp in Species)
        {
            UpdateCondition(sp);
        }

        // ========== STEP 5: FINAL PERFORMANCE ==========
        // Condition is NOT included — it does not affect reproduction at all.
        // Condition only governs condition-death (below DeathThreshold → DeathRate kill).
        // FinalPerformance = ThermalPerf × FedRate, used for reproduction threshold + birth count.
        SimLog("--- Step 5: Final Performance ---");
        foreach (var sp in Species)
        {
            sp.FinalPerformance = sp.ThermalPerformance * sp.FedRate;
            SimLog($"  {sp.FullName}: FinalPerf={sp.FinalPerformance:F3} (Thermal={sp.ThermalPerformance:F3} x Fed={sp.FedRate:F3})");
        }

        // ========== STEP 6: THERMAL DEATH (instant at lethal limits) ==========
        SimLog("--- Step 6: Thermal Death (lethal limits) ---");
        foreach (var sp in Species)
        {
            ApplyThermalDeath(sp);
        }

        // ========== STEP 7: CONDITION DEATH (chronic stress) ==========
        SimLog("--- Step 7: Condition Death ---");
        foreach (var sp in Species)
        {
            ApplyConditionDeath(sp);
        }

        // ========== STEP 8: REPRODUCTION (with birth accumulator) ==========
        SimLog("--- Step 8: Reproduction ---");
        foreach (var sp in Species)
        {
            ApplyReproduction(sp);
        }

        // ========== STEP 9: NATURAL DEATH (flat rate) ==========
        SimLog("--- Step 9: Natural Death ---");
        foreach (var sp in Species)
        {
            ApplyNaturalDeathWithAccumulator(sp);
        }

        // ========== STEP 10: POPULATION ROUNDING ==========
        SimLog("--- Step 10: Population Rounding ---");
        foreach (var sp in Species)
        {
            float oldPop = sp.Population;
            sp.Population = (float)Math.Round(sp.Population, MidpointRounding.AwayFromZero);
            if (Math.Abs(oldPop - sp.Population) > 0.01f)
            {
                SimLog($"  {sp.FullName}: {oldPop:F2} → {sp.Population:F0}");
            }
        }

        // Compute average condition per tier
        ComputeAverageCondition();

        // Record end populations and accumulator totals
        EndPopT1 = GetTier1Population();
        EndPopT2 = GetTier2Population();
        UpdateAccumulatorTotals();

        SimLog($"  END: T1={EndPopT1:F0}, T2={EndPopT2:F0}");
        SimLog($"  Deaths: Eaten={LastEatenT1:F0}, Temp={LastTempDeathsT1 + LastTempDeathsT2:F0}, Condition={LastConditionDeathsT1 + LastConditionDeathsT2:F0}, Natural={LastNaturalDeathsT1 + LastNaturalDeathsT2:F0}");
        SimLog($"  Births: T1={LastBirthsT1:F0}, T2={LastBirthsT2:F0}");
        SimLog($"  Condition: AvgT1={AvgConditionT1:F3}, AvgT2={AvgConditionT2:F3}");
    }

    /// <summary>
    /// Process Tier 1 FedRate (v10 — density-dependent extraction from shared food pool)
    /// AND Tier 2 feeding with Holling Type II functional response + PREDATION ACCUMULATOR.
    ///
    /// Tier 1 (passive extractors / plankton-style):
    ///   food_density = max(0, 1 - tier1Pop / CarryingCapacityPerTier)  [or 1.0 if cap disabled]
    ///   FedRate_T1   = min(1, HuntingEfficiency × food_density)
    /// LINEAR, not Holling II — see in-code comment block for biological reasoning.
    /// Tier 1 FedRate is computed before the predator section so it is available to
    /// downstream Steps 3 (Raw Final Performance) and 4 (Update Condition).
    ///
    /// Tier 2 (active predators):
    ///   Holling Type II: hunting efficiency scales with prey:predator ratio.
    ///   At NORMAL_PREY_RATIO (20:1), efficiency equals the species' base value.
    ///   Above → efficiency increases toward 1.0 (abundance).
    ///   Below → efficiency decreases toward 0.0 (scarcity).
    /// Predator demand uses ThermalPerformance (with Pmax). Prey removal is proportional
    /// across variants with fractional accumulation.
    /// </summary>
    private void ProcessFeedingWithAccumulator()
    {
        var predators = Species.Where(s => s.Tier == 2 && s.Population >= MIN_ALIVE_POP).ToList();
        var prey = Species.Where(s => s.Tier == 1 && s.Population >= MIN_ALIVE_POP).ToList();

        SimLog($"  Predators: {predators.Count} species, {predators.Sum(p => p.Population):F0} total");
        SimLog($"  Prey: {prey.Count} species, {prey.Sum(p => p.Population):F0} total");

        // ====================================================================
        // TIER 1 FEDRATE (v10): density-dependent extraction from shared food pool
        // ====================================================================
        // Carrying capacity acts as a shared food/resource pool, not a soft cap on
        // births. food_density falls linearly with population pressure on the pool;
        // each species' FedRate scales by its HuntingEfficiency (semantically:
        // resource extraction efficiency for Tier 1 — passive extractors like
        // plankton are at HE=1 by default).
        //
        // LINEAR, not Holling II: prey are passive extractors (filter feeding,
        // surface-area-driven uptake) — no search-time + handling-time structure
        // that motivates Holling II. Holling II also collapses at HE=1 default
        // (halfSat = REF × (1-HE)/HE = 0 when HE=1, so FedRate → 1 whenever any
        // food exists), defeating the food-pool effect entirely at the common
        // setting. Linear avoids both problems and matches plankton-style
        // extraction biology directly. Tier 2 keeps Holling II below — active
        // predation does have search/handling phases that justify it.
        //
        // When UseCarryingCapacity is false (or cap is 0): food_density forced
        // to 1.0 → FedRate_T1 = HE = 1 by default (legacy "Tier 1 always
        // satisfied" behaviour). HE < 1 still scales the result.
        float tier1Pop = GetTierPopulation(1);
        float foodDensity = (UseCarryingCapacity && CarryingCapacityPerTier > 0f)
            ? Math.Max(0f, 1f - (tier1Pop / CarryingCapacityPerTier))
            : 1f;
        LastFoodDensityT1 = foodDensity;
        float fedRateSumT1 = 0f;
        float fedRatePopT1 = 0f;
        foreach (var sp in Species.Where(s => s.Tier == 1))
        {
            if (sp.Population >= MIN_ALIVE_POP)
            {
                sp.FedRate = Math.Min(1f, sp.HuntingEfficiency * foodDensity);
                fedRateSumT1 += sp.FedRate * sp.Population;
                fedRatePopT1 += sp.Population;
            }
            else
            {
                sp.FedRate = 0f;
            }
        }
        LastFedRateT1 = fedRatePopT1 > 0f ? fedRateSumT1 / fedRatePopT1 : 1f;
        SimLog($"  Tier 1 food: tier1Pop={tier1Pop:F0}, foodDensity={foodDensity:F3}, avgFedRateT1={LastFedRateT1:F3}");

        if (predators.Count == 0 || prey.Count == 0)
        {
            foreach (var p in predators) p.FedRate = 0f;
            LastFedRateT2 = predators.Count > 0 ? 0f : 1f;
            LastAvgHuntingEfficiency = 1f;
            SimLog($"  No predator feeding (predators={predators.Count}, prey={prey.Count})");
            return;
        }

        float availablePrey = prey.Sum(p => p.Population);
        float totalPredators = predators.Sum(p => p.Population);

        // Calculate prey-to-predator ratio for Holling Type II efficiency scaling
        float preyRatio = totalPredators > 0 ? availablePrey / totalPredators : 0f;
        SimLog($"  Prey ratio: {preyRatio:F1}:1");

        // Calculate hunting success and demand for each predator
        float totalRawDemand = 0f;       // What predators NEED (full nutritional requirement)
        float totalActualDemand = 0f;    // What predators CAN catch (after Holling efficiency)
        float huntingEfficiencySum = 0f;
        int predatorCount = 0;

        foreach (var pred in predators)
        {
            // Holling Type II: hunting efficiency scales with prey availability
            float hollingEff = CalculateHollingEfficiency(pred.HuntingEfficiency, preyRatio);
            float variance = (float)((_rng.NextDouble() * 2 - 1) * pred.HuntingVariance);
            float huntingSuccess = Math.Max(MIN_HUNTING_SUCCESS, Math.Min(MAX_HUNTING_SUCCESS, hollingEff + variance));
            pred.CurrentHuntingSuccess = huntingSuccess;

            // Raw demand (what they NEED) — uses ThermalPerformance (with Pmax)
            float rawDemand = pred.Population * pred.EatingAmount * pred.ThermalPerformance * BiologyStep;
            totalRawDemand += rawDemand;

            // Actual demand (what they CAN catch — reduced by Holling efficiency)
            float actualDemand = rawDemand * huntingSuccess;
            totalActualDemand += actualDemand;

            huntingEfficiencySum += huntingSuccess;
            predatorCount++;

            SimLog($"  {pred.FullName}: Hunting={huntingSuccess:P0} (holling={hollingEff:F3}, variance={variance:+0.00;-0.00;0}), RawDemand={rawDemand:F1}, ActualDemand={actualDemand:F1}");
        }

        LastAvgHuntingEfficiency = predatorCount > 0 ? huntingEfficiencySum / predatorCount : 1f;
        SimLog($"  Avg Hunting Efficiency: {LastAvgHuntingEfficiency:P0}");

        // Calculate total eaten (capped by available prey)
        float totalEaten = Math.Min(availablePrey, totalActualDemand);
        LastEatenT1 = 0f;  // Will be counted by actual removals

        // FedRate = what was caught / what was NEEDED (not what was attempted)
        // This is critical: Holling efficiency reduces actual demand, so predators catch less.
        // But FedRate must reflect their true nutritional satisfaction — how much of their
        // actual need was met. At ratio 5:1 with Holling efficiency 0.43, predators only
        // catch 43% of what they need, so FedRate ≈ 0.43, not 1.0.
        // This directly affects FinalPerformance, reproduction, and Condition drain.
        float fedRate;
        if (totalRawDemand <= 0f)
            fedRate = 1f;
        else
            fedRate = Math.Min(1f, totalEaten / totalRawDemand);

        LastFedRateT2 = fedRate;
        foreach (var pred in predators)
        {
            pred.FedRate = fedRate;
        }

        SimLog($"  Total Eaten: {totalEaten:F1}, FedRate: {fedRate:F3} (eaten/rawDemand = {totalEaten:F1}/{totalRawDemand:F1}, actualDemand={totalActualDemand:F1})");

        // Remove prey PROPORTIONALLY with PREDATION ACCUMULATOR
        if (totalEaten > 0f && availablePrey > 0f)
        {
            foreach (var p in prey)
            {
                float share = p.Population / availablePrey;
                float preyLost = totalEaten * share;

                // Add to predation accumulator
                if (!_predationAccumulators.ContainsKey(p.FullName))
                    _predationAccumulators[p.FullName] = 0f;

                _predationAccumulators[p.FullName] += preyLost;
                float accumulated = _predationAccumulators[p.FullName];

                // Extract whole deaths
                int wholeDeaths = (int)Math.Floor(accumulated);
                _predationAccumulators[p.FullName] = accumulated - wholeDeaths;

                // Apply deaths
                wholeDeaths = Math.Min(wholeDeaths, (int)p.Population);
                float oldPop = p.Population;
                p.Population = Math.Max(0f, p.Population - wholeDeaths);
                LastEatenT1 += wholeDeaths;

                SimLog($"    {p.FullName}: share={share:F3}, lost={preyLost:F2}, accum={accumulated:F2}, deaths={wholeDeaths}, Pop {oldPop:F0} → {p.Population:F0}");
            }
        }
    }

    /// <summary>
    /// Holling Type II Functional Response (Holling 1959).
    /// Reference: Holling, C.S. (1959), "The Components of Predation as Revealed by a
    /// Study of Small-Mammal Predation of the European Pine Sawfly",
    /// Canadian Entomologist, 91(5), 293-320.
    ///
    /// Models how predator hunting success scales with prey availability.
    /// As prey becomes scarcer, predators spend more time searching and less time eating.
    /// The curve naturally produces:
    ///   - 0.0 at zero prey (nothing to hunt)
    ///   - baseEfficiency at NORMAL_PREY_RATIO (normal hunting conditions)
    ///   - Approaches 1.0 at very high prey density (abundance, easy to find prey)
    ///
    /// Formula: efficiency = ratio / (ratio + halfSaturation)
    /// where halfSaturation = NORMAL_PREY_RATIO × (1 - baseEff) / baseEff
    ///
    /// The half-saturation constant is derived from the species' own base efficiency,
    /// so the curve always passes through (NORMAL_PREY_RATIO, baseEfficiency).
    /// No arbitrary tuning constants are needed.
    ///
    /// Example for Sheplik (base 0.75, normal ratio 20:1):
    ///   halfSat = 20 × 0.25 / 0.75 = 6.67
    ///   ratio  0 → 0.00 | ratio  5 → 0.43 | ratio 10 → 0.60
    ///   ratio 20 → 0.75 | ratio 50 → 0.88 | ratio ∞  → 1.00
    /// </summary>
    private float CalculateHollingEfficiency(float baseEfficiency, float preyRatio)
    {
        if (preyRatio <= 0f) return 0f;
        if (baseEfficiency <= 0f) return 0f;
        if (baseEfficiency >= 1f) return 1f;

        float halfSaturation = NORMAL_PREY_RATIO * (1f - baseEfficiency) / baseEfficiency;
        return preyRatio / (preyRatio + halfSaturation);
    }

    /// <summary>
    /// Update Condition (health/energy reserves) for each species.
    /// Condition moves toward RawFinalPerformance asymmetrically:
    ///   - Drains faster than it recovers (base rates: 0.15 drain vs 0.10 recovery)
    ///   - Both drain and recovery use continuous quadratic acceleration (Buckley et al. 2025)
    ///   - Drain: multiplier = 1 + (1-target)², max 2x at perf=0
    ///   - Recovery: multiplier = 1 + target², max 2x at perf=1
    ///   - Feeding contributes via FedRate (starving predators drain even at good temps;
    ///     in v10 Tier 1 also drains when food density is low even at good temps).
    ///   - Pmax scales the rates: drain /= Pmax, recovery *= Pmax.
    ///     Specialists (high Pmax) drain slower and recover faster than generalists.
    ///     Pmax does NOT enter the target — Condition's [0, 1] semantics are preserved
    ///     across species, so ReproThreshold/DeathThreshold need no per-species tuning.
    /// In v10, Tier 1's RawFinalPerformance = RawThermalPerf × FedRate now varies with
    /// food density too (FedRate is no longer hardcoded to 1 for prey), so Tier 1
    /// Condition no longer plateaus at 1.0 even at perfect temperature when the
    /// shared resource pool is depleted.
    /// </summary>
    private void UpdateCondition(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP) return;

        float target = sp.RawFinalPerformance;  // environmental target (thermal × fed); Pmax applied to rates, not target
        float oldCondition = sp.Condition;

        // Safety: avoid divide-by-zero if Pmax is ever 0 for a species (clamp to small positive).
        float pmaxSafe = Math.Max(sp.Pmax, 1e-4f);

        if (sp.Condition > target)
        {
            // Draining — continuous quadratic acceleration (Buckley et al. 2025)
            // (1-target)² ranges from 0 at perf=1 to 1 at perf=0
            // Effective multiplier: 1x at optimal → 2x at lethal
            // Pmax scaling: generalists (low Pmax) drain faster; specialists drain slower.
            float severity = 1f - target;
            severity *= severity;
            float effectiveDrain = ConditionDrainRate * (1f + severity) / pmaxSafe;
            sp.Condition -= (sp.Condition - target) * effectiveDrain;
        }
        else
        {
            // Recovering — continuous quadratic acceleration (Buckley et al. 2025)
            // target² ranges from 0 at perf=0 to 1 at perf=1
            // Effective multiplier: 1x at lethal → 2x at optimal
            // Pmax scaling: specialists (high Pmax) recover faster; generalists slower.
            float boost = target;
            boost *= boost;
            float effectiveRecovery = ConditionRecoveryRate * (1f + boost) * pmaxSafe;
            sp.Condition += (target - sp.Condition) * effectiveRecovery;
        }

        sp.Condition = Math.Max(0f, Math.Min(1f, sp.Condition));

        SimLog($"  {sp.FullName}: Condition {oldCondition:F3} → {sp.Condition:F3} (target={target:F3})");
    }

    /// <summary>
    /// Apply INSTANT thermal death at lethal temperature limits.
    /// Triggers ONLY when RawThermalPerformance == 0 (at/beyond CTmin/CTmax).
    /// Beyond lethal limits = total wipeout. No Condition buffer can save you.
    /// Suboptimal temperatures are handled by the Condition system instead.
    /// </summary>
    private void ApplyThermalDeath(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP)
        {
            SimLog($"  {sp.FullName}: Already extinct");
            return;
        }

        if (sp.RawThermalPerformance > 0f)
        {
            SimLog($"  {sp.FullName}: SURVIVES (RawThermalPerf {sp.RawThermalPerformance:F3} > 0)");
            return;
        }

        // Beyond lethal limits — total wipeout
        float deaths = sp.Population;
        sp.Population = 0f;
        sp.Condition = 0f;

        SimLog($"  {sp.FullName}: THERMAL DEATH (lethal limit) - {deaths:F0} deaths (RawThermalPerf=0), Pop → 0");

        if (sp.Tier == 1) LastTempDeathsT1 += deaths;
        else if (sp.Tier == 2) LastTempDeathsT2 += deaths;
    }

    /// <summary>
    /// Apply GRADUATED condition-based death (chronic stress, exhaustion, starvation).
    ///
    /// Ecological basis: Casini et al. (2016) established critical condition thresholds
    /// for Baltic cod; Dutil & Lambert (2000) showed starvation mortality is continuous,
    /// not binary. Booth & Hixon (1999) found survivorship of well-fed reef fish was
    /// double that of poorly-fed fish — mortality scales with condition severity.
    ///
    /// Instead of a binary cliff (below threshold → flat DeathRate kill), deaths are
    /// proportional to how far below the threshold Condition has fallen:
    ///
    ///   severity = (DeathThreshold - Condition) / DeathThreshold    // 0 at threshold, 1 at zero
    ///   rawDeaths = Population × severity × DeathRate × BiologyStep
    ///
    /// This models realistic population dynamics: barely below threshold = a few weak
    /// individuals die; severely depleted = mass die-off. At Condition=0, the full
    /// DeathRate applies (same maximum as the old system).
    ///
    /// SURVIVOR FITNESS BOOST: After deaths, the surviving population's Condition is
    /// recalculated assuming the dead were the weakest members (condition ≈ 0):
    ///
    ///   new_condition = old_condition × old_population / new_population
    ///
    /// No new variables — this is conservation of the population's total health pool
    /// distributed among fewer (healthier) survivors. This creates self-correction:
    /// deaths push condition back toward the threshold, preventing death spirals.
    ///
    /// Uses CONDITION DEATH ACCUMULATOR for fractional death tracking.
    /// </summary>
    private void ApplyConditionDeath(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP) return;
        if (sp.Condition >= sp.DeathThreshold) return;

        // Graduated severity: 0 at threshold, 1 at condition=0
        float severity = (sp.DeathThreshold - sp.Condition) / sp.DeathThreshold;
        float rawDeaths = sp.Population * severity * sp.DeathRate * BiologyStep;

        // Accumulator pattern — fractional deaths carry over between days
        _conditionDeathAccumulators[sp.FullName] += rawDeaths;
        float accumulated = _conditionDeathAccumulators[sp.FullName];
        int wholeDeaths = (int)Math.Floor(accumulated);
        _conditionDeathAccumulators[sp.FullName] = accumulated - wholeDeaths;
        wholeDeaths = Math.Min(wholeDeaths, (int)sp.Population);

        if (wholeDeaths > 0)
        {
            float oldPop = sp.Population;
            float oldCondition = sp.Condition;
            sp.Population = Math.Max(0f, sp.Population - wholeDeaths);

            // Survivor fitness boost: the dead were the weakest (condition ≈ 0).
            // Same total health pool, fewer individuals → higher average condition.
            // This prevents death spirals by pushing condition back toward threshold.
            if (sp.Population > 0f)
            {
                sp.Condition = oldCondition * oldPop / sp.Population;
                sp.Condition = Math.Min(1f, sp.Condition); // Cap at 1.0
            }

            SimLog($"  {sp.FullName}: CONDITION DEATH - severity={severity:F3}, raw={rawDeaths:F2}, deaths={wholeDeaths}, Pop {oldPop:F0} → {sp.Population:F0}, Condition {oldCondition:F3} → {sp.Condition:F3}");

            if (sp.Tier == 1) LastConditionDeathsT1 += wholeDeaths;
            else if (sp.Tier == 2) LastConditionDeathsT2 += wholeDeaths;
        }
        else
        {
            SimLog($"  {sp.FullName}: Condition death - severity={severity:F3}, raw={rawDeaths:F2}, accum={_conditionDeathAccumulators[sp.FullName]:F2} (no whole deaths yet, Condition {sp.Condition:F3} < {sp.DeathThreshold})");
        }
    }

    /// <summary>
    /// Apply reproduction with CONDITION-BASED GRADUATED SCALE, BIRTH ACCUMULATOR,
    /// Tier 1 penalty, and BIRTH ACCUMULATOR. (Soft cap on births was REMOVED in v10 —
    /// see Tier 1 throttling note below.)
    ///
    /// CONDITION-BASED REPRODUCTION (v8) + Pmax MULTIPLIER (v9) + parent-condition newborn (v10):
    /// Reproduction is driven by Condition (species health). Pmax then scales the
    /// final birth count — a specialist with higher Pmax converts the same Condition
    /// into more offspring than a generalist.
    ///
    /// Two regions, continuous at ReproThreshold:
    ///   Above threshold: reproScale = STRUGGLING_RATE + (1 - STRUGGLING_RATE) × (Cond - thresh) / (1 - thresh)
    ///   Below threshold: reproScale = STRUGGLING_RATE × (Cond / thresh)
    ///   At threshold: both give STRUGGLING_RATE (0.10) — no discontinuity
    ///   At Condition = 0: reproScale = 0 (only truly dead species don't reproduce)
    ///
    /// births = Population × reproScale × ReproMult × Pmax × BiologyStep
    ///
    /// Why Condition, not FinalPerformance:
    /// - FinalPerformance is instantaneous (thermal × fed) — drops to near-zero in winter
    /// - Condition is lagged — drains gradually, preserving summer health into early winter
    /// - This prevents the "90-day zero reproduction" winter problem
    /// - No death spiral: Condition drains toward RawFinalPerf (environmental), not birth-dependent
    ///
    /// Why Pmax is multiplied here and not folded into Condition's drain target:
    /// - Keeps Condition on a species-agnostic 0–1 scale (ReproThreshold/DeathThreshold
    ///   stay semantically unchanged across specialists and generalists).
    /// - Separates "health" (Condition) from "reproductive efficiency" (Pmax).
    /// - Pmax also scales Condition drain/recovery rates (see UpdateCondition), so peak
    ///   metabolism affects both survivability and reproduction without distorting thresholds.
    ///
    /// Tier 1 throttling via Condition (v10):
    /// The carrying-capacity soft cap on Tier 1 births was removed. Reproduction is now
    /// throttled indirectly through the Condition pathway: high population → low food
    /// density (computed in Step 2) → low Tier 1 FedRate → low RawFinalPerformance
    /// target → Condition drains → reproScale shrinks AND condition deaths fire.
    /// This eliminates the live-tierPop read that previously caused the processing-order
    /// bug (first-listed species reproducing against a smaller pool than later-listed).
    ///
    /// Newborn Condition (v10):
    /// Newborns inherit the species' current group Condition. No fixed constant, no
    /// food-density multiplier — the parent's Condition is itself a lagged integral of
    /// recent food density / hunting success, so it already encodes recent provisioning
    /// capacity. Multiplying by today's FedRate would double-count the same signal.
    /// Newborn vulnerability emerges from same-drain-no-head-start dynamics, not from
    /// a starting-Condition penalty. Mathematically the population-weighted average is
    /// unchanged when newborns match the group, so no explicit Condition update is
    /// needed in the births-application step. Same logic for Tier 1 and Tier 2.
    /// </summary>
    private void ApplyReproduction(SimSpecies sp)
    {
        if (sp.Population < MIN_POPULATION_FOR_REPRODUCTION)
        {
            SimLog($"  {sp.FullName}: Cannot reproduce (Pop={sp.Population:F1} < {MIN_POPULATION_FOR_REPRODUCTION})");
            return;
        }

        // Condition-based graduated reproduction scale (v8)
        // Two continuous regions joined at STRUGGLING_REPRO_RATE:
        //   Above ReproThreshold: healthy reproduction, ramps from 0.10 to 1.0
        //   Below ReproThreshold: struggling reproduction, ramps from 0 to 0.10
        //   At Condition = 0: reproScale = 0 (truly dead species don't reproduce)
        //
        // No hard cliff anywhere — even poor-condition species produce a trickle of
        // births that the accumulator captures over multiple days. This models the
        // ecological reality that animals with energy reserves reproduce in all seasons,
        // just at reduced rates in harsh conditions.
        float reproScale;
        if (sp.ReproThreshold >= 1.0f)
        {
            // Edge case: threshold at max — all reproduction is "struggling" mode
            reproScale = STRUGGLING_REPRO_RATE * sp.Condition;
        }
        else if (sp.ReproThreshold <= 0f)
        {
            // Edge case: no threshold — reproScale equals Condition directly
            reproScale = sp.Condition;
        }
        else if (sp.Condition >= sp.ReproThreshold)
        {
            // Healthy: ramp from STRUGGLING_REPRO_RATE at threshold to 1.0 at full condition
            float t = (sp.Condition - sp.ReproThreshold) / (1.0f - sp.ReproThreshold);
            reproScale = STRUGGLING_REPRO_RATE + (1.0f - STRUGGLING_REPRO_RATE) * t;
        }
        else
        {
            // Struggling: ramp from 0 at Condition=0 to STRUGGLING_REPRO_RATE at threshold
            // Non-zero as long as Condition > 0 — the accumulator will capture fractional births
            reproScale = STRUGGLING_REPRO_RATE * (sp.Condition / sp.ReproThreshold);
        }
        reproScale = Math.Max(0f, Math.Min(1f, reproScale)); // Safety clamp

        // Track reproScale per tier for CSV output and debugging
        if (sp.Tier == 1) LastReproScaleT1 = reproScale;
        else if (sp.Tier == 2) LastReproScaleT2 = reproScale;

        // births = Population × reproScale × ReproMult × Pmax × BiologyStep
        // Pmax (v9): specialists convert Condition into offspring more efficiently.
        // Newborns inherit group Condition (v10) — see comment block below the births
        // application; no separate dilution step.
        float births = sp.Population * reproScale * sp.ReproductionMultiplier * sp.Pmax * BiologyStep;

        // Tier 1 penalty if no predators exist
        if (sp.Tier == 1)
        {
            float tier2Pop = GetTierPopulation(2);
            if (tier2Pop < MIN_ALIVE_POP)
            {
                births *= SimSpecies.NO_PREDATOR_PENALTY;
                SimLog($"  {sp.FullName}: No predator penalty applied ({SimSpecies.NO_PREDATOR_PENALTY:P0})");
            }
        }

        // Carrying-capacity soft cap on births was REMOVED in v10. Tier 1 reproduction
        // is now throttled indirectly through the Condition pathway: high population →
        // low food density (in Step 2) → low FedRate → low RawFinalPerformance target →
        // Condition drains → reproScale shrinks AND condition deaths fire. This removes
        // the live-tierPop read that caused the processing-order bug (first-listed
        // species reproducing against a smaller pool than later-listed ones).

        // BIRTH ACCUMULATOR
        if (!_birthAccumulators.ContainsKey(sp.FullName))
            _birthAccumulators[sp.FullName] = 0f;

        _birthAccumulators[sp.FullName] += births;
        float accumulated = _birthAccumulators[sp.FullName];

        int wholeBirths = (int)Math.Floor(accumulated);
        _birthAccumulators[sp.FullName] = accumulated - wholeBirths;

        float oldPop = sp.Population;
        sp.Population += wholeBirths;

        // Newborn Condition (v10): newborns inherit the species' current group
        // Condition. No fixed constant, no food-density multiplier.
        //
        // Rationale: parent's current Condition is itself a lagged integral of
        // recent food density (Tier 1) or hunting success (Tier 2), so it
        // already encodes recent provisioning capacity. Multiplying by today's
        // FedRate would double-count the same signal — the egg/larva's reserves
        // were determined by past conditions, not by the moment of birth.
        // Newborn vulnerability emerges from same-drain-no-head-start dynamics
        // in subsequent days, not from a starting-condition penalty.
        //
        // The population-weighted average is mathematically unchanged when
        // newborns match the group, so no explicit Condition update is needed.
        // Same logic for Tier 1 and Tier 2.

        SimLog($"  {sp.FullName}: reproScale={reproScale:F3} (Condition={sp.Condition:F3}, thresh={sp.ReproThreshold}), +{births:F2} raw, accum={accumulated:F2}, actual={wholeBirths}, Pop {oldPop:F0} → {sp.Population:F0}");

        // Track by tier
        if (sp.Tier == 1)
            LastBirthsT1 += wholeBirths;
        else if (sp.Tier == 2)
            LastBirthsT2 += wholeBirths;
    }

    /// <summary>
    /// Apply natural death with FLAT RATE and ACCUMULATOR.
    /// Natural death represents old age, disease, accidents — independent of performance.
    /// </summary>
    private void ApplyNaturalDeathWithAccumulator(SimSpecies sp)
    {
        if (sp.Population <= 0f)
        {
            return;
        }

        // Calculate death rate with variance
        float variance = (float)((_rng.NextDouble() * 2 - 1) * sp.NaturalDeathVariance);
        float baseRate = Math.Max(0f, sp.NaturalDeathRate + variance);

        // Flat rate — natural death is independent of performance
        float effectiveRate = baseRate;

        // Calculate raw deaths
        float deaths = sp.Population * effectiveRate * BiologyStep;

        // NATURAL DEATH ACCUMULATOR
        if (!_naturalDeathAccumulators.ContainsKey(sp.FullName))
            _naturalDeathAccumulators[sp.FullName] = 0f;

        _naturalDeathAccumulators[sp.FullName] += deaths;
        float accumulated = _naturalDeathAccumulators[sp.FullName];

        int wholeDeaths = (int)Math.Floor(accumulated);
        _naturalDeathAccumulators[sp.FullName] = accumulated - wholeDeaths;

        // Cap deaths at population
        wholeDeaths = Math.Min(wholeDeaths, (int)sp.Population);

        if (wholeDeaths > 0)
        {
            float oldPop = sp.Population;
            sp.Population = Math.Max(0f, sp.Population - wholeDeaths);

            SimLog($"  {sp.FullName}: NATURAL DEATH - rate={effectiveRate:P1}, raw={deaths:F2}, accum={accumulated:F2}, deaths={wholeDeaths}, Pop {oldPop:F0} → {sp.Population:F0}");

            if (sp.Tier == 1)
                LastNaturalDeathsT1 += wholeDeaths;
            else if (sp.Tier == 2)
                LastNaturalDeathsT2 += wholeDeaths;
        }
        else
        {
            SimLog($"  {sp.FullName}: Natural death - effectiveRate={effectiveRate:P1}, raw={deaths:F2}, accum={_naturalDeathAccumulators[sp.FullName]:F2} (no deaths yet)");
        }
    }

    /// <summary>
    /// Compute population-weighted average Condition per tier.
    /// </summary>
    private void ComputeAverageCondition()
    {
        float sumT1 = 0f, popT1 = 0f;
        float sumT2 = 0f, popT2 = 0f;

        foreach (var sp in Species)
        {
            if (sp.Population < MIN_ALIVE_POP) continue;
            if (sp.Tier == 1) { sumT1 += sp.Condition * sp.Population; popT1 += sp.Population; }
            else if (sp.Tier == 2) { sumT2 += sp.Condition * sp.Population; popT2 += sp.Population; }
        }

        AvgConditionT1 = popT1 > 0 ? sumT1 / popT1 : 0f;
        AvgConditionT2 = popT2 > 0 ? sumT2 / popT2 : 0f;
    }

    /// <summary>
    /// Update accumulator totals for CSV output
    /// </summary>
    private void UpdateAccumulatorTotals()
    {
        BirthAccumT1 = 0f;
        BirthAccumT2 = 0f;
        NaturalDeathAccumT1 = 0f;
        NaturalDeathAccumT2 = 0f;
        PredationAccumT1 = 0f;
        ConditionDeathAccumT1 = 0f;
        ConditionDeathAccumT2 = 0f;

        foreach (var sp in Species)
        {
            if (_birthAccumulators.ContainsKey(sp.FullName))
            {
                if (sp.Tier == 1) BirthAccumT1 += _birthAccumulators[sp.FullName];
                else if (sp.Tier == 2) BirthAccumT2 += _birthAccumulators[sp.FullName];
            }

            if (_naturalDeathAccumulators.ContainsKey(sp.FullName))
            {
                if (sp.Tier == 1) NaturalDeathAccumT1 += _naturalDeathAccumulators[sp.FullName];
                else if (sp.Tier == 2) NaturalDeathAccumT2 += _naturalDeathAccumulators[sp.FullName];
            }

            if (_predationAccumulators.ContainsKey(sp.FullName))
            {
                if (sp.Tier == 1) PredationAccumT1 += _predationAccumulators[sp.FullName];
            }

            if (_conditionDeathAccumulators.ContainsKey(sp.FullName))
            {
                if (sp.Tier == 1) ConditionDeathAccumT1 += _conditionDeathAccumulators[sp.FullName];
                else if (sp.Tier == 2) ConditionDeathAccumT2 += _conditionDeathAccumulators[sp.FullName];
            }
        }
    }

    // ==================== POPULATION QUERIES ====================

    public float GetTier1Population() => Species.Where(s => s.Tier == 1).Sum(s => s.Population);
    public float GetTier2Population() => Species.Where(s => s.Tier == 2).Sum(s => s.Population);
    public float GetTierPopulation(int tier) => Species.Where(s => s.Tier == tier).Sum(s => s.Population);
    public float GetVariantPopulation(int tier, ThermalVariant variant) =>
        Species.Where(s => s.Tier == tier && s.Variant == variant).Sum(s => s.Population);

    public bool HasCrashed()
    {
        float totalPop = GetTier1Population() + GetTier2Population();
        return totalPop == 0;
    }

    public int GetCrashedTier()
    {
        if (_tier1WasPopulated && GetTier1Population() == 0 && GetTier2Population() == 0) return 0; // all dead
        if (_tier1WasPopulated && GetTier1Population() == 0) return 1;
        if (_tier2WasPopulated && GetTier2Population() == 0) return 2;
        return -1;
    }
}