using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

/// <summary>
/// Per-species data recorded each biology step.
/// One instance per species per day, stored in StepRecord.SpeciesData
/// keyed by SimSpecies.FullName (e.g., "Hexapod_Common", "Coral_Custom").
///
/// Designed to coexist with the tier-level fields on StepRecord — the per-species
/// values are additive (new CSV columns) and must sum to the tier-level columns
/// (used as a tier-rollup invariant in RecordStep).
///
/// struct (value type) chosen over class to avoid per-day heap allocation overhead
/// at ~365 days × N species. ~80 bytes × N species per day is negligible.
/// </summary>
public struct PerSpeciesStepData
{
    // Population & physiology (carried across days regardless of biologyRan)
    public long  Population;          // Rounded
    public float Condition;           // [0,1]
    public float ThermalPerf;         // RawThermalPerformance (no Pmax)
    public float FinalPerf;           // FinalPerformance — logging only (ThermalPerf × FedRate × Pmax via ThermalPerformance)
    public float FedRate;             // T1: density × HE; T2: huntingSuccess × scarcityFactor
    public float HuntingEff;          // T2 only (CurrentHuntingSuccess); 0 for T1

    // Daily events (zero on non-biology days)
    public long  Births;
    public long  TempDeaths;
    public long  ConditionDeaths;
    public long  NaturalDeaths;
    public long  Eaten;               // T1 only (predation-driven); 0 for T2

    // Per-capita rate (Births / max(StartOfDayPop, 1))
    public float BirthRate;

    // Reproduction internals
    public float ReproScale;

    // Accumulator residuals (carried between days; current values after this step)
    public float BirthAccum;
    public float NaturalDeathAccum;
    public float ConditionDeathAccum;
    public float PredationAccum;      // T1 only (T2 has none)
}

/// <summary>
/// Data recorded each biology step.
/// 
/// CSV COLUMNS:
/// - Day, Year, Temperature, BiologyCycle
/// - StartPop, EndPop: Total population (T1+T2)
/// - Tier1Pop, Tier2Pop, Tier1Arctic...Tier2Tropical: Populations by tier/variant
/// - EatenT1: Prey eaten (Tier 1 deaths from predation)
/// - TempDeathsT1, TempDeathsT2: Thermal deaths (instant at lethal limits)
/// - ConditionDeathsT1, ConditionDeathsT2: Condition deaths (chronic stress)
/// - NaturalDeathsT1, NaturalDeathsT2: Natural mortality deaths (flat rate)
/// - TotalDeaths: All deaths combined
/// - BirthsT1, BirthsT2: New offspring
/// - FedRateT2, AvgHuntingEff: Feeding metrics
/// - AvgConditionT1, AvgConditionT2: Population-weighted average Condition per tier
/// - BirthAccumT1, BirthAccumT2: Birth accumulator totals
/// - NaturalDeathAccumT1, NaturalDeathAccumT2: Natural death accumulator totals
/// - ConditionDeathAccumT1, ConditionDeathAccumT2: Condition death accumulator totals
/// - PredationAccumT1: Predation accumulator total for Tier 1
/// - ReproScaleT1, ReproScaleT2: Condition-based reproduction scale factor per tier [0-1]
///
/// NOTE: Population fields use 'long' to prevent integer overflow with large populations.
/// </summary>
public class StepRecord
{
    // Time
    public int Day;
    public int Year;

    // Environment
    public float Temperature;

    // Biology tracking
    public int BiologyCycle;

    // Start/End population (TOTAL = T1 + T2) - using long to prevent overflow
    public long StartPop;
    public long EndPop;

    // Population by tier (using long to prevent overflow)
    public long Tier1Pop;
    public long Tier2Pop;
    public long Tier1Arctic;
    public long Tier1Common;
    public long Tier1Tropical;
    public long Tier2Arctic;
    public long Tier2Common;
    public long Tier2Tropical;
    public long Tier1Custom;
    public long Tier2Custom;

    // Death tracking (using long to prevent overflow)
    public long EatenT1;           // Prey eaten = T1 deaths from predation
    public long TempDeathsT1;      // Temperature deaths T1 (lethal limits)
    public long TempDeathsT2;      // Temperature deaths T2 (lethal limits)
    public long ConditionDeathsT1; // Condition deaths T1 (chronic stress)
    public long ConditionDeathsT2; // Condition deaths T2 (chronic stress)
    public long NaturalDeathsT1;   // Natural deaths T1
    public long NaturalDeathsT2;   // Natural deaths T2
    public long TotalDeaths;       // ALL deaths combined

    // Birth tracking (using long to prevent overflow)
    public long BirthsT1;
    public long BirthsT2;

    // Feeding tracking
    public float FedRateT2;
    public float AvgHuntingEff;

    // v10: Tier 1 feeding from shared resource pool
    public float FedRateT1;        // Population-weighted average across live Tier 1 species
    public float FoodDensityT1;    // Daily food density (1 - tier1Pop/cap), or 1.0 if cap disabled

    // Condition tracking
    public float AvgConditionT1;
    public float AvgConditionT2;

    // Accumulator tracking (float values for transparency)
    public float BirthAccumT1;
    public float BirthAccumT2;
    public float NaturalDeathAccumT1;
    public float NaturalDeathAccumT2;
    public float ConditionDeathAccumT1;
    public float ConditionDeathAccumT2;
    public float PredationAccumT1;

    // Reproduction scale tracking (graduated reproduction)
    public float ReproScaleT1;
    public float ReproScaleT2;

    // ==================== PER-SPECIES DATA ====================
    // Keyed by SimSpecies.FullName (e.g., "Hexapod_Common", "Coral_Custom").
    // Tier-level fields above are sums of these per-species values.
    public Dictionary<string, PerSpeciesStepData> SpeciesData = new Dictionary<string, PerSpeciesStepData>();

    /// <summary>
    /// Build CSV row. Tier-level columns first (byte-identical to v11.1 layout),
    /// then per-species columns appended in the same order as orderedSpecies (which
    /// must be identical to the order used in CsvHeader for the same scenario).
    ///
    /// orderedSpecies may be null/empty — in that case only tier-level columns are emitted
    /// (back-compat path; not used by SimulationRunner.ToCsvInternal anymore).
    /// </summary>
    public string ToCsvLine(IList<SimSpecies> orderedSpecies)
    {
        var sb = new StringBuilder();
        sb.Append($"{Day},{Year},{Temperature:F2},{BiologyCycle},");
        sb.Append($"{StartPop},{EndPop},");
        sb.Append($"{Tier1Pop},{Tier2Pop},");
        sb.Append($"{Tier1Arctic},{Tier1Common},{Tier1Tropical},{Tier1Custom},");
        sb.Append($"{Tier2Arctic},{Tier2Common},{Tier2Tropical},{Tier2Custom},");
        sb.Append($"{EatenT1},{TempDeathsT1},{TempDeathsT2},");
        sb.Append($"{ConditionDeathsT1},{ConditionDeathsT2},");
        sb.Append($"{NaturalDeathsT1},{NaturalDeathsT2},");
        sb.Append($"{TotalDeaths},");
        sb.Append($"{BirthsT1},{BirthsT2},");
        sb.Append($"{FedRateT2:F3},{AvgHuntingEff:F3},");
        sb.Append($"{FedRateT1:F3},{FoodDensityT1:F3},");
        sb.Append($"{AvgConditionT1:F3},{AvgConditionT2:F3},");
        sb.Append($"{BirthAccumT1:F3},{BirthAccumT2:F3},");
        sb.Append($"{NaturalDeathAccumT1:F3},{NaturalDeathAccumT2:F3},");
        sb.Append($"{ConditionDeathAccumT1:F3},{ConditionDeathAccumT2:F3},");
        sb.Append($"{PredationAccumT1:F3},");
        sb.Append($"{ReproScaleT1:F3},{ReproScaleT2:F3}");

        // Per-species columns (v12). Each species contributes 17 columns.
        if (orderedSpecies != null)
        {
            foreach (var sp in orderedSpecies)
            {
                if (!SpeciesData.TryGetValue(sp.FullName, out var d))
                    d = default;
                sb.Append($",{d.Population},{d.Condition:F3},{d.ThermalPerf:F3},{d.FinalPerf:F3},");
                sb.Append($"{d.FedRate:F3},{d.HuntingEff:F3},");
                sb.Append($"{d.Births},{d.TempDeaths},{d.ConditionDeaths},{d.NaturalDeaths},{d.Eaten},");
                sb.Append($"{d.BirthRate:F4},{d.ReproScale:F3},");
                sb.Append($"{d.BirthAccum:F3},{d.NaturalDeathAccum:F3},{d.ConditionDeathAccum:F3},{d.PredationAccum:F3}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Build CSV header. Tier-level columns first (byte-identical to v11.1 layout),
    /// then per-species columns: each species in orderedSpecies contributes 17 columns
    /// named "{SanitizedFullName}_{Field}", where Field is one of:
    ///   Pop, Cond, ThermalPerf, FinalPerf, FedRate, HuntingEff,
    ///   Births, TempDeaths, CondDeaths, NatDeaths, Eaten,
    ///   BirthRate, ReproScale,
    ///   BirthAccum, NatDeathAccum, CondDeathAccum, PredAccum.
    ///
    /// orderedSpecies must be a stable order — typically sort by Tier asc, FullName asc.
    /// The SAME ordered list must be passed to CsvHeader and to every ToCsvLine call
    /// in the same scenario; otherwise the row data will misalign with the header.
    /// </summary>
    public static string CsvHeader(IList<SimSpecies> orderedSpecies)
    {
        var sb = new StringBuilder();
        sb.Append("Day,Year,Temperature,BiologyCycle,");
        sb.Append("StartPop,EndPop,");
        sb.Append("Tier1Pop,Tier2Pop,");
        sb.Append("Tier1Arctic,Tier1Common,Tier1Tropical,Tier1Custom,");
        sb.Append("Tier2Arctic,Tier2Common,Tier2Tropical,Tier2Custom,");
        sb.Append("EatenT1,TempDeathsT1,TempDeathsT2,");
        sb.Append("ConditionDeathsT1,ConditionDeathsT2,");
        sb.Append("NaturalDeathsT1,NaturalDeathsT2,");
        sb.Append("TotalDeaths,");
        sb.Append("BirthsT1,BirthsT2,");
        sb.Append("FedRateT2,AvgHuntingEff,");
        sb.Append("FedRateT1,FoodDensityT1,");
        sb.Append("AvgConditionT1,AvgConditionT2,");
        sb.Append("BirthAccumT1,BirthAccumT2,");
        sb.Append("NaturalDeathAccumT1,NaturalDeathAccumT2,");
        sb.Append("ConditionDeathAccumT1,ConditionDeathAccumT2,");
        sb.Append("PredationAccumT1,");
        sb.Append("ReproScaleT1,ReproScaleT2");

        if (orderedSpecies != null)
        {
            // Track sanitized names to detect collisions (e.g. two custom species with
            // colliding names after sanitization). Append _2, _3, ... on collision.
            var seen = new HashSet<string>();
            foreach (var sp in orderedSpecies)
            {
                string baseName = SanitizeColumnName(sp.FullName);
                string c = baseName;
                int suffix = 2;
                while (seen.Contains(c))
                {
                    c = $"{baseName}_{suffix}";
                    suffix++;
                }
                seen.Add(c);

                sb.Append($",{c}_Pop,{c}_Cond,{c}_ThermalPerf,{c}_FinalPerf,");
                sb.Append($"{c}_FedRate,{c}_HuntingEff,");
                sb.Append($"{c}_Births,{c}_TempDeaths,{c}_CondDeaths,{c}_NatDeaths,{c}_Eaten,");
                sb.Append($"{c}_BirthRate,{c}_ReproScale,");
                sb.Append($"{c}_BirthAccum,{c}_NatDeathAccum,{c}_CondDeathAccum,{c}_PredAccum");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Sanitize a species FullName for safe use as a CSV column name.
    /// ASCII-only — Unicode letters trip up downstream R/pandas pipelines.
    /// Replaces any character outside [A-Za-z0-9_] with '_'. If the result starts
    /// with a digit, prepends '_' (R/pandas handle digit-leading names but some
    /// SQL exports don't).
    /// </summary>
    public static string SanitizeColumnName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "Unknown";
        var sb = new StringBuilder(fullName.Length);
        foreach (char ch in fullName)
        {
            bool ascii = (ch >= 'a' && ch <= 'z') ||
                         (ch >= 'A' && ch <= 'Z') ||
                         (ch >= '0' && ch <= '9') ||
                         ch == '_';
            sb.Append(ascii ? ch : '_');
        }
        if (sb.Length > 0 && char.IsDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }
}

/// <summary>
/// Main simulation runner.
/// 
/// v6 CHANGES:
/// - Now uses TotalDays instead of MaxYears
/// - Direct day control for flexible scenario lengths
/// </summary>
public class SimulationRunner
{
    // Components
    public TemperatureCalculator TempCalc { get; private set; }
    public EcosystemSimulator Ecosystem { get; private set; }

    // Settings
    public int TotalDays = 365;  // Changed from MaxYears
    public int BiologyStep = 1;

    // Species list reference
    public RunSpeciesList RunSpecies { get; set; }

    // Results
    private List<StepRecord> _records = new List<StepRecord>();
    private int _biologyCycleCounter = 0;

    // Crash tracking
    public bool HasCrashed { get; private set; } = false;
    public int CrashDay { get; private set; } = -1;
    public int CrashTier { get; private set; } = -1;

    // Seed tracking (for results)
    public int UsedSeed { get; private set; } = -1;

    /// <summary>
    /// Verbose simulation log. Stripped everywhere by default.
    /// To enable: add TINYSEA_SIM_LOG to Scripting Define Symbols.
    /// </summary>
    [System.Diagnostics.Conditional("TINYSEA_SIM_LOG")]
    private static void SimLog(string message) => Debug.Log(message);

    public SimulationRunner(int seed = -1)
    {
        UsedSeed = seed;
        TempCalc = new TemperatureCalculator(seed);
        Ecosystem = new EcosystemSimulator(seed);
    }

    /// <summary>
    /// Run the simulation for TotalDays.
    /// </summary>
    public void Run()
    {
        _records.Clear();
        _biologyCycleCounter = 0;
        HasCrashed = false;
        CrashDay = -1;
        CrashTier = -1;

        Ecosystem.BiologyStep = BiologyStep;

        // Initialize from RunSpeciesList (primary) or fall back to defaults
        if (RunSpecies != null && RunSpecies.speciesList != null && RunSpecies.speciesList.Count > 0)
        {
            Ecosystem.InitializeFromRunSpeciesList(RunSpecies);
        }
        else
        {
            Debug.LogWarning("No RunSpeciesList provided or empty, using hardcoded defaults!");
            Ecosystem.InitializeDefaultSpecies();
        }

        SimLog($"=== Starting Simulation: {TotalDays} days, BiologyStep={BiologyStep} ===");

        for (int dayIndex = 0; dayIndex < TotalDays; dayIndex++)
        {
            int displayDay = dayIndex + 1;
            int year = (dayIndex / TemperatureCalculator.DAYS_PER_YEAR) + 1;

            float temp = TempCalc.GetTemperature(dayIndex);

            bool runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0);

            if (runBiology)
            {
                _biologyCycleCounter++;
                Ecosystem.ProcessBiologyStep(temp);
            }

            RecordStep(displayDay, year, temp, runBiology);

            if (runBiology && Ecosystem.HasCrashed())
            {
                HasCrashed = true;
                CrashDay = displayDay;
                CrashTier = Ecosystem.GetCrashedTier();
                Debug.LogWarning($"=== ECOSYSTEM CRASH on Day {displayDay} (Year {year}) - All populations extinct ===");
                break;
            }
        }

        var lastRecord = _records.Count > 0 ? _records[_records.Count - 1] : null;
        SimLog($"=== Simulation Complete ===");
        SimLog($"Days simulated: {_records.Count}");
        SimLog($"Biology cycles: {_biologyCycleCounter}");
        SimLog($"Crashed: {HasCrashed} (Day: {CrashDay}, Tier: {CrashTier})");
        if (lastRecord != null)
        {
            SimLog($"Final populations: Tier1={lastRecord.Tier1Pop}, Tier2={lastRecord.Tier2Pop}");
        }
    }

    /// <summary>
    /// Record current state for a day.
    /// </summary>
    private void RecordStep(int day, int year, float temperature, bool biologyRan)
    {
        // Get death components
        float eatenT1 = biologyRan ? Ecosystem.LastEatenT1 : 0f;
        float tempDeathsT1 = biologyRan ? Ecosystem.LastTempDeathsT1 : 0f;
        float tempDeathsT2 = biologyRan ? Ecosystem.LastTempDeathsT2 : 0f;
        float conditionDeathsT1 = biologyRan ? Ecosystem.LastConditionDeathsT1 : 0f;
        float conditionDeathsT2 = biologyRan ? Ecosystem.LastConditionDeathsT2 : 0f;
        float naturalDeathsT1 = biologyRan ? Ecosystem.LastNaturalDeathsT1 : 0f;
        float naturalDeathsT2 = biologyRan ? Ecosystem.LastNaturalDeathsT2 : 0f;

        // TotalDeaths = all death sources combined
        float totalDeaths = eatenT1 + tempDeathsT1 + tempDeathsT2 +
                           conditionDeathsT1 + conditionDeathsT2 +
                           naturalDeathsT1 + naturalDeathsT2;

        var record = new StepRecord
        {
            Day = day,
            Year = year,
            Temperature = temperature,
            BiologyCycle = biologyRan ? _biologyCycleCounter : 0,

            // Start/End population (T1 + T2 combined) - using long to prevent overflow
            StartPop = biologyRan ? (long)Math.Round(Ecosystem.StartPopT1 + Ecosystem.StartPopT2) : 0,
            EndPop = (long)Math.Round(Ecosystem.GetTier1Population() + Ecosystem.GetTier2Population()),

            // Population by tier - using long to prevent overflow
            Tier1Pop = (long)Math.Round(Ecosystem.GetTier1Population()),
            Tier2Pop = (long)Math.Round(Ecosystem.GetTier2Population()),
            Tier1Arctic = (long)Math.Round(Ecosystem.GetVariantPopulation(1, ThermalVariant.Arctic)),
            Tier1Common = (long)Math.Round(Ecosystem.GetVariantPopulation(1, ThermalVariant.Common)),
            Tier1Tropical = (long)Math.Round(Ecosystem.GetVariantPopulation(1, ThermalVariant.Tropical)),
            Tier2Arctic = (long)Math.Round(Ecosystem.GetVariantPopulation(2, ThermalVariant.Arctic)),
            Tier2Common = (long)Math.Round(Ecosystem.GetVariantPopulation(2, ThermalVariant.Common)),
            Tier2Tropical = (long)Math.Round(Ecosystem.GetVariantPopulation(2, ThermalVariant.Tropical)),
            Tier1Custom = (long)Math.Round(Ecosystem.GetVariantPopulation(1, ThermalVariant.Custom)),
            Tier2Custom = (long)Math.Round(Ecosystem.GetVariantPopulation(2, ThermalVariant.Custom)),

            // Death tracking - using long to prevent overflow
            EatenT1 = (long)Math.Round(eatenT1),
            TempDeathsT1 = (long)Math.Round(tempDeathsT1),
            TempDeathsT2 = (long)Math.Round(tempDeathsT2),
            ConditionDeathsT1 = (long)Math.Round(conditionDeathsT1),
            ConditionDeathsT2 = (long)Math.Round(conditionDeathsT2),
            NaturalDeathsT1 = (long)Math.Round(naturalDeathsT1),
            NaturalDeathsT2 = (long)Math.Round(naturalDeathsT2),
            TotalDeaths = (long)Math.Round(totalDeaths),

            // Birth tracking - using long to prevent overflow
            BirthsT1 = biologyRan ? (long)Math.Round(Ecosystem.LastBirthsT1) : 0,
            BirthsT2 = biologyRan ? (long)Math.Round(Ecosystem.LastBirthsT2) : 0,

            // Reproduction scale tracking (graduated reproduction)
            ReproScaleT1 = biologyRan ? Ecosystem.LastReproScaleT1 : 0f,
            ReproScaleT2 = biologyRan ? Ecosystem.LastReproScaleT2 : 0f,

            // Feeding tracking
            FedRateT2 = biologyRan ? Ecosystem.LastFedRateT2 : 0f,
            AvgHuntingEff = biologyRan ? Ecosystem.LastAvgHuntingEfficiency : 0f,
            // v10: Tier 1 feeding from shared resource pool. On non-biology days,
            // we still want valid values — use the last computed values rather than
            // zeroing, since food density itself doesn't change in skipped-biology days.
            FedRateT1 = Ecosystem.LastFedRateT1,
            FoodDensityT1 = Ecosystem.LastFoodDensityT1,

            // Condition tracking
            AvgConditionT1 = Ecosystem.AvgConditionT1,
            AvgConditionT2 = Ecosystem.AvgConditionT2,

            // Accumulator tracking
            BirthAccumT1 = Ecosystem.BirthAccumT1,
            BirthAccumT2 = Ecosystem.BirthAccumT2,
            NaturalDeathAccumT1 = Ecosystem.NaturalDeathAccumT1,
            NaturalDeathAccumT2 = Ecosystem.NaturalDeathAccumT2,
            ConditionDeathAccumT1 = Ecosystem.ConditionDeathAccumT1,
            ConditionDeathAccumT2 = Ecosystem.ConditionDeathAccumT2,
            PredationAccumT1 = Ecosystem.PredationAccumT1
        };

        // v12: Populate per-species data. Each species gets a PerSpeciesStepData
        // entry keyed by FullName. Tier-level fields above are sums of these values.
        foreach (var sp in Ecosystem.Species)
        {
            string fn = sp.FullName;
            long startPop = GetOrZeroLong(Ecosystem.StartPopBySpecies, fn);
            // Fall back to current rounded population if reset block didn't run
            // (defensive — should not happen under normal flow).
            if (startPop == 0L && sp.Population > 0f)
                startPop = (long)Math.Round(sp.Population);

            long births = biologyRan ? GetOrZeroLong(Ecosystem.LastBirthsBySpecies, fn) : 0L;

            record.SpeciesData[fn] = new PerSpeciesStepData
            {
                Population          = (long)Math.Round(sp.Population),
                Condition           = sp.Condition,
                ThermalPerf         = sp.RawThermalPerformance,
                FinalPerf           = sp.FinalPerformance,
                FedRate             = GetOrFallbackFloat(Ecosystem.LastFedRateBySpecies, fn, sp.FedRate),
                HuntingEff          = sp.Tier == 2 ? sp.CurrentHuntingSuccess : 0f,
                Births              = births,
                TempDeaths          = biologyRan ? GetOrZeroLong(Ecosystem.LastTempDeathsBySpecies, fn) : 0L,
                ConditionDeaths     = biologyRan ? GetOrZeroLong(Ecosystem.LastConditionDeathsBySpecies, fn) : 0L,
                NaturalDeaths       = biologyRan ? GetOrZeroLong(Ecosystem.LastNaturalDeathsBySpecies, fn) : 0L,
                Eaten               = biologyRan ? GetOrZeroLong(Ecosystem.LastEatenBySpecies, fn) : 0L,
                BirthRate           = startPop > 0L ? (float)births / startPop : 0f,
                ReproScale          = biologyRan ? GetOrFallbackFloat(Ecosystem.LastReproScaleBySpecies, fn, 0f) : 0f,
                BirthAccum          = Ecosystem.GetBirthAccum(fn),
                NaturalDeathAccum   = Ecosystem.GetNaturalDeathAccum(fn),
                ConditionDeathAccum = Ecosystem.GetConditionDeathAccum(fn),
                PredationAccum      = Ecosystem.GetPredationAccum(fn)
            };
        }

        _records.Add(record);
    }

    // ==================== Per-species dict accessor helpers (v12) ====================
    // GetValueOrDefault is in netstandard 2.1 but not always exposed; use TryGetValue.
    private static long GetOrZeroLong(IDictionary<string, long> d, string key)
        => d != null && d.TryGetValue(key, out var v) ? v : 0L;

    private static float GetOrFallbackFloat(IDictionary<string, float> d, string key, float fallback)
        => d != null && d.TryGetValue(key, out var v) ? v : fallback;

    public List<StepRecord> GetRecords() => new List<StepRecord>(_records);

    private struct PopulationStats
    {
        public Dictionary<string, double> Mean;
        public Dictionary<string, long> Max;
        public Dictionary<string, long> Min;
        public Dictionary<string, double> StdDev;
        public Dictionary<string, int> ExtinctionDay;
    }

    private static long GetPopColumn(StepRecord r, string column)
    {
        switch (column)
        {
            case "Tier1Pop":      return r.Tier1Pop;
            case "Tier2Pop":      return r.Tier2Pop;
            case "Tier1Arctic":   return r.Tier1Arctic;
            case "Tier1Common":   return r.Tier1Common;
            case "Tier1Tropical": return r.Tier1Tropical;
            case "Tier2Arctic":   return r.Tier2Arctic;
            case "Tier2Common":   return r.Tier2Common;
            case "Tier2Tropical": return r.Tier2Tropical;
            case "Tier1Custom":   return r.Tier1Custom;
            case "Tier2Custom":   return r.Tier2Custom;
            default:              return 0;
        }
    }

    private PopulationStats ComputePopulationStats()
    {
        var stats = new PopulationStats
        {
            Mean = new Dictionary<string, double>(),
            Max = new Dictionary<string, long>(),
            Min = new Dictionary<string, long>(),
            StdDev = new Dictionary<string, double>(),
            ExtinctionDay = new Dictionary<string, int>()
        };

        if (_records.Count == 0) return stats;

        foreach (var col in ScenarioResult.PopColumns)
        {
            long max = long.MinValue;
            long min = long.MaxValue;
            double sum = 0;

            foreach (var r in _records)
            {
                long val = GetPopColumn(r, col);
                if (val > max) max = val;
                if (val < min) min = val;
                sum += val;
            }

            double mean = sum / _records.Count;

            double varianceSum = 0;
            foreach (var r in _records)
            {
                double diff = GetPopColumn(r, col) - mean;
                varianceSum += diff * diff;
            }
            double stddev = Math.Sqrt(varianceSum / _records.Count);

            stats.Mean[col] = mean;
            stats.Max[col] = max;
            stats.Min[col] = min;
            stats.StdDev[col] = stddev;
        }

        foreach (var variant in ScenarioResult.VariantColumns)
        {
            int extinctionDay = -1;
            bool wasAlive = false;
            foreach (var r in _records)
            {
                long pop = GetPopColumn(r, variant);
                if (pop > 0) wasAlive = true;
                if (wasAlive && pop == 0)
                {
                    extinctionDay = r.Day;
                    break;
                }
            }
            stats.ExtinctionDay[variant] = extinctionDay;
        }

        // v12.2: Also compute per-species Mean/Max/Min/StdDev across days, keyed
        // by SimSpecies.FullName. Same calculation as the tier-variant block above
        // but pulls per-species population from record.SpeciesData. Lets the
        // aggregate writer's "SUMMARY STATISTICS - PER SPECIES" section consume
        // this from the same dicts.
        if (Ecosystem != null && Ecosystem.Species != null)
        {
            foreach (var sp in Ecosystem.Species)
            {
                string fn = sp.FullName;
                long max = long.MinValue;
                long min = long.MaxValue;
                double sum = 0;
                foreach (var r in _records)
                {
                    long val = r.SpeciesData.TryGetValue(fn, out var d) ? d.Population : 0L;
                    if (val > max) max = val;
                    if (val < min) min = val;
                    sum += val;
                }
                double mean = sum / _records.Count;
                double varianceSum = 0;
                foreach (var r in _records)
                {
                    long val = r.SpeciesData.TryGetValue(fn, out var d) ? d.Population : 0L;
                    double diff = val - mean;
                    varianceSum += diff * diff;
                }
                double stddev = Math.Sqrt(varianceSum / _records.Count);

                stats.Mean[fn] = mean;
                stats.Max[fn] = max == long.MinValue ? 0L : max;
                stats.Min[fn] = min == long.MaxValue ? 0L : min;
                stats.StdDev[fn] = stddev;
            }
        }

        return stats;
    }

    public string ToCsv(int scenarioIndex = 0, int numberOfScenarios = 1)
    {
        return ToCsvInternal(scenarioIndex, numberOfScenarios, null);
    }

    private string ToCsvInternal(int scenarioIndex, int numberOfScenarios, PopulationStats? populationStats)
    {
        var sb = new StringBuilder();

        // Embed configuration as comment lines (# is default comment char in R's read.csv)
        // Model version line first so downstream tools know which simulator produced this file.
        sb.AppendLine($"#config:model_version,v12-per-species-tracking");
        sb.AppendLine($"#config:days_per_scenario,{TotalDays}");
        sb.AppendLine($"#config:number_of_scenarios,{numberOfScenarios}");
        sb.AppendLine($"#config:scenario_index,{scenarioIndex}");
        sb.AppendLine($"#config:random_seed,{UsedSeed}");
        sb.AppendLine($"#config:biology_step,{BiologyStep}");
        sb.AppendLine($"#config:base_temperature,{TempCalc.BaseTemperature}");
        sb.AppendLine($"#config:seasonal_amplitude,{TempCalc.SeasonalAmplitude}");
        sb.AppendLine($"#config:climate_trend_per_year,{TempCalc.ClimateTrendPerYear}");
        sb.AppendLine($"#config:variability_magnitude,{TempCalc.VariabilityMagnitude}");
        sb.AppendLine($"#config:warming_bias,{TempCalc.WarmingBias}");
        sb.AppendLine($"#config:daily_variation_range,{TempCalc.BaseRandomness}");
        sb.AppendLine($"#config:randomness_growth_rate,{TempCalc.RandomnessGrowthRate}");
        sb.AppendLine($"#config:autocorrelated,{TempCalc.UseAutocorrelation.ToString().ToLower()}");
        sb.AppendLine($"#config:temperature_bounds_min,{TempCalc.MinTemp}");
        sb.AppendLine($"#config:temperature_bounds_max,{TempCalc.MaxTemp}");
        sb.AppendLine($"#config:carrying_capacity_tier1,{Ecosystem.CarryingCapacityPerTier}");
        sb.AppendLine($"#config:condition_drain_rate,{Ecosystem.ConditionDrainRate}");
        sb.AppendLine($"#config:condition_recovery_rate,{Ecosystem.ConditionRecoveryRate}");

        sb.AppendLine("#");
        if (RunSpecies != null && RunSpecies.speciesList != null && RunSpecies.speciesList.Count > 0)
        {
            sb.AppendLine("#species:Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier," +
                "DeathThreshold,DeathRate,ReproThreshold," +
                "NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance," +
                "OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper," +
                "LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC," +
                "Pmax,CTminC,CTmaxC,TemperatureDebuff");
            foreach (var sp in RunSpecies.speciesList)
            {
                string spName = !string.IsNullOrEmpty(sp.displayName) ? sp.displayName : sp.speciesName.ToString();
                sb.AppendLine($"#species:{spName},{sp.variant},{sp.tier},{sp.count}," +
                    $"{sp.eatingAmount},{sp.reproductionMultiplier}," +
                    $"{sp.deathThreshold},{sp.deathRate},{sp.reproThreshold}," +
                    $"{sp.naturalDeathRate},{sp.naturalDeathVariance}," +
                    $"{sp.huntingEfficiency},{sp.huntingVariance}," +
                    $"{sp.optimalTempK},{sp.optimalTempK - 273.15f:F2}," +
                    $"{sp.arrhenBreadth},{sp.arrhenLower},{sp.arrhenUpper}," +
                    $"{sp.lowerBoundK},{sp.lowerBoundK - 273.15f:F2}," +
                    $"{sp.upperBoundK},{sp.upperBoundK - 273.15f:F2}," +
                    $"{sp.pmax:F2},{sp.ctMinC:F2},{sp.ctMaxC:F2},{sp.TemperatureDebuff:F2}");
            }
        }
        sb.AppendLine("#");

        // v12: Build a stable species order once. The same list is passed to header
        // and to every row so columns line up. Sort by Tier asc, FullName asc.
        var orderedSpecies = (Ecosystem != null && Ecosystem.Species != null)
            ? Ecosystem.Species.OrderBy(s => s.Tier).ThenBy(s => s.FullName).ToList()
            : new List<SimSpecies>();

        sb.AppendLine(StepRecord.CsvHeader(orderedSpecies));
        foreach (var record in _records)
        {
            sb.AppendLine(record.ToCsvLine(orderedSpecies));
        }

        // Append summary statistics and extinction timing
        if (_records.Count > 0)
        {
            var stats = populationStats ?? ComputePopulationStats();

            // v12.1: Per-species summary statistics block.
            // Columns: Tier1Pop, Tier2Pop (tier totals), then one column per species
            // by FullName, in the same (Tier asc, FullName asc) order as the daily
            // header. Variant rollup columns (Tier1Arctic, Tier1Common, etc.) are no
            // longer emitted here — species are tracked individually so the variant
            // intermediate level is now redundant. Tier totals are kept because they
            // are real ecosystem-level aggregates (not redundant with per-species).
            var summaryCols = new List<string> { "Tier1Pop", "Tier2Pop" };
            foreach (var sp in orderedSpecies)
                summaryCols.Add(StepRecord.SanitizeColumnName(sp.FullName));

            // Pre-compute per-species Mean/Max/Min/StdDev/ExtinctionDay in one pass.
            int dayCount = _records.Count;
            var spMean = new Dictionary<string, double>();
            var spMax = new Dictionary<string, long>();
            var spMin = new Dictionary<string, long>();
            var spStdDev = new Dictionary<string, double>();
            var spExtinctionDay = new Dictionary<string, int>();
            foreach (var sp in orderedSpecies)
            {
                string fn = sp.FullName;
                long mn = long.MaxValue;
                long mx = long.MinValue;
                double sum = 0.0;
                double sqSum = 0.0;
                int extinctionDay = -1;
                bool wasAlive = false;
                foreach (var rec in _records)
                {
                    long pop = rec.SpeciesData.TryGetValue(fn, out var d) ? d.Population : 0L;
                    sum += pop;
                    sqSum += (double)pop * pop;
                    if (pop < mn) mn = pop;
                    if (pop > mx) mx = pop;
                    if (pop > 0L) wasAlive = true;
                    if (extinctionDay < 0 && wasAlive && pop == 0L) extinctionDay = rec.Day;
                }
                double mean = dayCount > 0 ? sum / dayCount : 0.0;
                double variance = dayCount > 0 ? (sqSum / dayCount) - (mean * mean) : 0.0;
                if (variance < 0.0) variance = 0.0;
                spMean[fn] = mean;
                spMax[fn] = mx == long.MinValue ? 0L : mx;
                spMin[fn] = mn == long.MaxValue ? 0L : mn;
                spStdDev[fn] = Math.Sqrt(variance);
                spExtinctionDay[fn] = extinctionDay;
            }

            // Summary statistics block — tier totals + per-species columns.
            // THREE header rows: species name (Statistic), variant (Variant), tier (Tier).
            // For tier-total columns: Variant=All, Tier=actual tier number (1 or 2).
            sb.AppendLine("#");
            sb.AppendLine("#summary:Statistic," + string.Join(",", summaryCols));

            sb.Append("#summary:Variant");
            sb.Append(",All,All");
            foreach (var sp in orderedSpecies)
                sb.Append($",{sp.Variant}");
            sb.AppendLine();

            sb.Append("#summary:Tier");
            sb.Append(",1,2");
            foreach (var sp in orderedSpecies)
                sb.Append($",{sp.Tier}");
            sb.AppendLine();

            sb.Append("#summary:Mean");
            sb.Append($",{stats.Mean["Tier1Pop"]:F1},{stats.Mean["Tier2Pop"]:F1}");
            foreach (var sp in orderedSpecies) sb.Append($",{spMean[sp.FullName]:F1}");
            sb.AppendLine();

            sb.Append("#summary:Max");
            sb.Append($",{stats.Max["Tier1Pop"]},{stats.Max["Tier2Pop"]}");
            foreach (var sp in orderedSpecies) sb.Append($",{spMax[sp.FullName]}");
            sb.AppendLine();

            sb.Append("#summary:Min");
            sb.Append($",{stats.Min["Tier1Pop"]},{stats.Min["Tier2Pop"]}");
            foreach (var sp in orderedSpecies) sb.Append($",{spMin[sp.FullName]}");
            sb.AppendLine();

            sb.Append("#summary:StdDev");
            sb.Append($",{stats.StdDev["Tier1Pop"]:F1},{stats.StdDev["Tier2Pop"]:F1}");
            foreach (var sp in orderedSpecies) sb.Append($",{spStdDev[sp.FullName]:F1}");
            sb.AppendLine();

            // Extinction timing block — Species, Variant, Tier, DayReachedZero columns
            sb.AppendLine("#");
            sb.AppendLine("#extinction:Species,Variant,Tier,DayReachedZero");
            foreach (var sp in orderedSpecies)
            {
                string col = StepRecord.SanitizeColumnName(sp.FullName);
                sb.AppendLine($"#extinction:{col},{sp.Variant},{sp.Tier},{spExtinctionDay[sp.FullName]}");
            }
            sb.AppendLine("#");
        }

        return sb.ToString();
    }

    public string SaveToFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string crashSuffix = HasCrashed ? $"_crash_day{CrashDay}" : "";
        string filename = $"tinysea_v6_{timestamp}{crashSuffix}.csv";
        string path = Path.Combine(directory, filename);

        File.WriteAllText(path, ToCsv());
        return path;
    }

    /// <summary>
    /// Get summary statistics for this run
    /// </summary>
    public SimulationSummary GetSummary()
    {
        if (_records.Count == 0) return null;

        var summary = new SimulationSummary
        {
            TotalDays = _records.Count,
            TotalBiologyCycles = _biologyCycleCounter,
            Crashed = HasCrashed,
            CrashDay = CrashDay,
            CrashTier = CrashTier
        };

        var lastRecord = _records[_records.Count - 1];
        summary.FinalTier1Pop = lastRecord.Tier1Pop;
        summary.FinalTier2Pop = lastRecord.Tier2Pop;
        summary.FinalTier1Arctic = lastRecord.Tier1Arctic;
        summary.FinalTier1Common = lastRecord.Tier1Common;
        summary.FinalTier1Tropical = lastRecord.Tier1Tropical;
        summary.FinalTier2Arctic = lastRecord.Tier2Arctic;
        summary.FinalTier2Common = lastRecord.Tier2Common;
        summary.FinalTier2Tropical = lastRecord.Tier2Tropical;
        summary.FinalTier1Custom = lastRecord.Tier1Custom;
        summary.FinalTier2Custom = lastRecord.Tier2Custom;

        long maxT1 = 0, minT1 = long.MaxValue;
        long maxT2 = 0, minT2 = long.MaxValue;
        float tempSum = 0;
        float minTemp = float.MaxValue;
        float maxTemp = float.MinValue;

        foreach (var r in _records)
        {
            tempSum += r.Temperature;
            if (r.Temperature < minTemp) minTemp = r.Temperature;
            if (r.Temperature > maxTemp) maxTemp = r.Temperature;
            if (r.Tier1Pop > maxT1) maxT1 = r.Tier1Pop;
            if (r.Tier1Pop < minT1 && r.Tier1Pop >= 1) minT1 = r.Tier1Pop;
            if (r.Tier2Pop > maxT2) maxT2 = r.Tier2Pop;
            if (r.Tier2Pop < minT2 && r.Tier2Pop >= 1) minT2 = r.Tier2Pop;
        }

        summary.MaxTier1Pop = maxT1;
        summary.MinTier1Pop = minT1 == long.MaxValue ? 0 : minT1;
        summary.MaxTier2Pop = maxT2;
        summary.MinTier2Pop = minT2 == long.MaxValue ? 0 : minT2;
        summary.AvgTemperature = tempSum / _records.Count;
        summary.MinTemperature = minTemp;
        summary.MaxTemperature = maxTemp;

        return summary;
    }

    /// <summary>
    /// Convert this run's results to a ScenarioResult for the results screen
    /// </summary>
    public ScenarioResult ToScenarioResult(int scenarioIndex, int numberOfScenarios = 1)
    {
        var summary = GetSummary();
        var popStats = ComputePopulationStats();
        var speciesMetrics = ComputePerSpeciesScenarioMetrics();

        return new ScenarioResult
        {
            ScenarioIndex = scenarioIndex,
            RandomSeed = UsedSeed,
            TotalDays = summary?.TotalDays ?? 0,
            BiologyCycles = summary?.TotalBiologyCycles ?? 0,
            Crashed = HasCrashed,
            CrashDay = CrashDay,
            CrashTier = CrashTier,
            FinalTier1Pop = summary?.FinalTier1Pop ?? 0,
            FinalTier2Pop = summary?.FinalTier2Pop ?? 0,
            FinalTier1Arctic = summary?.FinalTier1Arctic ?? 0,
            FinalTier1Common = summary?.FinalTier1Common ?? 0,
            FinalTier1Tropical = summary?.FinalTier1Tropical ?? 0,
            FinalTier2Arctic = summary?.FinalTier2Arctic ?? 0,
            FinalTier2Common = summary?.FinalTier2Common ?? 0,
            FinalTier2Tropical = summary?.FinalTier2Tropical ?? 0,
            FinalTier1Custom = summary?.FinalTier1Custom ?? 0,
            FinalTier2Custom = summary?.FinalTier2Custom ?? 0,
            FinalSpeciesPopulations = CaptureSpeciesPopulations(),
            SpeciesMetrics = speciesMetrics,
            MaxTier1Pop = summary?.MaxTier1Pop ?? 0,
            MinTier1Pop = summary?.MinTier1Pop ?? 0,
            MaxTier2Pop = summary?.MaxTier2Pop ?? 0,
            MinTier2Pop = summary?.MinTier2Pop ?? 0,
            AvgTemperature = summary?.AvgTemperature ?? 0,
            MinTemperature = summary?.MinTemperature ?? 0,
            MaxTemperature = summary?.MaxTemperature ?? 0,
            PopMean = popStats.Mean,
            PopMax = popStats.Max,
            PopMin = popStats.Min,
            PopStdDev = popStats.StdDev,
            ExtinctionDay = popStats.ExtinctionDay,
            CsvData = ToCsvInternal(scenarioIndex, numberOfScenarios, popStats)
        };
    }

    private Dictionary<string, long> CaptureSpeciesPopulations()
    {
        var pops = new Dictionary<string, long>();
        foreach (var sp in Ecosystem.Species)
            pops[sp.FullName] = (long)Math.Round(sp.Population);
        return pops;
    }

    // ==================== PER-SPECIES SCENARIO METRICS (v12) ====================
    // Final-year window (last 365 days) is the canonical metric for "suboptimal is
    // optimal" — it gives the simulation time to settle past startup transients.
    // For runs shorter than 365 days, the slice covers all days (i.e. final-year
    // metrics equal full-run metrics).
    private const int FINAL_YEAR_DAYS = 365;

    // Crash threshold: a species is "crashed" on the first day it drops below
    // max(CRASH_FLOOR, CRASH_FRACTION × StartPop). Placeholder defaults — Brian
    // may want to tune these (e.g. 1% with floor 50, or rate-of-change-based).
    private const float CRASH_FRACTION = 0.05f;
    private const long  CRASH_FLOOR    = 10L;

    private Dictionary<string, PerSpeciesScenarioMetrics> ComputePerSpeciesScenarioMetrics()
    {
        var result = new Dictionary<string, PerSpeciesScenarioMetrics>();
        if (_records == null || _records.Count == 0 || Ecosystem == null || Ecosystem.Species == null)
            return result;

        int totalDays = _records.Count;
        int finalYearStart = Math.Max(0, totalDays - FINAL_YEAR_DAYS);

        foreach (var sp in Ecosystem.Species)
        {
            string fn = sp.FullName;

            // Single-pass accumulators (avoid LINQ Skip().ToList() per species — see plan §3.5)
            long minPop = long.MaxValue;
            long maxPop = long.MinValue;
            double condSumFull = 0.0;       int condCountFull = 0;
            double brSumFull   = 0.0;       int brCountFull   = 0;
            double popSumFull  = 0.0;       int popCountFull  = 0;
            double popSqSumFull = 0.0;
            double condSumYear = 0.0;       int condCountYear = 0;
            double brSumYear   = 0.0;       int brCountYear   = 0;
            double popSumYear  = 0.0;       int popCountYear  = 0;
            double popSqSumYear = 0.0;

            int extinctionDay = -1;
            bool wasAlive = false;
            int crashDay = -1;
            long startPop = 0L;
            long finalPop = 0L;

            for (int i = 0; i < _records.Count; i++)
            {
                var rec = _records[i];
                if (!rec.SpeciesData.TryGetValue(fn, out var d))
                    continue;

                if (i == 0) startPop = d.Population;
                finalPop = d.Population;

                // Min / Max population
                if (d.Population < minPop) minPop = d.Population;
                if (d.Population > maxPop) maxPop = d.Population;

                // Extinction day: first day Pop reaches 0 after being alive
                if (d.Population > 0L) wasAlive = true;
                if (extinctionDay < 0 && wasAlive && d.Population == 0L)
                    extinctionDay = rec.Day;

                // Crash day: first day Pop drops below threshold
                if (crashDay < 0 && startPop > 0L)
                {
                    long threshold = Math.Max(CRASH_FLOOR, (long)(startPop * CRASH_FRACTION));
                    if (d.Population < threshold)
                        crashDay = rec.Day;
                }

                // Full-run accumulators.
                // Condition / BirthRate are only meaningful on days the species was alive.
                // Once Population hits 0, biology no longer updates sp.Condition, so the
                // recorded Condition value sticks at either its initial 1.0 (species
                // never recruited) or its last pre-extinction value. Including those
                // dead-day samples inflates / distorts the mean. Same logic for
                // BirthRate (already 0 on dead days because StartPop == 0). Population
                // accumulators include zeros — those are biologically meaningful for
                // population statistics (zero is a real datum for an extinct species).
                if (d.Population > 0L)
                {
                    condSumFull += d.Condition; condCountFull++;
                    brSumFull   += d.BirthRate; brCountFull++;
                }
                popSumFull   += d.Population;      popSqSumFull += (double)d.Population * d.Population;
                popCountFull++;

                // Final-year accumulators (last 365 days). Same alive-only filter for
                // condition / birth rate; population includes all days.
                if (i >= finalYearStart)
                {
                    if (d.Population > 0L)
                    {
                        condSumYear += d.Condition; condCountYear++;
                        brSumYear   += d.BirthRate; brCountYear++;
                    }
                    popSumYear   += d.Population;      popSqSumYear += (double)d.Population * d.Population;
                    popCountYear++;
                }
            }

            // Defensive: if species never appeared in any StepRecord (shouldn't happen
            // because RecordStep iterates Ecosystem.Species), skip it.
            if (popCountFull == 0) continue;

            float meanPopFull = (float)(popSumFull / popCountFull);
            float meanPopYear = popCountYear > 0 ? (float)(popSumYear / popCountYear) : meanPopFull;

            result[fn] = new PerSpeciesScenarioMetrics
            {
                FullName                = fn,
                FinalPopulation         = finalPop,
                MeanConditionFullRun    = condCountFull > 0 ? (float)(condSumFull / condCountFull) : 0f,
                MeanConditionFinalYear  = condCountYear > 0 ? (float)(condSumYear / condCountYear) : (condCountFull > 0 ? (float)(condSumFull / condCountFull) : 0f),
                MeanBirthRateFullRun    = brCountFull > 0 ? (float)(brSumFull / brCountFull) : 0f,
                MeanBirthRateFinalYear  = brCountYear > 0 ? (float)(brSumYear / brCountYear) : (brCountFull > 0 ? (float)(brSumFull / brCountFull) : 0f),
                PopCvFullRun            = ComputeCvFromSums(popSumFull, popSqSumFull, popCountFull),
                PopCvFinalYear          = popCountYear > 0
                                              ? ComputeCvFromSums(popSumYear, popSqSumYear, popCountYear)
                                              : ComputeCvFromSums(popSumFull, popSqSumFull, popCountFull),
                MeanPopulationFinalYear = meanPopYear,
                MinPopulation           = minPop == long.MaxValue ? 0L : minPop,
                MaxPopulation           = maxPop == long.MinValue ? 0L : maxPop,
                ExtinctionDay           = extinctionDay,
                CrashDay                = crashDay
            };
        }
        return result;
    }

    /// <summary>
    /// Compute coefficient of variation (StdDev / Mean) from running sums.
    /// Returns 0 when mean is ~0 (CV undefined for zero population).
    /// </summary>
    private static float ComputeCvFromSums(double sum, double sqSum, int count)
    {
        if (count <= 0) return 0f;
        double mean = sum / count;
        if (mean <= 1e-4) return 0f;   // CV undefined for ~zero mean
        double variance = (sqSum / count) - (mean * mean);
        if (variance <= 0.0) return 0f; // numerical guard against tiny negatives
        return (float)(Math.Sqrt(variance) / mean);
    }
}

/// <summary>
/// Summary statistics for a simulation run.
/// </summary>
public class SimulationSummary
{
    public int TotalDays;
    public int TotalBiologyCycles;
    public bool Crashed;
    public int CrashDay;
    public int CrashTier;
    public long FinalTier1Pop;
    public long FinalTier2Pop;
    public long FinalTier1Arctic;
    public long FinalTier1Common;
    public long FinalTier1Tropical;
    public long FinalTier2Arctic;
    public long FinalTier2Common;
    public long FinalTier2Tropical;
    public long FinalTier1Custom;
    public long FinalTier2Custom;
    public long MaxTier1Pop;
    public long MinTier1Pop;
    public long MaxTier2Pop;
    public long MinTier2Pop;
    public float AvgTemperature;
    public float MinTemperature;
    public float MaxTemperature;

    public override string ToString()
    {
        return $"Days: {TotalDays}, Cycles: {TotalBiologyCycles}, " +
               $"Crashed: {Crashed} (Day {CrashDay}, Tier {CrashTier}), " +
               $"Final T1: {FinalTier1Pop}, Final T2: {FinalTier2Pop}, " +
               $"Avg Temp: {AvgTemperature:F1}°C";
    }
}
