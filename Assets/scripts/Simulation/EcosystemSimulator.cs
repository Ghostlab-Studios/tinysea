using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TinySea Ecosystem Simulator v5
/// 
/// BIOLOGY SEQUENCE (7 steps):
/// 1. Thermal Performance - Arrhenius formula
/// 2. Feeding/Predation - With hunting efficiency + PREDATION ACCUMULATOR
/// 3. Final Performance - ThermalPerf × FedRate
/// 4. Thermal Death - If FinalPerf less than 0.3 (MinimumDeaths=1, NO accumulator)
/// 5. Reproduction - With BIRTH ACCUMULATOR + Tier 1 penalty when no predators
/// 6. Natural Death - Performance-scaled + NATURAL DEATH ACCUMULATOR
/// 7. Population Rounding - All populations become integers
/// 
/// ACCUMULATORS:
/// - Birth: Fractional births carry over (allows slow-reproducing Tier 2 to grow)
/// - Predation: Fractional prey deaths carry over (rare variants eventually eaten)
/// - Natural Death: Fractional deaths carry over (small populations eventually die)
/// 
/// NO DENSITY DEATH - Removed in v5
/// NO CARRYING CAPACITY - Removed in v5
/// </summary>
public class EcosystemSimulator
{
    public List<SimSpecies> Species { get; private set; }
    public int BiologyStep { get; set; } = 1;

    // Random number generator
    private System.Random _rng;

    // ==================== ACCUMULATORS ====================
    private Dictionary<string, float> _birthAccumulators = new Dictionary<string, float>();
    private Dictionary<string, float> _naturalDeathAccumulators = new Dictionary<string, float>();
    private Dictionary<string, float> _predationAccumulators = new Dictionary<string, float>();

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

    // Tier 2
    public float LastTempDeathsT2 { get; private set; } = 0f;
    public float LastNaturalDeathsT2 { get; private set; } = 0f;
    public float LastBirthsT2 { get; private set; } = 0f;
    public float LastFedRateT2 { get; private set; } = 1f;
    public float LastAvgHuntingEfficiency { get; private set; } = 1f;

    // Combined
    public float LastTotalDeaths => LastEatenT1 + LastTempDeathsT1 + LastTempDeathsT2 +
                                    LastNaturalDeathsT1 + LastNaturalDeathsT2;
    public float LastTotalBirths => LastBirthsT1 + LastBirthsT2;

    // ==================== ACCUMULATOR TOTALS (for CSV output) ====================
    public float BirthAccumT1 { get; private set; } = 0f;
    public float BirthAccumT2 { get; private set; } = 0f;
    public float NaturalDeathAccumT1 { get; private set; } = 0f;
    public float NaturalDeathAccumT2 { get; private set; } = 0f;
    public float PredationAccumT1 { get; private set; } = 0f;

    // ==================== CARRYING CAPACITY (Soft Limit) ====================
    public bool UseCarryingCapacity { get; set; } = true;
    public float CarryingCapacityPerTier { get; set; } = 5000f;

    // ==================== CONSTANTS ====================
    private const float MIN_ALIVE_POP = 1.0f;

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
    /// Initialize species from SpeciesDatabase
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

        foreach (var data in database.speciesList)
        {
            var simSpecies = new SimSpecies
            {
                Name = data.speciesName.ToString(),
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
                UpperBoundK = data.upperBoundK
            };

            Species.Add(simSpecies);
            InitializeAccumulators(simSpecies.FullName);

            Debug.Log($"Loaded: {simSpecies.FullName} (Tier {simSpecies.Tier}) - Pop: {simSpecies.Population}");
        }

        Debug.Log($"Total species loaded: {Species.Count}");
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
    }

    private void InitializeAccumulators(string fullName)
    {
        _birthAccumulators[fullName] = 0f;
        _naturalDeathAccumulators[fullName] = 0f;
        _predationAccumulators[fullName] = 0f;
    }

    /// <summary>
    /// Initialize with default species (fallback only)
    /// </summary>
    public void InitializeDefaultSpecies()
    {
        Species.Clear();
        ClearAccumulators();

        // Tier 1: Hexapod (4 of each variant)
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Arctic, 4f));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Common, 4f));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Tropical, 4f));

        // Tier 2: Sheplik (2 of each variant)
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Arctic, 2f));
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Common, 2f));
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Tropical, 2f));

        foreach (var sp in Species)
        {
            InitializeAccumulators(sp.FullName);
        }
    }

    /// <summary>
    /// Run one biology step at the given temperature.
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
        LastNaturalDeathsT1 = 0f;
        LastNaturalDeathsT2 = 0f;
        LastBirthsT1 = 0f;
        LastBirthsT2 = 0f;
        LastFedRateT2 = 1f;
        LastAvgHuntingEfficiency = 1f;

        Debug.Log($"=== Biology Step at {temperature:F2}°C (BiologyStep={BiologyStep}) ===");
        Debug.Log($"  START: T1={StartPopT1:F0}, T2={StartPopT2:F0}");

        // ========== STEP 1: THERMAL PERFORMANCE ==========
        Debug.Log("--- Step 1: Thermal Performance ---");
        foreach (var sp in Species)
        {
            sp.ThermalPerformance = sp.CalculatePerformance(temperature);
            sp.FedRate = 1f;
            sp.CurrentHuntingSuccess = 1f;
            Debug.Log($"  {sp.FullName}: Pop={sp.Population:F0}, ThermalPerf={sp.ThermalPerformance:F3}");
        }

        // ========== STEP 2: FEEDING (with predation accumulator) ==========
        Debug.Log("--- Step 2: Feeding/Predation ---");
        ProcessFeedingWithAccumulator();

        // ========== STEP 3: FINAL PERFORMANCE ==========
        Debug.Log("--- Step 3: Final Performance ---");
        foreach (var sp in Species)
        {
            sp.FinalPerformance = sp.ThermalPerformance * sp.FedRate;
            Debug.Log($"  {sp.FullName}: FinalPerf = {sp.ThermalPerformance:F3} × {sp.FedRate:F3} = {sp.FinalPerformance:F3}");
        }

        // ========== STEP 4: THERMAL DEATH ==========
        Debug.Log("--- Step 4: Thermal Death ---");
        foreach (var sp in Species)
        {
            ApplyThermalDeath(sp);
        }

        // ========== STEP 5: REPRODUCTION (with birth accumulator) ==========
        Debug.Log("--- Step 5: Reproduction ---");
        foreach (var sp in Species)
        {
            ApplyReproduction(sp);
        }

        // ========== STEP 6: NATURAL DEATH (with accumulator + performance scaling) ==========
        Debug.Log("--- Step 6: Natural Death ---");
        foreach (var sp in Species)
        {
            ApplyNaturalDeathWithAccumulator(sp);
        }

        // ========== STEP 7: POPULATION ROUNDING ==========
        Debug.Log("--- Step 7: Population Rounding ---");
        foreach (var sp in Species)
        {
            float oldPop = sp.Population;
            sp.Population = (float)Math.Round(sp.Population, MidpointRounding.AwayFromZero);
            if (Math.Abs(oldPop - sp.Population) > 0.01f)
            {
                Debug.Log($"  {sp.FullName}: {oldPop:F2} → {sp.Population:F0}");
            }
        }

        // Record end populations and accumulator totals
        EndPopT1 = GetTier1Population();
        EndPopT2 = GetTier2Population();
        UpdateAccumulatorTotals();

        Debug.Log($"  END: T1={EndPopT1:F0}, T2={EndPopT2:F0}");
        Debug.Log($"  Deaths: Eaten={LastEatenT1:F0}, Temp={LastTempDeathsT1 + LastTempDeathsT2:F0}, Natural={LastNaturalDeathsT1 + LastNaturalDeathsT2:F0}");
        Debug.Log($"  Births: T1={LastBirthsT1:F0}, T2={LastBirthsT2:F0}");
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

        Debug.Log($"  Predators: {predators.Count} species, {predators.Sum(p => p.Population):F0} total");
        Debug.Log($"  Prey: {prey.Count} species, {prey.Sum(p => p.Population):F0} total");

        if (predators.Count == 0 || prey.Count == 0)
        {
            foreach (var p in predators) p.FedRate = 0f;
            LastFedRateT2 = predators.Count > 0 ? 0f : 1f;
            LastAvgHuntingEfficiency = 1f;
            Debug.Log($"  No feeding (predators={predators.Count}, prey={prey.Count})");
            return;
        }

        float availablePrey = prey.Sum(p => p.Population);
        float totalPredators = predators.Sum(p => p.Population);

        // Calculate prey-to-predator ratio for hunting efficiency scaling
        float preyRatio = totalPredators > 0 ? availablePrey / totalPredators : 0f;
        float huntingBonus = CalculateHuntingBonus(preyRatio);
        Debug.Log($"  Prey ratio: {preyRatio:F1}:1, Hunting bonus: {huntingBonus:+0.00;-0.00;0}");

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
            float rawDemand = pred.Population * pred.EatingAmount * pred.ThermalPerformance * BiologyStep;
            totalRawDemand += rawDemand;

            // Actual demand (what they can attempt to catch)
            float actualDemand = rawDemand * huntingSuccess;
            totalActualDemand += actualDemand;

            huntingEfficiencySum += huntingSuccess;
            predatorCount++;

            Debug.Log($"  {pred.FullName}: Hunting={huntingSuccess:P0} (base={pred.HuntingEfficiency:P0}, bonus={huntingBonus:+0.00;-0.00;0}), RawDemand={rawDemand:F1}, ActualDemand={actualDemand:F1}");
        }

        LastAvgHuntingEfficiency = predatorCount > 0 ? huntingEfficiencySum / predatorCount : 1f;
        Debug.Log($"  Avg Hunting Efficiency: {LastAvgHuntingEfficiency:P0}");

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
        
        Debug.Log($"  Prey per predator: {preyPerPredator:F1}, Scarcity multiplier: {scarcityMultiplier:F2}");

        LastFedRateT2 = fedRate;
        foreach (var pred in predators)
        {
            pred.FedRate = fedRate;
        }

        Debug.Log($"  Total Eaten: {totalEaten:F1}, FedRate: {fedRate:F3} (eaten/attemptedDemand = {totalEaten:F1}/{totalActualDemand:F1})");

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

                Debug.Log($"    {p.FullName}: share={share:F3}, lost={preyLost:F2}, accum={accumulated:F2}, deaths={wholeDeaths}, Pop {oldPop:F0} → {p.Population:F0}");
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
    /// Apply thermal death if FinalPerformance less than DeathThreshold.
    /// NO ACCUMULATOR - MinimumDeaths ensures at least 1 dies when triggered.
    /// </summary>
    private void ApplyThermalDeath(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP)
        {
            Debug.Log($"  {sp.FullName}: Already extinct");
            return;
        }

        if (sp.FinalPerformance >= sp.DeathThreshold)
        {
            Debug.Log($"  {sp.FullName}: SURVIVES (FinalPerf {sp.FinalPerformance:F3} >= {sp.DeathThreshold})");
            return;
        }

        // Species is stressed - calculate deaths
        float deaths = Math.Max(sp.MinimumDeaths, sp.Population * sp.DeathRate * BiologyStep);
        deaths = Math.Min(deaths, sp.Population);

        float oldPop = sp.Population;
        sp.Population = Math.Max(0f, sp.Population - deaths);

        Debug.Log($"  {sp.FullName}: THERMAL DEATH - {deaths:F1} deaths (FinalPerf {sp.FinalPerformance:F3} < {sp.DeathThreshold}), Pop {oldPop:F0} → {sp.Population:F0}");

        // Track by tier
        if (sp.Tier == 1)
            LastTempDeathsT1 += deaths;
        else if (sp.Tier == 2)
            LastTempDeathsT2 += deaths;
    }

    /// <summary>
    /// Apply reproduction with BIRTH ACCUMULATOR, Tier 1 penalty, and carrying capacity.
    /// </summary>
    private void ApplyReproduction(SimSpecies sp)
    {
        if (sp.Population < 2f)
        {
            Debug.Log($"  {sp.FullName}: Cannot reproduce (Pop={sp.Population:F1} < 2)");
            return;
        }

        if (sp.FinalPerformance < sp.ReproThreshold)
        {
            Debug.Log($"  {sp.FullName}: Cannot reproduce (FinalPerf {sp.FinalPerformance:F3} < {sp.ReproThreshold})");
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
                Debug.Log($"  {sp.FullName}: No predator penalty applied ({SimSpecies.NO_PREDATOR_PENALTY:P0})");
            }
        }

        // Carrying capacity (soft limit) - reduces birth rate as population approaches limit
        if (UseCarryingCapacity)
        {
            float tierPop = GetTierPopulation(sp.Tier);
            float growthFactor = Math.Max(0f, 1f - (tierPop / CarryingCapacityPerTier));
            float oldBirths = births;
            births *= growthFactor;
            Debug.Log($"  {sp.FullName}: Carrying capacity - tierPop={tierPop:F0}, factor={growthFactor:F3}, births {oldBirths:F2} → {births:F2}");
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

        Debug.Log($"  {sp.FullName}: +{births:F2} raw, accum={accumulated:F2}, actual={wholeBirths}, Pop {oldPop:F0} → {sp.Population:F0}");

        // Track by tier
        if (sp.Tier == 1)
            LastBirthsT1 += wholeBirths;
        else if (sp.Tier == 2)
            LastBirthsT2 += wholeBirths;
    }

    /// <summary>
    /// Apply natural death with PERFORMANCE SCALING and ACCUMULATOR.
    /// Creatures with poor performance have higher natural mortality.
    /// </summary>
    private void ApplyNaturalDeathWithAccumulator(SimSpecies sp)
    {
        if (sp.Population < MIN_ALIVE_POP)
        {
            return;
        }

        // Calculate death rate with variance
        float variance = (float)((_rng.NextDouble() * 2 - 1) * sp.NaturalDeathVariance);
        float baseRate = Math.Max(0f, sp.NaturalDeathRate + variance);

        // Performance scaling with division-by-zero safeguard
        float safeFinalPerf = Math.Max(SimSpecies.MIN_FINAL_PERF_FOR_NATURAL_DEATH, sp.FinalPerformance);
        float effectiveRate = baseRate * (1f / safeFinalPerf);

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

            Debug.Log($"  {sp.FullName}: NATURAL DEATH - baseRate={baseRate:P1}, effectiveRate={effectiveRate:P1} (perf={safeFinalPerf:F2}), raw={deaths:F2}, accum={accumulated:F2}, deaths={wholeDeaths}, Pop {oldPop:F0} → {sp.Population:F0}");

            if (sp.Tier == 1)
                LastNaturalDeathsT1 += wholeDeaths;
            else if (sp.Tier == 2)
                LastNaturalDeathsT2 += wholeDeaths;
        }
        else
        {
            Debug.Log($"  {sp.FullName}: Natural death - effectiveRate={effectiveRate:P1}, raw={deaths:F2}, accum={_naturalDeathAccumulators[sp.FullName]:F2} (no deaths yet)");
        }
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
        }
    }

    // ==================== POPULATION QUERIES ====================

    public float GetTier1Population() => Species.Where(s => s.Tier == 1).Sum(s => s.Population);
    public float GetTier2Population() => Species.Where(s => s.Tier == 2).Sum(s => s.Population);
    public float GetTierPopulation(int tier) => Species.Where(s => s.Tier == tier).Sum(s => s.Population);
    public float GetVariantPopulation(int tier, ThermalVariant variant) =>
        Species.Where(s => s.Tier == tier && s.Variant == variant).Sum(s => s.Population);

    public bool HasCrashed() => GetTier1Population() == 0 || GetTier2Population() == 0;

    public int GetCrashedTier()
    {
        if (GetTier1Population() == 0) return 1;
        if (GetTier2Population() == 0) return 2;
        return -1;
    }
}
