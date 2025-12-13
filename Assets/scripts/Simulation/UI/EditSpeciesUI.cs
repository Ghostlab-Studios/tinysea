using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Edit Species UI panel.
/// Subscribes to SpeciesEditEvents to know when to open.
/// Uses the species index to access data from RunSpeciesList.
/// 
/// Buttons:
/// - Close (X): Close without saving
/// - Cancel: Close without saving (same as Close)
/// - Save Data: Save changes and close
/// - Delete: Delete species and close
/// - Reset: Reset to original database values
/// </summary>
public class EditSpeciesUI : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private RunSpeciesList runSpeciesList;
    [SerializeField] private SpeciesDatabase originalDatabase; // For reset functionality

    [Header("Panel Reference")]
    [SerializeField] private GameObject editPanel; // The panel to show/hide (can be this gameObject or a child)

    [Header("Thermal Graph")]
    [SerializeField] private ThermalGraphUI thermalGraphUI; // Graph display for thermal performance curve

    [Header("UI Fields - Header")]
    [SerializeField] private TextMeshProUGUI tierField; // Shows "Tier 1" or "Tier 2"

    [Header("UI Fields - Basic Info")]
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_Dropdown variantDropdown;
    [SerializeField] private TMP_InputField countField;

    [Header("UI Fields - Gameplay Stats")]
    [SerializeField] private TMP_InputField eatingAmountField;
    [SerializeField] private TMP_InputField reproThresholdField;
    [SerializeField] private TMP_InputField reproMultiplierField;
    [SerializeField] private TMP_InputField tempDeathThresholdField;
    [SerializeField] private TMP_InputField tempDeathRateField;
    [SerializeField] private TMP_InputField naturalDeathVarianceField;
    [SerializeField] private TMP_InputField naturalDeathRateField;

    [Header("UI Fields - Hunting (Tier 2+ only)")]
    [SerializeField] private GameObject huntingSection; // Parent object to show/hide for Tier 2+
    [SerializeField] private TMP_InputField huntingEfficiencyField;
    [SerializeField] private TMP_InputField huntingVarianceField;

    [Header("Buttons (assign in inspector or wire via OnClick)")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button resetButton;

    [Header("Debug")]
    [SerializeField] private int currentEditingIndex = -1;

    private SpeciesData currentEditingData;

    private void OnEnable()
    {
        // Subscribe to edit request events
        SpeciesEditEvents.OnEditRequested += HandleEditRequested;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SpeciesEditEvents.OnEditRequested -= HandleEditRequested;
    }

    private void Start()
    {
        // Wire up button listeners if assigned
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        if (saveButton != null) saveButton.onClick.AddListener(SaveData);
        if (deleteButton != null) deleteButton.onClick.AddListener(Delete);
        if (resetButton != null) resetButton.onClick.AddListener(Reset);

        // Start with panel hidden
        if (editPanel != null)
            editPanel.SetActive(false);
    }

    /// <summary>
    /// Called when SpeciesEditEvents.RequestEdit is invoked.
    /// Opens the edit panel for the specified species index.
    /// </summary>
    private void HandleEditRequested(int speciesIndex)
    {
        Debug.Log($"EditSpeciesUI: Edit requested for species index {speciesIndex}");

        currentEditingIndex = speciesIndex;

        // Get the species data from RunSpeciesList
        if (runSpeciesList != null &&
            runSpeciesList.speciesList != null &&
            speciesIndex >= 0 &&
            speciesIndex < runSpeciesList.speciesList.Count)
        {
            currentEditingData = runSpeciesList.speciesList[speciesIndex];
            Debug.Log($"EditSpeciesUI: Editing {currentEditingData.speciesName} - {currentEditingData.variant}");

            // Populate UI fields with currentEditingData
            PopulateFields();
        }
        else
        {
            Debug.LogWarning($"EditSpeciesUI: Invalid species index {speciesIndex} or RunSpeciesList not assigned");
            currentEditingData = null;
        }

        // Show the edit panel
        Open();
    }

    /// <summary>
    /// Populate UI fields with current species data.
    /// Called when opening the edit panel.
    /// </summary>
    private void PopulateFields()
    {
        if (currentEditingData == null) return;

        Debug.Log($"PopulateFields: Name={currentEditingData.speciesName}, " +
                  $"Variant={currentEditingData.variant}, " +
                  $"Tier={currentEditingData.tier}, " +
                  $"Count={currentEditingData.count}");

        // === TIER DISPLAY ===
        // tier in SpeciesData is 0-based (0 = Tier 1, 1 = Tier 2)
        if (tierField != null)
        {
            int displayTier = currentEditingData.tier + 1; // Convert to 1-based for display
            tierField.text = $"Tier {displayTier}";
        }

        // === BASIC INFO ===
        if (nameField != null)
        {
            nameField.text = currentEditingData.speciesName.ToString();
        }

        if (variantDropdown != null)
        {
            // Set dropdown to current variant
            // Assumes dropdown options are in order: Common=0, Tropical=1, Arctic=2
            variantDropdown.value = (int)currentEditingData.variant;
        }

        if (countField != null)
        {
            countField.text = currentEditingData.count.ToString();
        }

        // === GAMEPLAY STATS ===
        if (eatingAmountField != null)
        {
            eatingAmountField.text = currentEditingData.eatingAmount.ToString("F2");
        }

        if (reproThresholdField != null)
        {
            reproThresholdField.text = currentEditingData.reproThreshold.ToString("F2");
        }

        if (reproMultiplierField != null)
        {
            reproMultiplierField.text = currentEditingData.reproductionMultiplier.ToString("F2");
        }

        if (tempDeathThresholdField != null)
        {
            tempDeathThresholdField.text = currentEditingData.deathThreshold.ToString("F2");
        }

        if (tempDeathRateField != null)
        {
            tempDeathRateField.text = currentEditingData.deathRate.ToString("F2");
        }

        if (naturalDeathVarianceField != null)
        {
            naturalDeathVarianceField.text = currentEditingData.naturalDeathVariance.ToString("F3");
        }

        if (naturalDeathRateField != null)
        {
            naturalDeathRateField.text = currentEditingData.naturalDeathRate.ToString("F3");
        }

        // === HUNTING SECTION (Tier 2+ only) ===
        // Show hunting fields only for Tier 2 and above (tier >= 1 in 0-based)
        bool showHunting = currentEditingData.tier >= 1;

        if (huntingSection != null)
        {
            huntingSection.SetActive(showHunting);
        }

        if (showHunting)
        {
            if (huntingEfficiencyField != null)
            {
                huntingEfficiencyField.text = currentEditingData.huntingEfficiency.ToString("F2");
            }

            if (huntingVarianceField != null)
            {
                huntingVarianceField.text = currentEditingData.huntingVariance.ToString("F3");
            }
        }

        // === THERMAL GRAPH ===
        ApplyThermalValuesToGraph();
    }

    /// <summary>
    /// Apply thermal parameters to the ThermalGraphUI to display the performance curve.
    /// </summary>
    private void ApplyThermalValuesToGraph()
    {
        if (currentEditingData == null || thermalGraphUI == null)
        {
            Debug.LogWarning("EditSpeciesUI: Cannot update thermal graph - data or graph reference missing");
            return;
        }

        // Set thermal parameters
        thermalGraphUI.optimalTemp = currentEditingData.optimalTempK;
        thermalGraphUI.arrhenBreadth = currentEditingData.arrhenBreadth;
        thermalGraphUI.arrhenLower = currentEditingData.arrhenLower;
        thermalGraphUI.arrhenUpper = currentEditingData.arrhenUpper;
        thermalGraphUI.lowerBound = currentEditingData.lowerBoundK;
        thermalGraphUI.upperBound = currentEditingData.upperBoundK;

        // Force graph to update
        thermalGraphUI.OnValidate();

        Debug.Log($"EditSpeciesUI: Applied thermal values - OptimalTemp={currentEditingData.optimalTempK}K, " +
                  $"LowerBound={currentEditingData.lowerBoundK}K, UpperBound={currentEditingData.upperBoundK}K");
    }

    /// <summary>
    /// Open/show the edit panel.
    /// </summary>
    public void Open()
    {
        if (editPanel != null)
        {
            editPanel.SetActive(true);
            Debug.Log("EditSpeciesUI: Panel opened");
        }
        else
        {
            // If no separate panel assigned, assume this gameObject is the panel
            gameObject.SetActive(true);
            Debug.Log("EditSpeciesUI: GameObject activated");
        }
    }

    /// <summary>
    /// Close the edit panel without saving.
    /// Can be called from Close button (X) or Cancel button.
    /// </summary>
    public void Close()
    {
        Debug.Log("EditSpeciesUI: Closing panel");

        currentEditingIndex = -1;
        currentEditingData = null;

        if (editPanel != null)
        {
            editPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        // Notify listeners that edit panel closed
        SpeciesEditEvents.NotifyEditClosed();
    }

    /// <summary>
    /// Cancel editing and close. Same as Close for now.
    /// Could show confirmation dialog in the future.
    /// </summary>
    public void Cancel()
    {
        Debug.Log("EditSpeciesUI: Cancel pressed");
        Close();
    }

    /// <summary>
    /// Save the edited data back to RunSpeciesList.
    /// </summary>
    public void SaveData()
    {
        Debug.Log($"EditSpeciesUI: Saving data for index {currentEditingIndex}");

        if (currentEditingIndex < 0 || currentEditingData == null)
        {
            Debug.LogWarning("EditSpeciesUI: No species being edited, cannot save");
            return;
        }

        // Read values from UI fields and update currentEditingData

        // Count
        if (countField != null && int.TryParse(countField.text, out int count))
        {
            currentEditingData.count = count;
        }

        // Eating Amount
        if (eatingAmountField != null && float.TryParse(eatingAmountField.text, out float eating))
        {
            currentEditingData.eatingAmount = eating;
        }

        // Repro Threshold
        if (reproThresholdField != null && float.TryParse(reproThresholdField.text, out float reproThresh))
        {
            currentEditingData.reproThreshold = reproThresh;
        }

        // Reproduction Multiplier
        if (reproMultiplierField != null && float.TryParse(reproMultiplierField.text, out float reproMult))
        {
            currentEditingData.reproductionMultiplier = reproMult;
        }

        // Temperature Death Threshold
        if (tempDeathThresholdField != null && float.TryParse(tempDeathThresholdField.text, out float tempDeathThresh))
        {
            currentEditingData.deathThreshold = tempDeathThresh;
        }

        // Temperature Death Rate
        if (tempDeathRateField != null && float.TryParse(tempDeathRateField.text, out float tempDeathRate))
        {
            currentEditingData.deathRate = tempDeathRate;
        }

        // Natural Death Variance
        if (naturalDeathVarianceField != null && float.TryParse(naturalDeathVarianceField.text, out float natDeathVar))
        {
            currentEditingData.naturalDeathVariance = natDeathVar;
        }

        // Natural Death Rate
        if (naturalDeathRateField != null && float.TryParse(naturalDeathRateField.text, out float natDeathRate))
        {
            currentEditingData.naturalDeathRate = natDeathRate;
        }

        // Hunting fields (only for Tier 2+)
        if (currentEditingData.tier >= 1)
        {
            if (huntingEfficiencyField != null && float.TryParse(huntingEfficiencyField.text, out float huntEff))
            {
                currentEditingData.huntingEfficiency = huntEff;
            }

            if (huntingVarianceField != null && float.TryParse(huntingVarianceField.text, out float huntVar))
            {
                currentEditingData.huntingVariance = huntVar;
            }
        }

        // Variant (from dropdown)
        if (variantDropdown != null)
        {
            currentEditingData.variant = (SpeciesVariant)variantDropdown.value;
        }

        // The data is already a reference to the item in runSpeciesList.speciesList,
        // so changes are automatically reflected. But we should mark it dirty for saving.

#if UNITY_EDITOR
        if (runSpeciesList != null)
        {
            UnityEditor.EditorUtility.SetDirty(runSpeciesList);
        }
#endif

        // Notify listeners that data was saved
        SpeciesEditEvents.NotifySpeciesSaved(currentEditingIndex);

        Debug.Log("EditSpeciesUI: Data saved");
        Close();
    }

    /// <summary>
    /// Delete the current species from RunSpeciesList.
    /// </summary>
    public void Delete()
    {
        Debug.Log($"EditSpeciesUI: Delete pressed for index {currentEditingIndex}");

        if (currentEditingIndex < 0 || runSpeciesList == null)
        {
            Debug.LogWarning("EditSpeciesUI: No species being edited, cannot delete");
            return;
        }

        // TODO: Add confirmation dialog before deleting

        int deletedIndex = currentEditingIndex;

        // Remove from list
        if (currentEditingIndex < runSpeciesList.speciesList.Count)
        {
            runSpeciesList.speciesList.RemoveAt(currentEditingIndex);
            Debug.Log($"EditSpeciesUI: Deleted species at index {deletedIndex}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif
        }

        // Notify listeners
        SpeciesEditEvents.NotifySpeciesDeleted(deletedIndex);

        Close();
    }

    /// <summary>
    /// Reset current species to original values from SpeciesDatabase.
    /// </summary>
    public void Reset()
    {
        Debug.Log($"EditSpeciesUI: Reset pressed for index {currentEditingIndex}");

        if (currentEditingData == null || originalDatabase == null)
        {
            Debug.LogWarning("EditSpeciesUI: Cannot reset - no data or original database not assigned");
            return;
        }

        // Find original data in SpeciesDatabase
        SpeciesData originalData = originalDatabase.GetSpecies(
            currentEditingData.speciesName,
            currentEditingData.variant
        );

        if (originalData == null)
        {
            Debug.LogWarning($"EditSpeciesUI: Original data not found for {currentEditingData.speciesName} - {currentEditingData.variant}");
            return;
        }

        // Copy values from original to current
        currentEditingData.count = originalData.count;
        currentEditingData.eatingAmount = originalData.eatingAmount;
        currentEditingData.reproductionMultiplier = originalData.reproductionMultiplier;
        currentEditingData.reproThreshold = originalData.reproThreshold;
        currentEditingData.deathThreshold = originalData.deathThreshold;
        currentEditingData.deathRate = originalData.deathRate;
        currentEditingData.minimumDeaths = originalData.minimumDeaths;
        currentEditingData.naturalDeathRate = originalData.naturalDeathRate;
        currentEditingData.naturalDeathVariance = originalData.naturalDeathVariance;
        currentEditingData.huntingEfficiency = originalData.huntingEfficiency;
        currentEditingData.huntingVariance = originalData.huntingVariance;

        // Thermal parameters
        currentEditingData.optimalTempK = originalData.optimalTempK;
        currentEditingData.arrhenBreadth = originalData.arrhenBreadth;
        currentEditingData.arrhenLower = originalData.arrhenLower;
        currentEditingData.arrhenUpper = originalData.arrhenUpper;
        currentEditingData.lowerBoundK = originalData.lowerBoundK;
        currentEditingData.upperBoundK = originalData.upperBoundK;

        Debug.Log($"EditSpeciesUI: Reset {currentEditingData.speciesName} to original values");

        // Refresh UI fields to show reset values
        PopulateFields();
    }

    /// <summary>
    /// Get the currently editing species index.
    /// Returns -1 if not editing.
    /// </summary>
    public int GetCurrentEditingIndex()
    {
        return currentEditingIndex;
    }

    /// <summary>
    /// Get the currently editing species data.
    /// Returns null if not editing.
    /// </summary>
    public SpeciesData GetCurrentEditingData()
    {
        return currentEditingData;
    }
}