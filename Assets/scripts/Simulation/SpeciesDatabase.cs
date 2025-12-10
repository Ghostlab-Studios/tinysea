using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Enum for all species names (unique identifiers)
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
    public int tier;
    public float eatingAmount;
    public float reproductionMultiplier;
    public float deathThreshold;
    public float deathRate;
    public float minimumDeaths;
    public float reproThreshold;

    [Header("Star Ratings")]
    public int eatingStars;
    public int reproductionStars;
    public int deathThresholdStars;
    public int deathRateStars;
    public int thermalBreadthStars;

    [Header("Display Text")]
    public string temperatureThresholdText;
    public string reproductionRateText;
    public string description;

    [Header("Thermal Curve Parameters")]
    public float optimalTempK = 295.15f;
    public float arrhenBreadth = 4258f;
    public float arrhenLower = 7457f;
    public float arrhenUpper = 19664f;
    public float lowerBoundK = 286f;
    public float upperBoundK = 298f;
}

[CreateAssetMenu(fileName = "SpeciesDatabase", menuName = "TinySea/Species Database")]
public class SpeciesDatabase : ScriptableObject
{
    public List<SpeciesData> speciesList = new List<SpeciesData>();

    private int defaultT1SpeciesCount = 4;
    private int defaultT2SpeciesCount = 2;

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
        // From CSV: Tier=1, EatingAmount=N/A(0), ReproMult=0.45, DeathThresh=0.3, DeathRate=0.6, MinDeaths=1, ReproThresh=0.25
        // Thermal: ArrhenBreadth=5273.15, ArrhenLower=10273.15, ArrhenUpper=21273.15
        
        // Hexapod Common - OptimalK=293.15, LowerK=285.15, UpperK=295.15
        AddSpecies(
            index: 0,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Common,
            tier: 0,  // 0 = Tier 1
            count: defaultT1SpeciesCount,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            eatStars: 0,
            reproStars: 4,
            deathThreshStars: 3,
            deathRateStars: 2,
            thermalStars: 5,
            tempText: "High",
            reproText: "Low",
            optimalK: 293.15f,
            breadth: 5273.15f,
            lower: 10273.15f,
            upper: 21273.15f,
            lowerBound: 285.15f,
            upperBound: 295.15f
        );

        // Hexapod Tropical - OptimalK=308.65, LowerK=300.15, UpperK=310.15
        AddSpecies(
            index: 1,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Tropical,
            tier: 0,
            count: defaultT1SpeciesCount,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            eatStars: 0,
            reproStars: 4,
            deathThreshStars: 3,
            deathRateStars: 2,
            thermalStars: 5,
            tempText: "High",
            reproText: "Low",
            optimalK: 308.65f,
            breadth: 5273.15f,
            lower: 10273.15f,
            upper: 21273.15f,
            lowerBound: 300.15f,
            upperBound: 310.15f
        );

        // Hexapod Arctic - OptimalK=278.15, LowerK=270.15, UpperK=280.15
        AddSpecies(
            index: 2,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Arctic,
            tier: 0,
            count: defaultT1SpeciesCount,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            eatStars: 0,
            reproStars: 4,
            deathThreshStars: 3,
            deathRateStars: 2,
            thermalStars: 5,
            tempText: "High",
            reproText: "Low",
            optimalK: 278.15f,
            breadth: 5273.15f,
            lower: 10273.15f,
            upper: 21273.15f,
            lowerBound: 270.15f,
            upperBound: 280.15f
        );

        // ===== SHELPIK (Tier 2 - Predator) =====
        // From CSV: Tier=2, EatingAmount=1.5, ReproMult=0.1, DeathThresh=0.3, DeathRate=0.3, MinDeaths=1, ReproThresh=0.25
        // Thermal: ArrhenBreadth=5273.15, ArrhenLower=10273.15, ArrhenUpper=21273.15

        // Shelpik Common - OptimalK=293.15, LowerK=285.15, UpperK=295.15
        AddSpecies(
            index: 3,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Common,
            tier: 1,  // 1 = Tier 2
            count: defaultT2SpeciesCount,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            eatStars: 4,
            reproStars: 2,
            deathThreshStars: 4,
            deathRateStars: 4,
            thermalStars: 5,
            tempText: "High",
            reproText: "Low",
            optimalK: 293.15f,
            breadth: 5273.15f,
            lower: 10273.15f,
            upper: 21273.15f,
            lowerBound: 285.15f,
            upperBound: 295.15f
        );

        // Shelpik Tropical - OptimalK=308.65, LowerK=300.15, UpperK=310.15
        AddSpecies(
            index: 4,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Tropical,
            tier: 1,
            count: defaultT2SpeciesCount,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            eatStars: 4,
            reproStars: 2,
            deathThreshStars: 4,
            deathRateStars: 4,
            thermalStars: 3,
            tempText: "High",
            reproText: "Low",
            optimalK: 308.65f,
            breadth: 5273.15f,
            lower: 10273.15f,
            upper: 21273.15f,
            lowerBound: 300.15f,
            upperBound: 310.15f
        );

        // Shelpik Arctic - OptimalK=278.15, LowerK=270.15, UpperK=280.15
        AddSpecies(
            index: 5,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Arctic,
            tier: 1,
            count: defaultT2SpeciesCount,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            minDeaths: 1f,
            reproThresh: 0.25f,
            eatStars: 4,
            reproStars: 2,
            deathThreshStars: 4,
            deathRateStars: 4,
            thermalStars: 3,
            tempText: "High",
            reproText: "Low",
            optimalK: 278.15f,
            breadth: 5273.15f,
            lower: 10273.15f,
            upper: 21273.15f,
            lowerBound: 270.15f,
            upperBound: 280.15f
        );

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"Populated {speciesList.Count} species entries (Hexapod + Shelpik, 3 variants each)");
    }

    private void AddSpecies(int index, SpeciesName name, SpeciesVariant variant, int tier, int count,
                           float eating, float repro, float deathThresh, float deathRate,
                           float minDeaths, float reproThresh, int eatStars, int reproStars,
                           int deathThreshStars, int deathRateStars, int thermalStars,
                           string tempText, string reproText, float optimalK, float breadth,
                           float lower, float upper, float lowerBound, float upperBound)
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
            eatingStars = eatStars,
            reproductionStars = reproStars,
            deathThresholdStars = deathThreshStars,
            deathRateStars = deathRateStars,
            thermalBreadthStars = thermalStars,
            temperatureThresholdText = tempText,
            reproductionRateText = reproText,
            optimalTempK = optimalK,
            arrhenBreadth = breadth,
            arrhenLower = lower,
            arrhenUpper = upper,
            lowerBoundK = lowerBound,
            upperBoundK = upperBound,
            description = $"{name} - {variant} variant"
        };

        speciesList.Add(data);
    }
#endif
}
