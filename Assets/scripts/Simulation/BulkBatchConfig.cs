using System.Collections.Generic;

/// <summary>
/// Species configuration parsed from one CSV row (sp1_, sp2_, sp3_, ... prefix).
/// All temperature values are in Celsius (converted to Kelvin when mapping to SpeciesData).
/// </summary>
public class BulkSpeciesConfig
{
    public string Name;
    public string Variant;
    public int Tier;
    public int Pop;
    public float Eating;
    public float ReproMult;
    public float DeathThresh;
    public float DeathRate;
    public float ReproThresh;
    public float NaturalDeathRate;
    public float NaturalDeathVar;
    public float HuntEff;
    public float HuntVar;
    public float OptTempC;
    public float ArrhenBreadth;
    public float ArrhenLower;
    public float ArrhenUpper;
    public float LowerBoundC;
    public float UpperBoundC;
    public float Pmax = 1.0f;
    public float CTminC = -5.0f;
    public float CTmaxC = 50.0f;
    public float TempOffset = 0f;
    // Per-species condition timescale (Batch 2). Negative = inherit the row-global rate.
    public float ConditionDrainRate = -1f;
    public float ConditionRecoveryRate = -1f;
    // Batch 4: per-species temperature multiplier (1 = no change; <1 dampens swing) and Day-0 condition seed.
    public float TempMultiplier = 1.0f;
    public float InitialCondition = 1.0f;
}

/// <summary>
/// All parsed values for one CSV row (one batch configuration).
/// Contains simulation parameters + N species definitions (sp1_, sp2_, sp3_, ...).
/// </summary>
public class BulkBatchConfig
{
    public string BatchName;
    public int Days;
    public int NumScenarios;
    public float BaseTemp;
    public float SeasonalAmp;
    public float ClimateTrend;
    public float VariabilityMag;
    public float WarmingBias;
    public float DailyVarRange;
    public float RandomnessGrowth;
    public bool Autocorrelated;
    public bool InterannualVariation;
    public float TempMin;
    public float TempMax;
    // UseCarryingCap field removed in v11.1 — carrying capacity is always on.
    public float CarryingCapT1;
    public float ConditionDrainRate = 0.15f;
    public float ConditionRecoveryRate = 0.10f;
    // Batch 4: AR(1) autocorrelation coefficient for daily variation (0 = white noise, 0.7 = default smoothing).
    public float AutocorrelationCoefficient = 0.7f;
    // Batch 3: optional path to a "Day,Temperature_C" CSV. Empty => parametric model.
    public string TemperatureTimeseriesFile = "";
    public List<BulkSpeciesConfig> Species = new List<BulkSpeciesConfig>();
}
