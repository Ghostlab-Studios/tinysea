using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controller that manages all thermal parameter sliders and connects them to the graph editor.
/// Handles loading species data, tracking active edits, and saving changes.
/// </summary>
public class ThermalParameterController : MonoBehaviour
{
    [Header("Graph Reference")]
    [SerializeField] private ThermalGraphEditor graphEditor;

    [Header("Temperature Sliders (displayed in °C, stored in K)")]
    [SerializeField] private ThermalParameterSlider optimalTempSlider;
    [SerializeField] private ThermalParameterSlider lowerBoundSlider;
    [SerializeField] private ThermalParameterSlider upperBoundSlider;

    [Header("Shape Coefficient Sliders (raw values)")]
    [SerializeField] private ThermalParameterSlider arrhenBreadthSlider;
    [SerializeField] private ThermalParameterSlider arrhenLowerSlider;
    [SerializeField] private ThermalParameterSlider arrhenUpperSlider;

    [Header("Slider Ranges - Temperatures (in Celsius for display)")]
    [SerializeField] private Vector2 optimalTempRange = new Vector2(-5f, 45f);
    [SerializeField] private Vector2 lowerBoundRange = new Vector2(-10f, 40f);
    [SerializeField] private Vector2 upperBoundRange = new Vector2(0f, 50f);

    [Header("Slider Ranges - Shape Coefficients")]
    [SerializeField] private Vector2 arrhenBreadthRange = new Vector2(1000f, 15000f);
    [SerializeField] private Vector2 arrhenLowerRange = new Vector2(3000f, 25000f);
    [SerializeField] private Vector2 arrhenUpperRange = new Vector2(5000f, 35000f);

    [Header("Events")]
    [Tooltip("Fired whenever any parameter changes. Useful for marking data as dirty.")]
    public UnityEvent OnAnyParameterChanged;

    // Current values (in internal units - Kelvin for temps)
    private float currentOptimalTemp;
    private float currentLowerBound;
    private float currentUpperBound;
    private float currentArrhenBreadth;
    private float currentArrhenLower;
    private float currentArrhenUpper;

    // Track if we're currently loading (to prevent feedback loops)
    private bool isLoading = false;

    private void Start()
    {
        InitializeSliders();
        SubscribeToSliders();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSliders();
    }

    /// <summary>
    /// Initialize all sliders with their ranges and default values.
    /// </summary>
    private void InitializeSliders()
    {
        // Temperature sliders (isTemperature = true)
        if (optimalTempSlider != null)
            optimalTempSlider.Initialize(optimalTempRange.x, optimalTempRange.y, 20f, true, 1);

        if (lowerBoundSlider != null)
            lowerBoundSlider.Initialize(lowerBoundRange.x, lowerBoundRange.y, 12f, true, 1);

        if (upperBoundSlider != null)
            upperBoundSlider.Initialize(upperBoundRange.x, upperBoundRange.y, 22f, true, 1);

        // Shape coefficient sliders (isTemperature = false)
        if (arrhenBreadthSlider != null)
            arrhenBreadthSlider.Initialize(arrhenBreadthRange.x, arrhenBreadthRange.y, 5273f, false, 0);

        if (arrhenLowerSlider != null)
            arrhenLowerSlider.Initialize(arrhenLowerRange.x, arrhenLowerRange.y, 10273f, false, 0);

        if (arrhenUpperSlider != null)
            arrhenUpperSlider.Initialize(arrhenUpperRange.x, arrhenUpperRange.y, 21273f, false, 0);
    }

    /// <summary>
    /// Subscribe to all slider value change events.
    /// </summary>
    private void SubscribeToSliders()
    {
        if (optimalTempSlider != null)
            optimalTempSlider.OnValueChanged.AddListener(OnOptimalTempChanged);

        if (lowerBoundSlider != null)
            lowerBoundSlider.OnValueChanged.AddListener(OnLowerBoundChanged);

        if (upperBoundSlider != null)
            upperBoundSlider.OnValueChanged.AddListener(OnUpperBoundChanged);

        if (arrhenBreadthSlider != null)
            arrhenBreadthSlider.OnValueChanged.AddListener(OnArrhenBreadthChanged);

        if (arrhenLowerSlider != null)
            arrhenLowerSlider.OnValueChanged.AddListener(OnArrhenLowerChanged);

        if (arrhenUpperSlider != null)
            arrhenUpperSlider.OnValueChanged.AddListener(OnArrhenUpperChanged);
    }

    /// <summary>
    /// Unsubscribe from all slider events.
    /// </summary>
    private void UnsubscribeFromSliders()
    {
        if (optimalTempSlider != null)
            optimalTempSlider.OnValueChanged.RemoveListener(OnOptimalTempChanged);

        if (lowerBoundSlider != null)
            lowerBoundSlider.OnValueChanged.RemoveListener(OnLowerBoundChanged);

        if (upperBoundSlider != null)
            upperBoundSlider.OnValueChanged.RemoveListener(OnUpperBoundChanged);

        if (arrhenBreadthSlider != null)
            arrhenBreadthSlider.OnValueChanged.RemoveListener(OnArrhenBreadthChanged);

        if (arrhenLowerSlider != null)
            arrhenLowerSlider.OnValueChanged.RemoveListener(OnArrhenLowerChanged);

        if (arrhenUpperSlider != null)
            arrhenUpperSlider.OnValueChanged.RemoveListener(OnArrhenUpperChanged);
    }

    // ==================== Slider Change Handlers ====================

    private void OnOptimalTempChanged(float valueKelvin)
    {
        currentOptimalTemp = valueKelvin;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.OptimalTemp);
        UpdateGraph();
    }

    private void OnLowerBoundChanged(float valueKelvin)
    {
        currentLowerBound = valueKelvin;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.LowerBound);
        UpdateGraph();
    }

    private void OnUpperBoundChanged(float valueKelvin)
    {
        currentUpperBound = valueKelvin;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.UpperBound);
        UpdateGraph();
    }

    private void OnArrhenBreadthChanged(float value)
    {
        currentArrhenBreadth = value;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.ArrhenBreadth);
        UpdateGraph();
    }

    private void OnArrhenLowerChanged(float value)
    {
        currentArrhenLower = value;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.ArrhenLower);
        UpdateGraph();
    }

    private void OnArrhenUpperChanged(float value)
    {
        currentArrhenUpper = value;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.ArrhenUpper);
        UpdateGraph();
    }

    // ==================== Graph Updates ====================

    private void SetActiveParameter(ThermalGraphEditor.EditingParameter param)
    {
        if (graphEditor != null && !isLoading)
        {
            graphEditor.SetActiveParameter(param);
        }
    }

    private void UpdateGraph()
    {
        if (graphEditor != null)
        {
            graphEditor.SetParameters(
                currentOptimalTemp,
                currentArrhenBreadth,
                currentArrhenLower,
                currentArrhenUpper,
                currentLowerBound,
                currentUpperBound
            );
        }

        if (!isLoading)
        {
            OnAnyParameterChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clear the active parameter highlight (call when user stops editing).
    /// </summary>
    public void ClearActiveHighlight()
    {
        if (graphEditor != null)
        {
            graphEditor.ClearActiveParameter();
        }
    }

    // ==================== Data Loading ====================

    /// <summary>
    /// Load thermal parameters from a SpeciesData object.
    /// </summary>
    public void LoadFromSpeciesData(SpeciesData data)
    {
        if (data == null)
        {
            Debug.LogWarning("ThermalParameterController: Cannot load null SpeciesData");
            return;
        }

        isLoading = true;

        // Store current values
        currentOptimalTemp = data.optimalTempK;
        currentLowerBound = data.lowerBoundK;
        currentUpperBound = data.upperBoundK;
        currentArrhenBreadth = data.arrhenBreadth;
        currentArrhenLower = data.arrhenLower;
        currentArrhenUpper = data.arrhenUpper;

        // Update sliders (using internal units)
        if (optimalTempSlider != null)
            optimalTempSlider.SetValue(data.optimalTempK, isInternalUnits: true);

        if (lowerBoundSlider != null)
            lowerBoundSlider.SetValue(data.lowerBoundK, isInternalUnits: true);

        if (upperBoundSlider != null)
            upperBoundSlider.SetValue(data.upperBoundK, isInternalUnits: true);

        if (arrhenBreadthSlider != null)
            arrhenBreadthSlider.SetValue(data.arrhenBreadth, isInternalUnits: false);

        if (arrhenLowerSlider != null)
            arrhenLowerSlider.SetValue(data.arrhenLower, isInternalUnits: false);

        if (arrhenUpperSlider != null)
            arrhenUpperSlider.SetValue(data.arrhenUpper, isInternalUnits: false);

        // Update graph
        UpdateGraph();

        // Clear any active highlight
        if (graphEditor != null)
            graphEditor.ClearActiveParameter();

        isLoading = false;

        Debug.Log($"ThermalParameterController: Loaded data - OptimalTemp={data.optimalTempK}K, " +
                  $"Bounds=[{data.lowerBoundK}K, {data.upperBoundK}K], " +
                  $"Breadth={data.arrhenBreadth}, Lower={data.arrhenLower}, Upper={data.arrhenUpper}");
    }

    /// <summary>
    /// Load thermal parameters directly (all in Kelvin/raw units).
    /// </summary>
    public void LoadValues(float optTempK, float lowerK, float upperK,
                           float breadth, float arrLower, float arrUpper)
    {
        isLoading = true;

        currentOptimalTemp = optTempK;
        currentLowerBound = lowerK;
        currentUpperBound = upperK;
        currentArrhenBreadth = breadth;
        currentArrhenLower = arrLower;
        currentArrhenUpper = arrUpper;

        if (optimalTempSlider != null)
            optimalTempSlider.SetValue(optTempK, isInternalUnits: true);

        if (lowerBoundSlider != null)
            lowerBoundSlider.SetValue(lowerK, isInternalUnits: true);

        if (upperBoundSlider != null)
            upperBoundSlider.SetValue(upperK, isInternalUnits: true);

        if (arrhenBreadthSlider != null)
            arrhenBreadthSlider.SetValue(breadth, isInternalUnits: false);

        if (arrhenLowerSlider != null)
            arrhenLowerSlider.SetValue(arrLower, isInternalUnits: false);

        if (arrhenUpperSlider != null)
            arrhenUpperSlider.SetValue(arrUpper, isInternalUnits: false);

        UpdateGraph();

        if (graphEditor != null)
            graphEditor.ClearActiveParameter();

        isLoading = false;
    }

    // ==================== Data Saving ====================

    /// <summary>
    /// Apply current slider values to a SpeciesData object.
    /// </summary>
    public void SaveToSpeciesData(SpeciesData data)
    {
        if (data == null)
        {
            Debug.LogWarning("ThermalParameterController: Cannot save to null SpeciesData");
            return;
        }

        data.optimalTempK = currentOptimalTemp;
        data.lowerBoundK = currentLowerBound;
        data.upperBoundK = currentUpperBound;
        data.arrhenBreadth = currentArrhenBreadth;
        data.arrhenLower = currentArrhenLower;
        data.arrhenUpper = currentArrhenUpper;

        Debug.Log($"ThermalParameterController: Saved data - OptimalTemp={data.optimalTempK}K, " +
                  $"Bounds=[{data.lowerBoundK}K, {data.upperBoundK}K]");
    }

    /// <summary>
    /// Get all current values as a struct for external use.
    /// </summary>
    public ThermalParameters GetCurrentValues()
    {
        return new ThermalParameters
        {
            optimalTempK = currentOptimalTemp,
            lowerBoundK = currentLowerBound,
            upperBoundK = currentUpperBound,
            arrhenBreadth = currentArrhenBreadth,
            arrhenLower = currentArrhenLower,
            arrhenUpper = currentArrhenUpper
        };
    }

    // ==================== Utility ====================

    /// <summary>
    /// Reset all sliders to their default values.
    /// </summary>
    public void ResetToDefaults()
    {
        optimalTempSlider?.ResetToDefault();
        lowerBoundSlider?.ResetToDefault();
        upperBoundSlider?.ResetToDefault();
        arrhenBreadthSlider?.ResetToDefault();
        arrhenLowerSlider?.ResetToDefault();
        arrhenUpperSlider?.ResetToDefault();
    }

    /// <summary>
    /// Get the area under the current curve (useful for specialist vs generalist comparison).
    /// </summary>
    public float GetAreaUnderCurve()
    {
        return graphEditor != null ? graphEditor.CalculateAreaUnderCurve() : 0f;
    }
}

/// <summary>
/// Struct to hold all thermal parameters together.
/// </summary>
[System.Serializable]
public struct ThermalParameters
{
    public float optimalTempK;
    public float lowerBoundK;
    public float upperBoundK;
    public float arrhenBreadth;
    public float arrhenLower;
    public float arrhenUpper;
}