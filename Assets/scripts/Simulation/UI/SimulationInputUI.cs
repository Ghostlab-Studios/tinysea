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
    public TMP_InputField AutocorrelationCoefficient;
    public TMP_InputField DailyVariationRange;
    public TMP_InputField RandomnessGrowthRate;

    [Header("=== TEMPERATURE: BOUNDS ===")]
    public TMP_InputField TemperatureBoundsMin;
    public TMP_InputField TemperatureBoundsMax;

    [Header("=== CONDITION (HEALTH) SYSTEM ===")]

    [Tooltip("How fast Condition drains toward poor performance.\n" +
         "0.15 = ~8 days from full health to death threshold at suboptimal temps.\n" +
         "Drain accelerates up to 2x near lethal limits.")]
    public TMP_InputField ConditionDrainRate;

    [Tooltip("How fast Condition recovers toward good performance.\n" +
         "Slower than drain (asymmetric recovery).\n" +
         "0.10 = ~10 good days to fully recover.")]
    public TMP_InputField ConditionRecoveryRate;

    [Header("=== RUN SIMULATION ===")]

    [Tooltip("Maximum sustainable population for Tier 1.\n\n" +
         "Represents the resource limit of the environment.\n" +
         "Recommended: 1000-10000 depending on desired ecosystem size.")]
    public TMP_InputField CarryingCapacityTier1;

    [Tooltip("Number of days per scenario (e.g., 365 for 1 year, 3650 for 10 years)")]
    public TMP_InputField DaysPerScenarioInput;

    [Tooltip("Number of times to run the scenario for statistical analysis")]
    public TMP_InputField NumberOfScenariosInput;

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
        public float AutocorrelationCoefficient;
        public float DailyVariationRange;
        public float RandomnessGrowthRate;
        public float TemperatureBoundsMin;
        public float TemperatureBoundsMax;
        public float CarryingCapacityTier1;
        public float ConditionDrainRate;
        public float ConditionRecoveryRate;
        public int DaysPerScenario;
        public int NumberOfScenarios;

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
                AutocorrelationCoefficient = config.AutocorrelationCoefficient,
                DailyVariationRange = config.DailyVariationRange,
                RandomnessGrowthRate = config.RandomnessGrowthRate,
                TemperatureBoundsMin = config.TemperatureBoundsMin,
                TemperatureBoundsMax = config.TemperatureBoundsMax,
                CarryingCapacityTier1 = config.CarryingCapacityTier1,
                ConditionDrainRate = config.ConditionDrainRate,
                ConditionRecoveryRate = config.ConditionRecoveryRate,
                DaysPerScenario = config.DaysPerScenario,
                NumberOfScenarios = config.NumberOfScenarios
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
            config.AutocorrelationCoefficient = AutocorrelationCoefficient;
            config.DailyVariationRange = DailyVariationRange;
            config.RandomnessGrowthRate = RandomnessGrowthRate;
            config.TemperatureBoundsMin = TemperatureBoundsMin;
            config.TemperatureBoundsMax = TemperatureBoundsMax;
            config.CarryingCapacityTier1 = CarryingCapacityTier1;
            config.ConditionDrainRate = ConditionDrainRate;
            config.ConditionRecoveryRate = ConditionRecoveryRate;
            config.DaysPerScenario = DaysPerScenario;
            config.NumberOfScenarios = NumberOfScenarios;
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

        // Batch 4: enable the autocorrelation-coefficient input only while autocorrelation is on.
        if (Autocorrelated != null)
        {
            Autocorrelated.onValueChanged.RemoveListener(OnAutocorrelatedToggled);
            Autocorrelated.onValueChanged.AddListener(OnAutocorrelatedToggled);
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

        if (Autocorrelated != null)
            Autocorrelated.onValueChanged.RemoveListener(OnAutocorrelatedToggled);
    }

    // Batch 4: the autocorrelation coefficient only matters when autocorrelation is on,
    // so its input box is interactable only while the toggle is checked.
    private void OnAutocorrelatedToggled(bool on)
    {
        if (AutocorrelationCoefficient != null) AutocorrelationCoefficient.interactable = on;
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
        SetFloat(AutocorrelationCoefficient, config.AutocorrelationCoefficient);
        if (AutocorrelationCoefficient != null && Autocorrelated != null)
            AutocorrelationCoefficient.interactable = Autocorrelated.isOn;

        SetFloat(DailyVariationRange, config.DailyVariationRange);
        SetFloat(RandomnessGrowthRate, config.RandomnessGrowthRate);

        SetFloat(TemperatureBoundsMin, config.TemperatureBoundsMin);
        SetFloat(TemperatureBoundsMax, config.TemperatureBoundsMax);

        SetFloat(CarryingCapacityTier1, config.CarryingCapacityTier1);

        SetFloat(ConditionDrainRate, config.ConditionDrainRate);
        SetFloat(ConditionRecoveryRate, config.ConditionRecoveryRate);

        SetInt(DaysPerScenarioInput, config.DaysPerScenario);
        SetInt(NumberOfScenariosInput, config.NumberOfScenarios);
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
        float carryingCapacity = 0f;
        float condDrain = 0f;
        float condRecovery = 0f;
        float autocorrCoeff = 0f;
        int daysPerScenario = 0;
        int numberOfScenarios = 0;

        allValid &= TryReadFloat(BaseTemperatureInput, out baseTemp);
        allValid &= TryReadFloat(SeasonalAmplitude, out seasonalAmp);
        allValid &= TryReadFloat(ClimateTrend, out climateTrend);

        allValid &= TryReadFloat(VariabilityMagnitude, out variabilityMag);
        allValid &= TryReadFloat(WarmingBias, out warmingBias);

        allValid &= TryReadFloat(DailyVariationRange, out dailyVarRange);
        allValid &= TryReadFloat(RandomnessGrowthRate, out randGrowth);

        allValid &= TryReadFloat(TemperatureBoundsMin, out boundsMin);
        allValid &= TryReadFloat(TemperatureBoundsMax, out boundsMax);
        allValid &= TryReadFloat(CarryingCapacityTier1, out carryingCapacity);

        allValid &= TryReadFloat(ConditionDrainRate, out condDrain);
        allValid &= TryReadFloat(ConditionRecoveryRate, out condRecovery);
        allValid &= TryReadFloat(AutocorrelationCoefficient, out autocorrCoeff, 0f, 1f);

        allValid &= TryReadInt(DaysPerScenarioInput, out daysPerScenario, minValue: 1, maxValue: 182500);
        allValid &= TryReadInt(NumberOfScenariosInput, out numberOfScenarios, minValue: 1, maxValue: 100);

        if (!allValid)
        {
            Debug.LogWarning("SimulationInputUI: Validation failed - check highlighted fields");
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

        // Only overwrite condition fields if their UI fields are assigned
        if (ConditionDrainRate != null) config.ConditionDrainRate = condDrain;
        if (ConditionRecoveryRate != null) config.ConditionRecoveryRate = condRecovery;
        if (AutocorrelationCoefficient != null) config.AutocorrelationCoefficient = autocorrCoeff;

        config.DaysPerScenario = daysPerScenario;
        config.NumberOfScenarios = numberOfScenarios;

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

    private bool TryReadFloat(TMP_InputField field, out float value, float min = float.MinValue, float max = float.MaxValue)
    {
        value = 0f;

        if (field == null)
        {
            // Unassigned field — skip validation, keep config default
            Debug.Log("SimulationInputUI: A float input field is not assigned in Inspector — using config default.");
            return true;
        }

        string s = field.text;
        if (s == null) s = "";
        s = s.Trim();

        bool ok = float.TryParse(
            s,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        ) && value >= min && value <= max;

        SetFieldColor(field, ok ? ValidColor : InvalidColor);
        return ok;
    }

    private bool TryReadInt(TMP_InputField field, out int value, int minValue = int.MinValue, int maxValue = int.MaxValue)
    {
        value = 0;

        if (field == null)
        {
            // Unassigned field — skip validation, keep config default
            Debug.Log("SimulationInputUI: An int input field is not assigned in Inspector — using config default.");
            return true;
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

        // Additional range validation
        if (ok)
        {
            if (value < minValue || value > maxValue)
            {
                ok = false;
                Debug.LogWarning($"SimulationInputUI: Value {value} out of range [{minValue}, {maxValue}]");
            }
        }

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
        SetFieldColor(ConditionDrainRate, ValidColor);
        SetFieldColor(ConditionRecoveryRate, ValidColor);
        SetFieldColor(DaysPerScenarioInput, ValidColor);
        SetFieldColor(NumberOfScenariosInput, ValidColor);
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