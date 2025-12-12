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
    public TMP_InputField MaxYears;
    public Button RunSimulationButton;


    private static readonly Color InvalidColor = new Color(1f, 0.80f, 0.80f, 1f);
    private static readonly Color ValidColor = Color.white;

    private void OnEnable()
    {
        if (RunSimulationButton != null)
        {
            RunSimulationButton.onClick.RemoveListener(OnRunSimulationClicked);
            RunSimulationButton.onClick.AddListener(OnRunSimulationClicked);
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