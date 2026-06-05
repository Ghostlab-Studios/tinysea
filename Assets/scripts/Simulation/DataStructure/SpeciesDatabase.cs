using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpeciesName
{
    Hexapod,
    Gelgi,
    Yelloa,
    Sheplik,
    Grabbler,
    Cyplo,
    Rooda,
    Sploof,
    Silu,
    Custom
}

public enum SpeciesVariant
{
    Common,
    Tropical,
    Arctic,
    Custom
}

[System.Serializable]
public class SpeciesData
{
    [Header("Basic Info")]
    public int index;
    public SpeciesName speciesName;
    public SpeciesVariant variant;
    // Free-text variant label (Batch 1A). When set, used for FullName/output; the
    // `variant` enum stays for legacy bucket columns + default-parameter lookup.
    public string variantLabel;
    public string displayName;
    public Sprite icon;
    public int count;

    [Header("Gameplay Stats")]
    public int tier;                        // 0 = Tier 1 (prey), 1 = Tier 2 (predator)
    public float eatingAmount;              // Prey consumed per creature per step
    public float reproductionMultiplier;    // Birth rate multiplier
    public float deathThreshold = 0.3f;     // FinalPerf below this triggers thermal death
    public float deathRate;                 // Fraction dying when thermal death triggers
    public float TemperatureDebuff = 0.0f;  // Additional performance debuff from temperature applied after thermal curve 
    public float reproThreshold = 0.25f;

    [Header("Condition Timescale (per-species τ, Batch 2)")]
    // Negative = inherit the simulator-global rate (blank CSV columns stay backward compatible).
    public float conditionDrainRate = -1f;
    public float conditionRecoveryRate = -1f;    // FinalPerf required to reproduce
    
    
    [Header("Natural Mortality")]


    [Tooltip("Base natural death rate per biology step (e.g., 0.02 = 2%)")]
    public float naturalDeathRate = 0.02f;
    [Tooltip("Random variance range (e.g., 0.01 = ±1%)")]
    public float naturalDeathVariance = 0.01f;


    [Header("Hunting Efficiency (Tier 2 only)")]
    [Tooltip("Base hunting success rate (e.g., 0.75 = 75%). Tier 1 ignores this.")]
    public float huntingEfficiency = 0.75f;
    [Tooltip("Random variance range (e.g., 0.15 = ±15%)")]
    public float huntingVariance = 0.15f;

    [Header("Star Ratings (UI)")]
    public int eatingStars;
    public int reproductionStars;
    public int deathThresholdStars;
    public int deathRateStars;
    public int thermalBreadthStars;

    [Header("Display Text (UI)")]
    public string temperatureThresholdText;
    public string reproductionRateText;
    public string description;

    [Header("Thermal Curve Parameters (Kelvin)")]
    public float optimalTempK = 297.0f;     // 24°C default (Common)
    public float arrhenBreadth = 8000.0f;
    public float arrhenLower = 3000.0f;
    public float arrhenUpper = 35000.0f;
    public float lowerBoundK = 296.0f;      // 23°C default (Common)
    public float upperBoundK = 298.0f;      // 25°C default (Common)

    [Header("Thermal Curve - Peak & Lethal Limits")]
    [Tooltip("Maximum performance at optimal temperature (0-1). Scales the curve output.")]
    [Range(0f, 1f)]
    public float pmax = 0.65f;
    [Tooltip("Critical thermal minimum in Celsius. Below this, performance = 0.")]
    public float ctMinC = 0.0f;
    [Tooltip("Critical thermal maximum in Celsius. Above this, performance = 0.")]
    public float ctMaxC = 40.0f;

    /// <summary>
    /// Get variant-based thermal defaults (Pmax, CTminC, CTmaxC).
    /// For Custom or unknown variants, returns Common defaults.
    /// </summary>
    public static void GetVariantThermalDefaults(SpeciesVariant variant,
        out float pmax, out float ctMinC, out float ctMaxC)
    {
        switch (variant)
        {
            case SpeciesVariant.Tropical:
                pmax = 0.85f;
                ctMinC = 0f;
                ctMaxC = 40f;
                break;
            case SpeciesVariant.Arctic:
                pmax = 0.85f;
                ctMinC = 0f;
                ctMaxC = 40f;
                break;
            default: // Common and Custom
                pmax = 0.65f;
                ctMinC = 0f;
                ctMaxC = 40f;
                break;
        }
    }

    /// <summary>
    /// Batch 1A: canonical-case the four legacy variant names (Common/Tropical/
    /// Arctic/Custom, case-insensitive) and pass through any other free-text label
    /// unchanged. Keeps legacy output byte-identical while allowing new labels.
    /// </summary>
    public static string NormalizeVariantLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        raw = raw.Trim();
        return System.Enum.TryParse<SpeciesVariant>(raw, true, out var v) ? v.ToString() : raw;
    }

    /// <summary>
    /// Batch 1B: resolve a variant label to its SpeciesVariant enum bucket, accepting
    /// the new names (Cold/Warm/Hot) and the legacy aliases (Arctic/Common/Tropical).
    /// Cold=Arctic, Warm=Common, Hot=Tropical; unknown labels => Custom. Used only for
    /// default-parameter lookup + legacy bucket columns — the display label is kept
    /// separately via variantLabel/NormalizeVariantLabel.
    /// </summary>
    public static SpeciesVariant ResolveVariantEnum(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return SpeciesVariant.Custom;
        switch (label.Trim().ToLowerInvariant())
        {
            case "cold": case "arctic":   return SpeciesVariant.Arctic;
            case "warm": case "common":   return SpeciesVariant.Common;
            case "hot":  case "tropical": return SpeciesVariant.Tropical;
            case "custom":                return SpeciesVariant.Custom;
            default:
                return System.Enum.TryParse<SpeciesVariant>(label.Trim(), true, out var e) ? e : SpeciesVariant.Custom;
        }
    }

    /// <summary>
    /// Group 2: normalized match-key for a variant label — lowercase, keep only [a-z0-9]
    /// (strips spaces/dashes/underscores/punctuation). Two labels that normalize equal are
    /// the same variant. "Common-Leaning Tropic" -> "commonleaningtropic"; "topic3" != "topic4".
    /// </summary>
    public static string VariantMatchKey(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (char ch in raw)
        {
            char c = char.ToLowerInvariant(ch);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
        }
        return sb.ToString();
    }
}

[CreateAssetMenu(fileName = "SpeciesDatabase", menuName = "TinySea/Species Database")]
public class SpeciesDatabase : ScriptableObject
{
    public List<SpeciesData> speciesList = new List<SpeciesData>();

    private const int DEFAULT_T1_COUNT = 20;
    private const int DEFAULT_T2_COUNT = 4;
    private const int DEFAULT_T3_COUNT = 2;

    // Quick lookup by enum
    public SpeciesData GetSpecies(SpeciesName name, SpeciesVariant variant)
    {
        return speciesList.Find(s => s.speciesName == name && s.variant == variant);
    }

    public SpeciesData GetSpeciesByName(string displayName)
    {
        return speciesList.Find(s => s.displayName == displayName);
    }

    // Get all species of a specific tier
    public List<SpeciesData> GetSpeciesByTier(int tier)
    {
        return speciesList.FindAll(s => s.tier == tier);
    }

    // Get all variants of a species
    public List<SpeciesData> GetVariants(SpeciesName name)
    {
        return speciesList.FindAll(s => s.speciesName == name);
    }

#if UNITY_EDITOR
    [ContextMenu("Populate Default Data")]
    private void PopulateDefaultData()
    {
        speciesList.Clear();

        // ===== HEXAPOD (Tier 1 - Prey) =====
        // DeathRate 0.6 — smaller prey have less physiological buffering against
        // chronic stress (allometric scaling: M ∝ W^-0.25, Peterson & Wroblewski 1984)
        // Natural death: 2% base ±1% variance
        // Hunting: N/A (Tier 1 doesn't hunt)

        AddSpecies(
            index: 0,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Common,
            tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.02f,
            naturalDeathVariance: 0.01f,
            huntingEfficiency: 1.0f,
            huntingVariance: 0f,
            optimalK: 297.0f,       // 24°C
            arrhenBreadth: 8000.0f,
            arrhenLower: 3000.0f,
            arrhenUpper: 35000.0f,
            lowerBound: 296.0f,     // 23°C
            upperBound: 298.0f,     // 25°C
            pmax: 0.65f,
            ctMinC: 0f,
            ctMaxC: 40f
        );

        AddSpecies(
            index: 1,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Tropical,
            tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.02f,
            naturalDeathVariance: 0.01f,
            huntingEfficiency: 1.0f,
            huntingVariance: 0f,
            optimalK: 303.0f,       // 30°C
            arrhenBreadth: 4000.0f,
            arrhenLower: 15827.0f,
            arrhenUpper: 35000.0f,
            lowerBound: 302.9f,     // 29.75°C
            upperBound: 303.1f,     // 29.95°C
            pmax: 0.85f,
            ctMinC: 0f,
            ctMaxC: 40f
        );

        AddSpecies(
            index: 2,
            name: SpeciesName.Hexapod,
            variant: SpeciesVariant.Arctic,
            tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: 0f,
            repro: 0.45f,
            deathThresh: 0.3f,
            deathRate: 0.6f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.02f,
            naturalDeathVariance: 0.01f,
            huntingEfficiency: 1.0f,
            huntingVariance: 0f,
            optimalK: 291.0f,       // 18°C
            arrhenBreadth: 4000.0f,
            arrhenLower: 13974.0f,
            arrhenUpper: 35000.0f,
            lowerBound: 290.9f,     // 17.75°C
            upperBound: 291.1f,     // 17.95°C
            pmax: 0.85f,
            ctMinC: 0f,
            ctMaxC: 40f
        );

        // ===== SHEPLIK (Tier 2 - Predator) =====
        // DeathRate 0.3 — larger predators have greater energy reserves and stress
        // tolerance, dying at roughly half the rate of prey (allometric scaling:
        // M ∝ W^-0.25; cod M≈0.2 vs capelin M≈0.8, McCoy & Gillooly 2008)
        // Natural death: 2% base ±1% variance
        // Hunting: 75% base ±15% variance

        AddSpecies(
            index: 3,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Common,
            tier: 1,
            count: DEFAULT_T2_COUNT,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.01f,      // Allometric: larger predators have lower background mortality
            naturalDeathVariance: 0.005f,
            huntingEfficiency: 0.75f,
            huntingVariance: 0.15f,
            optimalK: 297.0f,       // 24°C
            arrhenBreadth: 8000.0f,
            arrhenLower: 3000.0f,
            arrhenUpper: 35000.0f,
            lowerBound: 296.0f,     // 23°C
            upperBound: 298.0f,     // 25°C
            pmax: 0.65f,
            ctMinC: 0f,
            ctMaxC: 40f
        );

        AddSpecies(
            index: 4,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Tropical,
            tier: 1,
            count: DEFAULT_T2_COUNT,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.01f,      // Allometric: larger predators have lower background mortality
            naturalDeathVariance: 0.005f,
            huntingEfficiency: 0.75f,
            huntingVariance: 0.15f,
            optimalK: 303.0f,       // 30°C
            arrhenBreadth: 4000.0f,
            arrhenLower: 15827.0f,
            arrhenUpper: 35000.0f,
            lowerBound: 302.9f,     // 29.75°C
            upperBound: 303.1f,     // 29.95°C
            pmax: 0.85f,
            ctMinC: 0f,
            ctMaxC: 40f
        );

        AddSpecies(
            index: 5,
            name: SpeciesName.Sheplik,
            variant: SpeciesVariant.Arctic,
            tier: 1,
            count: DEFAULT_T2_COUNT,
            eating: 1.5f,
            repro: 0.1f,
            deathThresh: 0.3f,
            deathRate: 0.3f,
            reproThresh: 0.25f,
            naturalDeathRate: 0.01f,      // Allometric: larger predators have lower background mortality
            naturalDeathVariance: 0.005f,
            huntingEfficiency: 0.75f,
            huntingVariance: 0.15f,
            optimalK: 291.0f,       // 18°C
            arrhenBreadth: 4000.0f,
            arrhenLower: 13974.0f,
            arrhenUpper: 35000.0f,
            lowerBound: 290.9f,     // 17.75°C
            upperBound: 291.1f,     // 17.95°C
            pmax: 0.85f,
            ctMinC: 0f,
            ctMaxC: 40f
        );

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"Populated {speciesList.Count} species entries");
        Debug.Log("Tier 1 (Hexapod): NaturalDeath=2%±1%, Hunting=N/A");
        Debug.Log("Tier 2 (Sheplik): NaturalDeath=2%±1%, Hunting=75%±15%");
    }

    private void AddSpecies(int index, SpeciesName name, SpeciesVariant variant, int tier, int count,
                           float eating, float repro, float deathThresh, float deathRate,
                           float reproThresh,
                           float naturalDeathRate, float naturalDeathVariance,
                           float huntingEfficiency, float huntingVariance,
                           float optimalK, float arrhenBreadth, float arrhenLower, float arrhenUpper,
                           float lowerBound, float upperBound,
                           float pmax, float ctMinC, float ctMaxC)
    {
        var data = new SpeciesData
        {
            index = index,
            speciesName = name,
            variant = variant,
            displayName = name.ToString(),
            tier = tier,
            count = count,
            eatingAmount = eating,
            reproductionMultiplier = repro,
            deathThreshold = deathThresh,
            deathRate = deathRate,
            reproThreshold = reproThresh,
            naturalDeathRate = naturalDeathRate,
            naturalDeathVariance = naturalDeathVariance,
            huntingEfficiency = huntingEfficiency,
            huntingVariance = huntingVariance,
            // Thermal parameters
            optimalTempK = optimalK,
            arrhenBreadth = arrhenBreadth,
            arrhenLower = arrhenLower,
            arrhenUpper = arrhenUpper,
            lowerBoundK = lowerBound,
            upperBoundK = upperBound,
            // Peak & lethal limits
            pmax = pmax,
            ctMinC = ctMinC,
            ctMaxC = ctMaxC,
            // UI defaults
            eatingStars = tier == 0 ? 0 : 4,
            reproductionStars = tier == 0 ? 4 : 2,
            deathThresholdStars = 3,
            deathRateStars = tier == 0 ? 2 : 4,
            thermalBreadthStars = 5,
            temperatureThresholdText = "High",
            reproductionRateText = "Low",
            description = $"{name} - {variant} variant"
        };

        speciesList.Add(data);
    }

    [ContextMenu("Reset Values (Preserve Icons)")]
    private void ResetValuesPreserveIcons()
    {
        int resetCount = 0;

        foreach (var data in speciesList)
        {
            // Preserve: icon, index, speciesName, variant, displayName, count

            // --- Universal defaults ---
            data.deathThreshold = 0.3f;
            data.reproThreshold = 0.25f;
            data.TemperatureDebuff = 0f;
            data.ctMinC = 0f;
            data.ctMaxC = 40f;

            // --- Variant-based defaults (thermal params) ---
            switch (data.variant)
            {
                case SpeciesVariant.Common:
                    data.optimalTempK = 297.0f;
                    data.arrhenBreadth = 8000.0f;
                    data.arrhenLower = 3000.0f;
                    data.arrhenUpper = 35000.0f;
                    data.lowerBoundK = 296.0f;
                    data.upperBoundK = 298.0f;
                    data.pmax = 0.65f;
                    break;
                case SpeciesVariant.Tropical:
                    data.optimalTempK = 303.0f;
                    data.arrhenBreadth = 4000.0f;
                    data.arrhenLower = 15827.0f;
                    data.arrhenUpper = 35000.0f;
                    data.lowerBoundK = 302.9f;
                    data.upperBoundK = 303.1f;
                    data.pmax = 0.85f;
                    break;
                case SpeciesVariant.Arctic:
                    data.optimalTempK = 291.0f;
                    data.arrhenBreadth = 4000.0f;
                    data.arrhenLower = 13974.0f;
                    data.arrhenUpper = 35000.0f;
                    data.lowerBoundK = 290.9f;
                    data.upperBoundK = 291.1f;
                    data.pmax = 0.85f;
                    break;
                default:
                    Debug.LogWarning($"Skipping thermal reset for Custom variant: {data.displayName}");
                    break;
            }

            // --- Per-species defaults (biology + UI) ---
            switch (data.speciesName)
            {
                case SpeciesName.Hexapod:
                    data.tier = 0;
                    data.eatingAmount = 0f;
                    data.reproductionMultiplier = 0.45f;
                    data.deathRate = 0.6f;
                    data.naturalDeathRate = 0.02f;
                    data.naturalDeathVariance = 0.01f;
                    data.huntingEfficiency = 1.0f;
                    data.huntingVariance = 0f;
                    data.eatingStars = 0;
                    data.reproductionStars = 4;
                    data.deathThresholdStars = 3;
                    data.deathRateStars = 2;
                    data.thermalBreadthStars = 5;
                    data.temperatureThresholdText = "High";
                    data.reproductionRateText = "Low";
                    data.description = $"Hexapod - {data.variant} variant";
                    break;
                case SpeciesName.Sheplik:
                    data.tier = 1;
                    data.eatingAmount = 1.5f;
                    data.reproductionMultiplier = 0.1f;
                    data.deathRate = 0.3f;  // Predators die at half prey rate (allometric scaling)
                    data.naturalDeathRate = 0.01f;   // Allometric: larger predators have lower background mortality
                    data.naturalDeathVariance = 0.005f;
                    data.huntingEfficiency = 0.75f;
                    data.huntingVariance = 0.15f;
                    data.eatingStars = 4;
                    data.reproductionStars = 2;
                    data.deathThresholdStars = 4;
                    data.deathRateStars = 4;
                    data.thermalBreadthStars = data.variant == SpeciesVariant.Common ? 5 : 3;
                    data.temperatureThresholdText = "High";
                    data.reproductionRateText = "Low";
                    data.description = $"Sheplik - {data.variant} variant";
                    break;
                default:
                    Debug.LogWarning($"No per-species defaults for: {data.speciesName} {data.variant} — skipping biology reset");
                    continue;
            }

            resetCount++;
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"Reset {resetCount}/{speciesList.Count} species values (icons preserved)");
    }

    private void AddSpecies(int index, string displayname, int tier, int count,
                           float eating, float repro, float deathThresh, float deathRate,
                           float reproThresh,
                           float naturalDeathRate, float naturalDeathVariance,
                           float huntingEfficiency, float huntingVariance,
                           float optimalK, float lowerBound, float upperBound)
    {
        var data = new SpeciesData
        {
            index = index,
            speciesName = SpeciesName.Custom,
            variant = SpeciesVariant.Custom,
            displayName = displayname,
            tier = tier,
            count = count,
            eatingAmount = eating,
            reproductionMultiplier = repro,
            deathThreshold = deathThresh,
            deathRate = deathRate,
            reproThreshold = reproThresh,
            naturalDeathRate = naturalDeathRate,
            naturalDeathVariance = naturalDeathVariance,
            huntingEfficiency = huntingEfficiency,
            huntingVariance = huntingVariance,
            // Thermal parameters
            optimalTempK = optimalK,
            arrhenBreadth = 5273.15f,
            arrhenLower = 10273.15f,
            arrhenUpper = 21273.15f,
            lowerBoundK = lowerBound,
            upperBoundK = upperBound,
            // Peak & lethal limits (defaults)
            pmax = 1.0f,
            ctMinC = -5.0f,
            ctMaxC = 40.0f,
            // UI defaults
            eatingStars = tier == 0 ? 0 : 4,
            reproductionStars = tier == 0 ? 4 : 2,
            deathThresholdStars = 3,
            deathRateStars = tier == 0 ? 2 : 4,
            thermalBreadthStars = 5,
            temperatureThresholdText = "High",
            reproductionRateText = "Low",
            description = $"{displayname} - {SpeciesVariant.Custom} variant"
        };
    }

#endif
}

