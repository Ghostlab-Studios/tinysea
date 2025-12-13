using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class SpeciesUIController : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private SpeciesDatabase speciesDatabase;

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

    /// <summary>
    /// The actual index of this species in the RunSpeciesList.
    /// This is what we send to the edit panel - NOT the visual display index.
    /// </summary>
    private int runSpeciesListIndex = -1;

    void Start()
    {
        UpdateSpeciesData();

        // Wire up edit button to fire event
        if (editButton != null)
        {
            editButton.onClick.AddListener(OnEditButtonClicked);
        }
    }

    void OnDestroy()
    {
        // Clean up listener
        if (editButton != null)
        {
            editButton.onClick.RemoveListener(OnEditButtonClicked);
        }
    }

    void OnValidate()
    {
        // Check if selection changed
        if (speciesName != lastSpeciesName || speciesVariant != lastSpeciesVariant)
        {
            UpdateSpeciesData();
            lastSpeciesName = speciesName;
            lastSpeciesVariant = speciesVariant;
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
    /// Initialize this UI controller with a specific species.
    /// Called by SpeciesTierConfig when instantiating entries.
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
        displayNameOverride = "";

        lastSpeciesName = data.speciesName;
        lastSpeciesVariant = data.variant;

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
    /// Update the species data and apply to thermal graph
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

        // Force graph update if in editor
        if (!Application.isPlaying && thermalGraphUI != null)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (thermalGraphUI != null)
                    thermalGraphUI.OnValidate();
            };
#endif
        }

        Debug.Log($"Applied thermal values for {speciesName} - {speciesVariant}");
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
            nameText.text = $"{speciesName} {speciesVariant}";
        }

        // Update type text
        if (typeText != null)
        {
            typeText.text = speciesVariant.ToString();
        }

        // Update count (assuming count exists in SpeciesData)
        if (countText != null)
        {
            countText.text = currentSpeciesData.count.ToString();
        }
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
        UpdateSpeciesData();
    }
}