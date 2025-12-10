using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles ecosystem biology: feeding, reproduction, death.
/// 
/// CORRECT SEQUENCE (from specification):
/// 1. Calculate Thermal Performance for all species
/// 2. Feeding (Tier 2 eats Tier 1) → sets FedRate, removes prey PROPORTIONALLY
/// 3. Calculate Final Performance = ThermalPerf × FedRate
/// 4. Death check (thermal death if FinalPerf < DeathThreshold)
/// 5. Density Death check (if enabled and population > threshold)
/// 6. Reproduction check (if FinalPerf >= ReproThreshold AND Pop >= 2)
/// 
/// KEY DESIGN DECISIONS:
/// - Population is FLOAT for precision (allows fractional accumulation)
/// - No rounding during calculations
/// - Species extinct when Population < 1.0
/// - Prey removal is PROPORTIONAL to population share
/// - BiologyStep multiplier applied to demand, deaths, births
/// 
/// POPULATION CONTROL (prevents infinite growth):
/// 
/// 1. CARRYING CAPACITY (Logistic Growth Model)
///    - Reduces birth rate as tier population approaches environmental limit
///    - Formula: births = rawBirths × (1 - tierPop/carryingCapacity)
///    - At 50% capacity → 50% birth rate; At 100% capacity → 0 births
///    - Simulates: limited food, limited space, intra-species competition
/// 
/// 2. DENSITY-DEPENDENT MORTALITY
///    - Adds extra deaths when tier population exceeds comfortable threshold
///    - Applies even to thermally-healthy species
///    - Death rate scales linearly with how far over threshold
///    - Simulates: disease spread, overcrowding stress, resource depletion
/// </summary>
public class EcosystemSimulator
{
    public List<SimSpecies> Species { get; private set; }
    public int BiologyStep { get; set; } = 1;  // Default: daily biology

    // ==================== POPULATION CONTROL SETTINGS ====================
    // These are set from SimulationConfig before running

    /// <summary>
    /// Enable carrying capacity (logistic growth model).
    /// Reduces birth rate as population approaches CarryingCapacityPerTier.
    /// </summary>
    public bool UseCarryingCapacity { get; set; } = true;

    /// <summary>
    /// Maximum sustainable population per tier (all variants combined).
    /// At this population, birth rate drops to 0.
    /// </summary>
    public float CarryingCapacityPerTier { get; set; } = 5000f;

    /// <summary>
    /// Enable density-dependent mortality.
    /// Adds extra deaths when population exceeds DensityDeathThreshold.
    /// </summary>
    public bool UseDensityDeath { get; set; } = true;

    /// <summary>
    /// Population threshold (per tier) above which density deaths begin.
    /// </summary>
    public float DensityDeathThreshold { get; set; } = 4000f;

    /// <summary>
    /// Maximum daily death rate from overcrowding.
    /// Reached when population is 2× the threshold.
    /// </summary>
    public float MaxDensityDeathRate { get; set; } = 0.1f;

    // ==================== TRACKING ====================

    // Tracking from last biology step (FLOAT values)
    public float LastTotalEaten { get; private set; } = 0f;
    public float LastTotalDeaths { get; private set; } = 0f;
    public float LastTotalBirths { get; private set; } = 0f;
    public float LastFedRate { get; private set; } = 1f;
    public float LastDensityDeaths { get; private set; } = 0f;  // Track density deaths separately

    // Constants from specification
    private const float DEATH_THRESHOLD = 0.3f;
    private const float REPRO_THRESHOLD = 0.25f;
    private const float TIER1_PENALTY = 0.85f;      // 15% reduction when no predators
    private const float EXTINCTION_THRESHOLD = 1.0f; // Population below this = extinct

    public EcosystemSimulator()
    {
        Species = new List<SimSpecies>();
    }

    /// <summary>
    /// Initialize species from SpeciesDatabase
    /// </summary>
    public void InitializeFromDatabase(SpeciesDatabase database)
    {
        Species.Clear();

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
                Population = data.count,  // Float population
                EatingAmount = data.eatingAmount,
                ReproductionMultiplier = data.reproductionMultiplier,
                DeathThreshold = data.deathThreshold,
                DeathRate = data.deathRate,
                MinimumDeaths = data.minimumDeaths,
                ReproThreshold = data.reproThreshold,
                OptimalTempK = data.optimalTempK,
                ArrhenBreadth = data.arrhenBreadth,
                ArrhenLower = data.arrhenLower,
                ArrhenUpper = data.arrhenUpper,
                LowerBoundK = data.lowerBoundK,
                UpperBoundK = data.upperBoundK
            };

            Species.Add(simSpecies);
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

    /// <summary>
    /// Initialize with default species (fallback only)
    /// </summary>
    public void InitializeDefaultSpecies()
    {
        Species.Clear();

        // Tier 1: Hexapod (4 of each variant)
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Arctic, 4f));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Common, 4f));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Tropical, 4f));

        // Tier 2: Sheplik (2 of each variant)
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Arctic, 2f));
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Common, 2f));
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Tropical, 2f));
    }

    /// <summary>
    /// Run one biology step at the given temperature.
    /// Follows the exact sequence from specification.
    /// </summary>
    public void ProcessBiologyStep(float temperature)
    {
        // Reset tracking
        LastTotalEaten = 0f;
        LastTotalDeaths = 0f;
        LastTotalBirths = 0f;
        LastFedRate = 1f;
        LastDensityDeaths = 0f;

        Debug.Log($"=== Biology Step at {temperature:F2}°C (BiologyStep={BiologyStep}) ===");

        // Log population control settings
        if (UseCarryingCapacity || UseDensityDeath)
        {
            Debug.Log($"  Population Control: CarryingCap={UseCarryingCapacity} ({CarryingCapacityPerTier}), DensityDeath={UseDensityDeath} (threshold={DensityDeathThreshold}, maxRate={MaxDensityDeathRate:P0})");
        }

        // ========== STEP 1: THERMAL PERFORMANCE ==========
        Debug.Log("--- Step 1: Thermal Performance ---");
        foreach (var sp in Species)
        {
            sp.ThermalPerformance = sp.CalculatePerformance(temperature);
            sp.FedRate = 1f;  // Initialize (Tier 1 stays at 1.0)
            Debug.Log($"  {sp.FullName}: Pop={sp.Population:F2}, ThermalPerf={sp.ThermalPerformance:F3}");
        }

        // ========== STEP 2: FEEDING (Tier 2 eats Tier 1) ==========
        Debug.Log("--- Step 2: Feeding ---");
        ProcessFeeding(tier: 2, preyTier: 1);

        // Future: Tier 3 eats Tier 2
        // ProcessFeeding(tier: 3, preyTier: 2);

        // ========== STEP 3: FINAL PERFORMANCE ==========
        Debug.Log("--- Step 3: Final Performance ---");
        foreach (var sp in Species)
        {
            sp.FinalPerformance = sp.ThermalPerformance * sp.FedRate;
            Debug.Log($"  {sp.FullName}: FinalPerf = {sp.ThermalPerformance:F3} × {sp.FedRate:F3} = {sp.FinalPerformance:F3}");
        }

        // ========== STEP 4: THERMAL DEATH ==========
        Debug.Log("--- Step 4: Thermal Death Check ---");
        foreach (var sp in Species)
        {
            ApplyThermalDeath(sp);
        }

        // ========== STEP 5: DENSITY-DEPENDENT DEATH ==========
        if (UseDensityDeath)
        {
            Debug.Log("--- Step 5: Density Death Check ---");
            ApplyDensityDeathToTier(1);
            ApplyDensityDeathToTier(2);
        }

        // ========== STEP 6: REPRODUCTION ==========
        Debug.Log("--- Step 6: Reproduction ---");
        foreach (var sp in Species)
        {
            ApplyReproduction(sp);
        }

        Debug.Log($"--- End Biology Step: Eaten={LastTotalEaten:F2}, Deaths={LastTotalDeaths:F2} (density={LastDensityDeaths:F2}), Births={LastTotalBirths:F2}, FedRate={LastFedRate:F2} ---");
    }

    /// <summary>
    /// Process feeding for a predator tier eating prey tier.
    /// Uses PROPORTIONAL prey removal (no rounding).
    /// </summary>
    private void ProcessFeeding(int tier, int preyTier)
    {
        var predators = Species.Where(s => s.Tier == tier && s.Population >= EXTINCTION_THRESHOLD).ToList();
        var prey = Species.Where(s => s.Tier == preyTier && s.Population >= EXTINCTION_THRESHOLD).ToList();

        Debug.Log($"  Predators (Tier {tier}): {predators.Count} species alive");
        Debug.Log($"  Prey (Tier {preyTier}): {prey.Count} species alive");

        if (predators.Count == 0 || prey.Count == 0)
        {
            // No feeding possible
            foreach (var p in predators) p.FedRate = 0f;
            LastFedRate = predators.Count > 0 ? 0f : 1f;
            Debug.Log($"  No feeding (predators={predators.Count}, prey={prey.Count})");
            return;
        }

        // Calculate total demand from ALL predators
        // Demand = Population × EatingAmount × ThermalPerf × BiologyStep
        float totalDemand = 0f;
        foreach (var pred in predators)
        {
            float demand = pred.Population * pred.EatingAmount * pred.ThermalPerformance * BiologyStep;
            Debug.Log($"  {pred.FullName}: {pred.Population:F2} × {pred.EatingAmount} × {pred.ThermalPerformance:F3} × {BiologyStep} = {demand:F2} demand");
            totalDemand += demand;
        }

        // Total available prey
        float availablePrey = prey.Sum(p => p.Population);
        Debug.Log($"  Total Demand: {totalDemand:F2}, Available Prey: {availablePrey:F2}");

        // Calculate FedRate
        float fedRate;
        if (totalDemand <= 0f)
        {
            fedRate = 1f;
        }
        else if (availablePrey <= 0f)
        {
            fedRate = 0f;
        }
        else
        {
            fedRate = Math.Min(1f, availablePrey / totalDemand);
        }

        LastFedRate = fedRate;
        Debug.Log($"  FedRate: min(1.0, {availablePrey:F2} / {totalDemand:F2}) = {fedRate:F3}");

        // Apply FedRate to all predators of this tier
        foreach (var pred in predators)
        {
            pred.FedRate = fedRate;
        }

        // Calculate total eaten (float, no rounding)
        float totalEaten = Math.Min(availablePrey, totalDemand);
        LastTotalEaten = totalEaten;

        // Remove prey PROPORTIONALLY based on population share (no rounding)
        if (totalEaten > 0f && availablePrey > 0f)
        {
            Debug.Log($"  Prey removal (proportional):");
            foreach (var p in prey)
            {
                float share = p.Population / availablePrey;
                float removed = totalEaten * share;
                float oldPop = p.Population;
                p.Population = Math.Max(0f, p.Population - removed);
                Debug.Log($"    {p.FullName}: share={share:F3}, removed={removed:F2}, pop {oldPop:F2} → {p.Population:F2}");
            }
        }
    }

    /// <summary>
    /// Apply thermal death if FinalPerformance < DeathThreshold.
    /// Deaths = max(MinDeaths, Population × DeathRate × BiologyStep)
    /// No rounding - deaths is float.
    /// </summary>
    private void ApplyThermalDeath(SimSpecies sp)
    {
        if (sp.Population < EXTINCTION_THRESHOLD)
        {
            Debug.Log($"  {sp.FullName}: Already extinct (Pop={sp.Population:F2})");
            return;
        }

        if (sp.FinalPerformance >= sp.DeathThreshold)
        {
            Debug.Log($"  {sp.FullName}: SURVIVES (FinalPerf {sp.FinalPerformance:F3} >= {sp.DeathThreshold})");
            return;
        }

        // Calculate deaths (float, no rounding)
        float deaths = Math.Max(sp.MinimumDeaths, sp.Population * sp.DeathRate * BiologyStep);
        deaths = Math.Min(deaths, sp.Population);  // Cannot exceed population

        float oldPop = sp.Population;
        sp.Population = Math.Max(0f, sp.Population - deaths);

        Debug.Log($"  {sp.FullName}: THERMAL DEATH - {deaths:F2} deaths (FinalPerf {sp.FinalPerformance:F3} < {sp.DeathThreshold}), Pop {oldPop:F2} → {sp.Population:F2}");

        LastTotalDeaths += deaths;
    }

    /// <summary>
    /// Apply density-dependent mortality to a tier.
    /// This applies to ALL species in the tier (even thermally-healthy ones)
    /// when the total tier population exceeds DensityDeathThreshold.
    /// 
    /// Simulates: disease spread, overcrowding stress, resource depletion.
    /// 
    /// Formula:
    ///   excess = (tierPop - threshold) / threshold
    ///   deathRate = min(maxRate, excess × maxRate)
    ///   deaths per species = species.Population × deathRate × BiologyStep
    /// 
    /// At 1× threshold: 0% density death
    /// At 2× threshold: maxRate density death (e.g., 10%)
    /// </summary>
    private void ApplyDensityDeathToTier(int tier)
    {
        float tierPop = GetTierPopulation(tier);

        if (tierPop <= DensityDeathThreshold)
        {
            Debug.Log($"  Tier {tier}: Pop {tierPop:F2} <= threshold {DensityDeathThreshold:F0} → No density deaths");
            return;
        }

        // Calculate death rate based on how far over threshold
        // At 2× threshold, death rate = MaxDensityDeathRate
        float excessRatio = (tierPop - DensityDeathThreshold) / DensityDeathThreshold;
        float densityDeathRate = Math.Min(MaxDensityDeathRate, excessRatio * MaxDensityDeathRate);

        Debug.Log($"  Tier {tier}: Pop {tierPop:F2} > threshold {DensityDeathThreshold:F0} → excessRatio={excessRatio:F2}, deathRate={densityDeathRate:P1}");

        // Apply to each species in tier (proportional to their share)
        var tierSpecies = Species.Where(s => s.Tier == tier && s.Population >= EXTINCTION_THRESHOLD).ToList();
        foreach (var sp in tierSpecies)
        {
            float deaths = sp.Population * densityDeathRate * BiologyStep;
            deaths = Math.Min(deaths, sp.Population - 0.1f);  // Leave at least 0.1 to avoid instant extinction
            deaths = Math.Max(0f, deaths);

            if (deaths > 0.01f)  // Only log meaningful deaths
            {
                float oldPop = sp.Population;
                sp.Population = Math.Max(0f, sp.Population - deaths);

                Debug.Log($"    {sp.FullName}: DENSITY DEATH - {deaths:F2} deaths, Pop {oldPop:F2} → {sp.Population:F2}");

                LastTotalDeaths += deaths;
                LastDensityDeaths += deaths;
            }
        }
    }

    /// <summary>
    /// Apply reproduction if FinalPerformance >= ReproThreshold AND Population >= 2.
    /// 
    /// Base formula: Births = Population × FinalPerf × ReproMult × BiologyStep
    /// 
    /// With Carrying Capacity enabled:
    ///   growthFactor = max(0, 1 - tierPop/carryingCapacity)
    ///   Births = baseBirths × growthFactor
    /// 
    /// This creates logistic growth where birth rate decreases as population
    /// approaches the environmental carrying capacity.
    /// </summary>
    private void ApplyReproduction(SimSpecies sp)
    {
        if (sp.Population < 2f)
        {
            Debug.Log($"  {sp.FullName}: Cannot reproduce (Pop={sp.Population:F2} < 2)");
            return;
        }

        if (sp.FinalPerformance < sp.ReproThreshold)
        {
            Debug.Log($"  {sp.FullName}: Cannot reproduce (FinalPerf {sp.FinalPerformance:F3} < {sp.ReproThreshold})");
            return;
        }

        // Calculate base births (float, no rounding)
        float births = sp.Population * sp.FinalPerformance * sp.ReproductionMultiplier * BiologyStep;

        // Special rule for Tier 1: 15% reduction if no Tier 2 exists
        if (sp.Tier == 1)
        {
            float tier2Pop = GetTierPopulation(2);
            if (tier2Pop < EXTINCTION_THRESHOLD)
            {
                float originalBirths = births;
                births *= TIER1_PENALTY;
                Debug.Log($"  {sp.FullName}: Tier 1 penalty (no predators): {originalBirths:F2} × 0.85 = {births:F2}");
            }
        }

        // Apply carrying capacity if enabled
        if (UseCarryingCapacity)
        {
            float tierPop = GetTierPopulation(sp.Tier);
            float growthFactor = 1f - (tierPop / CarryingCapacityPerTier);
            growthFactor = Math.Max(0f, growthFactor);  // Cannot go negative

            if (growthFactor < 1f)
            {
                float originalBirths = births;
                births *= growthFactor;
                Debug.Log($"  {sp.FullName}: Carrying capacity ({tierPop:F0}/{CarryingCapacityPerTier:F0}={tierPop/CarryingCapacityPerTier:P0}): {originalBirths:F2} × {growthFactor:F3} = {births:F2}");
            }
        }

        float oldPop = sp.Population;
        sp.Population += births;

        Debug.Log($"  {sp.FullName}: +{births:F2} births, Pop {oldPop:F2} → {sp.Population:F2}");

        LastTotalBirths += births;
    }

    // ========== POPULATION QUERIES ==========

    public float GetTier1Population()
    {
        return GetTierPopulation(1);
    }

    public float GetTier2Population()
    {
        return GetTierPopulation(2);
    }

    public float GetTierPopulation(int tier)
    {
        return Species.Where(s => s.Tier == tier).Sum(s => s.Population);
    }

    public float GetVariantPopulation(int tier, ThermalVariant variant)
    {
        return Species.Where(s => s.Tier == tier && s.Variant == variant).Sum(s => s.Population);
    }

    /// <summary>
    /// Check if ecosystem has crashed (any tier below extinction threshold)
    /// </summary>
    public bool HasCrashed()
    {
        return GetTier1Population() < EXTINCTION_THRESHOLD || GetTier2Population() < EXTINCTION_THRESHOLD;
    }

    /// <summary>
    /// Get which tier crashed (for logging)
    /// </summary>
    public int GetCrashedTier()
    {
        if (GetTier1Population() < EXTINCTION_THRESHOLD) return 1;
        if (GetTier2Population() < EXTINCTION_THRESHOLD) return 2;
        return -1;
    }
}
