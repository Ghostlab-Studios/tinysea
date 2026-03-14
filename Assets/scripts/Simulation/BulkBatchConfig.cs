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
    public float MinDeaths;
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
    public bool UseCarryingCap;
    public float CarryingCapT1;
    public List<BulkSpeciesConfig> Species = new List<BulkSpeciesConfig>();
}
