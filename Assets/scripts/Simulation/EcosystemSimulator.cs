using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles ecosystem biology: feeding, reproduction, death.
/// 
/// CORRECT ORDER:
/// 1. Calculate thermal performance
/// 2. Apply temperature deaths FIRST
/// 3. Tier 2 feeds on Tier 1 (SAME VARIANT: Arctic→Arctic, Common→Common, Tropical→Tropical)
/// 4. Apply reproduction for survivors
/// </summary>
public class EcosystemSimulator
{
    public List<SimSpecies> Species { get; private set; }
    public int DaysPerStep { get; set; } = 5;

    // Tracking from last biology step (integers)
    public int LastTotalEaten { get; private set; } = 0;
    public int LastTotalTempDeaths { get; private set; } = 0;
    public int LastTotalBirths { get; private set; } = 0;
    public float LastFedRate { get; private set; } = 1f;

    public EcosystemSimulator()
    {
        Species = new List<SimSpecies>();
    }

    /// <summary>
    /// Initialize species from SpeciesDatabase (from config)
    /// </summary>
    public void InitializeFromDatabase(SpeciesDatabase database)
    {
        Species.Clear();

        if (database == null || database.speciesList == null)
        {
            UnityEngine.Debug.LogError("SpeciesDatabase is null or empty!");
            return;
        }

        foreach (var data in database.speciesList)
        {
            // Convert SpeciesData to SimSpecies
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
                OptimalTempK = data.optimalTempK,
                ArrhenBreadth = data.arrhenBreadth,
                ArrhenLower = data.arrhenLower,
                ArrhenUpper = data.arrhenUpper,
                LowerBoundK = data.lowerBoundK,
                UpperBoundK = data.upperBoundK
            };

            Species.Add(simSpecies);
            UnityEngine.Debug.Log($"Loaded: {simSpecies.Name} {simSpecies.Variant} (Tier {simSpecies.Tier}) - Count: {simSpecies.Population}");
        }

        UnityEngine.Debug.Log($"Total species loaded: {Species.Count}");
    }

    /// <summary>
    /// Convert Unity SpeciesVariant enum to our ThermalVariant enum
    /// </summary>
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
    /// Initialize with default species (2 of each variant) - FALLBACK ONLY
    /// </summary>
    public void InitializeDefaultSpecies()
    {
        Species.Clear();

        // Tier 1: Hexapod (prey) - 2 of each variant = 6 total
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Arctic, 2));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Common, 2));
        Species.Add(SimSpecies.CreateHexapod(ThermalVariant.Tropical, 2));

        // Tier 2: Shelpik (predator) - 2 of each variant = 6 total
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Arctic, 2));
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Common, 2));
        Species.Add(SimSpecies.CreateShelpik(ThermalVariant.Tropical, 2));
    }

    /// <summary>
    /// Run one biology step at the given temperature.
    /// </summary>
    public void ProcessBiologyStep(float temperature)
    {
        // Reset tracking
        LastTotalEaten = 0;
        LastTotalTempDeaths = 0;
        LastTotalBirths = 0;
        LastFedRate = 1f;

        // STEP 1: Calculate thermal performance for each species
        foreach (var sp in Species)
        {
            sp.ThermalPerformance = sp.CalculatePerformance(temperature);
            sp.FedRate = 1f;
        }

        // STEP 2: Apply temperature deaths FIRST (before feeding)
        foreach (var sp in Species)
        {
            ApplyTemperatureDeath(sp);
        }

        // STEP 3: Tier 2 feeds on Tier 1 (ANY Tier 2 can eat ANY Tier 1)
        ProcessPoolFeeding();

        // STEP 4: Apply reproduction for ALL survivors (both Tier 1 and Tier 2)
        foreach (var sp in Species)
        {
            ApplyReproduction(sp);
        }
    }

    /// <summary>
    /// Apply temperature death - if performance below threshold, species dies
    /// </summary>
    private void ApplyTemperatureDeath(SimSpecies sp)
    {
        if (sp.Population <= 0) return;
        if (sp.ThermalPerformance >= sp.DeathThreshold) return;

        // Deaths = max(minDeaths, population × deathRate × days)
        float deathsFloat = Math.Max(sp.MinimumDeaths, sp.Population * sp.DeathRate * DaysPerStep);
        int deaths = (int)Math.Ceiling(deathsFloat);
        deaths = Math.Min(deaths, (int)sp.Population);
        
        sp.Population = Math.Max(0f, sp.Population - deaths);
        LastTotalTempDeaths += deaths;
    }

    /// <summary>
    /// Pool-based feeding: ANY surviving Tier 2 can eat ANY surviving Tier 1
    /// </summary>
    private void ProcessPoolFeeding()
    {
        var predators = Species.Where(s => s.Tier == 2 && s.Population > 0).ToList();
        var prey = Species.Where(s => s.Tier == 1 && s.Population > 0).ToList();

        if (predators.Count == 0 || prey.Count == 0)
        {
            foreach (var p in predators) p.FedRate = 0f;
            LastFedRate = 0f;
            return;
        }

        // Calculate total demand from ALL surviving predators
        float totalDemand = 0f;
        foreach (var pred in predators)
        {
            totalDemand += pred.Population * pred.EatingAmount * pred.ThermalPerformance * DaysPerStep;
        }

        // Total available prey (survivors after temp deaths)
        float availablePrey = prey.Sum(p => p.Population);

        // Fed rate = how much demand was satisfied
        float fedRate;
        if (totalDemand <= 0f)
        {
            fedRate = 1f;
        }
        else
        {
            fedRate = Math.Min(1f, availablePrey / totalDemand);
        }

        LastFedRate = fedRate;

        // Apply fed rate to all predators
        foreach (var pred in predators)
        {
            pred.FedRate = fedRate;
        }

        // How many prey get eaten (integer)
        int eaten = (int)Math.Min(availablePrey, totalDemand);

        // Remove eaten prey proportionally from each prey species
        if (eaten > 0 && availablePrey > 0)
        {
            foreach (var p in prey)
            {
                float proportion = p.Population / availablePrey;
                int thisEaten = (int)Math.Ceiling(proportion * eaten);
                thisEaten = Math.Min(thisEaten, (int)p.Population);
                p.Population = Math.Max(0f, p.Population - thisEaten);
            }
        }

        LastTotalEaten = eaten;
    }

    /// <summary>
    /// Species reproduces if performance is good and population >= 2
    /// </summary>
    private void ApplyReproduction(SimSpecies sp)
    {
        if (sp.Population < 2) return;
        if (sp.ThermalPerformance < sp.ReproThreshold) return;

        // Births = population × thermalPerf × reproMult × days
        float birthsFloat = sp.Population * sp.ThermalPerformance * sp.ReproductionMultiplier * DaysPerStep;
        int births = (int)Math.Floor(birthsFloat);  // Round down to integer

        // Tier 1 special: if no Tier 2 exists, reduce births by 15%
        if (sp.Tier == 1)
        {
            bool anyTier2Alive = Species.Any(s => s.Tier == 2 && s.Population > 0);
            if (!anyTier2Alive)
            {
                births = (int)(births * 0.85f);
            }
        }

        sp.Population += births;
        LastTotalBirths += births;
    }

    // ========== POPULATION QUERIES ==========

    public float GetTier1Population()
    {
        return Species.Where(s => s.Tier == 1).Sum(s => s.Population);
    }

    public float GetTier2Population()
    {
        return Species.Where(s => s.Tier == 2).Sum(s => s.Population);
    }

    public float GetVariantPopulation(int tier, ThermalVariant variant)
    {
        return Species.Where(s => s.Tier == tier && s.Variant == variant).Sum(s => s.Population);
    }

    /// <summary>
    /// Check if ecosystem has crashed (any tier = 0)
    /// </summary>
    public bool HasCrashed()
    {
        return GetTier1Population() <= 0 || GetTier2Population() <= 0;
    }
}
