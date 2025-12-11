using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpeciesName
{
    Hexapod,
    Gelgi,
    Yelloa,
    Sheplik,
    Grabbler,
    Cyplo,
    Rooda,
    Sploof,
    Silu
}

public enum SpeciesVariant
{
    Common,
    Tropical,
    Arctic
}

[System.Serializable]
public class SpeciesData
{
    [Header("Basic Info")]
    public int index;
    public SpeciesName speciesName;
    public SpeciesVariant variant;
    public Sprite icon;
    public int count;

    [Header("Gameplay Stats")]
    public int tier;                        // 0 = Tier 1 (prey), 1 = Tier 2 (predator)
    public float eatingAmount;              // Prey consumed per creature per step
    public float reproductionMultiplier;    // Birth rate multiplier
    public float deathThreshold = 0.3f;     // FinalPerf below this triggers thermal death
    public float deathRate;                 // Fraction dying when thermal death triggers
    public float minimumDeaths = 1f;        // Minimum deaths when thermal death triggers
    public float reproThreshold = 0.25f;    // FinalPerf required to reproduce

    [Header("Natural Mortality")]
    [Tooltip("Base natural death rate per biology step (e.g., 0.02 = 2%)")]
    public float naturalDeathRate = 0.02f;
    [Tooltip("Random variance range (e.g., 0.01 = ±1%)")]
    public float naturalDeathVariance = 0.01f;

    [Header("Hunting Efficiency (Tier 2 only)")]
    [Tooltip("Base hunting success rate (e.g., 0.75 = 75%). Tier 1 ignores this.")]
    public float huntingEfficiency = 0.75f;
    [Tooltip("Random variance range (e.g., 0.15 = ±15%)")]
    public float huntingVariance = 0.15f;

    [Header("Star Ratings (UI)")]
    public int eatingStars;
    public int reproductionStars;
    public int deathThresholdStars;
    public int deathRateStars;
    public int thermalBreadthStars;

    [Header("Display Text (UI)")]
    public string temperatureThresholdText;
    public string reproductionRateText;
    public string description;

    [Header("Thermal Curve Parameters (Kelvin)")]
    public float optimalTempK = 293.15f;    // 20°C default
    public float arrhenBreadth = 5273.15f;
    public float arrhenLower = 10273.15f;
    public float arrhenUpper = 21273.15f;
    public float lowerBoundK = 285.15f;     // 12°C default
    public float upperBoundK = 295.15f;     // 22°C default
}

[CreateAssetMenu(fileName = "SpeciesDatabase", menuName = "TinySea/Species Database")]
public class SpeciesDatabase : ScriptableObject
{
    public List<SpeciesData> speciesList = new List<SpeciesData>();

    private const int DEFAULT_T1_COUNT = 4;
    private const int DEFAULT_T2_COUNT = 2;

    // Quick lookup by enum
    public SpeciesData GetSpecies(SpeciesName name, SpeciesVariant variant)
    {
        return speciesList.Find(s => s.speciesName == name && s.variant == variant);
    }

    // Get all species of a specific tier
    public List<SpeciesData> GetSpeciesByTier(int tier)
    {
        return speciesList.FindAll(s => s.tier == tier);
    }

    // Get all variants of a species
    public List<SpeciesData> GetVariants(SpeciesName name)
    {
        return speciesList.FindAll(s => s.speciesName == name);
    }

#if UNITY_EDITOR
    [ContextMenu("Populate Default Data")]
    private void PopulateDefaultData()
    {
        speciesList.Clear();

        // ===== HEXAPOD (Tier 1 - Prey) =====
        // Natural death: 2% base ±1% variance
        // Hunting: N/A (Tier 1 doesn't hunt)

        AddSpecies(
            index: 0,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Common,
            tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.02f,
            naturalDeathVariance: 0.01f,
            huntingEfficiency: 1.0f,
            huntingVariance: 0f,
            optimalK: 293.15f,      // 20°C
            lowerBound: 285.15f,    // 12°C
            upperBound: 295.15f     // 22°C
        );

        AddSpecies(
            index: 1,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Tropical,
            tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.02f,
            naturalDeathVariance: 0.01f,
            huntingEfficiency: 1.0f,
            huntingVariance: 0f,
            optimalK: 308.65f,      // 35.5°C
            lowerBound: 300.15f,    // 27°C
            upperBound: 310.15f     // 37°C
        );

        AddSpecies(
            index: 2,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Arctic,
            tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.02f,
            naturalDeathVariance: 0.01f,
            huntingEfficiency: 1.0f,
            huntingVariance: 0f,
            optimalK: 278.15f,      // 5°C
            lowerBound: 270.15f,    // -3°C
            upperBound: 280.15f     // 7°C
        );

        // ===== SHEPLIK (Tier 2 - Predator) =====
        // Natural death: 3% base ±1.5% variance
        // Hunting: 75% base ±15% variance

        AddSpecies(
            index: 3,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Common,
            tier: 1,
            count: DEFAULT_T2_COUNT,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.03f,
            naturalDeathVariance: 0.015f,
            huntingEfficiency: 0.75f,
            huntingVariance: 0.15f,
            optimalK: 293.15f,      // 20°C
            lowerBound: 285.15f,    // 12°C
            upperBound: 295.15f     // 22°C
        );

        AddSpecies(
            index: 4,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Tropical,
            tier: 1,
            count: DEFAULT_T2_COUNT,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.03f,
            naturalDeathVariance: 0.015f,
            huntingEfficiency: 0.75f,
            huntingVariance: 0.15f,
            optimalK: 308.65f,      // 35.5°C
            lowerBound: 300.15f,    // 27°C
            upperBound: 310.15f     // 37°C
        );

        AddSpecies(
            index: 5,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Arctic,
            tier: 1,
            count: DEFAULT_T2_COUNT,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.03f,
            naturalDeathVariance: 0.015f,
            huntingEfficiency: 0.75f,
            huntingVariance: 0.15f,
            optimalK: 278.15f,      // 5°C
            lowerBound: 270.15f,    // -3°C
            upperBound: 280.15f     // 7°C
        );

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"Populated {speciesList.Count} species entries");
        Debug.Log("Tier 1 (Hexapod): NaturalDeath=2%±1%, Hunting=N/A");
        Debug.Log("Tier 2 (Sheplik): NaturalDeath=3%±1.5%, Hunting=75%±15%");
    }

    private void AddSpecies(int index, SpeciesName name, SpeciesVariant variant, int tier, int count,
                           float eating, float repro, float deathThresh, float deathRate,
                           float minDeaths, float reproThresh,
                           float naturalDeathRate, float naturalDeathVariance,
                           float huntingEfficiency, float huntingVariance,
                           float optimalK, float lowerBound, float upperBound)
    {
        var data = new SpeciesData
        {
            index = index,
            speciesName = name,
            variant = variant,
            tier = tier,
            count = count,
            eatingAmount = eating,
            reproductionMultiplier = repro,
            deathThreshold = deathThresh,
            deathRate = deathRate,
            minimumDeaths = minDeaths,
            reproThreshold = reproThresh,
            naturalDeathRate = naturalDeathRate,
            naturalDeathVariance = naturalDeathVariance,
            huntingEfficiency = huntingEfficiency,
            huntingVariance = huntingVariance,
            // Thermal parameters
            optimalTempK = optimalK,
            arrhenBreadth = 5273.15f,
            arrhenLower = 10273.15f,
            arrhenUpper = 21273.15f,
            lowerBoundK = lowerBound,
            upperBoundK = upperBound,
            // UI defaults
            eatingStars = tier == 0 ? 0 : 4,
            reproductionStars = tier == 0 ? 4 : 2,
            deathThresholdStars = 3,
            deathRateStars = tier == 0 ? 2 : 4,
            thermalBreadthStars = 5,
            temperatureThresholdText = "High",
            reproductionRateText = "Low",
            description = $"{name} - {variant} variant"
        };

        speciesList.Add(data);
    }
#endif
}
