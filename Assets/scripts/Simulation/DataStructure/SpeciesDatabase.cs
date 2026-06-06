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

    /// <summary>
    /// Variant-selector redesign: canonical deep copy of every serialized field
    /// (thermal + biology + variantLabel + displayName + condition rates + icon ref).
    /// Single source of copy truth — reuse wherever a catalog template is instantiated
    /// into a RunSpeciesList working entry.
    /// </summary>
    public SpeciesData DeepCopy()
    {
        var c = UnityEngine.JsonUtility.FromJson<SpeciesData>(UnityEngine.JsonUtility.ToJson(this));
        c.icon = this.icon;  // UnityEngine.Object ref isn't round-tripped by JsonUtility
        return c;
    }

    /// <summary>
    /// Variant-selector redesign: overwrite EVERY serialized field of THIS instance from
    /// <paramref name="src"/> while preserving this object's reference (so callers holding
    /// the RunSpeciesList entry keep their pointer). Counterpart to DeepCopy.
    /// </summary>
    public void CopyFrom(SpeciesData src)
    {
        UnityEngine.JsonUtility.FromJsonOverwrite(UnityEngine.JsonUtility.ToJson(src), this);
        this.icon = src.icon;
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

        // ===== Six-organism canonical default family (Tier 1 / prey only) =====
        // Source of truth: Phase2plus_SixOrganismDefaults_Kelvin.csv (2026-06-05).
        // Hex = narrow-breadth specialist (B=5000) -> SpeciesName.Hexapod.
        // Gol = broad-breadth  generalist (B=7000) -> SpeciesName.Gelgi.
        // Variant Cold/Warm/Hot => Topt 20/22/24 °C (= 293.15/295.15/297.15 K).
        // Display label is Cold/Warm/Hot (variantLabel); the legacy `variant`
        // enum bucket is resolved via the alias map (Cold->Arctic, Warm->Common,
        // Hot->Tropical) so legacy bucket columns / param lookups stay valid.
        // Shared non-thermal defaults: eating=3, repro=0.45, deathThresh=0.3,
        // deathRate=0.6, reproThresh=0.25, naturalDeath=0.02±0.01, tempOffset=0,
        // hunting N/A (Tier 1), conditionDrain=0.15, conditionRecovery=0.10.
        // NO Tier-2 / NO Sheplik here — that is a separate, pending decision.

        const float SHARED_EATING = 3f;
        const float SHARED_REPRO = 0.45f;
        const float SHARED_DEATH_THRESH = 0.3f;
        const float SHARED_DEATH_RATE = 0.6f;
        const float SHARED_REPRO_THRESH = 0.25f;
        const float SHARED_NATURAL_DEATH = 0.02f;
        const float SHARED_NATURAL_DEATH_VAR = 0.01f;
        const float SHARED_HUNT_EFF = 1.0f;   // Tier 1 ignores hunting
        const float SHARED_HUNT_VAR = 0f;
        const float SHARED_COND_DRAIN = 0.15f;
        const float SHARED_COND_RECOVERY = 0.10f;

        // ----- HEXAPOD (specialist, B=5000) -----
        AddSpecies(
            index: 0, name: SpeciesName.Hexapod, variant: SpeciesVariant.Arctic, tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: SHARED_EATING, repro: SHARED_REPRO, deathThresh: SHARED_DEATH_THRESH,
            deathRate: SHARED_DEATH_RATE, reproThresh: SHARED_REPRO_THRESH,
            naturalDeathRate: SHARED_NATURAL_DEATH, naturalDeathVariance: SHARED_NATURAL_DEATH_VAR,
            huntingEfficiency: SHARED_HUNT_EFF, huntingVariance: SHARED_HUNT_VAR,
            optimalK: 293.15f, arrhenBreadth: 5000f, arrhenLower: 15998f, arrhenUpper: 43798f,
            lowerBound: 292.4f, upperBound: 293.9f,
            pmax: 0.9843f, ctMinC: 0f, ctMaxC: 35f,
            conditionDrainRate: SHARED_COND_DRAIN, conditionRecoveryRate: SHARED_COND_RECOVERY,
            variantLabel: "Cold"
        );

        AddSpecies(
            index: 1, name: SpeciesName.Hexapod, variant: SpeciesVariant.Common, tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: SHARED_EATING, repro: SHARED_REPRO, deathThresh: SHARED_DEATH_THRESH,
            deathRate: SHARED_DEATH_RATE, reproThresh: SHARED_REPRO_THRESH,
            naturalDeathRate: SHARED_NATURAL_DEATH, naturalDeathVariance: SHARED_NATURAL_DEATH_VAR,
            huntingEfficiency: SHARED_HUNT_EFF, huntingVariance: SHARED_HUNT_VAR,
            optimalK: 295.15f, arrhenBreadth: 5000f, arrhenLower: 16000f, arrhenUpper: 43800f,
            lowerBound: 294.4f, upperBound: 295.9f,
            pmax: 0.972f, ctMinC: 2f, ctMaxC: 37f,
            conditionDrainRate: SHARED_COND_DRAIN, conditionRecoveryRate: SHARED_COND_RECOVERY,
            variantLabel: "Warm"
        );

        AddSpecies(
            index: 2, name: SpeciesName.Hexapod, variant: SpeciesVariant.Tropical, tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: SHARED_EATING, repro: SHARED_REPRO, deathThresh: SHARED_DEATH_THRESH,
            deathRate: SHARED_DEATH_RATE, reproThresh: SHARED_REPRO_THRESH,
            naturalDeathRate: SHARED_NATURAL_DEATH, naturalDeathVariance: SHARED_NATURAL_DEATH_VAR,
            huntingEfficiency: SHARED_HUNT_EFF, huntingVariance: SHARED_HUNT_VAR,
            optimalK: 297.15f, arrhenBreadth: 5000f, arrhenLower: 16002f, arrhenUpper: 43802f,
            lowerBound: 296.4f, upperBound: 297.9f,
            pmax: 0.96f, ctMinC: 4f, ctMaxC: 39f,
            conditionDrainRate: SHARED_COND_DRAIN, conditionRecoveryRate: SHARED_COND_RECOVERY,
            variantLabel: "Hot"
        );

        // ----- GELGI (generalist, B=7000) -----
        AddSpecies(
            index: 3, name: SpeciesName.Gelgi, variant: SpeciesVariant.Arctic, tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: SHARED_EATING, repro: SHARED_REPRO, deathThresh: SHARED_DEATH_THRESH,
            deathRate: SHARED_DEATH_RATE, reproThresh: SHARED_REPRO_THRESH,
            naturalDeathRate: SHARED_NATURAL_DEATH, naturalDeathVariance: SHARED_NATURAL_DEATH_VAR,
            huntingEfficiency: SHARED_HUNT_EFF, huntingVariance: SHARED_HUNT_VAR,
            optimalK: 293.15f, arrhenBreadth: 7000f, arrhenLower: 4998f, arrhenUpper: 31098f,
            lowerBound: 292.4f, upperBound: 293.9f,
            pmax: 0.6616f, ctMinC: 0f, ctMaxC: 35f,
            conditionDrainRate: SHARED_COND_DRAIN, conditionRecoveryRate: SHARED_COND_RECOVERY,
            variantLabel: "Cold"
        );

        AddSpecies(
            index: 4, name: SpeciesName.Gelgi, variant: SpeciesVariant.Common, tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: SHARED_EATING, repro: SHARED_REPRO, deathThresh: SHARED_DEATH_THRESH,
            deathRate: SHARED_DEATH_RATE, reproThresh: SHARED_REPRO_THRESH,
            naturalDeathRate: SHARED_NATURAL_DEATH, naturalDeathVariance: SHARED_NATURAL_DEATH_VAR,
            huntingEfficiency: SHARED_HUNT_EFF, huntingVariance: SHARED_HUNT_VAR,
            optimalK: 295.15f, arrhenBreadth: 7000f, arrhenLower: 5000f, arrhenUpper: 31100f,
            lowerBound: 294.4f, upperBound: 295.9f,
            pmax: 0.6547f, ctMinC: 2f, ctMaxC: 37f,
            conditionDrainRate: SHARED_COND_DRAIN, conditionRecoveryRate: SHARED_COND_RECOVERY,
            variantLabel: "Warm"
        );

        AddSpecies(
            index: 5, name: SpeciesName.Gelgi, variant: SpeciesVariant.Tropical, tier: 0,
            count: DEFAULT_T1_COUNT,
            eating: SHARED_EATING, repro: SHARED_REPRO, deathThresh: SHARED_DEATH_THRESH,
            deathRate: SHARED_DEATH_RATE, reproThresh: SHARED_REPRO_THRESH,
            naturalDeathRate: SHARED_NATURAL_DEATH, naturalDeathVariance: SHARED_NATURAL_DEATH_VAR,
            huntingEfficiency: SHARED_HUNT_EFF, huntingVariance: SHARED_HUNT_VAR,
            optimalK: 297.15f, arrhenBreadth: 7000f, arrhenLower: 5002f, arrhenUpper: 31102f,
            lowerBound: 296.4f, upperBound: 297.9f,
            pmax: 0.6481f, ctMinC: 4f, ctMaxC: 39f,
            conditionDrainRate: SHARED_COND_DRAIN, conditionRecoveryRate: SHARED_COND_RECOVERY,
            variantLabel: "Hot"
        );

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"Populated {speciesList.Count} species entries (6 Tier-1 Cold/Warm/Hot organisms)");
        Debug.Log("Hexapod (specialist B=5000) x Cold/Warm/Hot; Gelgi (generalist B=7000) x Cold/Warm/Hot");
        Debug.Log("conditionDrain=0.15, conditionRecovery=0.10; eating=3, repro=0.45, deathRate=0.6");
    }

    private void AddSpecies(int index, SpeciesName name, SpeciesVariant variant, int tier, int count,
                           float eating, float repro, float deathThresh, float deathRate,
                           float reproThresh,
                           float naturalDeathRate, float naturalDeathVariance,
                           float huntingEfficiency, float huntingVariance,
                           float optimalK, float arrhenBreadth, float arrhenLower, float arrhenUpper,
                           float lowerBound, float upperBound,
                           float pmax, float ctMinC, float ctMaxC,
                           float conditionDrainRate = -1f, float conditionRecoveryRate = -1f,
                           string variantLabel = null)
    {
        var data = new SpeciesData
        {
            index = index,
            speciesName = name,
            variant = variant,
            variantLabel = variantLabel,
            displayName = name.ToString(),
            tier = tier,
            count = count,
            eatingAmount = eating,
            reproductionMultiplier = repro,
            deathThreshold = deathThresh,
            deathRate = deathRate,
            reproThreshold = reproThresh,
            conditionDrainRate = conditionDrainRate,
            conditionRecoveryRate = conditionRecoveryRate,
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
            description = $"{name} - {(string.IsNullOrEmpty(variantLabel) ? variant.ToString() : variantLabel)} variant"
        };

        speciesList.Add(data);
    }

    [ContextMenu("Reset Values (Preserve Icons)")]
    private void ResetValuesPreserveIcons()
    {
        int resetCount = 0;

        foreach (var data in speciesList)
        {
            // Preserve: icon, index, speciesName, variant, displayName, count.
            // Canonical source: Phase2plus_SixOrganismDefaults_Kelvin.csv (2026-06-05).
            // Thermal params are keyed on BOTH speciesName AND variant — Hexapod
            // (specialist B=5000) and Gelgi (generalist B=7000) differ in B/L/U/Pmax.
            // Variant enum bucket -> display label: Arctic=Cold, Common=Warm, Tropical=Hot.

            // --- Universal (shared Tier-1) defaults ---
            data.deathThreshold = 0.3f;
            data.reproThreshold = 0.25f;
            data.TemperatureDebuff = 0f;
            data.eatingAmount = 3f;
            data.reproductionMultiplier = 0.45f;
            data.deathRate = 0.6f;
            data.naturalDeathRate = 0.02f;
            data.naturalDeathVariance = 0.01f;
            data.huntingEfficiency = 1.0f;   // Tier 1 ignores hunting
            data.huntingVariance = 0f;
            data.conditionDrainRate = 0.15f;
            data.conditionRecoveryRate = 0.10f;
            data.tier = 0;

            // --- Per-(species, variant) thermal params + variant label ---
            bool thermalSet = ApplyCanonicalThermal(data);
            if (!thermalSet)
            {
                Debug.LogWarning($"No canonical thermal defaults for {data.speciesName} {data.variant} — skipping reset");
                continue;
            }

            // --- Shared Tier-1 UI defaults ---
            data.eatingStars = 0;
            data.reproductionStars = 4;
            data.deathThresholdStars = 3;
            data.deathRateStars = 2;
            data.thermalBreadthStars = 5;
            data.temperatureThresholdText = "High";
            data.reproductionRateText = "Low";
            data.description = $"{data.speciesName} - {(string.IsNullOrEmpty(data.variantLabel) ? data.variant.ToString() : data.variantLabel)} variant";

            resetCount++;
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"Reset {resetCount}/{speciesList.Count} species values (icons preserved)");
    }

    /// <summary>
    /// Apply the canonical Cold/Warm/Hot thermal params (optimalK, B, L, U, bounds,
    /// pmax, CTmin/max) for the given species, keyed on BOTH speciesName AND variant.
    /// Also sets variantLabel to Cold/Warm/Hot. Source: Phase2plus_SixOrganismDefaults_Kelvin.csv.
    /// Returns false (caller skips) for species/variant combos that have no canonical entry.
    /// </summary>
    private static bool ApplyCanonicalThermal(SpeciesData data)
    {
        switch (data.speciesName)
        {
            case SpeciesName.Hexapod: // specialist, B = 5000
                switch (data.variant)
                {
                    case SpeciesVariant.Arctic:  // Cold, Topt 20 °C
                        data.optimalTempK = 293.15f; data.arrhenBreadth = 5000f;
                        data.arrhenLower = 15998f; data.arrhenUpper = 43798f;
                        data.lowerBoundK = 292.4f; data.upperBoundK = 293.9f;
                        data.pmax = 0.9843f; data.ctMinC = 0f; data.ctMaxC = 35f;
                        data.variantLabel = "Cold"; data.displayName = "Cold Specialist"; return true;
                    case SpeciesVariant.Common:  // Warm, Topt 22 °C
                        data.optimalTempK = 295.15f; data.arrhenBreadth = 5000f;
                        data.arrhenLower = 16000f; data.arrhenUpper = 43800f;
                        data.lowerBoundK = 294.4f; data.upperBoundK = 295.9f;
                        data.pmax = 0.972f; data.ctMinC = 2f; data.ctMaxC = 37f;
                        data.variantLabel = "Warm"; data.displayName = "Warm Specialist"; return true;
                    case SpeciesVariant.Tropical: // Hot, Topt 24 °C
                        data.optimalTempK = 297.15f; data.arrhenBreadth = 5000f;
                        data.arrhenLower = 16002f; data.arrhenUpper = 43802f;
                        data.lowerBoundK = 296.4f; data.upperBoundK = 297.9f;
                        data.pmax = 0.96f; data.ctMinC = 4f; data.ctMaxC = 39f;
                        data.variantLabel = "Hot"; data.displayName = "Hot Specialist"; return true;
                    default: return false;
                }
            case SpeciesName.Gelgi: // generalist, B = 7000
                switch (data.variant)
                {
                    case SpeciesVariant.Arctic:  // Cold, Topt 20 °C
                        data.optimalTempK = 293.15f; data.arrhenBreadth = 7000f;
                        data.arrhenLower = 4998f; data.arrhenUpper = 31098f;
                        data.lowerBoundK = 292.4f; data.upperBoundK = 293.9f;
                        data.pmax = 0.6616f; data.ctMinC = 0f; data.ctMaxC = 35f;
                        data.variantLabel = "Cold"; data.displayName = "Cold Generalist"; return true;
                    case SpeciesVariant.Common:  // Warm, Topt 22 °C
                        data.optimalTempK = 295.15f; data.arrhenBreadth = 7000f;
                        data.arrhenLower = 5000f; data.arrhenUpper = 31100f;
                        data.lowerBoundK = 294.4f; data.upperBoundK = 295.9f;
                        data.pmax = 0.6547f; data.ctMinC = 2f; data.ctMaxC = 37f;
                        data.variantLabel = "Warm"; data.displayName = "Warm Generalist"; return true;
                    case SpeciesVariant.Tropical: // Hot, Topt 24 °C
                        data.optimalTempK = 297.15f; data.arrhenBreadth = 7000f;
                        data.arrhenLower = 5002f; data.arrhenUpper = 31102f;
                        data.lowerBoundK = 296.4f; data.upperBoundK = 297.9f;
                        data.pmax = 0.6481f; data.ctMinC = 4f; data.ctMaxC = 39f;
                        data.variantLabel = "Hot"; data.displayName = "Hot Generalist"; return true;
                    default: return false;
                }
            default:
                return false;
        }
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

