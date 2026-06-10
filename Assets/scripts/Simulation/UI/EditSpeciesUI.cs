using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Globalization;

/// <summary>
/// Controls the Edit Species UI panel.
/// Subscribes to SpeciesEditEvents to know when to open.
/// Uses the species index to access data from RunSpeciesList.
/// 
/// Buttons:
/// - Close (X): Close without saving
/// - Cancel: Close without saving (same as Close)
/// - Save Data: Save changes and close (only if validation passes)
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
    [SerializeField] private TMP_Dropdown variantDropdown;     // Organism selector (catalog + Custom)
    [SerializeField] private TMP_InputField variantLabelField; // free-text variant label (Custom path)
    [SerializeField] private TMP_InputField countField;

    [Header("UI Fields - Gameplay Stats")]
    [SerializeField] private TMP_InputField eatingAmountField;
    [SerializeField] private TMP_InputField reproThresholdField;
    [SerializeField] private TMP_InputField reproMultiplierField;
    [SerializeField] private TMP_InputField tempDeathThresholdField;
    [SerializeField] private TMP_InputField tempDeathRateField;
    [SerializeField] private TMP_InputField tempDebuff;
    [SerializeField] private TMP_InputField naturalDeathVarianceField;
    [SerializeField] private TMP_InputField naturalDeathRateField;

    [Header("UI Fields - Condition Timescale (per-species tau; blank = inherit global)")]
    [SerializeField] private TMP_InputField conditionDrainRateField;
    [SerializeField] private TMP_InputField conditionRecoveryRateField;

    [Header("UI Fields - Resource Finding / Foraging (all tiers)")]
    // The Tier-2-only "hunting section" wrapper is gone. These foraging fields live directly in
    // the species editor for every tier; their on-screen labels are set from the constants below
    // (Resource Finding for prey, Hunting for predators). They edit the same data as before,
    // SpeciesData.huntingEfficiency / huntingVariance (CSV columns hunt_eff / hunt_var).
    [SerializeField] private TMP_InputField resourceFindingEfficiencyField;
    [SerializeField] private TMP_InputField resourceFindingVarianceField;
    [SerializeField] private TMP_Text resourceFindingEfficiencyLabel;
    [SerializeField] private TMP_Text resourceFindingVarianceLabel;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button resetButton;

    [Header("Debug")]
    [SerializeField] private int currentEditingIndex = -1;

    // Variant-selector redesign: dropdown option index -> catalog speciesList index
    // (-1 = the trailing "Custom" sentinel). Rebuilt each time the panel opens.
    private readonly System.Collections.Generic.List<int> _organismCatalogIndices = new System.Collections.Generic.List<int>();
    private bool _suppressOrganismCallback = false;
    private const string CUSTOM_OPTION = "Custom";

    // On-screen labels for the foraging fields, kept as constants so the wording is a one-line
    // change. Tier 1 forages for resources; for Tier 2/3 (when re-enabled) change these to
    // "Hunting Efficiency" / "Hunting Variance". Field, data, and validation stay identical.
    private const string FORAGING_EFFICIENCY_LABEL = "Resource Finding Efficiency";
    private const string FORAGING_VARIANCE_LABEL = "Resource Finding Variance";

    // Validation colors
    private static readonly Color InvalidColor = new Color(1f, 0.80f, 0.80f, 1f);
    private static readonly Color ValidColor = Color.white;

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
        public string displayName;
        public SpeciesName speciesName;

        // Gameplay
        public float eatingAmount;
        public float reproThreshold;
        public float reproductionMultiplier;
        public float deathThreshold;
        public float deathRate;
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
        public float pmax;
        public float ctMinC;
        public float ctMaxC;
        public float temperatureDebuff;

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
                displayName = data.speciesLabel,
                speciesName = data.speciesName,
                eatingAmount = data.eatingAmount,
                reproThreshold = data.reproThreshold,
                reproductionMultiplier = data.reproductionMultiplier,
                deathThreshold = data.deathThreshold,
                deathRate = data.deathRate,
                naturalDeathRate = data.naturalDeathRate,
                naturalDeathVariance = data.naturalDeathVariance,
                huntingEfficiency = data.huntingEfficiency,
                huntingVariance = data.huntingVariance,
                optimalTempK = data.optimalTempK,
                arrhenBreadth = data.arrhenBreadth,
                arrhenLower = data.arrhenLower,
                arrhenUpper = data.arrhenUpper,
                lowerBoundK = data.lowerBoundK,
                upperBoundK = data.upperBoundK,
                pmax = data.pmax,
                ctMinC = data.ctMinC,
                ctMaxC = data.ctMaxC,
                temperatureDebuff = data.TemperatureDebuff
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
            data.speciesLabel = displayName;
            data.speciesName = speciesName;
            data.eatingAmount = eatingAmount;
            data.reproThreshold = reproThreshold;
            data.reproductionMultiplier = reproductionMultiplier;
            data.deathThreshold = deathThreshold;
            data.deathRate = deathRate;
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
            data.pmax = pmax;
            data.ctMinC = ctMinC;
            data.ctMaxC = ctMaxC;
            data.TemperatureDebuff = temperatureDebuff;
        }
    }

    private void OnEnable()
    {
        SpeciesEditEvents.OnEditRequested += HandleEditRequested;

        // Variant-selector redesign: data-driven Organism dropdown change handler.
        if (variantDropdown != null)
        {
            variantDropdown.onValueChanged.RemoveListener(OnOrganismSelected);
            variantDropdown.onValueChanged.AddListener(OnOrganismSelected);
        }

        // Wire up button listeners (RemoveListener first to prevent duplicates)
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Cancel);
            cancelButton.onClick.AddListener(Cancel);
        }
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveData);
            saveButton.onClick.AddListener(SaveData);
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(Delete);
            deleteButton.onClick.AddListener(Delete);
        }
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(Reset);
            resetButton.onClick.AddListener(Reset);
        }
    }

    private void OnDisable()
    {
        SpeciesEditEvents.OnEditRequested -= HandleEditRequested;

        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
        if (saveButton != null) saveButton.onClick.RemoveListener(SaveData);
        if (deleteButton != null) deleteButton.onClick.RemoveListener(Delete);
        if (resetButton != null) resetButton.onClick.RemoveListener(Reset);
    }

    private void Start()
    {
        // Start with panel hidden
        if (editPanel != null)
            editPanel.SetActive(false);

        // Set content types so keyboard/input only allows valid characters
        SetContentType(countField, TMP_InputField.ContentType.IntegerNumber);
        SetContentType(eatingAmountField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(reproThresholdField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(reproMultiplierField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(tempDeathThresholdField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(tempDeathRateField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(tempDebuff, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(naturalDeathRateField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(naturalDeathVarianceField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(resourceFindingEfficiencyField, TMP_InputField.ContentType.DecimalNumber);
        SetContentType(resourceFindingVarianceField, TMP_InputField.ContentType.DecimalNumber);
    }

    private static void SetContentType(TMP_InputField field, TMP_InputField.ContentType type)
    {
        if (field != null)
            field.contentType = type;
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

            // CREATE BACKUP when opening
            backupData = SpeciesDataBackup.CreateFrom(currentEditingData);

            Debug.Log($"EditSpeciesUI: Editing {currentEditingData.speciesName} - {currentEditingData.variant} (backup created)");

            PopulateOrganismDropdown();   // rebuild from catalog (extensibility: N organisms)
            PopulateFields();
            ClearAllValidationColors();
        }
        else
        {
            Debug.LogWarning($"EditSpeciesUI: Invalid species index {speciesIndex}");
            currentEditingData = null;
            backupData = null;
        }

        Open();
    }

    // ==================== Variant-selector redesign (data-driven Organism dropdown) ====================

    /// <summary>
    /// Rebuild the Organism dropdown as a projection of the catalog (every Tier-1
    /// SpeciesDatabase entry by displayName) + a trailing "Custom". No hard-coded list —
    /// add a catalog entry and it appears here with zero code change.
    /// </summary>
    private void PopulateOrganismDropdown()
    {
        if (variantDropdown == null) return;
        _organismCatalogIndices.Clear();
        var options = new System.Collections.Generic.List<string>();
        if (originalDatabase != null && originalDatabase.speciesList != null)
        {
            for (int i = 0; i < originalDatabase.speciesList.Count; i++)
            {
                var e = originalDatabase.speciesList[i];
                if (e.tier != 0) continue;  // Tier 1 only (Tier 2 is gated off)
                string label = e.DisplayName;
                options.Add(label);
                _organismCatalogIndices.Add(i);
            }
        }
        options.Add(CUSTOM_OPTION);
        _organismCatalogIndices.Add(-1);

        _suppressOrganismCallback = true;
        variantDropdown.ClearOptions();
        variantDropdown.AddOptions(options);
        _suppressOrganismCallback = false;
    }

    /// <summary>Dropdown index of the catalog organism matching this row (speciesName+variant),
    /// or the trailing "Custom" option when none matches.</summary>
    private int FindOrganismOption(SpeciesData data)
    {
        if (data != null && originalDatabase != null)
        {
            for (int opt = 0; opt < _organismCatalogIndices.Count; opt++)
            {
                int ci = _organismCatalogIndices[opt];
                if (ci < 0) continue;
                var e = originalDatabase.speciesList[ci];
                if (e.speciesName == data.speciesName && e.variant == data.variant)
                    return opt;
            }
        }
        return Mathf.Max(0, _organismCatalogIndices.Count - 1);  // Custom (last option)
    }

    /// <summary>
    /// The user picked an organism. A preset copies the catalog template's params into the
    /// working row (preserving its count + list index); "Custom" switches the row to a
    /// free-text, fully-editable organism. The label never re-derives params — it's a copy.
    /// </summary>
    private void OnOrganismSelected(int dropdownIdx)
    {
        if (_suppressOrganismCallback || currentEditingData == null) return;
        if (dropdownIdx < 0 || dropdownIdx >= _organismCatalogIndices.Count) return;
        int catalogIdx = _organismCatalogIndices[dropdownIdx];

        if (catalogIdx >= 0)
        {
            int keepCount = currentEditingData.count;
            int keepIndex = currentEditingData.index;
            currentEditingData.CopyFrom(originalDatabase.speciesList[catalogIdx]);
            currentEditingData.count = keepCount;
            currentEditingData.index = keepIndex;
        }
        else
        {
            currentEditingData.variant = SpeciesVariant.Custom;
            currentEditingData.speciesName = SpeciesName.Custom;
        }
        PopulateFields();  // refresh fields to reflect the new params/label
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
        // Name field shows speciesLabel if set, otherwise falls back to speciesName
        if (nameField != null)
        {
            string displayText = !string.IsNullOrEmpty(currentEditingData.speciesLabel)
                ? currentEditingData.speciesLabel
                : currentEditingData.speciesName.ToString();
            nameField.text = displayText;
        }

        // Variant-selector redesign: select the catalog organism matching this row
        // (speciesName+variant), else the trailing "Custom". SetValueWithoutNotify so
        // this load doesn't fire OnOrganismSelected.
        if (variantDropdown != null)
            variantDropdown.SetValueWithoutNotify(FindOrganismOption(currentEditingData));

        if (variantLabelField != null)
            variantLabelField.text = currentEditingData.variantLabel ?? "";

        if (countField != null)
            countField.text = currentEditingData.count.ToString();

        // === GAMEPLAY STATS ===
        if (eatingAmountField != null)
            eatingAmountField.text = currentEditingData.eatingAmount.ToString("F2", CultureInfo.InvariantCulture);

        // Group 3: per-species condition drain/recovery — blank means inherit global (value < 0)
        if (conditionDrainRateField != null)
            conditionDrainRateField.text = currentEditingData.conditionDrainRate < 0f
                ? "" : currentEditingData.conditionDrainRate.ToString("F3", CultureInfo.InvariantCulture);
        if (conditionRecoveryRateField != null)
            conditionRecoveryRateField.text = currentEditingData.conditionRecoveryRate < 0f
                ? "" : currentEditingData.conditionRecoveryRate.ToString("F3", CultureInfo.InvariantCulture);

        if (reproThresholdField != null)
            reproThresholdField.text = currentEditingData.reproThreshold.ToString("F2", CultureInfo.InvariantCulture);

        if (reproMultiplierField != null)
            reproMultiplierField.text = currentEditingData.reproductionMultiplier.ToString("F2", CultureInfo.InvariantCulture);

        if (tempDeathThresholdField != null)
            tempDeathThresholdField.text = currentEditingData.deathThreshold.ToString("F2", CultureInfo.InvariantCulture);

        if (tempDeathRateField != null)
            tempDeathRateField.text = currentEditingData.deathRate.ToString("F2", CultureInfo.InvariantCulture);

        if (tempDebuff != null)
            tempDebuff.text = currentEditingData.TemperatureDebuff.ToString("F2", CultureInfo.InvariantCulture);

        if (naturalDeathVarianceField != null)
            naturalDeathVarianceField.text = currentEditingData.naturalDeathVariance.ToString("F3", CultureInfo.InvariantCulture);

        if (naturalDeathRateField != null)
            naturalDeathRateField.text = currentEditingData.naturalDeathRate.ToString("F3", CultureInfo.InvariantCulture);

        // === RESOURCE FINDING (foraging) FIELDS (all tiers) ===
        // Labels come from constants so the wording swaps per tier (Resource Finding for prey,
        // Hunting for predators) without touching the data or validation.
        if (resourceFindingEfficiencyLabel != null)
            resourceFindingEfficiencyLabel.text = FORAGING_EFFICIENCY_LABEL;
        if (resourceFindingVarianceLabel != null)
            resourceFindingVarianceLabel.text = FORAGING_VARIANCE_LABEL;

        if (resourceFindingEfficiencyField != null)
            resourceFindingEfficiencyField.text = currentEditingData.huntingEfficiency.ToString("F2", CultureInfo.InvariantCulture);

        if (resourceFindingVarianceField != null)
            resourceFindingVarianceField.text = currentEditingData.huntingVariance.ToString("F3", CultureInfo.InvariantCulture);

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

        // Clear validation colors
        ClearAllValidationColors();

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
    /// Only saves if all validation passes.
    /// </summary>
    public void SaveData()
    {
        Debug.Log($"EditSpeciesUI: Attempting to save data for index {currentEditingIndex}");

        if (currentEditingIndex < 0 || currentEditingData == null)
        {
            Debug.LogWarning("EditSpeciesUI: No species being edited, cannot save");
            return;
        }

        // Clear previous validation colors
        ClearAllValidationColors();

        // === VALIDATE ALL FIELDS ===
        bool allValid = true;

        // Temporary variables to hold parsed values
        int count = 0;
        float eatingAmount = 0f;
        float reproThreshold = 0f;
        float reproMultiplier = 0f;
        float tempDeathThreshold = 0f;
        float tempDeathRate = 0f;
        float naturalDeathVariance = 0f;
        float naturalDeathRate = 0f;
        float tempDebuffValue = 0f;
        float huntingEfficiency = 0f;
        float huntingVariance = 0f;

        // Validate name field (must not be empty)
        if (nameField != null)
        {
            string nameText = nameField.text?.Trim();
            if (string.IsNullOrEmpty(nameText))
            {
                SetFieldColor(nameField, InvalidColor);
                allValid = false;
                Debug.LogWarning("EditSpeciesUI: Name field is empty");
            }
        }

        // Validate count (integer, non-negative)
        allValid &= TryReadInt(countField, out count, min: 0);

        // Validate gameplay stats (floats, non-negative)
        allValid &= TryReadFloat(eatingAmountField, out eatingAmount, min: 0f);
        allValid &= TryReadFloat(reproThresholdField, out reproThreshold, min: 0f, max: 1f);
        allValid &= TryReadFloat(reproMultiplierField, out reproMultiplier, min: 0f);
        allValid &= TryReadFloat(tempDeathThresholdField, out tempDeathThreshold, min: 0f, max: 1f);
        allValid &= TryReadFloat(tempDeathRateField, out tempDeathRate, min: 0f, max: 1f);
        allValid &= TryReadFloat(tempDebuff, out tempDebuffValue);
        allValid &= TryReadFloat(naturalDeathVarianceField, out naturalDeathVariance, min: 0f);
        allValid &= TryReadFloat(naturalDeathRateField, out naturalDeathRate, min: 0f);

        // Validate the resource-finding (foraging) fields for all tiers.
        allValid &= TryReadFloat(resourceFindingEfficiencyField, out huntingEfficiency, min: 0f, max: 1f);
        allValid &= TryReadFloat(resourceFindingVarianceField, out huntingVariance, min: 0f);

        // If any validation failed, stop here and don't save
        if (!allValid)
        {
            Debug.LogWarning("EditSpeciesUI: Validation failed - not saving. Please fix highlighted fields.");
            return;
        }

        // === ALL VALID - SAVE DATA ===

        // Save display name
        if (nameField != null)
        {
            string newDisplayName = nameField.text.Trim();
            currentEditingData.speciesLabel = newDisplayName;
            Debug.Log($"EditSpeciesUI: Display name set to '{newDisplayName}'");
        }

        // Variant-selector redesign: the organism (enum bucket + params) is set by the
        // dropdown selection, not derived from the dropdown index. Persist the free-text
        // variant label here.
        if (variantLabelField != null)
            currentEditingData.variantLabel = variantLabelField.text?.Trim() ?? "";

        // Save validated values
        currentEditingData.count = count;
        currentEditingData.eatingAmount = eatingAmount;
        currentEditingData.reproThreshold = reproThreshold;
        currentEditingData.reproductionMultiplier = reproMultiplier;
        currentEditingData.deathThreshold = tempDeathThreshold;
        currentEditingData.deathRate = tempDeathRate;
        currentEditingData.naturalDeathVariance = naturalDeathVariance;
        currentEditingData.naturalDeathRate = naturalDeathRate;
        currentEditingData.TemperatureDebuff = tempDebuffValue;

        // Group 3: per-species condition drain/recovery — blank/empty (or invalid) => -1 (inherit global)
        currentEditingData.conditionDrainRate = ParseRateOrInherit(conditionDrainRateField);
        currentEditingData.conditionRecoveryRate = ParseRateOrInherit(conditionRecoveryRateField);

        // Save foraging fields (all tiers).
        currentEditingData.huntingEfficiency = huntingEfficiency;
        currentEditingData.huntingVariance = huntingVariance;

        // Save thermal parameters from controller
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

        Debug.Log($"EditSpeciesUI: Data saved for {currentEditingData.speciesLabel} ({currentEditingData.speciesName} - {currentEditingData.variant})");

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
            string deletedName = currentEditingData?.speciesLabel ??
                                 currentEditingData?.speciesName.ToString() ?? "Unknown";
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

        // Refresh all UI fields and clear validation
        PopulateFields();
        ClearAllValidationColors();
    }

    /// <summary>
    /// Reset to original values from SpeciesDatabase (factory reset).
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

        // Variant-selector redesign: canonical full copy (the old field-by-field block
        // omitted variantLabel + conditionDrainRate/conditionRecoveryRate). Preserve this
        // row's list-index identity.
        int keepIndex = currentEditingData.index;
        currentEditingData.CopyFrom(originalData);
        currentEditingData.index = keepIndex;

        Debug.Log($"EditSpeciesUI: Factory reset {currentEditingData.speciesName} to original database values");

        // Also update the backup
        backupData = SpeciesDataBackup.CreateFrom(currentEditingData);

        PopulateFields();
        ClearAllValidationColors();
    }

    // ==================== Validation Methods ====================

    /// <summary>
    /// Try to read a float from an input field. Highlights red if invalid or out of range.
    /// </summary>
    private bool TryReadFloat(TMP_InputField field, out float value,
        float min = float.MinValue, float max = float.MaxValue)
    {
        value = 0f;

        if (field == null)
            return true; // Null field is considered valid (optional field)

        string text = field.text;
        if (string.IsNullOrEmpty(text))
            text = "";
        text = text.Trim();

        bool valid = float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );

        if (valid && (value < min || value > max))
            valid = false;

        SetFieldColor(field, valid ? ValidColor : InvalidColor);
        return valid;
    }

    /// <summary>
    /// Group 3: read a per-species condition rate. Blank/empty => -1 (inherit the global
    /// rate). A valid non-negative number is used as-is; anything else falls back to -1.
    /// </summary>
    private float ParseRateOrInherit(TMP_InputField field)
    {
        if (field == null) return -1f;
        string t = field.text?.Trim();
        if (string.IsNullOrEmpty(t)) return -1f;
        if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v >= 0f)
            return v;
        return -1f;
    }

    /// <summary>
    /// Try to read an integer from an input field. Highlights red if invalid or out of range.
    /// </summary>
    private bool TryReadInt(TMP_InputField field, out int value,
        int min = int.MinValue, int max = int.MaxValue)
    {
        value = 0;

        if (field == null)
            return true; // Null field is considered valid (optional field)

        string text = field.text;
        if (string.IsNullOrEmpty(text))
            text = "";
        text = text.Trim();

        bool valid = int.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value
        );

        if (valid && (value < min || value > max))
            valid = false;

        SetFieldColor(field, valid ? ValidColor : InvalidColor);
        return valid;
    }

    /// <summary>
    /// Set the background color of an input field.
    /// </summary>
    private static void SetFieldColor(TMP_InputField field, Color color)
    {
        if (field == null) return;

        // Try to get the Image component (usually the background)
        Image img = field.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
            return;
        }

        // Fallback to targetGraphic
        if (field.targetGraphic != null)
        {
            field.targetGraphic.color = color;
        }
    }

    /// <summary>
    /// Clear all validation colors back to white.
    /// </summary>
    private void ClearAllValidationColors()
    {
        SetFieldColor(nameField, ValidColor);
        SetFieldColor(countField, ValidColor);
        SetFieldColor(eatingAmountField, ValidColor);
        SetFieldColor(reproThresholdField, ValidColor);
        SetFieldColor(reproMultiplierField, ValidColor);
        SetFieldColor(tempDeathThresholdField, ValidColor);
        SetFieldColor(tempDeathRateField, ValidColor);
        SetFieldColor(naturalDeathVarianceField, ValidColor);
        SetFieldColor(naturalDeathRateField, ValidColor);
        SetFieldColor(resourceFindingEfficiencyField, ValidColor);
        SetFieldColor(resourceFindingVarianceField, ValidColor);
        SetFieldColor(tempDebuff, ValidColor);
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
        if (currentEditingData.speciesLabel != backupData.displayName) return true;

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
            if (!Mathf.Approximately(currentThermal.pmax, backupData.pmax)) return true;
            if (!Mathf.Approximately(currentThermal.ctMinC, backupData.ctMinC)) return true;
            if (!Mathf.Approximately(currentThermal.ctMaxC, backupData.ctMaxC)) return true;
        }

        return false;
    }
}