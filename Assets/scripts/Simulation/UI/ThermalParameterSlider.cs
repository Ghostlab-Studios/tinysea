using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Controls a single thermal parameter slider panel.
/// Handles bidirectional sync between slider and input field.
/// Supports both temperature values (displayed in Celsius, stored in Kelvin)
/// and shape coefficients (displayed and stored as-is).
/// </summary>
public class ThermalParameterSlider : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI unitLabel; // Optional: to change "°C" to other units

    [Header("Parameter Configuration")]
    [Tooltip("If true, displays in Celsius but stores/returns Kelvin. If false, displays and stores the raw value.")]
    [SerializeField] private bool isTemperature = true;
    
    [Tooltip("Minimum value (in display units - Celsius for temps, raw for coefficients)")]
    [SerializeField] private float minValue = 0f;
    
    [Tooltip("Maximum value (in display units - Celsius for temps, raw for coefficients)")]
    [SerializeField] private float maxValue = 40f;
    
    [Tooltip("Default value (in display units)")]
    [SerializeField] private float defaultValue = 20f;
    
    [Tooltip("Number of decimal places to show")]
    [SerializeField] private int decimalPlaces = 1;

    [Header("Events")]
    [Tooltip("Fired when value changes. Passes the INTERNAL value (Kelvin for temps, raw for coefficients)")]
    public UnityEvent<float> OnValueChanged;

    // Constants
    private const float KELVIN_OFFSET = 273.15f;

    // Internal state
    private float currentDisplayValue; // What's shown in UI (Celsius or raw)
    private bool isUpdating = false;   // Prevents recursive updates

    /// <summary>
    /// The current value in INTERNAL units (Kelvin for temperatures, raw for coefficients).
    /// Use this when applying to the thermal graph or simulation.
    /// </summary>
    public float Value
    {
        get => isTemperature ? CelsiusToKelvin(currentDisplayValue) : currentDisplayValue;
        set => SetValue(value, isInternalUnits: true);
    }

    /// <summary>
    /// The current value in DISPLAY units (Celsius for temperatures, raw for coefficients).
    /// Use this for UI purposes.
    /// </summary>
    public float DisplayValue
    {
        get => currentDisplayValue;
        set => SetValue(value, isInternalUnits: false);
    }

    private void Awake()
    {
        // Setup slider
        if (slider != null)
        {
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        // Setup input field
        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputFieldChanged);
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        }
    }

    private void Start()
    {
        // Initialize to default value
        SetDisplayValue(defaultValue);
    }

    private void OnDestroy()
    {
        // Cleanup listeners
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        
        if (inputField != null)
            inputField.onEndEdit.RemoveListener(OnInputFieldChanged);
    }

    /// <summary>
    /// Initialize the parameter with configuration.
    /// Call this to setup the slider programmatically.
    /// </summary>
    /// <param name="min">Minimum value in display units</param>
    /// <param name="max">Maximum value in display units</param>
    /// <param name="initial">Initial value in display units</param>
    /// <param name="isTempParam">True if this is a temperature (Celsius display, Kelvin storage)</param>
    /// <param name="decimals">Number of decimal places to show</param>
    public void Initialize(float min, float max, float initial, bool isTempParam = true, int decimals = 1)
    {
        minValue = min;
        maxValue = max;
        defaultValue = initial;
        isTemperature = isTempParam;
        decimalPlaces = decimals;

        // Update slider bounds
        if (slider != null)
        {
            slider.minValue = min;
            slider.maxValue = max;
        }

        // Update unit label if needed
        if (unitLabel != null)
        {
            unitLabel.text = isTempParam ? "°C" : "";
        }

        // Set initial value
        SetDisplayValue(initial);
    }

    /// <summary>
    /// Initialize with internal units (Kelvin for temps).
    /// Useful when loading from saved data that's already in Kelvin.
    /// </summary>
    /// <param name="min">Minimum value in internal units</param>
    /// <param name="max">Maximum value in internal units</param>
    /// <param name="initial">Initial value in internal units</param>
    /// <param name="isTempParam">True if this is a temperature</param>
    /// <param name="decimals">Number of decimal places to show</param>
    public void InitializeWithInternalUnits(float min, float max, float initial, bool isTempParam = true, int decimals = 1)
    {
        if (isTempParam)
        {
            Initialize(
                KelvinToCelsius(min),
                KelvinToCelsius(max),
                KelvinToCelsius(initial),
                isTempParam,
                decimals
            );
        }
        else
        {
            Initialize(min, max, initial, isTempParam, decimals);
        }
    }

    /// <summary>
    /// Set the value. Can specify whether input is in internal or display units.
    /// </summary>
    /// <param name="value">The value to set</param>
    /// <param name="isInternalUnits">True if value is in Kelvin (for temps), false if in Celsius</param>
    public void SetValue(float value, bool isInternalUnits)
    {
        float displayValue;
        
        if (isInternalUnits && isTemperature)
        {
            // Convert from Kelvin to Celsius for display
            displayValue = KelvinToCelsius(value);
        }
        else
        {
            displayValue = value;
        }

        SetDisplayValue(displayValue);
    }

    /// <summary>
    /// Set the display value directly (Celsius for temps, raw for coefficients).
    /// </summary>
    private void SetDisplayValue(float displayValue)
    {
        if (isUpdating) return;
        isUpdating = true;

        // Clamp to bounds
        currentDisplayValue = Mathf.Clamp(displayValue, minValue, maxValue);

        // Update UI elements
        UpdateSlider();
        UpdateInputField();

        // Fire event with INTERNAL value
        float internalValue = isTemperature ? CelsiusToKelvin(currentDisplayValue) : currentDisplayValue;
        OnValueChanged?.Invoke(internalValue);

        isUpdating = false;
    }

    /// <summary>
    /// Called when slider is moved by user.
    /// </summary>
    private void OnSliderChanged(float value)
    {
        if (isUpdating) return;
        SetDisplayValue(value);
    }

    /// <summary>
    /// Called when user finishes editing the input field.
    /// </summary>
    private void OnInputFieldChanged(string text)
    {
        if (isUpdating) return;

        if (float.TryParse(text, out float value))
        {
            SetDisplayValue(value);
        }
        else
        {
            // Invalid input - revert to current value
            UpdateInputField();
        }
    }

    /// <summary>
    /// Update the slider to match current value.
    /// </summary>
    private void UpdateSlider()
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(currentDisplayValue);
        }
    }

    /// <summary>
    /// Update the input field to match current value.
    /// </summary>
    private void UpdateInputField()
    {
        if (inputField != null)
        {
            string format = "F" + decimalPlaces;
            inputField.SetTextWithoutNotify(currentDisplayValue.ToString(format));
        }
    }

    /// <summary>
    /// Reset to the default value.
    /// </summary>
    public void ResetToDefault()
    {
        SetDisplayValue(defaultValue);
    }

    // ==================== Conversion Utilities ====================

    /// <summary>
    /// Convert Celsius to Kelvin.
    /// </summary>
    public static float CelsiusToKelvin(float celsius)
    {
        return celsius + KELVIN_OFFSET;
    }

    /// <summary>
    /// Convert Kelvin to Celsius.
    /// </summary>
    public static float KelvinToCelsius(float kelvin)
    {
        return kelvin - KELVIN_OFFSET;
    }

    // ==================== Editor Helpers ====================

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure max is greater than min
        if (maxValue <= minValue)
            maxValue = minValue + 1f;

        // Clamp default to valid range
        defaultValue = Mathf.Clamp(defaultValue, minValue, maxValue);

        // Update unit label in editor
        if (unitLabel != null)
        {
            unitLabel.text = isTemperature ? "°C" : "";
        }
    }
#endif
}
