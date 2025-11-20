using UnityEngine;

[ExecuteAlways]
public class SpeciesUIController : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private SpeciesDatabase speciesDatabase;

    [Header("Species Selection")]
    [SerializeField] private SpeciesName speciesName = SpeciesName.Cyplo;
    [SerializeField] private SpeciesVariant speciesVariant = SpeciesVariant.Common;

    [Header("UI Reference")]
    [SerializeField] private ThermalGraphUI thermalGraphUI;

    private SpeciesData currentSpeciesData;
    private SpeciesName lastSpeciesName;
    private SpeciesVariant lastSpeciesVariant;

    void Start()
    {
        UpdateSpeciesData();
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
        currentSpeciesData = speciesDatabase.GetSpecies(speciesName, speciesVariant);

        if (currentSpeciesData == null)
        {
            Debug.LogWarning($"Species data not found for {speciesName} - {speciesVariant}");
            return;
        }

        // Apply thermal values to the graph
        ApplyThermalValues();
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
    /// Get current species data (for other scripts that might need it)
    /// </summary>
    public SpeciesData GetCurrentSpeciesData()
    {
        return currentSpeciesData;
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