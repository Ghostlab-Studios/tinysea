using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a single tier panel in the Species Count UI.
/// Handles adding/removing species entries and syncing with RunSpeciesList.
/// 
/// Subscribes to SpeciesEditEvents to handle:
/// - OnSpeciesDeleted: Remove the UI entry when species is deleted via EditSpeciesUI
/// - OnSpeciesSaved: (handled by SpeciesUIController directly)
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

    // Track instantiated UI entries and their RunSpeciesList indices
    private List<GameObject> instantiatedEntries = new List<GameObject>();
    private List<int> entryRunSpeciesListIndices = new List<int>(); // Parallel list tracking indices

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

    void OnEnable()
    {
        // Subscribe to delete events
        SpeciesEditEvents.OnSpeciesDeleted += HandleSpeciesDeleted;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SpeciesEditEvents.OnSpeciesDeleted -= HandleSpeciesDeleted;
    }

    private void SetupButtonListeners()
    {
        if (plusButton != null)
            plusButton.onClick.AddListener(OnPlusClicked);

        /*if (minusButton != null)
            minusButton.onClick.AddListener(OnMinusClicked);*/
    }

    /// <summary>
    /// Called when a species is deleted via EditSpeciesUI.
    /// Finds and removes the corresponding UI entry if it belongs to this tier.
    /// </summary>
    private void HandleSpeciesDeleted(int deletedIndex)
    {
        Debug.Log($"SpeciesTierConfig (Tier {tier}): Received delete event for index {deletedIndex}");

        // Find if we have an entry with this index
        int entryIndex = entryRunSpeciesListIndices.IndexOf(deletedIndex);

        if (entryIndex >= 0)
        {
            // This tier has the deleted species - remove its UI
            GameObject entryToRemove = instantiatedEntries[entryIndex];

            instantiatedEntries.RemoveAt(entryIndex);
            entryRunSpeciesListIndices.RemoveAt(entryIndex);

            Destroy(entryToRemove);

            Debug.Log($"SpeciesTierConfig (Tier {tier}): Removed UI entry at position {entryIndex}");

            // Update indices for remaining entries (they shifted down by 1 in RunSpeciesList)
            UpdateIndicesAfterDelete(deletedIndex);

            // Update visual indices (#01, #02, etc.)
            UpdateVisualIndices();

            // Update button visibility
            UpdateButtonVisibility();
        }
        else
        {
            // The deleted species wasn't in this tier, but indices may have shifted
            // Update any indices that were greater than the deleted index
            UpdateIndicesAfterDelete(deletedIndex);
        }
    }

    /// <summary>
    /// After a delete, all RunSpeciesList indices greater than the deleted index
    /// need to be decremented by 1.
    /// </summary>
    private void UpdateIndicesAfterDelete(int deletedIndex)
    {
        for (int i = 0; i < entryRunSpeciesListIndices.Count; i++)
        {
            if (entryRunSpeciesListIndices[i] > deletedIndex)
            {
                int oldIndex = entryRunSpeciesListIndices[i];
                entryRunSpeciesListIndices[i] = oldIndex - 1;

                // Also update the SpeciesUIController's stored index
                if (i < instantiatedEntries.Count)
                {
                    var uiController = instantiatedEntries[i].GetComponent<SpeciesUIController>();
                    if (uiController != null)
                    {
                        uiController.SetRunSpeciesListIndex(entryRunSpeciesListIndices[i]);
                    }
                }

                Debug.Log($"SpeciesTierConfig (Tier {tier}): Updated entry {i} index from {oldIndex} to {entryRunSpeciesListIndices[i]}");
            }
        }
    }

    /// <summary>
    /// Update visual indices (#01, #02, etc.) for all entries in this tier.
    /// </summary>
    private void UpdateVisualIndices()
    {
        for (int i = 0; i < instantiatedEntries.Count; i++)
        {
            var uiController = instantiatedEntries[i].GetComponent<SpeciesUIController>();
            if (uiController != null)
            {
                uiController.SetVisualIndex(i + 1); // 1-based visual index
            }
        }
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

        // Track both the entry and its RunSpeciesList index
        instantiatedEntries.Add(entry);
        entryRunSpeciesListIndices.Add(runSpeciesListIndex);

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
        int lastRunSpeciesListIndex = entryRunSpeciesListIndices[lastIndex];

        // Remove from RunSpeciesList
        RemoveFromRunSpeciesListAtIndex(lastRunSpeciesListIndex);

        // Remove from tracking lists and destroy GameObject
        instantiatedEntries.RemoveAt(lastIndex);
        entryRunSpeciesListIndices.RemoveAt(lastIndex);
        Destroy(lastEntry);

        // Update indices for entries in OTHER tiers that had higher indices
        // (This is handled by the delete event in a more complete system)

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
    /// Remove species at specific index from RunSpeciesList
    /// </summary>
    private void RemoveFromRunSpeciesListAtIndex(int index)
    {
        if (runSpeciesList == null || runSpeciesList.speciesList == null)
            return;

        if (index >= 0 && index < runSpeciesList.speciesList.Count)
        {
            runSpeciesList.speciesList.RemoveAt(index);
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif
    }

    /// <summary>
    /// Remove the last species of this tier from RunSpeciesList (legacy method)
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
            upperBoundK = original.upperBoundK,
            pmax = original.pmax,
            ctMinC = original.ctMinC,
            ctMaxC = original.ctMaxC
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