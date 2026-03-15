using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class SpeciesUIController : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private SpeciesDatabase speciesDatabase; // Immutable catalog (defaults/fallback)
    [SerializeField] private RunSpeciesList runSpeciesList;   // Mutable runtime data source

    [Header("Species Selection")]
    [SerializeField] private string displayNameOverride = "";
    [SerializeField] private SpeciesName speciesName = SpeciesName.Cyplo;
    [SerializeField] private SpeciesVariant speciesVariant = SpeciesVariant.Common;

    [Header("Graph UI")]
    [SerializeField] private ThermalGraphUI thermalGraphUI;

    [Header("Display UI References")]
    [SerializeField] private TextMeshProUGUI index;  // Visual display index (#01, #02, etc.) - just for UI
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TMP_InputField countText;
    [SerializeField] private Button editButton;

    private SpeciesData currentSpeciesData;
    private SpeciesName lastSpeciesName;
    private SpeciesVariant lastSpeciesVariant;
    private string lastSpeciesDisplayNameOverride;

    /// <summary>
    /// The actual index of this species in the RunSpeciesList.
    /// This is what we send to the edit panel - NOT the visual display index.
    /// </summary>
    private int runSpeciesListIndex = -1;

    void Start()
    {
        // Only fall back to speciesDatabase if Initialize() hasn't already
        // provided data from RunSpeciesList. Initialize() sets runSpeciesListIndex >= 0
        // which means currentSpeciesData already points to the RunSpeciesList object.
        if (runSpeciesListIndex < 0)
        {
            UpdateSpeciesData();
        }

        // Wire up edit button to fire event
        if (editButton != null)
        {
            editButton.onClick.AddListener(OnEditButtonClicked);
        }

        // Wire up count field for immediate save on edit
        if (countText != null)
        {
            countText.contentType = TMP_InputField.ContentType.IntegerNumber;
            countText.onEndEdit.AddListener(OnCountFieldEndEdit);
        }
    }

    void OnEnable()
    {
        // Subscribe to save events to auto-refresh when data changes
        SpeciesEditEvents.OnSpeciesSaved += HandleSpeciesSaved;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SpeciesEditEvents.OnSpeciesSaved -= HandleSpeciesSaved;
    }

    void OnDestroy()
    {
        // Clean up listeners
        if (editButton != null)
        {
            editButton.onClick.RemoveListener(OnEditButtonClicked);
        }
        if (countText != null)
        {
            countText.onEndEdit.RemoveListener(OnCountFieldEndEdit);
        }
    }

    void OnValidate()
    {
        // Check if selection changed (editor only)
        if (speciesName != lastSpeciesName || speciesVariant != lastSpeciesVariant)
        {
            UpdateSpeciesData();
            lastSpeciesName = speciesName;
            lastSpeciesVariant = speciesVariant;
            lastSpeciesDisplayNameOverride = displayNameOverride;
        }
    }

    /// <summary>
    /// Called when any species data is saved via EditSpeciesUI.
    /// Checks if this controller's species was the one edited and refreshes if so.
    /// </summary>
    private void HandleSpeciesSaved(int savedIndex)
    {
        // Check if this is the species that was saved
        if (savedIndex == runSpeciesListIndex && runSpeciesListIndex >= 0)
        {
            Debug.Log($"SpeciesUIController: Refreshing display for index {runSpeciesListIndex} after save");

            // Re-fetch the data from RunSpeciesList (it may have changed)
            RefreshFromRunSpeciesList();
        }
    }

    /// <summary>
    /// Refresh data from RunSpeciesList using the stored index.
    /// Call this after data has been modified externally.
    /// </summary>
    public void RefreshFromRunSpeciesList()
    {
        if (runSpeciesList == null || runSpeciesList.speciesList == null)
        {
            Debug.LogWarning("SpeciesUIController: Cannot refresh - RunSpeciesList not assigned");
            return;
        }

        if (runSpeciesListIndex < 0 || runSpeciesListIndex >= runSpeciesList.speciesList.Count)
        {
            Debug.LogWarning($"SpeciesUIController: Cannot refresh - invalid index {runSpeciesListIndex}");
            return;
        }

        // Get fresh data from RunSpeciesList
        currentSpeciesData = runSpeciesList.speciesList[runSpeciesListIndex];

        // Update local tracking vars to match
        speciesName = currentSpeciesData.speciesName;
        speciesVariant = currentSpeciesData.variant;
        displayNameOverride = currentSpeciesData.displayName;
        lastSpeciesName = speciesName;
        lastSpeciesVariant = speciesVariant;
        lastSpeciesDisplayNameOverride = displayNameOverride;

        // Refresh the UI
        ApplyThermalValues();
        UpdateUIDisplay();

        Debug.Log($"SpeciesUIController: Refreshed from RunSpeciesList - {speciesName} {speciesVariant}, Count={currentSpeciesData.count}");
    }

    /// <summary>
    /// Called when user finishes editing the count field on the background UI.
    /// Saves immediately (no Save button needed).
    /// </summary>
    private void OnCountFieldEndEdit(string newValue)
    {
        if (currentSpeciesData == null) return;

        if (int.TryParse(newValue, out int newCount) && newCount >= 0)
        {
            currentSpeciesData.count = newCount;
            Debug.Log($"SpeciesUIController: Count updated to {newCount} for {speciesName} {speciesVariant}");

#if UNITY_EDITOR
            if (runSpeciesList != null)
                UnityEditor.EditorUtility.SetDirty(runSpeciesList);
#endif
        }
        else
        {
            // Invalid input — revert to current data value
            countText.text = currentSpeciesData.count.ToString();
            Debug.LogWarning($"SpeciesUIController: Invalid count '{newValue}', reverted to {currentSpeciesData.count}");
        }
    }

    /// <summary>
    /// Called when the edit button is clicked.
    /// Fires the edit event with the ACTUAL species list index (not visual index).
    /// </summary>
    private void OnEditButtonClicked()
    {
        if (runSpeciesListIndex < 0)
        {
            Debug.LogWarning($"SpeciesUIController: Cannot edit - runSpeciesListIndex not set for {speciesName} {speciesVariant}");
            return;
        }

        Debug.Log($"SpeciesUIController: Edit button clicked for runSpeciesListIndex={runSpeciesListIndex} ({speciesName} {speciesVariant})");

        // Fire the event - EditSpeciesUI will receive this
        SpeciesEditEvents.RequestEdit(runSpeciesListIndex);
    }

    /// <summary>
    /// Initialize this UI controller with a specific species from RunSpeciesList.
    /// Called by SpeciesTierConfig when instantiating entries.
    /// </summary>
    /// <param name="visualIndex">Display index for UI (e.g., 1 shows as #01) - just for visual</param>
    /// <param name="listIndex">The actual index in RunSpeciesList.speciesList</param>
    /// <param name="speciesData">Direct reference to the SpeciesData from RunSpeciesList</param>
    public void Initialize(int visualIndex, int listIndex, SpeciesData speciesData)
    {
        // Set visual display index (just for UI display)
        if (index != null)
        {
            index.text = $"#{visualIndex:D2}";
        }

        // Store the RunSpeciesList index for edit events and refresh
        this.runSpeciesListIndex = listIndex;

        // Store direct reference to the data
        this.currentSpeciesData = speciesData;
        this.speciesName = speciesData.speciesName;
        this.speciesVariant = speciesData.variant;
        this.displayNameOverride = speciesData.displayName;

        this.lastSpeciesName = speciesName;
        this.lastSpeciesVariant = speciesVariant;

        // Update UI with the data
        ApplyThermalValues();
        UpdateUIDisplay();

        Debug.Log($"SpeciesUIController: Initialized with RunSpeciesList data - index={listIndex}, {speciesName} {speciesVariant}");
    }

    /// <summary>
    /// Initialize this UI controller with a specific species.
    /// Called by SpeciesTierConfig when instantiating entries.
    /// This overload fetches data from SpeciesDatabase (for backwards compatibility).
    /// </summary>
    /// <param name="visualIndex">Display index for UI (e.g., 1 shows as #01) - just for visual</param>
    /// <param name="name">The species name (e.g., Hexapod, Sheplik)</param>
    /// <param name="variant">The variant (Common, Arctic, Tropical)</param>
    public void Initialize(int visualIndex, SpeciesName name, SpeciesVariant variant)
    {
        // Set visual display index (just for UI display, not used for data lookup)
        if (index != null)
        {
            index.text = $"#{visualIndex:D2}";
        }

        speciesName = name;
        speciesVariant = variant;
        displayNameOverride = ""; // Clear any override

        lastSpeciesName = name;
        lastSpeciesVariant = variant;
        lastSpeciesDisplayNameOverride = "";

        UpdateSpeciesData();
    }

    /// <summary>
    /// Initialize with a specific SpeciesData directly.
    /// Useful when you already have the data and want to avoid a database lookup.
    /// </summary>
    /// <param name="data">The species data to display</param>
    public void Initialize(SpeciesData data)
    {
        if (data == null)
        {
            Debug.LogWarning("SpeciesUIController.Initialize: Null data provided!");
            return;
        }

        speciesName = data.speciesName;
        speciesVariant = data.variant;
        displayNameOverride = data.displayName;
        lastSpeciesName = data.speciesName;
        lastSpeciesVariant = data.variant;
        lastSpeciesDisplayNameOverride = data.displayName;
        currentSpeciesData = data;
        ApplyThermalValues();
        UpdateUIDisplay();
    }

    /// <summary>
    /// Set the actual index in RunSpeciesList.
    /// This is used for edit events - must be set separately from visual index.
    /// </summary>
    /// <param name="listIndex">The index in RunSpeciesList.speciesList</param>
    public void SetRunSpeciesListIndex(int listIndex)
    {
        this.runSpeciesListIndex = listIndex;
        Debug.Log($"SpeciesUIController: Set runSpeciesListIndex={listIndex} for {speciesName} {speciesVariant}");
    }

    /// <summary>
    /// Set the RunSpeciesList reference.
    /// Needed for refreshing data after edits.
    /// </summary>
    public void SetRunSpeciesList(RunSpeciesList list)
    {
        this.runSpeciesList = list;
    }

    /// <summary>
    /// Get the actual index in RunSpeciesList.
    /// </summary>
    public int GetRunSpeciesListIndex()
    {
        return runSpeciesListIndex;
    }

    /// <summary>
    /// Set just the visual display index (the #01, #02 text).
    /// This is separate from the RunSpeciesList index.
    /// </summary>
    public void SetVisualIndex(int visualIndex)
    {
        if (index != null)
        {
            index.text = $"#{visualIndex:D2}";
        }
    }

    /// <summary>
    /// Update the species data from SpeciesDatabase (editor preview / fallback)
    /// </summary>
    private void UpdateSpeciesData()
    {
        if (speciesDatabase == null)
        {
            Debug.LogWarning("Species Database not assigned!");
            return;
        }

        if (thermalGraphUI == null)
        {
            Debug.LogWarning("Thermal Graph UI not assigned!");
            return;
        }

        // Fetch the species data from database
        currentSpeciesData = GetSpecies();

        if (currentSpeciesData == null)
        {
            Debug.LogWarning($"Species data not found for {speciesName} - {speciesVariant}");
            return;
        }

        // Apply thermal values to the graph
        ApplyThermalValues();

        // Update UI display elements
        UpdateUIDisplay();
    }

    private SpeciesData GetSpecies()
    {
        if (!string.IsNullOrEmpty(displayNameOverride))
            return speciesDatabase.GetSpeciesByName(displayNameOverride) ?? speciesDatabase.GetSpecies(speciesName, speciesVariant);
        else
            return speciesDatabase.GetSpecies(speciesName, speciesVariant);
    }

    /// <summary>
    /// Apply thermal values from species data to the graph
    /// </summary>
    private void ApplyThermalValues()
    {
        if (currentSpeciesData == null || thermalGraphUI == null)
            return;

        thermalGraphUI.optimalTemp = currentSpeciesData.optimalTempK;
        thermalGraphUI.arrhenBreadth = currentSpeciesData.arrhenBreadth;
        thermalGraphUI.arrhenLower = currentSpeciesData.arrhenLower;
        thermalGraphUI.arrhenUpper = currentSpeciesData.arrhenUpper;
        thermalGraphUI.lowerBound = currentSpeciesData.lowerBoundK;
        thermalGraphUI.upperBound = currentSpeciesData.upperBoundK;
        thermalGraphUI.pmax = currentSpeciesData.pmax;
        thermalGraphUI.ctMinC = currentSpeciesData.ctMinC;
        thermalGraphUI.ctMaxC = currentSpeciesData.ctMaxC;

        // Force graph update
        if (Application.isPlaying)
        {
            thermalGraphUI.OnValidate();
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (thermalGraphUI != null)
                    thermalGraphUI.OnValidate();
            };
#endif
        }
    }

    /// <summary>
    /// Update UI display elements (icon, name, type, count)
    /// </summary>
    private void UpdateUIDisplay()
    {
        if (currentSpeciesData == null)
            return;

        // Update icon
        if (iconImage != null && currentSpeciesData.icon != null)
        {
            iconImage.sprite = currentSpeciesData.icon;
        }

        // Update name with type (e.g., "Hexapod Tropical")
        if (nameText != null)
        {
            nameText.text = getName();
        }

        // Update type text
        if (typeText != null)
        {
            typeText.text = currentSpeciesData.variant.ToString();
        }

        // Update count (editable — saves immediately on end edit)
        if (countText != null)
        {
            countText.text = currentSpeciesData.count.ToString();
        }
    }

    private string getName()
    {
       return string.IsNullOrEmpty(currentSpeciesData.displayName)
            ? $"{currentSpeciesData.speciesName} {currentSpeciesData.variant}"
            : currentSpeciesData.displayName;
    }

    /// <summary>
    /// Refresh display from current species data.
    /// Call this after data has been modified externally (e.g., after edit panel saves).
    /// </summary>
    public void RefreshDisplay()
    {
        if (currentSpeciesData != null)
        {
            UpdateUIDisplay();
            ApplyThermalValues();
        }
    }

    /// <summary>
    /// Get current species data (for other scripts that might need it)
    /// </summary>
    public SpeciesData GetCurrentSpeciesData()
    {
        return currentSpeciesData;
    }

    /// <summary>
    /// Get the current species name
    /// </summary>
    public SpeciesName GetSpeciesName()
    {
        return speciesName;
    }

    /// <summary>
    /// Get the current species variant
    /// </summary>
    public SpeciesVariant GetSpeciesVariant()
    {
        return speciesVariant;
    }

    /// <summary>
    /// Manually refresh the species data
    /// </summary>
    [ContextMenu("Refresh Species Data")]
    public void RefreshSpeciesData()
    {
        // If we have a valid RunSpeciesList index, refresh from there
        if (runSpeciesListIndex >= 0 && runSpeciesList != null)
        {
            RefreshFromRunSpeciesList();
        }
        else
        {
            // Fallback to database
            UpdateSpeciesData();
        }
    }
}
