using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationInputUI : MonoBehaviour
{

    [Header("Configuration")]
    [SerializeField] private SimulationConfig config;

    // ==================== TEMPERATURE UI ====================

    [Header("=== TEMPERATURE: BASE ===")]
    [Tooltip("Base/mean temperature in °C")]
    public TMP_InputField BaseTemperatureInput;

    [Header("=== TEMPERATURE: SEASONAL ===")]
    [Tooltip("Amplitude of seasonal variation (summer/winter swing)")]
    public TMP_InputField SeasonalAmplitude;

    [Header("=== TEMPERATURE: CLIMATE TREND ===")]
    [Tooltip("°C warming per year (climate change)")]
    public TMP_InputField ClimateTrend;

    [Tooltip("Enable year-to-year variation")]
    public Toggle InterannualVariation;

    [Header("=== TEMPERATURE: INTERANNUAL VARIATION ===")]
    [Tooltip("Magnitude of year-to-year temperature variation")]
    public TMP_InputField VariabilityMagnitude;

    [Tooltip("Bias towards warmer years (positive skew)")]
    public TMP_InputField WarmingBias;

    [Header("=== TEMPERATURE: DAILY VARIATION ===")]
    [Tooltip("Enable autocorrelated (smooth) daily variation")]
    public Toggle Autocorrelated;

    [Tooltip("Base daily random variation range")]
    public TMP_InputField DailyVariationRange;

    [Tooltip("How much daily randomness increases per year")]
    public TMP_InputField RandomnessGrowthRate;

    [Header("=== TEMPERATURE: BOUNDS ===")]
    [Tooltip("Minimum possible temperature")]
    public TMP_InputField TemperatureBoundsMin;

    [Tooltip("Maximum possible temperature")]
    public TMP_InputField TemperatureBoundsMax;

    [Header("=== RUN SIMULATION ===")]

    [Tooltip("Number of years to simulate")]
    public TMP_InputField MaxYears;

    [Tooltip("Run Simulation with current values")]
    public Button RunSimulationButton;

}
