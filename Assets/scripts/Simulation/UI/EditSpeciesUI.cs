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

    [Header("UI Fields (to be mapped later)")]
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_Dropdown variantDropdown;
    [SerializeField] private TMP_InputField countField;
    [SerializeField] private TMP_InputField eatingAmountField;
    [SerializeField] private TMP_InputField reproThresholdField;
    [SerializeField] private TMP_InputField reproMultiplierField;
    [SerializeField] private TMP_InputField tempDeathThresholdField;
    [SerializeField] private TMP_InputField tempDeathRateField;
    [SerializeField] private TMP_InputField naturalDeathVarianceField;
    [SerializeField] private TMP_InputField naturalDeathRateField;

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

            // TODO: Populate UI fields with currentEditingData
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

        // TODO: Map all fields once UI is fully set up
        // For now, just log what we would populate
        Debug.Log($"PopulateFields: Name={currentEditingData.speciesName}, " +
                  $"Variant={currentEditingData.variant}, " +
                  $"Count={currentEditingData.count}, " +
                  $"EatingAmount={currentEditingData.eatingAmount}");

        // Example field population (uncomment when fields are assigned):
        // if (nameField != null) nameField.text = currentEditingData.displayName;
        // if (countField != null) countField.text = currentEditingData.count.ToString();
        // if (eatingAmountField != null) eatingAmountField.text = currentEditingData.eatingAmount.ToString();
        // etc.
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

        // TODO: Read values from UI fields and update currentEditingData
        // Example:
        // if (countField != null && int.TryParse(countField.text, out int count))
        //     currentEditingData.count = count;
        // etc.

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
        // TODO: Copy all relevant fields
        currentEditingData.count = originalData.count;
        currentEditingData.eatingAmount = originalData.eatingAmount;
        currentEditingData.reproductionMultiplier = originalData.reproductionMultiplier;
        currentEditingData.deathThreshold = originalData.deathThreshold;
        currentEditingData.deathRate = originalData.deathRate;
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

        // Refresh UI fields
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