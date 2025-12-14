using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationInputUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SimulationConfig config;

    public SimulationController simulationController;

    [Header("=== TEMPERATURE: BASE ===")]
    public TMP_InputField BaseTemperatureInput;

    [Header("=== TEMPERATURE: SEASONAL ===")]
    public TMP_InputField SeasonalAmplitude;

    [Header("=== TEMPERATURE: CLIMATE TREND ===")]
    public TMP_InputField ClimateTrend;
    public Toggle InterannualVariation;

    [Header("=== TEMPERATURE: INTERANNUAL VARIATION ===")]
    public TMP_InputField VariabilityMagnitude;
    public TMP_InputField WarmingBias;

    [Header("=== TEMPERATURE: DAILY VARIATION ===")]
    public Toggle Autocorrelated;
    public TMP_InputField DailyVariationRange;
    public TMP_InputField RandomnessGrowthRate;

    [Header("=== TEMPERATURE: BOUNDS ===")]
    public TMP_InputField TemperatureBoundsMin;
    public TMP_InputField TemperatureBoundsMax;

    [Header("=== RUN SIMULATION ===")]

    [Tooltip("Maximum sustainable population for Tier 1.\n\n" +
         "Represents the resource limit of the environment.\n" +
         "Recommended: 1000-10000 depending on desired ecosystem size.")]
    public TMP_InputField CarryingCapacityTier1;

    public TMP_InputField MaxYears;
    public Button RunSimulationButton;

    [Header("=== RESET ===")]
    public Button ResetButton;

    private static readonly Color InvalidColor = new Color(1f, 0.80f, 0.80f, 1f);
    private static readonly Color ValidColor = Color.white;

    // Default values stored when script initializes
    private SimulationConfigDefaults defaults;
    private bool defaultsStored = false;

    /// <summary>
    /// Stores default values for reset functionality.
    /// </summary>
    private class SimulationConfigDefaults
    {
        public float BaseTemperature;
        public float SeasonalAmplitude;
        public float ClimateTrend;
        public bool InterannualVariation;
        public float VariabilityMagnitude;
        public float WarmingBias;
        public bool Autocorrelated;
        public float DailyVariationRange;
        public float RandomnessGrowthRate;
        public float TemperatureBoundsMin;
        public float TemperatureBoundsMax;
        public float CarryingCapacityTier1;
        public int MaxYears;

        public static SimulationConfigDefaults CreateFrom(SimulationConfig config)
        {
            if (config == null) return null;

            return new SimulationConfigDefaults
            {
                BaseTemperature = config.BaseTemperature,
                SeasonalAmplitude = config.SeasonalAmplitude,
                ClimateTrend = config.ClimateTrend,
                InterannualVariation = config.InterannualVariation,
                VariabilityMagnitude = config.VariabilityMagnitude,
                WarmingBias = config.WarmingBias,
                Autocorrelated = config.Autocorrelated,
                DailyVariationRange = config.DailyVariationRange,
                RandomnessGrowthRate = config.RandomnessGrowthRate,
                TemperatureBoundsMin = config.TemperatureBoundsMin,
                TemperatureBoundsMax = config.TemperatureBoundsMax,
                CarryingCapacityTier1 = config.CarryingCapacityTier1,
                MaxYears = config.MaxYears
            };
        }

        public void ApplyTo(SimulationConfig config)
        {
            if (config == null) return;

            config.BaseTemperature = BaseTemperature;
            config.SeasonalAmplitude = SeasonalAmplitude;
            config.ClimateTrend = ClimateTrend;
            config.InterannualVariation = InterannualVariation;
            config.VariabilityMagnitude = VariabilityMagnitude;
            config.WarmingBias = WarmingBias;
            config.Autocorrelated = Autocorrelated;
            config.DailyVariationRange = DailyVariationRange;
            config.RandomnessGrowthRate = RandomnessGrowthRate;
            config.TemperatureBoundsMin = TemperatureBoundsMin;
            config.TemperatureBoundsMax = TemperatureBoundsMax;
            config.CarryingCapacityTier1 = CarryingCapacityTier1;
            config.MaxYears = MaxYears;
        }
    }

    private void Awake()
    {
        // Store defaults on Awake (before any changes)
        StoreDefaults();
    }

    private void OnEnable()
    {
        if (RunSimulationButton != null)
        {
            RunSimulationButton.onClick.RemoveListener(OnRunSimulationClicked);
            RunSimulationButton.onClick.AddListener(OnRunSimulationClicked);
        }

        if (ResetButton != null)
        {
            ResetButton.onClick.RemoveListener(OnResetClicked);
            ResetButton.onClick.AddListener(OnResetClicked);
        }

        // Store defaults if not already stored
        if (!defaultsStored)
        {
            StoreDefaults();
        }

        PopulateUIFromConfig();
        ClearAllValidationColors();
    }

    private void OnDisable()
    {
        if (RunSimulationButton != null)
        {
            RunSimulationButton.onClick.RemoveListener(OnRunSimulationClicked);
        }

        if (ResetButton != null)
        {
            ResetButton.onClick.RemoveListener(OnResetClicked);
        }
    }

    /// <summary>
    /// Store the current config values as defaults.
    /// Called once when the script first initializes.
    /// </summary>
    private void StoreDefaults()
    {
        if (config == null) return;

        defaults = SimulationConfigDefaults.CreateFrom(config);
        defaultsStored = true;

        Debug.Log("SimulationInputUI: Default values stored");
    }

    /// <summary>
    /// Reset all values to defaults.
    /// Only affects SimulationConfig, not RunSpeciesList.
    /// </summary>
    private void OnResetClicked()
    {
        if (config == null || defaults == null)
        {
            Debug.LogWarning("SimulationInputUI: Cannot reset - config or defaults not available");
            return;
        }

        // Apply defaults to config
        defaults.ApplyTo(config);

        // Update UI to show reset values
        PopulateUIFromConfig();

        // Clear any validation errors
        ClearAllValidationColors();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(config);
#endif

        Debug.Log("SimulationInputUI: Reset to default values");
    }

    public void PopulateUIFromConfig()
    {
        if (config == null) return;

        SetFloat(BaseTemperatureInput, config.BaseTemperature);
        SetFloat(SeasonalAmplitude, config.SeasonalAmplitude);
        SetFloat(ClimateTrend, config.ClimateTrend);

        SetToggleNoNotify(InterannualVariation, config.InterannualVariation);

        SetFloat(VariabilityMagnitude, config.VariabilityMagnitude);
        SetFloat(WarmingBias, config.WarmingBias);

        SetToggleNoNotify(Autocorrelated, config.Autocorrelated);

        SetFloat(DailyVariationRange, config.DailyVariationRange);
        SetFloat(RandomnessGrowthRate, config.RandomnessGrowthRate);

        SetFloat(TemperatureBoundsMin, config.TemperatureBoundsMin);
        SetFloat(TemperatureBoundsMax, config.TemperatureBoundsMax);

        SetFloat(CarryingCapacityTier1, config.CarryingCapacityTier1);
        SetInt(MaxYears, config.MaxYears);
    }

    private void OnRunSimulationClicked()
    {
        if (config == null) return;

        ClearAllValidationColors();

        bool allValid = true;

        float baseTemp = 0f;
        float seasonalAmp = 0f;
        float climateTrend = 0f;
        float variabilityMag = 0f;
        float warmingBias = 0f;
        float dailyVarRange = 0f;
        float randGrowth = 0f;
        float boundsMin = 0f;
        float boundsMax = 0f;
        int maxYears = 0;

        allValid &= TryReadFloat(BaseTemperatureInput, out baseTemp);
        allValid &= TryReadFloat(SeasonalAmplitude, out seasonalAmp);
        allValid &= TryReadFloat(ClimateTrend, out climateTrend);

        allValid &= TryReadFloat(VariabilityMagnitude, out variabilityMag);
        allValid &= TryReadFloat(WarmingBias, out warmingBias);

        allValid &= TryReadFloat(DailyVariationRange, out dailyVarRange);
        allValid &= TryReadFloat(RandomnessGrowthRate, out randGrowth);

        allValid &= TryReadFloat(TemperatureBoundsMin, out boundsMin);
        allValid &= TryReadFloat(TemperatureBoundsMax, out boundsMax);
        allValid &= TryReadFloat(CarryingCapacityTier1, out float carryingCapacity);

        allValid &= TryReadInt(MaxYears, out maxYears);

        if (!allValid)
        {
            return;
        }

        config.BaseTemperature = baseTemp;
        config.SeasonalAmplitude = seasonalAmp;
        config.ClimateTrend = climateTrend;

        if (InterannualVariation != null) config.InterannualVariation = InterannualVariation.isOn;

        config.VariabilityMagnitude = variabilityMag;
        config.WarmingBias = warmingBias;

        if (Autocorrelated != null) config.Autocorrelated = Autocorrelated.isOn;

        config.DailyVariationRange = dailyVarRange;
        config.RandomnessGrowthRate = randGrowth;

        config.TemperatureBoundsMin = boundsMin;
        config.TemperatureBoundsMax = boundsMax;

        config.CarryingCapacityTier1 = carryingCapacity;
        config.MaxYears = maxYears;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(config);
#endif
        simulationController.StartSimulation();
    }

    private static void SetToggleNoNotify(Toggle t, bool value)
    {
        if (t == null) return;
        t.SetIsOnWithoutNotify(value);
    }

    private static void SetFloat(TMP_InputField field, float value)
    {
        if (field == null) return;
        field.text = value.ToString(CultureInfo.InvariantCulture);
    }

    private static void SetInt(TMP_InputField field, int value)
    {
        if (field == null) return;
        field.text = value.ToString(CultureInfo.InvariantCulture);
    }

    private bool TryReadFloat(TMP_InputField field, out float value)
    {
        value = 0f;

        if (field == null)
        {
            return false;
        }

        string s = field.text;
        if (s == null) s = "";
        s = s.Trim();

        bool ok = float.TryParse(
            s,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );

        SetFieldColor(field, ok ? ValidColor : InvalidColor);
        return ok;
    }

    private bool TryReadInt(TMP_InputField field, out int value)
    {
        value = 0;

        if (field == null)
        {
            return false;
        }

        string s = field.text;
        if (s == null) s = "";
        s = s.Trim();

        bool ok = int.TryParse(
            s,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value
        );

        SetFieldColor(field, ok ? ValidColor : InvalidColor);
        return ok;
    }

    private void ClearAllValidationColors()
    {
        SetFieldColor(BaseTemperatureInput, ValidColor);
        SetFieldColor(SeasonalAmplitude, ValidColor);
        SetFieldColor(ClimateTrend, ValidColor);

        SetFieldColor(VariabilityMagnitude, ValidColor);
        SetFieldColor(WarmingBias, ValidColor);

        SetFieldColor(DailyVariationRange, ValidColor);
        SetFieldColor(RandomnessGrowthRate, ValidColor);
        SetFieldColor(TemperatureBoundsMin, ValidColor);
        SetFieldColor(TemperatureBoundsMax, ValidColor);
        SetFieldColor(CarryingCapacityTier1, ValidColor);
        SetFieldColor(MaxYears, ValidColor);
    }

    private static void SetFieldColor(TMP_InputField field, Color color)
    {
        if (field == null) return;

        // TMP_InputField has an Image you can tint (usually the background).
        Image img = field.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
            return;
        }

        if (field.targetGraphic != null)
        {
            field.targetGraphic.color = color;
        }
    }
}