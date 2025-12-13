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

        // Track visual index separately (per-tier count)
        int visualIndex = 1;

        // Find all species in RunSpeciesList that match this tier
        // We need to track the ACTUAL index in runSpeciesList.speciesList (not just the visual count)
        for (int i = 0; i < runSpeciesList.speciesList.Count; i++)
        {
            var speciesData = runSpeciesList.speciesList[i];
            if (speciesData.tier == tier && instantiatedEntries.Count < maxSpeciesPerTier)
            {
                // Pass visual index, actual RunSpeciesList index, and the data reference
                InstantiateSpeciesEntry(visualIndex, i, speciesData);
                visualIndex++;
            }
        }

        Debug.Log($"SpeciesTierConfig (Tier {tier}): Populated {instantiatedEntries.Count} entries from RunSpeciesList");
    }

    /// <summary>
    /// Instantiate a species entry prefab and configure it
    /// </summary>
    /// <param name="visualIndex">Display index for UI (#01, #02, etc.)</param>
    /// <param name="runSpeciesListIndex">The actual index in RunSpeciesList.speciesList (for edit events)</param>
    /// <param name="speciesData">Direct reference to the SpeciesData from RunSpeciesList</param>
    private GameObject InstantiateSpeciesEntry(int visualIndex, int runSpeciesListIndex, SpeciesData speciesData)
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

        // Position just above the button row
        if (plusButton != null)
        {
            int buttonParentIndex = plusButton.transform.parent.GetSiblingIndex();
            entry.transform.SetSiblingIndex(buttonParentIndex);
        }

        // Configure the SpeciesUIController
        var uiController = entry.GetComponent<SpeciesUIController>();
        if (uiController != null)
        {
            // Pass the RunSpeciesList reference so it can refresh after edits
            uiController.SetRunSpeciesList(runSpeciesList);

            // Use the new Initialize overload that takes the actual data reference
            uiController.Initialize(visualIndex, runSpeciesListIndex, speciesData);
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
        SpeciesData newSpeciesData = AddToRunSpeciesList(templateData);

        // The new species was added at the END of runSpeciesList.speciesList
        // So its index is Count - 1
        int newRunSpeciesListIndex = runSpeciesList.speciesList.Count - 1;

        // Visual index is based on how many entries this tier has
        int visualIndex = instantiatedEntries.Count + 1;

        // Instantiate UI entry with the correct data reference
        InstantiateSpeciesEntry(visualIndex, newRunSpeciesListIndex, newSpeciesData);

        // Update button visibility
        UpdateButtonVisibility();

        Debug.Log($"SpeciesTierConfig (Tier {tier}): Added {speciesName} {nextVariant} at runSpeciesListIndex={newRunSpeciesListIndex}");
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
    /// Returns the newly added SpeciesData reference
    /// </summary>
    private SpeciesData AddToRunSpeciesList(SpeciesData templateData)
    {
        if (runSpeciesList == null)
            return null;

        // Clone the data so we don't modify the master database
        SpeciesData clonedData = CloneSpeciesData(templateData);
        runSpeciesList.speciesList.Add(clonedData);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif

        return clonedData;
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