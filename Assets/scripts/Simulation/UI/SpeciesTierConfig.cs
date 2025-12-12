using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a single tier panel in the Species Count UI.
/// Handles adding/removing species entries and syncing with RunSpeciesList.
/// </summary>
public class SpeciesTierConfig : MonoBehaviour
{
    [Header("Tier Configuration")]
    [Tooltip("0 = Tier 1 (Hexapod/Prey), 1 = Tier 2 (Sheplik/Predator)")]
    [SerializeField] private int tier = 0;
    [SerializeField] private int maxSpeciesPerTier = 3;

    [Header("Data References")]
    [SerializeField] private RunSpeciesList runSpeciesList;

    [Header("UI References")]
    [SerializeField] private GameObject speciesEntryPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private Button plusButton;
    //[SerializeField] private Button minusButton;

    // Track instantiated UI entries (in order of addition)
    private List<GameObject> instantiatedEntries = new List<GameObject>();

    // Variant sequence for cycling when adding
    private readonly SpeciesVariant[] variantSequence =
    {
        SpeciesVariant.Common,
        SpeciesVariant.Arctic,
        SpeciesVariant.Tropical
    };

    /// <summary>
    /// Returns the species name for this tier (Hexapod for Tier 0, Sheplik for Tier 1)
    /// </summary>
    private SpeciesName TierSpeciesName => tier == 0 ? SpeciesName.Hexapod : SpeciesName.Sheplik;

    void Start()
    {
        SetupButtonListeners();
        PopulateFromRunSpeciesList();
        UpdateButtonVisibility();
    }

    private void SetupButtonListeners()
    {
        if (plusButton != null)
            plusButton.onClick.AddListener(OnPlusClicked);

        /*if (minusButton != null)
            minusButton.onClick.AddListener(OnMinusClicked);*/
    }

    /// <summary>
    /// Populate UI entries from existing RunSpeciesList data for this tier
    /// </summary>
    private void PopulateFromRunSpeciesList()
    {
        if (runSpeciesList == null || runSpeciesList.speciesList == null)
        {
            Debug.LogWarning($"SpeciesTierConfig (Tier {tier}): RunSpeciesList not assigned or empty!");
            return;
        }

        // Find all species in RunSpeciesList that match this tier
        foreach (var speciesData in runSpeciesList.speciesList)
        {
            if (speciesData.tier == tier && instantiatedEntries.Count < maxSpeciesPerTier)
            {
                InstantiateSpeciesEntry(speciesData.speciesName, speciesData.variant);
            }
        }

        Debug.Log($"SpeciesTierConfig (Tier {tier}): Populated {instantiatedEntries.Count} entries from RunSpeciesList");
    }

    /// <summary>
    /// Instantiate a species entry prefab and configure it
    /// </summary>
    private GameObject InstantiateSpeciesEntry(SpeciesName speciesName, SpeciesVariant variant)
    {
        if (speciesEntryPrefab == null)
        {
            Debug.LogError("SpeciesTierConfig: Species entry prefab not assigned!");
            return null;
        }

        if (contentParent == null)
        {
            Debug.LogError("SpeciesTierConfig: Content parent not assigned!");
            return null;
        }

        // Instantiate and activate
        GameObject entry = Instantiate(speciesEntryPrefab, contentParent);
        entry.SetActive(true);

        // Configure the SpeciesUIController
        var uiController = entry.GetComponent<SpeciesUIController>();
        if (uiController != null)
        {
            uiController.Initialize(instantiatedEntries.Count+1, speciesName, variant);
        }
        else
        {
            Debug.LogWarning("SpeciesTierConfig: Prefab missing SpeciesUIController component!");
        }

        instantiatedEntries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Called when Plus button is clicked - adds next species variant
    /// </summary>
    private void OnPlusClicked()
    {
        if (instantiatedEntries.Count >= maxSpeciesPerTier)
        {
            Debug.Log($"SpeciesTierConfig (Tier {tier}): Already at max species ({maxSpeciesPerTier})");
            return;
        }

        // Determine which variant to add next (cycles through Common, Arctic, Tropical)
        SpeciesVariant nextVariant = GetNextVariant();
        SpeciesName speciesName = TierSpeciesName;

        // Get species data from the master database
        SpeciesData templateData = GetSpeciesFromDatabase(speciesName, nextVariant);
        if (templateData == null)
        {
            Debug.LogError($"SpeciesTierConfig: Could not find {speciesName} {nextVariant} in database!");
            return;
        }

        // Add cloned data to RunSpeciesList
        AddToRunSpeciesList(templateData);

        // Instantiate UI entry
        InstantiateSpeciesEntry(speciesName, nextVariant);

        // Update button visibility
        UpdateButtonVisibility();

        Debug.Log($"SpeciesTierConfig (Tier {tier}): Added {speciesName} {nextVariant}");
    }

    /// <summary>
    /// Called when Minus button is clicked - removes last added species
    /// </summary>
    private void OnMinusClicked()
    {
        if (instantiatedEntries.Count <= 0)
        {
            Debug.Log($"SpeciesTierConfig (Tier {tier}): No species to remove");
            return;
        }

        // Get last entry
        int lastIndex = instantiatedEntries.Count - 1;
        GameObject lastEntry = instantiatedEntries[lastIndex];

        // Remove from RunSpeciesList (last species of this tier)
        RemoveLastFromRunSpeciesList();

        // Remove from tracking list and destroy GameObject
        instantiatedEntries.RemoveAt(lastIndex);
        Destroy(lastEntry);

        // Update button visibility
        UpdateButtonVisibility();

        Debug.Log($"SpeciesTierConfig (Tier {tier}): Removed last species entry");
    }

    /// <summary>
    /// Get the next variant to add based on current count
    /// </summary>
    private SpeciesVariant GetNextVariant()
    {
        int index = instantiatedEntries.Count % variantSequence.Length;
        return variantSequence[index];
    }

    /// <summary>
    /// Fetch species data from the master database
    /// </summary>
    private SpeciesData GetSpeciesFromDatabase(SpeciesName name, SpeciesVariant variant)
    {
        if (runSpeciesList?.SpeciesDatabase == null)
        {
            Debug.LogError("SpeciesTierConfig: SpeciesDatabase reference missing from RunSpeciesList!");
            return null;
        }

        return runSpeciesList.SpeciesDatabase.GetSpecies(name, variant);
    }

    /// <summary>
    /// Clone species data and add to RunSpeciesList
    /// </summary>
    private void AddToRunSpeciesList(SpeciesData templateData)
    {
        if (runSpeciesList == null)
            return;

        // Clone the data so we don't modify the master database
        SpeciesData clonedData = CloneSpeciesData(templateData);
        runSpeciesList.speciesList.Add(clonedData);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif
    }

    /// <summary>
    /// Remove the last species of this tier from RunSpeciesList
    /// </summary>
    private void RemoveLastFromRunSpeciesList()
    {
        if (runSpeciesList == null || runSpeciesList.speciesList == null)
            return;

        // Find and remove the last species matching this tier
        for (int i = runSpeciesList.speciesList.Count - 1; i >= 0; i--)
        {
            if (runSpeciesList.speciesList[i].tier == tier)
            {
                runSpeciesList.speciesList.RemoveAt(i);
                break;
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif
    }

    /// <summary>
    /// Create a deep copy of SpeciesData
    /// </summary>
    private SpeciesData CloneSpeciesData(SpeciesData original)
    {
        return new SpeciesData
        {
            // Basic Info
            index = original.index,
            speciesName = original.speciesName,
            variant = original.variant,
            displayName = original.displayName,
            icon = original.icon,
            count = original.count,

            // Gameplay Stats
            tier = original.tier,
            eatingAmount = original.eatingAmount,
            reproductionMultiplier = original.reproductionMultiplier,
            deathThreshold = original.deathThreshold,
            deathRate = original.deathRate,
            minimumDeaths = original.minimumDeaths,
            reproThreshold = original.reproThreshold,

            // Natural Mortality
            naturalDeathRate = original.naturalDeathRate,
            naturalDeathVariance = original.naturalDeathVariance,

            // Hunting Efficiency
            huntingEfficiency = original.huntingEfficiency,
            huntingVariance = original.huntingVariance,

            // Star Ratings
            eatingStars = original.eatingStars,
            reproductionStars = original.reproductionStars,
            deathThresholdStars = original.deathThresholdStars,
            deathRateStars = original.deathRateStars,
            thermalBreadthStars = original.thermalBreadthStars,

            // Display Text
            temperatureThresholdText = original.temperatureThresholdText,
            reproductionRateText = original.reproductionRateText,
            description = original.description,

            // Thermal Curve Parameters
            optimalTempK = original.optimalTempK,
            arrhenBreadth = original.arrhenBreadth,
            arrhenLower = original.arrhenLower,
            arrhenUpper = original.arrhenUpper,
            lowerBoundK = original.lowerBoundK,
            upperBoundK = original.upperBoundK
        };
    }

    /// <summary>
    /// Update plus/minus button visibility based on current count
    /// </summary>
    private void UpdateButtonVisibility()
    {
        // Hide plus when at max
        if (plusButton != null)
            plusButton.transform.parent.gameObject.SetActive(instantiatedEntries.Count < maxSpeciesPerTier);

/*        // Hide minus when empty
        if (minusButton != null)
            minusButton.gameObject.SetActive(instantiatedEntries.Count > 0);*/
    }

    /// <summary>
    /// Get current species count for this tier
    /// </summary>
    public int GetSpeciesCount()
    {
        return instantiatedEntries.Count;
    }

    /// <summary>
    /// Clear all species from this tier
    /// </summary>
    [ContextMenu("Clear All Species")]
    public void ClearAllSpecies()
    {
        while (instantiatedEntries.Count > 0)
        {
            OnMinusClicked();
        }
    }
}