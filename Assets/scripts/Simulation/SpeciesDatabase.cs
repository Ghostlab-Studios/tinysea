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
    private int defaultT2SpeciesCount = 1;

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

        // Populate all 6 species from CSV data
        // Note: Icons need to be assigned manually after population

        // Row 0: Cyplo Arctic
        AddSpecies(0, SpeciesName.Sheplik, SpeciesVariant.Arctic, 1, defaultT2SpeciesCount, 1.5f, 2, 0.3f, 0.02f, 1, 30,
                  0, 5, 2, 3, 2, "Narrow", "Fast", 283.15f, 3564, 7088, 17554, 278.15f, 288.15f);

        // Row 1: Cyplo Tropical  
        AddSpecies(1, SpeciesName.Sheplik, SpeciesVariant.Tropical, 1, defaultT2SpeciesCount, 1.5f, 2, 0.3f, 0.02f, 1, 30,
                  0, 5, 2, 3, 2, "Narrow", "Fast", 303.15f, 3564, 7088, 17554, 298.15f, 308.15f);

        // Row 2: Cyplo Common
        AddSpecies(2, SpeciesName.Sheplik, SpeciesVariant.Common, 1, defaultT2SpeciesCount, 1.5f, 1.5f, 0.3f, 0.02f, 1, 20,
                  0, 3, 2, 3, 4, "Broad", "Medium", 293.15f, 4564, 8088, 21554, 283.15f, 303.15f);

        // Row 3: Hexapod Arctic
        AddSpecies(3, SpeciesName.Hexapod, SpeciesVariant.Arctic, 0, defaultT1SpeciesCount, 0, 1.5f, 0.3f, 0.02f, 1, 20,
                  0, 3, 2, 3, 2, "Narrow", "Medium", 283.15f, 3564, 7088, 17554, 278.15f, 288.15f);

        // Row 4: Hexapod Tropical
        AddSpecies(4, SpeciesName.Hexapod, SpeciesVariant.Tropical, 0, defaultT1SpeciesCount, 0, 1.5f, 0.3f, 0.02f, 1, 20,
                  0, 3, 2, 3, 2, "Narrow", "Medium", 303.15f, 3564, 7088, 17554, 298.15f, 308.15f);

        // Row 5: Hexapod Common
        AddSpecies(5, SpeciesName.Hexapod, SpeciesVariant.Common, 0, defaultT1SpeciesCount, 0, 1, 0.3f, 0.02f, 1, 15,
                  0, 1, 2, 3, 4, "Broad", "Slow", 293.15f, 4564, 8088, 21554, 283.15f, 303.15f);

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"Populated {speciesList.Count} species entries from CSV data");
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