using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TinySea Ecosystem Simulator v6
///
/// BIOLOGY SEQUENCE (9 steps):
/// 1. Thermal Performance - Arrhenius formula
/// 2. Feeding/Predation - With hunting efficiency + PREDATION ACCUMULATOR
/// 3. Final Performance - ThermalPerf × FedRate
/// 4. Update Condition - Drain/recover toward RawFinalPerformance (health buffer)
/// 5. Thermal Death - INSTANT kill at lethal limits (RawThermalPerf == 0)
/// 6. Condition Death - When Condition less than DeathThreshold after chronic stress
/// 7. Reproduction - With BIRTH ACCUMULATOR + Tier 1 penalty when no predators
/// 8. Natural Death - FLAT RATE + NATURAL DEATH ACCUMULATOR
/// 9. Population Rounding - All populations become integers
///
/// DEATH TYPES:
/// - Thermal: Instant kill when beyond CTmin/CTmax (RawThermalPerf == 0)
/// - Condition: Chronic stress — Condition drains on bad days, death when below threshold
/// - Natural: Flat 2% rate — old age, disease, accidents (no performance scaling)
/// - Predation: Tier 2 eats Tier 1 (unchanged)
///
/// CONDITION SYSTEM:
/// - Per-species health value [0-1], starts at 1.0
/// - Drains toward RawFinalPerformance (asymmetric: drains faster than recovers)
/// - Drain accelerates up to 5x near lethal temperatures
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
    private const float DRAIN_ACCEL_THRESHOLD = 0.2f;  // Performance below this accelerates drain
    private const float DRAIN_ACCEL_MAX = 4f;           // Max acceleration multiplier (5x total at perf=0)

    /// <summary>
    /// Editor-only simulation log. Stripped entirely from built players (including WebGL)
    /// via [Conditional]. The string interpolation in callers is also removed at compile time,
    /// preventing ~900,000 string allocations per 50-year scenario.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
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
                MinimumDeaths = data.minimumDeaths,
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
                MinimumDeaths = data.minimumDeaths,
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
    /// BIOLOGY SEQUENCE (9 steps):
    /// 1. Thermal Performance - Arrhenius formula
    /// 2. Feeding/Predation - With hunting efficiency + PREDATION ACCUMULATOR
    /// 3. Final Performance - ThermalPerf × FedRate
    /// 4. Update Condition - Drain/recover toward RawFinalPerformance
    /// 5. Thermal Death - INSTANT kill at lethal limits (RawThermalPerf == 0)
    /// 6. Condition Death - When Condition less than DeathThreshold after chronic stress
    /// 7. Reproduction - With BIRTH ACCUMULATOR + Tier 1 penalty when no predators
    /// 8. Natural Death - FLAT RATE (no performance scaling)
    /// 9. Population Rounding - All populations become integers
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

        // ========== STEP 3: FINAL PERFORMANCE ==========
        SimLog("--- Step 3: Final Performance ---");
        foreach (var sp in Species)
        {
            sp.FinalPerformance = sp.ThermalPerformance * sp.FedRate;
            sp.RawFinalPerformance = sp.RawThermalPerformance * sp.FedRate;
            SimLog($"  {sp.FullName}: RawFinalPerf={sp.RawFinalPerformance:F3}, FinalPerf={sp.FinalPerformance:F3} (Raw={sp.RawThermalPerformance:F3}×Fed={sp.FedRate:F3}, Pmax={sp.Pmax:F2})");
        }

        // ========== STEP 4: UPDATE CONDITION ==========
        SimLog("--- Step 4: Update Condition ---");
        foreach (var sp in Species)
        {
            UpdateCondition(sp);
        }

        // ========== STEP 5: THERMAL DEATH (instant at lethal limits) ==========
        SimLog("--- Step 5: Thermal Death (lethal limits) ---");
        foreach (var sp in Species)
        {
            ApplyThermalDeath(sp);
        }

        // ========== STEP 6: CONDITION DEATH (chronic stress) ==========
        SimLog("--- Step 6: Condition Death ---");
        foreach (var sp in Species)
        {
            ApplyConditionDeath(sp);
        }

        // ========== STEP 7: REPRODUCTION (with birth accumulator) ==========
        SimLog("--- Step 7: Reproduction ---");
        foreach (var sp in Species)
        {
            ApplyReproduction(sp);
        }

        // ========== STEP 8: NATURAL DEATH (flat rate) ==========
        SimLog("--- Step 8: Natural Death ---");
        foreach (var sp in Species)
        {
            ApplyNaturalDeathWithAccumulator(sp);
        }

        // ========== STEP 9: POPULATION ROUNDING ==========
        SimLog("--- Step 9: Population Rounding ---");
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
    /// Process feeding with hunting efficiency, prey-ratio scaling, and PREDATION ACCUMULATOR.
    /// Prey removal is proportional across variants with fractional accumulation.
    /// 
    /// HUNTING EFFICIENCY SCALING:
    /// When prey is abundant (high Tier1:Tier2 ratio), hunting is easier.
    /// When prey is scarce (low ratio), hunting is harder.
    /// </summary>
    private void ProcessFeedingWithAccumulator()
    {
        var predators = Species.Where(s => s.Tier == 2 && s.Population >= MIN_ALIVE_POP).ToList();
        var prey = Species.Where(s => s.Tier == 1 && s.Population >= MIN_ALIVE_POP).ToList();

        SimLog($"  Predators: {predators.Count} species, {predators.Sum(p => p.Population):F0} total");
        SimLog($"  Prey: {prey.Count} species, {prey.Sum(p => p.Population):F0} total");

        if (predators.Count == 0 || prey.Count == 0)
        {
            foreach (var p in predators) p.FedRate = 0f;
            LastFedRateT2 = predators.Count > 0 ? 0f : 1f;
            LastAvgHuntingEfficiency = 1f;
            SimLog($"  No feeding (predators={predators.Count}, prey={prey.Count})");
            return;
        }

        float availablePrey = prey.Sum(p => p.Population);
        float totalPredators = predators.Sum(p => p.Population);

        // Calculate prey-to-predator ratio for hunting efficiency scaling
        float preyRatio = totalPredators > 0 ? availablePrey / totalPredators : 0f;
        float huntingBonus = CalculateHuntingBonus(preyRatio);
        SimLog($"  Prey ratio: {preyRatio:F1}:1, Hunting bonus: {huntingBonus:+0.00;-0.00;0}");

        // Calculate hunting success and demand for each predator
        float totalActualDemand = 0f;
        float totalRawDemand = 0f;
        float huntingEfficiencySum = 0f;
        int predatorCount = 0;

        foreach (var pred in predators)
        {
            // Calculate hunting success with variance AND prey-ratio bonus
            float variance = (float)((_rng.NextDouble() * 2 - 1) * pred.HuntingVariance);
            float huntingSuccess = pred.HuntingEfficiency + variance + huntingBonus;
            huntingSuccess = Math.Max(0.05f, Math.Min(1f, huntingSuccess)); // Clamp to [0.05, 1.0]
            pred.CurrentHuntingSuccess = huntingSuccess;

            // Raw demand (what they want)
            float rawDemand = pred.Population * pred.EatingAmount * pred.RawThermalPerformance * BiologyStep;
            totalRawDemand += rawDemand;

            // Actual demand (what they can attempt to catch)
            float actualDemand = rawDemand * huntingSuccess;
            totalActualDemand += actualDemand;

            huntingEfficiencySum += huntingSuccess;
            predatorCount++;

            SimLog($"  {pred.FullName}: Hunting={huntingSuccess:P0} (base={pred.HuntingEfficiency:P0}, bonus={huntingBonus:+0.00;-0.00;0}), RawDemand={rawDemand:F1}, ActualDemand={actualDemand:F1}");
        }

        LastAvgHuntingEfficiency = predatorCount > 0 ? huntingEfficiencySum / predatorCount : 1f;
        SimLog($"  Avg Hunting Efficiency: {LastAvgHuntingEfficiency:P0}");

        // Calculate total eaten (capped by available prey)
        float totalEaten = Math.Min(availablePrey, totalActualDemand);
        LastEatenT1 = 0f;  // Will be counted by actual removals

        // FIX: Calculate FedRate based on what was ATTEMPTED (actualDemand), not what was WANTED (rawDemand)
        // If predator catches everything it attempted, it's satisfied (FedRate = 1.0)
        // FedRate only drops if prey is scarce and predator can't catch enough
        float fedRate;
        if (totalActualDemand <= 0f)
            fedRate = 1f;
        else
            fedRate = Math.Min(1f, totalEaten / totalActualDemand);

        // PREY SCARCITY PENALTY: Even if predators catch what they attempt, 
        // searching for scarce prey costs energy, prey quality is lower, etc.
        // This creates negative feedback when predators overpopulate.
        float preyPerPredator = totalPredators > 0 ? availablePrey / totalPredators : 0f;
        float scarcityMultiplier = CalculateScarcityMultiplier(preyPerPredator);
        fedRate *= scarcityMultiplier;

        SimLog($"  Prey per predator: {preyPerPredator:F1}, Scarcity multiplier: {scarcityMultiplier:F2}");

        LastFedRateT2 = fedRate;
        foreach (var pred in predators)
        {
            pred.FedRate = fedRate;
        }

        SimLog($"  Total Eaten: {totalEaten:F1}, FedRate: {fedRate:F3} (eaten/attemptedDemand = {totalEaten:F1}/{totalActualDemand:F1})");

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
    /// Calculate hunting efficiency bonus/penalty based on prey-to-predator ratio.
    /// When prey is abundant, hunting is easier. When prey is scarce, hunting is MUCH harder.
    /// 
    /// This creates strong negative feedback to prevent predator overpopulation:
    /// - As predators grow, ratio drops
    /// - Lower ratio = harder hunting
    /// - Harder hunting = less food = lower FinalPerf = higher natural death + fewer births
    /// </summary>
    private float CalculateHuntingBonus(float preyRatio)
    {
        // Very abundant prey = easier hunting (but not too easy)
        if (preyRatio >= 200f) return 0.15f;  // Extremely abundant: +15%
        if (preyRatio >= 100f) return 0.10f;  // Very abundant: +10%
        if (preyRatio >= 50f) return 0.05f;   // Abundant: +5%
        if (preyRatio >= 20f) return 0f;      // Baseline: balanced ecosystem

        // Scarce prey = MUCH harder hunting (strong negative feedback)
        if (preyRatio >= 10f) return -0.15f;  // Getting crowded: -15%
        if (preyRatio >= 5f) return -0.30f;   // Competitive: -30%
        if (preyRatio >= 2f) return -0.45f;   // Very competitive: -45%
        return -0.55f;                         // Desperate: -55% (ratio < 2:1)
    }

    /// <summary>
    /// Calculate FedRate multiplier based on prey availability per predator.
    /// Even if predators catch what they attempt, searching for scarce prey costs energy.
    /// This creates strong negative feedback when predators overpopulate.
    /// 
    /// Low preyPerPredator → low FedRate → low FinalPerf → higher natural death → predator decline
    /// </summary>
    private float CalculateScarcityMultiplier(float preyPerPredator)
    {
        // Abundant prey per predator = no penalty
        if (preyPerPredator >= 50f) return 1.0f;   // Plenty of prey
        if (preyPerPredator >= 20f) return 0.85f;  // Adequate prey
        if (preyPerPredator >= 10f) return 0.70f;  // Getting scarce
        if (preyPerPredator >= 5f) return 0.50f;   // Scarce - significant hunger
        if (preyPerPredator >= 2f) return 0.35f;   // Very scarce - severe hunger
        return 0.20f;                               // Critical scarcity (< 2 prey per predator)
    }

    /// <summary>
    /// Update Condition (health/energy reserves) for each species.
    /// Condition moves toward RawFinalPerformance asymmetrically:
    ///   - Drains faster than it recovers
    ///   - Drain accelerates up to 5x near lethal temperatures
    ///   - Feeding contributes via FedRate (starving predators drain even at good temps)
    /// </summary>
    private void UpdateCondition(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP) return;

        float target = sp.RawFinalPerformance;  // thermal × fed, no Pmax
        float oldCondition = sp.Condition;

        if (sp.Condition > target)
        {
            // Draining — calculate effective drain rate with acceleration near lethal temps
            float effectiveDrain = ConditionDrainRate;
            if (target < DRAIN_ACCEL_THRESHOLD)
            {
                float severity = 1f - target / DRAIN_ACCEL_THRESHOLD;  // 1.0 at perf=0, 0 at threshold
                effectiveDrain *= 1f + severity * DRAIN_ACCEL_MAX;     // Linear: up to 5x at perf=0
            }
            sp.Condition -= (sp.Condition - target) * effectiveDrain;
        }
        else
        {
            // Recovering — slower than drain
            sp.Condition += (target - sp.Condition) * ConditionRecoveryRate;
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
    /// Apply condition-based death (chronic stress, exhaustion, starvation).
    /// When Condition drops below DeathThreshold, species start dying at DeathRate.
    /// This replaces the old suboptimal-temperature death mechanism.
    /// Uses CONDITION DEATH ACCUMULATOR for fractional death tracking.
    /// </summary>
    private void ApplyConditionDeath(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP) return;
        if (sp.Condition >= sp.DeathThreshold) return;

        float rawDeaths = sp.Population * sp.DeathRate * BiologyStep;

        // Accumulator pattern — chronic decline, gradual
        _conditionDeathAccumulators[sp.FullName] += rawDeaths;
        float accumulated = _conditionDeathAccumulators[sp.FullName];
        int wholeDeaths = (int)Math.Floor(accumulated);
        _conditionDeathAccumulators[sp.FullName] = accumulated - wholeDeaths;
        wholeDeaths = Math.Min(wholeDeaths, (int)sp.Population);

        if (wholeDeaths > 0)
        {
            float oldPop = sp.Population;
            sp.Population = Math.Max(0f, sp.Population - wholeDeaths);

            SimLog($"  {sp.FullName}: CONDITION DEATH - raw={rawDeaths:F2}, accum={accumulated:F2}, deaths={wholeDeaths} (Condition {sp.Condition:F3} < {sp.DeathThreshold}), Pop {oldPop:F0} → {sp.Population:F0}");

            if (sp.Tier == 1) LastConditionDeathsT1 += wholeDeaths;
            else if (sp.Tier == 2) LastConditionDeathsT2 += wholeDeaths;
        }
        else
        {
            SimLog($"  {sp.FullName}: Condition death - raw={rawDeaths:F2}, accum={_conditionDeathAccumulators[sp.FullName]:F2} (no deaths yet, Condition {sp.Condition:F3} < {sp.DeathThreshold})");
        }
    }

    /// <summary>
    /// Apply reproduction with BIRTH ACCUMULATOR, Tier 1 penalty, and carrying capacity.
    /// CARRYING CAPACITY ONLY APPLIES TO TIER 1.
    /// </summary>
    private void ApplyReproduction(SimSpecies sp)
    {
        if (sp.Population < 2f)
        {
            SimLog($"  {sp.FullName}: Cannot reproduce (Pop={sp.Population:F1} < 2)");
            return;
        }

        if (sp.FinalPerformance < sp.ReproThreshold)
        {
            SimLog($"  {sp.FullName}: Cannot reproduce (FinalPerf {sp.FinalPerformance:F3} < {sp.ReproThreshold})");
            return;
        }

        // Calculate base births
        float births = sp.Population * sp.FinalPerformance * sp.ReproductionMultiplier * BiologyStep;

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

        // Carrying capacity (soft limit) - ONLY for Tier 1
        // This represents the resource limit of the environment
        if (UseCarryingCapacity && sp.Tier == 1)
        {
            float tierPop = GetTierPopulation(1);
            float growthFactor = Math.Max(0f, 1f - (tierPop / CarryingCapacityPerTier));
            float oldBirths = births;
            births *= growthFactor;
            SimLog($"  {sp.FullName}: Carrying capacity - tierPop={tierPop:F0}, factor={growthFactor:F3}, births {oldBirths:F2} → {births:F2}");
        }

        // BIRTH ACCUMULATOR
        if (!_birthAccumulators.ContainsKey(sp.FullName))
            _birthAccumulators[sp.FullName] = 0f;

        _birthAccumulators[sp.FullName] += births;
        float accumulated = _birthAccumulators[sp.FullName];

        int wholeBirths = (int)Math.Floor(accumulated);
        _birthAccumulators[sp.FullName] = accumulated - wholeBirths;

        float oldPop = sp.Population;
        sp.Population += wholeBirths;

        SimLog($"  {sp.FullName}: +{births:F2} raw, accum={accumulated:F2}, actual={wholeBirths}, Pop {oldPop:F0} → {sp.Population:F0}");

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