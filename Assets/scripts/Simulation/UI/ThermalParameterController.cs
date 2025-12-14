using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controller that manages all thermal parameter sliders and connects them to the graph editor.
/// Handles loading species data, tracking active edits, saving changes, and AUC-based proportional scaling.
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

    [Header("Area Under Curve Slider")]
    [Tooltip("Optional slider for controlling AUC. Adjusting this scales other parameters proportionally.")]
    [SerializeField] private ThermalParameterSlider aucSlider;

    [Header("Slider Ranges - Temperatures (in Celsius for display)")]
    [SerializeField] private Vector2 optimalTempRange = new Vector2(-5f, 45f);
    [SerializeField] private Vector2 lowerBoundRange = new Vector2(-10f, 40f);
    [SerializeField] private Vector2 upperBoundRange = new Vector2(0f, 50f);

    [Header("Slider Ranges - Shape Coefficients")]
    [SerializeField] private Vector2 arrhenBreadthRange = new Vector2(1000f, 15000f);
    [SerializeField] private Vector2 arrhenLowerRange = new Vector2(3000f, 25000f);
    [SerializeField] private Vector2 arrhenUpperRange = new Vector2(5000f, 35000f);

    [Header("AUC Settings")]
    [SerializeField] private Vector2 aucRange = new Vector2(2f, 35f);
    [Tooltip("Number of samples for AUC calculation (higher = more accurate, slower)")]
    [SerializeField] private int aucSamples = 100;
    [Tooltip("Temperature range for AUC calculation (Celsius)")]
    [SerializeField] private float aucTempMin = 0f;
    [SerializeField] private float aucTempMax = 40f;

    [Header("Events")]
    [Tooltip("Fired whenever any parameter changes. Useful for marking data as dirty.")]
    public UnityEvent OnAnyParameterChanged;

    // Constants
    private const float KELVIN_OFFSET = 273.15f;

    // Current values (in internal units - Kelvin for temps)
    private float currentOptimalTemp;
    private float currentLowerBound;
    private float currentUpperBound;
    private float currentArrhenBreadth;
    private float currentArrhenLower;
    private float currentArrhenUpper;

    // Cached AUC value
    private float currentAUC;

    // Baseline values for AUC scaling (stored when loading data)
    private float baselineOptimalTemp;
    private float baselineLowerBound;
    private float baselineUpperBound;
    private float baselineArrhenBreadth;
    private float baselineArrhenLower;
    private float baselineArrhenUpper;
    private float baselineAUC;

    // Flags to prevent feedback loops
    private bool isLoading = false;
    private bool isScalingFromAUC = false;
    private bool isUpdatingAUCSlider = false;

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

        // AUC slider (isTemperature = false, it's a derived value)
        if (aucSlider != null)
            aucSlider.Initialize(aucRange.x, aucRange.y, 15f, false, 1);
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

        if (aucSlider != null)
            aucSlider.OnValueChanged.AddListener(OnAUCSliderChanged);
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

        if (aucSlider != null)
            aucSlider.OnValueChanged.RemoveListener(OnAUCSliderChanged);
    }

    // ==================== Individual Slider Change Handlers ====================

    private void OnOptimalTempChanged(float valueKelvin)
    {
        if (isScalingFromAUC) return;

        currentOptimalTemp = valueKelvin;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.OptimalTemp);
        UpdateGraphAndAUC();

        // Update baseline when individual sliders change
        StoreBaseline();
    }

    private void OnLowerBoundChanged(float valueKelvin)
    {
        if (isScalingFromAUC) return;

        currentLowerBound = valueKelvin;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.LowerBound);
        UpdateGraphAndAUC();
        StoreBaseline();
    }

    private void OnUpperBoundChanged(float valueKelvin)
    {
        if (isScalingFromAUC) return;

        currentUpperBound = valueKelvin;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.UpperBound);
        UpdateGraphAndAUC();
        StoreBaseline();
    }

    private void OnArrhenBreadthChanged(float value)
    {
        if (isScalingFromAUC) return;

        currentArrhenBreadth = value;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.ArrhenBreadth);
        UpdateGraphAndAUC();
        StoreBaseline();
    }

    private void OnArrhenLowerChanged(float value)
    {
        if (isScalingFromAUC) return;

        currentArrhenLower = value;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.ArrhenLower);
        UpdateGraphAndAUC();
        StoreBaseline();
    }

    private void OnArrhenUpperChanged(float value)
    {
        if (isScalingFromAUC) return;

        currentArrhenUpper = value;
        SetActiveParameter(ThermalGraphEditor.EditingParameter.ArrhenUpper);
        UpdateGraphAndAUC();
        StoreBaseline();
    }

    // ==================== Baseline Management ====================

    /// <summary>
    /// Store current values as baseline for AUC scaling.
    /// This is called when data is loaded or when individual sliders change.
    /// </summary>
    private void StoreBaseline()
    {
        baselineOptimalTemp = currentOptimalTemp;
        baselineLowerBound = currentLowerBound;
        baselineUpperBound = currentUpperBound;
        baselineArrhenBreadth = currentArrhenBreadth;
        baselineArrhenLower = currentArrhenLower;
        baselineArrhenUpper = currentArrhenUpper;
        baselineAUC = currentAUC;
    }

    // ==================== AUC Slider Handler ====================

    /// <summary>
    /// Called when user moves the AUC slider.
    /// Triggers proportional scaling of all parameters except OptimalTemp.
    /// </summary>
    private void OnAUCSliderChanged(float targetAUC)
    {
        if (isLoading || isUpdatingAUCSlider) return;

        ApplyProportionalScaling(targetAUC);
    }

    /// <summary>
    /// Apply proportional scaling to achieve the target AUC.
    /// Uses BASELINE values for scaling to avoid cumulative errors.
    /// Scales from the original loaded values, not current values.
    /// </summary>
    private void ApplyProportionalScaling(float targetAUC)
    {
        // Use baseline AUC for scale calculation (not current, which might be clamped/broken)
        if (baselineAUC <= 0.001f)
        {
            // Baseline is invalid, recalculate from current valid values
            baselineAUC = CalculateAUC();
            if (baselineAUC <= 0.001f)
            {
                // Still invalid, reset to safe defaults and recalculate
                ResetToSafeDefaults();
                return;
            }
        }

        float scaleFactor = targetAUC / baselineAUC;

        // Allow wider range for scale factor since we're scaling from baseline
        scaleFactor = Mathf.Clamp(scaleFactor, 0.05f, 10f);

        isScalingFromAUC = true;

        // OptimalTemp stays fixed - it's the anchor point (peak position)
        float OT = baselineOptimalTemp;

        // Scale distances from optimal temp for bounds (from BASELINE, not current)
        float baseLBDistance = OT - baselineLowerBound;
        float baseUBDistance = baselineUpperBound - OT;

        float newLB = OT - (baseLBDistance * scaleFactor);
        float newUB = OT + (baseUBDistance * scaleFactor);

        // Scale shape coefficients from BASELINE
        float newBreadth = baselineArrhenBreadth * scaleFactor;
        float newArrhenLower = baselineArrhenLower * scaleFactor;
        float newArrhenUpper = baselineArrhenUpper * scaleFactor;

        // Clamp to valid ranges
        float newLBCelsius = newLB - KELVIN_OFFSET;
        float newUBCelsius = newUB - KELVIN_OFFSET;

        newLBCelsius = Mathf.Clamp(newLBCelsius, lowerBoundRange.x, lowerBoundRange.y);
        newUBCelsius = Mathf.Clamp(newUBCelsius, upperBoundRange.x, upperBoundRange.y);

        newLB = newLBCelsius + KELVIN_OFFSET;
        newUB = newUBCelsius + KELVIN_OFFSET;

        newBreadth = Mathf.Clamp(newBreadth, arrhenBreadthRange.x, arrhenBreadthRange.y);
        newArrhenLower = Mathf.Clamp(newArrhenLower, arrhenLowerRange.x, arrhenLowerRange.y);
        newArrhenUpper = Mathf.Clamp(newArrhenUpper, arrhenUpperRange.x, arrhenUpperRange.y);

        // Update internal values
        currentLowerBound = newLB;
        currentUpperBound = newUB;
        currentArrhenBreadth = newBreadth;
        currentArrhenLower = newArrhenLower;
        currentArrhenUpper = newArrhenUpper;

        // Update individual sliders
        UpdateIndividualSliders();

        // Update graph
        UpdateGraph();

        // Recalculate actual AUC
        currentAUC = CalculateAUC();

        // IMPORTANT: Sync the AUC slider to show the ACTUAL achievable AUC
        // This prevents the slider from showing an impossible value
        SyncAUCSliderToActual();

        isScalingFromAUC = false;

        if (!isLoading)
        {
            OnAnyParameterChanged?.Invoke();
        }

        Debug.Log($"ThermalParameterController: Scaling from baseline - Target={targetAUC:F2}, Actual={currentAUC:F2}, Scale={scaleFactor:F2}");
    }

    /// <summary>
    /// Reset to safe default values when the system gets into a bad state.
    /// </summary>
    private void ResetToSafeDefaults()
    {
        Debug.LogWarning("ThermalParameterController: Resetting to safe defaults due to invalid state");

        isScalingFromAUC = true;

        // Set safe middle-ground values
        currentOptimalTemp = 293.15f; // 20°C
        currentLowerBound = 285.15f;  // 12°C
        currentUpperBound = 301.15f;  // 28°C
        currentArrhenBreadth = 5000f;
        currentArrhenLower = 10000f;
        currentArrhenUpper = 20000f;

        // Update all sliders
        if (optimalTempSlider != null)
            optimalTempSlider.SetValue(currentOptimalTemp, isInternalUnits: true);

        UpdateIndividualSliders();
        UpdateGraph();

        currentAUC = CalculateAUC();
        StoreBaseline();
        SyncAUCSliderToActual();

        isScalingFromAUC = false;
    }

    /// <summary>
    /// Sync the AUC slider to show the actual calculated AUC value.
    /// Called after proportional scaling to ensure slider matches reality.
    /// </summary>
    private void SyncAUCSliderToActual()
    {
        if (aucSlider == null) return;

        isUpdatingAUCSlider = true;
        aucSlider.SetValue(currentAUC, isInternalUnits: false);
        isUpdatingAUCSlider = false;
    }

    /// <summary>
    /// Update all individual parameter sliders to reflect current values.
    /// </summary>
    private void UpdateIndividualSliders()
    {
        if (lowerBoundSlider != null)
            lowerBoundSlider.SetValue(currentLowerBound, isInternalUnits: true);

        if (upperBoundSlider != null)
            upperBoundSlider.SetValue(currentUpperBound, isInternalUnits: true);

        if (arrhenBreadthSlider != null)
            arrhenBreadthSlider.SetValue(currentArrhenBreadth, isInternalUnits: false);

        if (arrhenLowerSlider != null)
            arrhenLowerSlider.SetValue(currentArrhenLower, isInternalUnits: false);

        if (arrhenUpperSlider != null)
            arrhenUpperSlider.SetValue(currentArrhenUpper, isInternalUnits: false);
    }

    // ==================== AUC Calculation ====================

    /// <summary>
    /// Calculate the Area Under Curve using trapezoidal integration.
    /// </summary>
    public float CalculateAUC()
    {
        float area = 0f;
        float step = (aucTempMax - aucTempMin) / aucSamples;

        for (int i = 0; i < aucSamples; i++)
        {
            float t1Celsius = aucTempMin + i * step;
            float t2Celsius = t1Celsius + step;

            float perf1 = CalculatePerformanceAtTemp(t1Celsius);
            float perf2 = CalculatePerformanceAtTemp(t2Celsius);

            // Trapezoid rule
            area += step * (perf1 + perf2) / 2f;
        }

        return area;
    }

    /// <summary>
    /// Calculate thermal performance at a given temperature (Celsius).
    /// </summary>
    private float CalculatePerformanceAtTemp(float tempCelsius)
    {
        float T = tempCelsius + KELVIN_OFFSET;
        float OT = currentOptimalTemp;
        float B = currentArrhenBreadth;
        float L = currentArrhenLower;
        float U = currentArrhenUpper;
        float LB = currentLowerBound;
        float UB = currentUpperBound;

        // Safety checks - use minimum valid values if parameters are broken
        if (T <= 0) T = 273.15f;
        if (OT <= 0) OT = 293.15f;
        if (LB <= 0) LB = 273.15f;
        if (UB <= 0) UB = 313.15f;
        if (B <= 0) B = 1000f;
        if (L <= 0) L = 3000f;
        if (U <= 0) U = 5000f;

        float numerator = Mathf.Exp(B / OT - B / T) *
                         (1 + Mathf.Exp(L / OT - L / LB) + Mathf.Exp(U / UB - U / OT));

        float denominator = 1 + Mathf.Exp(L / T - L / LB) + Mathf.Exp(U / UB - U / T);

        if (denominator == 0) return 0f;

        return Mathf.Clamp01(numerator / denominator);
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
    }

    /// <summary>
    /// Update graph and recalculate AUC.
    /// Called when individual parameters change.
    /// </summary>
    private void UpdateGraphAndAUC()
    {
        UpdateGraph();

        // Recalculate AUC
        currentAUC = CalculateAUC();

        // Update AUC slider to reflect new value
        UpdateAUCSlider();

        if (!isLoading)
        {
            OnAnyParameterChanged?.Invoke();
        }
    }

    /// <summary>
    /// Update the AUC slider to match current calculated AUC.
    /// </summary>
    private void UpdateAUCSlider()
    {
        if (aucSlider == null) return;

        isUpdatingAUCSlider = true;
        aucSlider.SetValue(currentAUC, isInternalUnits: false);
        isUpdatingAUCSlider = false;
    }

    /// <summary>
    /// Clear the active parameter highlight.
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

        currentOptimalTemp = data.optimalTempK;
        currentLowerBound = data.lowerBoundK;
        currentUpperBound = data.upperBoundK;
        currentArrhenBreadth = data.arrhenBreadth;
        currentArrhenLower = data.arrhenLower;
        currentArrhenUpper = data.arrhenUpper;

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

        UpdateGraph();

        currentAUC = CalculateAUC();

        // Store baseline for AUC scaling
        StoreBaseline();

        UpdateAUCSlider();

        if (graphEditor != null)
            graphEditor.ClearActiveParameter();

        isLoading = false;

        Debug.Log($"ThermalParameterController: Loaded - OptimalTemp={data.optimalTempK}K, AUC={currentAUC:F1}");
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

        currentAUC = CalculateAUC();
        StoreBaseline();
        UpdateAUCSlider();

        if (graphEditor != null)
            graphEditor.ClearActiveParameter();

        isLoading = false;
    }

    // ==================== Data Saving ====================

    /// <summary>
    /// Apply current slider values to a SpeciesData object.
    /// Note: AUC is not saved - it's a derived value.
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

        Debug.Log($"ThermalParameterController: Saved - OptimalTemp={data.optimalTempK}K, AUC={currentAUC:F1}");
    }

    /// <summary>
    /// Get all current values as a struct.
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

    /// <summary>
    /// Get the current calculated AUC value.
    /// </summary>
    public float GetCurrentAUC()
    {
        return currentAUC;
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

        currentAUC = CalculateAUC();
        StoreBaseline();
        UpdateAUCSlider();
    }

    /// <summary>
    /// Get the area under the current curve.
    /// </summary>
    public float GetAreaUnderCurve()
    {
        return currentAUC;
    }

    /// <summary>
    /// Recalculate AUC and update slider. Call if parameters changed externally.
    /// </summary>
    public void RefreshAUC()
    {
        currentAUC = CalculateAUC();
        StoreBaseline();
        UpdateAUCSlider();
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