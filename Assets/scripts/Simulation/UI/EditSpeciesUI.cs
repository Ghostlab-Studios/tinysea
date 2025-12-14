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
/// - Reset: Reset to values when panel was opened (backup)
/// </summary>
public class EditSpeciesUI : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private RunSpeciesList runSpeciesList;
    [SerializeField] private SpeciesDatabase originalDatabase; // For factory reset

    [Header("Panel Reference")]
    [SerializeField] private GameObject editPanel;

    [Header("Thermal Editor")]
    [Tooltip("The controller that manages all thermal parameter sliders and the graph")]
    [SerializeField] private ThermalParameterController thermalController;

    [Header("UI Fields - Header")]
    [SerializeField] private TextMeshProUGUI tierField;

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
    [SerializeField] private GameObject huntingSection;
    [SerializeField] private TMP_InputField huntingEfficiencyField;
    [SerializeField] private TMP_InputField huntingVarianceField;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button resetButton;

    [Header("Debug")]
    [SerializeField] private int currentEditingIndex = -1;

    // Current data being edited (reference to the actual data in RunSpeciesList)
    private SpeciesData currentEditingData;

    // Backup of the data when panel was opened (for Reset functionality)
    private SpeciesDataBackup backupData;

    /// <summary>
    /// Stores a snapshot of species data for reset functionality.
    /// This is a value copy, not a reference.
    /// </summary>
    private class SpeciesDataBackup
    {
        // Basic
        public int count;
        public SpeciesVariant variant;

        // Gameplay
        public float eatingAmount;
        public float reproThreshold;
        public float reproductionMultiplier;
        public float deathThreshold;
        public float deathRate;
        public float minimumDeaths;
        public float naturalDeathRate;
        public float naturalDeathVariance;

        // Hunting
        public float huntingEfficiency;
        public float huntingVariance;

        // Thermal
        public float optimalTempK;
        public float arrhenBreadth;
        public float arrhenLower;
        public float arrhenUpper;
        public float lowerBoundK;
        public float upperBoundK;

        /// <summary>
        /// Create a backup from SpeciesData
        /// </summary>
        public static SpeciesDataBackup CreateFrom(SpeciesData data)
        {
            if (data == null) return null;

            return new SpeciesDataBackup
            {
                count = data.count,
                variant = data.variant,
                eatingAmount = data.eatingAmount,
                reproThreshold = data.reproThreshold,
                reproductionMultiplier = data.reproductionMultiplier,
                deathThreshold = data.deathThreshold,
                deathRate = data.deathRate,
                minimumDeaths = data.minimumDeaths,
                naturalDeathRate = data.naturalDeathRate,
                naturalDeathVariance = data.naturalDeathVariance,
                huntingEfficiency = data.huntingEfficiency,
                huntingVariance = data.huntingVariance,
                optimalTempK = data.optimalTempK,
                arrhenBreadth = data.arrhenBreadth,
                arrhenLower = data.arrhenLower,
                arrhenUpper = data.arrhenUpper,
                lowerBoundK = data.lowerBoundK,
                upperBoundK = data.upperBoundK
            };
        }

        /// <summary>
        /// Restore backup values to SpeciesData
        /// </summary>
        public void RestoreTo(SpeciesData data)
        {
            if (data == null) return;

            data.count = count;
            data.variant = variant;
            data.eatingAmount = eatingAmount;
            data.reproThreshold = reproThreshold;
            data.reproductionMultiplier = reproductionMultiplier;
            data.deathThreshold = deathThreshold;
            data.deathRate = deathRate;
            data.minimumDeaths = minimumDeaths;
            data.naturalDeathRate = naturalDeathRate;
            data.naturalDeathVariance = naturalDeathVariance;
            data.huntingEfficiency = huntingEfficiency;
            data.huntingVariance = huntingVariance;
            data.optimalTempK = optimalTempK;
            data.arrhenBreadth = arrhenBreadth;
            data.arrhenLower = arrhenLower;
            data.arrhenUpper = arrhenUpper;
            data.lowerBoundK = lowerBoundK;
            data.upperBoundK = upperBoundK;
        }
    }

    private void OnEnable()
    {
        SpeciesEditEvents.OnEditRequested += HandleEditRequested;
    }

    private void OnDisable()
    {
        SpeciesEditEvents.OnEditRequested -= HandleEditRequested;
    }

    private void Start()
    {
        // Wire up button listeners
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
    /// </summary>
    private void HandleEditRequested(int speciesIndex)
    {
        Debug.Log($"EditSpeciesUI: Edit requested for species index {speciesIndex}");

        currentEditingIndex = speciesIndex;

        if (runSpeciesList != null &&
            runSpeciesList.speciesList != null &&
            speciesIndex >= 0 &&
            speciesIndex < runSpeciesList.speciesList.Count)
        {
            currentEditingData = runSpeciesList.speciesList[speciesIndex];

            // *** CREATE BACKUP when opening ***
            backupData = SpeciesDataBackup.CreateFrom(currentEditingData);

            Debug.Log($"EditSpeciesUI: Editing {currentEditingData.speciesName} - {currentEditingData.variant} (backup created)");

            PopulateFields();
        }
        else
        {
            Debug.LogWarning($"EditSpeciesUI: Invalid species index {speciesIndex}");
            currentEditingData = null;
            backupData = null;
        }

        Open();
    }

    /// <summary>
    /// Populate all UI fields with current species data.
    /// </summary>
    private void PopulateFields()
    {
        if (currentEditingData == null) return;

        Debug.Log($"PopulateFields: {currentEditingData.speciesName}, Variant={currentEditingData.variant}, Tier={currentEditingData.tier}");

        // === TIER DISPLAY ===
        if (tierField != null)
        {
            int displayTier = currentEditingData.tier + 1;
            tierField.text = $"Tier {displayTier}";
        }

        // === BASIC INFO ===
        if (nameField != null)
            nameField.text = currentEditingData.speciesName.ToString();

        if (variantDropdown != null)
            variantDropdown.value = (int)currentEditingData.variant;

        if (countField != null)
            countField.text = currentEditingData.count.ToString();

        // === GAMEPLAY STATS ===
        if (eatingAmountField != null)
            eatingAmountField.text = currentEditingData.eatingAmount.ToString("F2");

        if (reproThresholdField != null)
            reproThresholdField.text = currentEditingData.reproThreshold.ToString("F2");

        if (reproMultiplierField != null)
            reproMultiplierField.text = currentEditingData.reproductionMultiplier.ToString("F2");

        if (tempDeathThresholdField != null)
            tempDeathThresholdField.text = currentEditingData.deathThreshold.ToString("F2");

        if (tempDeathRateField != null)
            tempDeathRateField.text = currentEditingData.deathRate.ToString("F2");

        if (naturalDeathVarianceField != null)
            naturalDeathVarianceField.text = currentEditingData.naturalDeathVariance.ToString("F3");

        if (naturalDeathRateField != null)
            naturalDeathRateField.text = currentEditingData.naturalDeathRate.ToString("F3");

        // === HUNTING SECTION (Tier 2+ only) ===
        bool showHunting = currentEditingData.tier >= 1;

        if (huntingSection != null)
            huntingSection.SetActive(showHunting);

        if (showHunting)
        {
            if (huntingEfficiencyField != null)
                huntingEfficiencyField.text = currentEditingData.huntingEfficiency.ToString("F2");

            if (huntingVarianceField != null)
                huntingVarianceField.text = currentEditingData.huntingVariance.ToString("F3");
        }

        // === THERMAL PARAMETERS (via Controller) ===
        LoadThermalParameters();
    }

    /// <summary>
    /// Load thermal parameters into the ThermalParameterController.
    /// </summary>
    private void LoadThermalParameters()
    {
        if (thermalController == null)
        {
            Debug.LogWarning("EditSpeciesUI: ThermalParameterController not assigned");
            return;
        }

        if (currentEditingData == null)
        {
            Debug.LogWarning("EditSpeciesUI: No species data to load thermal parameters from");
            return;
        }

        // Load all thermal values into the controller
        // The controller will update all sliders and the graph
        thermalController.LoadFromSpeciesData(currentEditingData);

        Debug.Log($"EditSpeciesUI: Loaded thermal parameters into controller - " +
                  $"OptimalTemp={currentEditingData.optimalTempK}K");
    }

    /// <summary>
    /// Open the edit panel.
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
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Close without saving. 
    /// Note: Changes to sliders don't affect actual data until Save is pressed.
    /// </summary>
    public void Close()
    {
        Debug.Log("EditSpeciesUI: Closing panel (no save)");

        currentEditingIndex = -1;
        currentEditingData = null;
        backupData = null;

        // Clear the graph highlight
        if (thermalController != null)
            thermalController.ClearActiveHighlight();

        if (editPanel != null)
            editPanel.SetActive(false);
        else
            gameObject.SetActive(false);

        SpeciesEditEvents.NotifyEditClosed();
    }

    /// <summary>
    /// Cancel editing. Same as Close.
    /// </summary>
    public void Cancel()
    {
        Debug.Log("EditSpeciesUI: Cancel pressed");
        Close();
    }

    /// <summary>
    /// Save all edited data back to the SpeciesData in RunSpeciesList.
    /// </summary>
    public void SaveData()
    {
        Debug.Log($"EditSpeciesUI: Saving data for index {currentEditingIndex}");

        if (currentEditingIndex < 0 || currentEditingData == null)
        {
            Debug.LogWarning("EditSpeciesUI: No species being edited, cannot save");
            return;
        }

        // === READ VALUES FROM UI FIELDS ===

        // Count
        if (countField != null && int.TryParse(countField.text, out int count))
            currentEditingData.count = count;

        // Variant
        if (variantDropdown != null)
            currentEditingData.variant = (SpeciesVariant)variantDropdown.value;

        // Eating Amount
        if (eatingAmountField != null && float.TryParse(eatingAmountField.text, out float eating))
            currentEditingData.eatingAmount = eating;

        // Repro Threshold
        if (reproThresholdField != null && float.TryParse(reproThresholdField.text, out float reproThresh))
            currentEditingData.reproThreshold = reproThresh;

        // Reproduction Multiplier
        if (reproMultiplierField != null && float.TryParse(reproMultiplierField.text, out float reproMult))
            currentEditingData.reproductionMultiplier = reproMult;

        // Temperature Death Threshold
        if (tempDeathThresholdField != null && float.TryParse(tempDeathThresholdField.text, out float tempDeathThresh))
            currentEditingData.deathThreshold = tempDeathThresh;

        // Temperature Death Rate
        if (tempDeathRateField != null && float.TryParse(tempDeathRateField.text, out float tempDeathRate))
            currentEditingData.deathRate = tempDeathRate;

        // Natural Death Variance
        if (naturalDeathVarianceField != null && float.TryParse(naturalDeathVarianceField.text, out float natDeathVar))
            currentEditingData.naturalDeathVariance = natDeathVar;

        // Natural Death Rate
        if (naturalDeathRateField != null && float.TryParse(naturalDeathRateField.text, out float natDeathRate))
            currentEditingData.naturalDeathRate = natDeathRate;

        // Hunting fields (Tier 2+ only)
        if (currentEditingData.tier >= 1)
        {
            if (huntingEfficiencyField != null && float.TryParse(huntingEfficiencyField.text, out float huntEff))
                currentEditingData.huntingEfficiency = huntEff;

            if (huntingVarianceField != null && float.TryParse(huntingVarianceField.text, out float huntVar))
                currentEditingData.huntingVariance = huntVar;
        }

        // === SAVE THERMAL PARAMETERS FROM CONTROLLER ===
        if (thermalController != null)
        {
            thermalController.SaveToSpeciesData(currentEditingData);
            Debug.Log($"EditSpeciesUI: Saved thermal parameters - OptimalTemp={currentEditingData.optimalTempK}K");
        }

        // Mark scriptable object as dirty for Unity to save
#if UNITY_EDITOR
        if (runSpeciesList != null)
            UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif

        // Notify listeners
        SpeciesEditEvents.NotifySpeciesSaved(currentEditingIndex);

        Debug.Log($"EditSpeciesUI: Data saved for {currentEditingData.speciesName} - {currentEditingData.variant}");

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

        int deletedIndex = currentEditingIndex;

        if (currentEditingIndex < runSpeciesList.speciesList.Count)
        {
            string deletedName = currentEditingData?.speciesName.ToString() ?? "Unknown";
            runSpeciesList.speciesList.RemoveAt(currentEditingIndex);
            Debug.Log($"EditSpeciesUI: Deleted {deletedName} at index {deletedIndex}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif
        }

        SpeciesEditEvents.NotifySpeciesDeleted(deletedIndex);
        Close();
    }

    /// <summary>
    /// Reset to the values that were present when the panel was opened.
    /// Uses the backup created in HandleEditRequested.
    /// </summary>
    public void Reset()
    {
        Debug.Log($"EditSpeciesUI: Reset pressed for index {currentEditingIndex}");

        if (currentEditingData == null || backupData == null)
        {
            Debug.LogWarning("EditSpeciesUI: Cannot reset - no data or backup available");
            return;
        }

        // Restore values from backup
        backupData.RestoreTo(currentEditingData);

        Debug.Log($"EditSpeciesUI: Reset {currentEditingData.speciesName} to values from when panel was opened");

        // Refresh all UI fields to show restored values
        PopulateFields();
    }

    /// <summary>
    /// Reset to original values from SpeciesDatabase (factory reset).
    /// Call this if you want to reset to the original database values instead of backup.
    /// </summary>
    public void ResetToOriginalDatabase()
    {
        Debug.Log($"EditSpeciesUI: Factory reset for index {currentEditingIndex}");

        if (currentEditingData == null || originalDatabase == null)
        {
            Debug.LogWarning("EditSpeciesUI: Cannot factory reset - no data or database");
            return;
        }

        SpeciesData originalData = originalDatabase.GetSpecies(
            currentEditingData.speciesName,
            currentEditingData.variant
        );

        if (originalData == null)
        {
            Debug.LogWarning($"EditSpeciesUI: Original data not found in database");
            return;
        }

        // Copy all values from original database
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
        currentEditingData.optimalTempK = originalData.optimalTempK;
        currentEditingData.arrhenBreadth = originalData.arrhenBreadth;
        currentEditingData.arrhenLower = originalData.arrhenLower;
        currentEditingData.arrhenUpper = originalData.arrhenUpper;
        currentEditingData.lowerBoundK = originalData.lowerBoundK;
        currentEditingData.upperBoundK = originalData.upperBoundK;

        Debug.Log($"EditSpeciesUI: Factory reset {currentEditingData.speciesName} to original database values");

        // Also update the backup so Reset goes to factory values
        backupData = SpeciesDataBackup.CreateFrom(currentEditingData);

        PopulateFields();
    }

    // ==================== Public Getters ====================

    public int GetCurrentEditingIndex() => currentEditingIndex;
    public SpeciesData GetCurrentEditingData() => currentEditingData;

    /// <summary>
    /// Check if there are unsaved changes by comparing current values to backup.
    /// </summary>
    public bool HasUnsavedChanges()
    {
        if (currentEditingData == null || backupData == null) return false;

        // Compare key fields
        if (currentEditingData.count != backupData.count) return true;
        if (currentEditingData.variant != backupData.variant) return true;

        // Check thermal parameters from controller
        if (thermalController != null)
        {
            var currentThermal = thermalController.GetCurrentValues();
            if (!Mathf.Approximately(currentThermal.optimalTempK, backupData.optimalTempK)) return true;
            if (!Mathf.Approximately(currentThermal.lowerBoundK, backupData.lowerBoundK)) return true;
            if (!Mathf.Approximately(currentThermal.upperBoundK, backupData.upperBoundK)) return true;
            if (!Mathf.Approximately(currentThermal.arrhenBreadth, backupData.arrhenBreadth)) return true;
            if (!Mathf.Approximately(currentThermal.arrhenLower, backupData.arrhenLower)) return true;
            if (!Mathf.Approximately(currentThermal.arrhenUpper, backupData.arrhenUpper)) return true;
        }

        return false;
    }
}