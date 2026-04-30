using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Results from a single scenario run.
/// Contains summary stats and the full CSV data for download.
/// </summary>
[Serializable]
public class ScenarioResult
{
    // Identity
    public int ScenarioIndex;           // 1-based index
    public int RandomSeed;              // Seed used for this run

    // Timing
    public int TotalDays;               // Days simulated
    public int BiologyCycles;           // Number of biology steps

    // Outcome
    public bool Crashed;                // Did a tier go extinct?
    public int CrashDay;                // Day of crash (-1 if no crash)
    public int CrashTier;               // Which tier crashed (-1 if no crash)

    // Final populations
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

    // Per-species final populations (key = FullName like "Hexapod_Arctic" or "Coral_Custom")
    public Dictionary<string, long> FinalSpeciesPopulations;

    // v12: Per-species rich metrics (final-year means, CV, min/max, extinction/crash day)
    // Populated by SimulationRunner.ComputePerSpeciesScenarioMetrics().
    // Key = FullName.
    public Dictionary<string, PerSpeciesScenarioMetrics> SpeciesMetrics;

    // Population stats (across all days)
    public long MaxTier1Pop;
    public long MinTier1Pop;
    public long MaxTier2Pop;
    public long MinTier2Pop;

    // Temperature stats
    public float AvgTemperature;
    public float MinTemperature;
    public float MaxTemperature;

    // Condition stats (averaged across all days)
    public float AvgConditionT1;
    public float AvgConditionT2;
    public float FinalConditionT1;
    public float FinalConditionT2;

    // Per-column population summary statistics (keyed by column name)
    public Dictionary<string, double> PopMean;
    public Dictionary<string, long> PopMax;
    public Dictionary<string, long> PopMin;
    public Dictionary<string, double> PopStdDev;

    // Extinction timing per variant: variant key -> day first reached 0 (-1 if survived)
    public Dictionary<string, int> ExtinctionDay;

    // Column name constants shared across SimulationRunner and AggregateResults
    public static readonly string[] PopColumns = {
        "Tier1Pop", "Tier2Pop",
        "Tier1Arctic", "Tier1Common", "Tier1Tropical", "Tier1Custom",
        "Tier2Arctic", "Tier2Common", "Tier2Tropical", "Tier2Custom"
    };

    public static readonly string[] VariantColumns = {
        "Tier1Arctic", "Tier1Common", "Tier1Tropical", "Tier1Custom",
        "Tier2Arctic", "Tier2Common", "Tier2Tropical", "Tier2Custom"
    };

    // CSV data (stored for download)
    public string CsvData;

    /// <summary>
    /// Quick summary string for display in the list
    /// </summary>
    public string GetSummaryLine()
    {
        if (Crashed)
        {
            return $"Scenario {ScenarioIndex}: Crashed Day {CrashDay} (Tier {CrashTier})";
        }
        return $"Scenario {ScenarioIndex}: T1={FinalTier1Pop:N0}, T2={FinalTier2Pop:N0}";
    }

    /// <summary>
    /// Status indicator for UI (TMP-compatible)
    /// </summary>
    public string GetStatusIcon()
    {
        return Crashed ? "X" : "OK";
    }
}

/// <summary>
/// Per-species rich metrics for a single scenario (v12).
///
/// Computed by SimulationRunner.ComputePerSpeciesScenarioMetrics() at end of run
/// by post-processing the per-day StepRecord.SpeciesData entries. These metrics
/// are NOT clamped by carrying capacity (Condition, BirthRate are physiological
/// signals), so they expose the "suboptimal is optimal" Jensen shift that
/// final-day population masks.
///
/// Final year = last 365 days; if total run is shorter, final-year metrics
/// equal full-run metrics (slice covers all days).
/// </summary>
[Serializable]
public class PerSpeciesScenarioMetrics
{
    public string FullName;
    public long  FinalPopulation;

    // Mean Condition over the window (population-weighted equivalent — each day
    // contributes one observation regardless of population size)
    public float MeanConditionFullRun;
    public float MeanConditionFinalYear;

    // Mean per-capita birth rate (Births / max(StartOfDayPop, 1) per day)
    public float MeanBirthRateFullRun;
    public float MeanBirthRateFinalYear;

    // Population coefficient of variation (StdDev / Mean) — instability signal
    public float PopCvFullRun;
    public float PopCvFinalYear;

    // Mean population over final year — different from FinalPopulation (single-day snapshot)
    public float MeanPopulationFinalYear;

    // Population extremes during full sim
    public long MinPopulation;
    public long MaxPopulation;

    // Timing events (-1 if event never occurred)
    public int ExtinctionDay;     // First day Pop reaches 0 after being alive
    public int CrashDay;          // First day Pop drops below max(CRASH_FLOOR, CRASH_FRACTION × StartPop)

    public bool Survived => FinalPopulation > 0;
}

/// <summary>
/// Per-metric Mean / StdDev / Min / Max stats with survived-only variants (v12).
/// Used inside PerSpeciesAggregate for cross-scenario aggregation.
/// </summary>
[Serializable]
public struct AggStat
{
    public float Mean;
    public float StdDev;
    public float Min;
    public float Max;
    public float SurvivedMean;
    public float SurvivedStdDev;
}

/// <summary>
/// Extinction/crash timing summary for a species across scenarios (v12).
/// </summary>
[Serializable]
public struct ExtinctionStat
{
    public int   NEvents;       // # scenarios where event occurred (day != -1)
    public int   NNonEvents;    // # scenarios where event did not occur (day == -1)
    public float MinDay;        // Earliest day among events; -1 if no events
    public float MaxDay;        // Latest day among events; -1 if no events
    public float MeanDay;       // Mean day among events; -1 if no events
}

/// <summary>
/// Per-species aggregate across all scenarios in a run (v12).
/// Wraps the AggStat for each metric defined on PerSpeciesScenarioMetrics.
/// </summary>
[Serializable]
public class PerSpeciesAggregate
{
    public string FullName;
    public int N;             // # scenarios contributing
    public int NSurvived;     // # scenarios where species final pop > 0

    public AggStat MeanConditionFullRun;
    public AggStat MeanConditionFinalYear;
    public AggStat MeanBirthRateFullRun;
    public AggStat MeanBirthRateFinalYear;
    public AggStat PopCvFullRun;
    public AggStat PopCvFinalYear;
    public AggStat MeanPopulationFinalYear;
    public AggStat MinPopulation;
    public AggStat MaxPopulation;
    public AggStat FinalPopulation;
    public ExtinctionStat ExtinctionTiming;
    public ExtinctionStat CrashTiming;
}

/// <summary>
/// Aggregate results across all scenario runs.
/// Used for the summary section of the results screen.
/// </summary>
[Serializable]
public class AggregateResults
{
    // Run info
    public int TotalScenarios;
    public int CompletedScenarios;
    public int SurvivedScenarios;
    public int CrashedScenarios;

    // Configuration - ALL parameters
    public int DaysPerScenario;
    public int BiologyStep;
    public int RandomSeed;

    // Carrying Capacity (always on as of v11.1)
    public float CarryingCapacity;

    // Condition System
    public float ConditionDrainRate;
    public float ConditionRecoveryRate;

    // Temperature - Base
    public float BaseTemperature;
    public float SeasonalAmplitude;

    // Temperature - Climate Trend
    public float ClimateTrend;
    public bool InterannualVariation;
    public float VariabilityMagnitude;
    public float WarmingBias;

    // Temperature - Daily Variation
    public bool Autocorrelated;
    public float DailyVariationRange;
    public float RandomnessGrowthRate;

    // Temperature - Bounds
    public float TemperatureBoundsMin;
    public float TemperatureBoundsMax;

    // Species data reference (for config export)
    public RunSpeciesList RunSpecies;

    // Timestamp
    public DateTime CompletedAt;

    // Population averages (across scenarios that survived)
    public float AvgFinalTier1Pop;
    public float AvgFinalTier2Pop;
    public float MinFinalTier1Pop;
    public float MaxFinalTier1Pop;
    public float MinFinalTier2Pop;
    public float MaxFinalTier2Pop;

    // Crash stats
    public float CrashRate;
    public float AvgCrashDay;

    // Condition stats (across scenarios)
    public float AvgConditionT1;   // Grand mean of per-scenario avg condition
    public float AvgConditionT2;
    public float AvgFinalConditionT1;  // Mean final condition (survived only)
    public float AvgFinalConditionT2;

    // Per-species population stats across all scenarios
    // Key = species FullName (e.g., "Hexapod_Arctic", "Coral_Custom")
    public Dictionary<string, float> PerSpeciesAvg;
    public Dictionary<string, float> PerSpeciesMin;
    public Dictionary<string, float> PerSpeciesMax;
    public Dictionary<string, int> PerSpeciesExtinct;  // scenarios where final pop = 0
    public Dictionary<string, int> PerSpeciesSurvived; // scenarios where final pop > 0
    public Dictionary<string, float> PerSpeciesSurvivedAvg; // avg pop only across survived scenarios

    // v12: Per-species rich aggregate (final-year means, CV, min/max, extinction/crash timing)
    public Dictionary<string, PerSpeciesAggregate> PerSpeciesMetrics;

    // Individual results
    public List<ScenarioResult> Scenarios = new List<ScenarioResult>();

    /// <summary>
    /// Calculate aggregate stats from individual scenario results
    /// </summary>
    public void CalculateAggregates()
    {
        if (Scenarios == null || Scenarios.Count == 0) return;

        TotalScenarios = Scenarios.Count;
        CompletedScenarios = Scenarios.Count;
        SurvivedScenarios = 0;
        CrashedScenarios = 0;

        float sumT1 = 0, sumT2 = 0;
        float sumCrashDay = 0;
        float sumCondT1 = 0, sumCondT2 = 0;
        float sumFinalCondT1 = 0, sumFinalCondT2 = 0;
        int survivedCount = 0;
        int crashedCount = 0;

        MinFinalTier1Pop = float.MaxValue;
        MaxFinalTier1Pop = float.MinValue;
        MinFinalTier2Pop = float.MaxValue;
        MaxFinalTier2Pop = float.MinValue;

        foreach (var scenario in Scenarios)
        {
            // Condition stats across ALL scenarios (crashed + survived)
            sumCondT1 += scenario.AvgConditionT1;
            sumCondT2 += scenario.AvgConditionT2;

            if (scenario.Crashed)
            {
                CrashedScenarios++;
                crashedCount++;
                sumCrashDay += scenario.CrashDay;
            }
            else
            {
                SurvivedScenarios++;
                survivedCount++;

                sumT1 += scenario.FinalTier1Pop;
                sumT2 += scenario.FinalTier2Pop;
                sumFinalCondT1 += scenario.FinalConditionT1;
                sumFinalCondT2 += scenario.FinalConditionT2;

                if (scenario.FinalTier1Pop < MinFinalTier1Pop) MinFinalTier1Pop = scenario.FinalTier1Pop;
                if (scenario.FinalTier1Pop > MaxFinalTier1Pop) MaxFinalTier1Pop = scenario.FinalTier1Pop;
                if (scenario.FinalTier2Pop < MinFinalTier2Pop) MinFinalTier2Pop = scenario.FinalTier2Pop;
                if (scenario.FinalTier2Pop > MaxFinalTier2Pop) MaxFinalTier2Pop = scenario.FinalTier2Pop;
            }
        }

        if (survivedCount > 0)
        {
            AvgFinalTier1Pop = sumT1 / survivedCount;
            AvgFinalTier2Pop = sumT2 / survivedCount;
            AvgFinalConditionT1 = sumFinalCondT1 / survivedCount;
            AvgFinalConditionT2 = sumFinalCondT2 / survivedCount;
        }

        if (crashedCount > 0)
        {
            AvgCrashDay = sumCrashDay / crashedCount;
        }

        // Condition averages across ALL scenarios
        AvgConditionT1 = sumCondT1 / TotalScenarios;
        AvgConditionT2 = sumCondT2 / TotalScenarios;

        CrashRate = (float)CrashedScenarios / TotalScenarios;

        if (survivedCount == 0)
        {
            MinFinalTier1Pop = 0;
            MaxFinalTier1Pop = 0;
            MinFinalTier2Pop = 0;
            MaxFinalTier2Pop = 0;
        }

        // Per-species aggregation (all scenarios, including crashed)
        var spSum = new Dictionary<string, float>();
        var spMin = new Dictionary<string, float>();
        var spMax = new Dictionary<string, float>();
        var spCount = new Dictionary<string, int>();

        foreach (var scenario in Scenarios)
        {
            if (scenario.FinalSpeciesPopulations == null) continue;
            foreach (var kvp in scenario.FinalSpeciesPopulations)
            {
                if (!spSum.ContainsKey(kvp.Key))
                {
                    spSum[kvp.Key] = 0;
                    spMin[kvp.Key] = float.MaxValue;
                    spMax[kvp.Key] = float.MinValue;
                    spCount[kvp.Key] = 0;
                }
                spSum[kvp.Key] += kvp.Value;
                spCount[kvp.Key]++;
                if (kvp.Value < spMin[kvp.Key]) spMin[kvp.Key] = kvp.Value;
                if (kvp.Value > spMax[kvp.Key]) spMax[kvp.Key] = kvp.Value;
            }
        }

        PerSpeciesAvg = new Dictionary<string, float>();
        PerSpeciesMin = new Dictionary<string, float>();
        PerSpeciesMax = new Dictionary<string, float>();
        PerSpeciesExtinct = new Dictionary<string, int>();
        PerSpeciesSurvived = new Dictionary<string, int>();
        PerSpeciesSurvivedAvg = new Dictionary<string, float>();
        foreach (var key in spSum.Keys)
        {
            PerSpeciesAvg[key] = spCount[key] > 0 ? spSum[key] / spCount[key] : 0;
            PerSpeciesMin[key] = spMin[key] == float.MaxValue ? 0 : spMin[key];
            PerSpeciesMax[key] = spMax[key] == float.MinValue ? 0 : spMax[key];

            // Count extinctions vs survivals, and compute survived-only average
            int extinct = 0, survived = 0;
            float survivedSum = 0;
            foreach (var scenario in Scenarios)
            {
                if (scenario.FinalSpeciesPopulations == null) continue;
                if (!scenario.FinalSpeciesPopulations.ContainsKey(key)) continue;
                if (scenario.FinalSpeciesPopulations[key] <= 0)
                    extinct++;
                else
                {
                    survived++;
                    survivedSum += scenario.FinalSpeciesPopulations[key];
                }
            }
            PerSpeciesExtinct[key] = extinct;
            PerSpeciesSurvived[key] = survived;
            PerSpeciesSurvivedAvg[key] = survived > 0 ? survivedSum / survived : 0;
        }

        // v12: Per-species rich aggregate (final-year means, CV, min/max, extinction/crash timing)
        BuildPerSpeciesAggregate();
    }

    /// <summary>
    /// v12: Build PerSpeciesMetrics dict by aggregating Scenarios[].SpeciesMetrics
    /// across scenarios. Existing tier-level dicts (PerSpeciesAvg etc.) are NOT
    /// touched — both old and new aggregates coexist for backward compatibility.
    /// </summary>
    private void BuildPerSpeciesAggregate()
    {
        PerSpeciesMetrics = new Dictionary<string, PerSpeciesAggregate>();
        if (Scenarios == null || Scenarios.Count == 0) return;

        // Union of species names across all scenarios
        var allKeys = new HashSet<string>();
        foreach (var s in Scenarios)
        {
            if (s.SpeciesMetrics == null) continue;
            foreach (var k in s.SpeciesMetrics.Keys) allKeys.Add(k);
        }

        foreach (var key in allKeys)
        {
            var rows = new List<PerSpeciesScenarioMetrics>();
            foreach (var s in Scenarios)
            {
                if (s.SpeciesMetrics == null) continue;
                if (s.SpeciesMetrics.TryGetValue(key, out var m))
                    rows.Add(m);
            }
            if (rows.Count == 0) continue;

            var agg = new PerSpeciesAggregate
            {
                FullName  = key,
                N         = rows.Count,
                NSurvived = 0
            };
            foreach (var r in rows) if (r.Survived) agg.NSurvived++;

            agg.MeanConditionFullRun    = ComputeAggStat(rows, r => r.MeanConditionFullRun);
            agg.MeanConditionFinalYear  = ComputeAggStat(rows, r => r.MeanConditionFinalYear);
            agg.MeanBirthRateFullRun    = ComputeAggStat(rows, r => r.MeanBirthRateFullRun);
            agg.MeanBirthRateFinalYear  = ComputeAggStat(rows, r => r.MeanBirthRateFinalYear);
            agg.PopCvFullRun            = ComputeAggStat(rows, r => r.PopCvFullRun);
            agg.PopCvFinalYear          = ComputeAggStat(rows, r => r.PopCvFinalYear);
            agg.MeanPopulationFinalYear = ComputeAggStat(rows, r => r.MeanPopulationFinalYear);
            agg.MinPopulation           = ComputeAggStat(rows, r => (float)r.MinPopulation);
            agg.MaxPopulation           = ComputeAggStat(rows, r => (float)r.MaxPopulation);
            agg.FinalPopulation         = ComputeAggStat(rows, r => (float)r.FinalPopulation);
            agg.ExtinctionTiming        = ComputeExtinctionStat(rows, r => r.ExtinctionDay);
            agg.CrashTiming             = ComputeExtinctionStat(rows, r => r.CrashDay);

            PerSpeciesMetrics[key] = agg;
        }
    }

    private static AggStat ComputeAggStat(List<PerSpeciesScenarioMetrics> rows, Func<PerSpeciesScenarioMetrics, float> sel)
    {
        var stat = new AggStat();
        if (rows == null || rows.Count == 0) return stat;
        float sum = 0f, sqSum = 0f;
        float sSum = 0f, sSqSum = 0f;
        float min = float.MaxValue, max = float.MinValue;
        int n = 0, nSurvived = 0;
        foreach (var r in rows)
        {
            float v = sel(r);
            sum += v; sqSum += v * v;
            if (v < min) min = v;
            if (v > max) max = v;
            n++;
            if (r.Survived)
            {
                sSum += v; sSqSum += v * v;
                nSurvived++;
            }
        }
        if (n > 0)
        {
            stat.Mean = sum / n;
            float variance = (sqSum / n) - (stat.Mean * stat.Mean);
            stat.StdDev = variance > 0f ? (float)Math.Sqrt(variance) : 0f;
            stat.Min = min;
            stat.Max = max;
        }
        if (nSurvived > 0)
        {
            stat.SurvivedMean = sSum / nSurvived;
            float sVar = (sSqSum / nSurvived) - (stat.SurvivedMean * stat.SurvivedMean);
            stat.SurvivedStdDev = sVar > 0f ? (float)Math.Sqrt(sVar) : 0f;
        }
        return stat;
    }

    private static ExtinctionStat ComputeExtinctionStat(List<PerSpeciesScenarioMetrics> rows, Func<PerSpeciesScenarioMetrics, int> selector)
    {
        var stat = new ExtinctionStat { MinDay = -1f, MaxDay = -1f, MeanDay = -1f };
        if (rows == null || rows.Count == 0) return stat;
        int sum = 0;
        int min = int.MaxValue, max = int.MinValue;
        foreach (var r in rows)
        {
            int day = selector(r);
            if (day < 0) { stat.NNonEvents++; continue; }
            stat.NEvents++;
            sum += day;
            if (day < min) min = day;
            if (day > max) max = day;
        }
        if (stat.NEvents > 0)
        {
            stat.MinDay = min;
            stat.MaxDay = max;
            stat.MeanDay = (float)sum / stat.NEvents;
        }
        return stat;
    }

    /// <summary>
    /// Get quick stats line for header display
    /// </summary>
    public string GetQuickStatsLine()
    {
        return $"{SurvivedScenarios} survived | {CrashedScenarios} crashed | " +
               $"Avg T1: {AvgFinalTier1Pop:N0} | Avg T2: {AvgFinalTier2Pop:N0}";
    }

    /// <summary>
    /// Get configuration summary line
    /// </summary>
    public string GetConfigLine()
    {
        return $"{DaysPerScenario} days x {TotalScenarios} scenarios";
    }

    /// <summary>
    /// Generate aggregate CSV for download (contains RESULTS)
    /// </summary>
    public string ToAggregateCsv()
    {
        var sb = new System.Text.StringBuilder();

        // v12.2: Build (Tier, Variant) lookup keyed by FullName for use in
        // per-species sections. Per-species dicts only carry FullName strings,
        // so we resolve Variant and Tier from RunSpecies at write time.
        var speciesMeta = new Dictionary<string, (int Tier, string Variant)>();
        if (RunSpecies?.speciesList != null)
        {
            foreach (var sp in RunSpecies.speciesList)
            {
                string name = !string.IsNullOrEmpty(sp.displayName)
                    ? sp.displayName
                    : sp.speciesName.ToString();
                string fullName = $"{name}_{sp.variant}";
                // SpeciesData.tier is 0-based (0=prey, 1=predator); internal Tier is 1-based.
                speciesMeta[fullName] = (sp.tier + 1, sp.variant.ToString());
            }
        }
        string GetVariant(string fn) => speciesMeta.TryGetValue(fn, out var m) ? m.Variant : "Unknown";
        string GetTier(string fn) => speciesMeta.TryGetValue(fn, out var m) ? m.Tier.ToString() : "?";

        sb.AppendLine("=== TINYSEA AGGREGATE RESULTS ===");
        sb.AppendLine($"# Generated,{CompletedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Configuration,{DaysPerScenario} days x {TotalScenarios} scenarios");
        sb.AppendLine($"# Base Temp,{BaseTemperature}C");
        sb.AppendLine($"# Climate Trend,{ClimateTrend}C/year");
        sb.AppendLine($"# Carrying Capacity,{CarryingCapacity}");
        sb.AppendLine($"# Condition Drain Rate,{ConditionDrainRate}");
        sb.AppendLine($"# Condition Recovery Rate,{ConditionRecoveryRate}");
        sb.AppendLine();

        sb.AppendLine("=== SUMMARY ===");
        sb.AppendLine($"Scenarios Run,{TotalScenarios}");
        sb.AppendLine($"Survived,{SurvivedScenarios}");
        sb.AppendLine($"Crashed,{CrashedScenarios}");
        sb.AppendLine($"Crash Rate,{CrashRate:P1}");
        if (CrashedScenarios > 0)
        {
            sb.AppendLine($"Avg Crash Day,{AvgCrashDay:F1}");
        }
        sb.AppendLine();

        sb.AppendLine("=== POPULATION STATS (Survived Only) ===");
        sb.AppendLine($"Avg Final T1,{AvgFinalTier1Pop:F1}");
        sb.AppendLine($"Avg Final T2,{AvgFinalTier2Pop:F1}");
        sb.AppendLine($"Min Final T1,{MinFinalTier1Pop}");
        sb.AppendLine($"Max Final T1,{MaxFinalTier1Pop}");
        sb.AppendLine($"Min Final T2,{MinFinalTier2Pop}");
        sb.AppendLine($"Max Final T2,{MaxFinalTier2Pop}");
        sb.AppendLine();

        if (PerSpeciesAvg != null && PerSpeciesAvg.Count > 0)
        {
            sb.AppendLine("=== PER-SPECIES POPULATION STATS (All Scenarios) ===");
            sb.AppendLine("Species,Variant,Tier,Avg,SurvivedAvg,Min,Max,Extinct,Survived,ExtinctionRate");
            foreach (var key in PerSpeciesAvg.Keys.OrderBy(k => k))
            {
                float avg = PerSpeciesAvg[key];
                float survivedAvg = PerSpeciesSurvivedAvg != null && PerSpeciesSurvivedAvg.ContainsKey(key) ? PerSpeciesSurvivedAvg[key] : 0;
                float min = PerSpeciesMin != null && PerSpeciesMin.ContainsKey(key) ? PerSpeciesMin[key] : 0;
                float max = PerSpeciesMax != null && PerSpeciesMax.ContainsKey(key) ? PerSpeciesMax[key] : 0;
                int extinct = PerSpeciesExtinct != null && PerSpeciesExtinct.ContainsKey(key) ? PerSpeciesExtinct[key] : 0;
                int survived = PerSpeciesSurvived != null && PerSpeciesSurvived.ContainsKey(key) ? PerSpeciesSurvived[key] : 0;
                int total = extinct + survived;
                float extinctionRate = total > 0 ? (float)extinct / total : 0;
                sb.AppendLine($"{key},{GetVariant(key)},{GetTier(key)},{avg:F1},{survivedAvg:F1},{min},{max},{extinct},{survived},{extinctionRate:P1}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== CONDITION STATS ===");
        sb.AppendLine($"Avg Condition T1 (All Scenarios),{AvgConditionT1:F3}");
        sb.AppendLine($"Avg Condition T2 (All Scenarios),{AvgConditionT2:F3}");
        sb.AppendLine($"Avg Final Condition T1 (Survived),{AvgFinalConditionT1:F3}");
        sb.AppendLine($"Avg Final Condition T2 (Survived),{AvgFinalConditionT2:F3}");
        sb.AppendLine();

        // v12: Per-species rich aggregate sections (final-year / full-run / stability)
        if (PerSpeciesMetrics != null && PerSpeciesMetrics.Count > 0)
        {
            sb.AppendLine("=== PER-SPECIES FINAL YEAR METRICS ===");
            sb.AppendLine("Species,Variant,Tier,N,NSurvived,MeanCondition,MeanCondition_StdDev,MeanCondition_SurvivedMean,MeanBirthRate,MeanBirthRate_StdDev,MeanBirthRate_SurvivedMean,PopCv,PopCv_StdDev,MeanPop,MeanPop_StdDev,MeanPop_SurvivedMean");
            foreach (var key in PerSpeciesMetrics.Keys.OrderBy(k => k))
            {
                var a = PerSpeciesMetrics[key];
                sb.AppendLine($"{key},{GetVariant(key)},{GetTier(key)},{a.N},{a.NSurvived}," +
                    $"{a.MeanConditionFinalYear.Mean:F3},{a.MeanConditionFinalYear.StdDev:F3},{a.MeanConditionFinalYear.SurvivedMean:F3}," +
                    $"{a.MeanBirthRateFinalYear.Mean:F4},{a.MeanBirthRateFinalYear.StdDev:F4},{a.MeanBirthRateFinalYear.SurvivedMean:F4}," +
                    $"{a.PopCvFinalYear.Mean:F3},{a.PopCvFinalYear.StdDev:F3}," +
                    $"{a.MeanPopulationFinalYear.Mean:F1},{a.MeanPopulationFinalYear.StdDev:F1},{a.MeanPopulationFinalYear.SurvivedMean:F1}");
            }
            sb.AppendLine();

            sb.AppendLine("=== PER-SPECIES FULL-RUN METRICS ===");
            sb.AppendLine("Species,Variant,Tier,N,NSurvived,MeanCondition,MeanCondition_StdDev,MeanBirthRate,MeanBirthRate_StdDev,PopCv,PopCv_StdDev");
            foreach (var key in PerSpeciesMetrics.Keys.OrderBy(k => k))
            {
                var a = PerSpeciesMetrics[key];
                sb.AppendLine($"{key},{GetVariant(key)},{GetTier(key)},{a.N},{a.NSurvived}," +
                    $"{a.MeanConditionFullRun.Mean:F3},{a.MeanConditionFullRun.StdDev:F3}," +
                    $"{a.MeanBirthRateFullRun.Mean:F4},{a.MeanBirthRateFullRun.StdDev:F4}," +
                    $"{a.PopCvFullRun.Mean:F3},{a.PopCvFullRun.StdDev:F3}");
            }
            sb.AppendLine();

            sb.AppendLine("=== PER-SPECIES STABILITY METRICS ===");
            sb.AppendLine("Species,Variant,Tier,N,NSurvived,MinPop_Mean,MinPop_Min,MaxPop_Mean,MaxPop_Max,FinalPop_Mean,FinalPop_SurvivedMean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay");
            foreach (var key in PerSpeciesMetrics.Keys.OrderBy(k => k))
            {
                var a = PerSpeciesMetrics[key];
                float extinctionRate = a.N > 0 ? (float)a.ExtinctionTiming.NEvents / a.N : 0f;
                float crashRate      = a.N > 0 ? (float)a.CrashTiming.NEvents / a.N : 0f;
                sb.AppendLine($"{key},{GetVariant(key)},{GetTier(key)},{a.N},{a.NSurvived}," +
                    $"{a.MinPopulation.Mean:F1},{a.MinPopulation.Min:F0}," +
                    $"{a.MaxPopulation.Mean:F1},{a.MaxPopulation.Max:F0}," +
                    $"{a.FinalPopulation.Mean:F1},{a.FinalPopulation.SurvivedMean:F1}," +
                    $"{extinctionRate:P1},{a.ExtinctionTiming.MeanDay:F1}," +
                    $"{crashRate:P1},{a.CrashTiming.MeanDay:F1}");
            }
            sb.AppendLine();
        }

        // Combined wide-format scenario table. One row per scenario:
        // scenario metadata + tier totals + temperatures + one FinalPop column
        // per species (sanitized FullName, alpha-sorted). Two extra header rows
        // annotate each species column with Variant and Tier; non-applicable
        // meta columns get blank annotations. Variant-rollup columns
        // (T1Arctic, ...) are dropped — per-species replaces them, and tier-total
        // values still satisfy the per-species-sums-to-tier invariant.
        var speciesCols = new List<string>();
        {
            var keys = new SortedSet<string>();
            foreach (var s in Scenarios)
            {
                if (s.FinalSpeciesPopulations == null) continue;
                foreach (var k in s.FinalSpeciesPopulations.Keys) keys.Add(k);
            }
            speciesCols.AddRange(keys);
        }

        sb.AppendLine("=== INDIVIDUAL SCENARIOS ===");

        sb.Append("Scenario,Seed,Crashed,CrashDay,CrashTier,FinalT1,FinalT2,AvgTemp,MinTemp,MaxTemp");
        foreach (var k in speciesCols) sb.Append($",{StepRecord.SanitizeColumnName(k)}");
        sb.AppendLine();

        sb.Append("Variant,,,,,All,All,,,");
        foreach (var k in speciesCols) sb.Append($",{GetVariant(k)}");
        sb.AppendLine();

        sb.Append("Tier,,,,,1,2,,,");
        foreach (var k in speciesCols) sb.Append($",{GetTier(k)}");
        sb.AppendLine();

        foreach (var s in Scenarios)
        {
            sb.Append($"{s.ScenarioIndex},{s.RandomSeed},{s.Crashed},{s.CrashDay},{s.CrashTier}");
            sb.Append($",{s.FinalTier1Pop},{s.FinalTier2Pop}");
            sb.Append($",{s.AvgTemperature:F2},{s.MinTemperature:F2},{s.MaxTemperature:F2}");
            foreach (var k in speciesCols)
            {
                long pop = (s.FinalSpeciesPopulations != null && s.FinalSpeciesPopulations.TryGetValue(k, out var p)) ? p : 0L;
                sb.Append($",{pop}");
            }
            sb.AppendLine();
        }

        // Grand Mean across all runs — combined wide-format table.
        // Columns: Tier1Pop, Tier2Pop, then one column per species (sanitized FullName)
        // ordered alphabetically by FullName for stable output. Three header rows
        // (Statistic / Variant / Tier) mirror the scenario-CSV summary block.
        // Tier-rollup variant columns (Tier1Arctic, ...) are intentionally omitted —
        // species are tracked individually so the variant rollup is redundant.
        bool hasPopStats = Scenarios.Any(s => s.PopMean != null);
        if (hasPopStats)
        {
            var perSpeciesKeys = (PerSpeciesMetrics != null && PerSpeciesMetrics.Count > 0)
                ? PerSpeciesMetrics.Keys.OrderBy(k => k).ToList()
                : new List<string>();

            double GrandMean(string statName, string col)
            {
                double sum = 0;
                int count = 0;
                foreach (var s in Scenarios)
                {
                    double val;
                    switch (statName)
                    {
                        case "Mean":
                            if (s.PopMean == null || !s.PopMean.ContainsKey(col)) continue;
                            val = s.PopMean[col]; break;
                        case "Max":
                            if (s.PopMax == null || !s.PopMax.ContainsKey(col)) continue;
                            val = s.PopMax[col]; break;
                        case "Min":
                            if (s.PopMin == null || !s.PopMin.ContainsKey(col)) continue;
                            val = s.PopMin[col]; break;
                        case "StdDev":
                            if (s.PopStdDev == null || !s.PopStdDev.ContainsKey(col)) continue;
                            val = s.PopStdDev[col]; break;
                        default: continue;
                    }
                    sum += val;
                    count++;
                }
                return count > 0 ? sum / count : 0;
            }

            sb.AppendLine();
            sb.AppendLine("=== SUMMARY STATISTICS (Grand Mean Across All Scenarios) ===");

            // Header row 1: Statistic + tier totals + per-species columns.
            var headerCols = new List<string> { "Tier1Pop", "Tier2Pop" };
            foreach (var key in perSpeciesKeys)
                headerCols.Add(StepRecord.SanitizeColumnName(key));
            sb.AppendLine("Statistic," + string.Join(",", headerCols));

            // Header row 2: Variant (All for tier totals, actual variant per species).
            sb.Append("Variant,All,All");
            foreach (var key in perSpeciesKeys) sb.Append($",{GetVariant(key)}");
            sb.AppendLine();

            // Header row 3: Tier (1/2 for tier totals, actual tier per species).
            sb.Append("Tier,1,2");
            foreach (var key in perSpeciesKeys) sb.Append($",{GetTier(key)}");
            sb.AppendLine();

            string[] statNames = { "Mean", "Max", "Min", "StdDev" };
            foreach (var statName in statNames)
            {
                sb.Append($"GrandMean_{statName}");
                sb.Append($",{GrandMean(statName, "Tier1Pop"):F1}");
                sb.Append($",{GrandMean(statName, "Tier2Pop"):F1}");
                foreach (var key in perSpeciesKeys)
                    sb.Append($",{GrandMean(statName, key):F1}");
                sb.AppendLine();
            }
        }

        // Extinction timing across all runs (tier-variant rollups)
        bool hasExtinction = Scenarios.Any(s => s.ExtinctionDay != null);
        if (hasExtinction)
        {
            sb.AppendLine();
            sb.AppendLine("=== EXTINCTION TIMING - TIER VARIANTS (Across All Scenarios) ===");
            sb.AppendLine("Variant,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived");

            foreach (var variant in ScenarioResult.VariantColumns)
            {
                var extinctDays = new List<int>();
                int numSurvived = 0;

                foreach (var s in Scenarios)
                {
                    if (s.ExtinctionDay == null || !s.ExtinctionDay.ContainsKey(variant)) continue;
                    int day = s.ExtinctionDay[variant];
                    if (day == -1)
                        numSurvived++;
                    else
                        extinctDays.Add(day);
                }

                int numExtinct = extinctDays.Count;
                if (numExtinct == 0)
                {
                    sb.AppendLine($"{variant},-1,-1,-1,0,{numSurvived}");
                }
                else
                {
                    int minDays = extinctDays.Min();
                    int maxDays = extinctDays.Max();
                    double avgDays = extinctDays.Average();
                    sb.AppendLine($"{variant},{minDays},{maxDays},{avgDays:F1},{numExtinct},{numSurvived}");
                }
            }
        }

        // v12.2: Per-species extinction timing (Across All Scenarios).
        // Source: PerSpeciesMetrics[key].ExtinctionTiming (already computed in CalculateAggregates).
        if (PerSpeciesMetrics != null && PerSpeciesMetrics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== EXTINCTION TIMING - PER SPECIES (Across All Scenarios) ===");
            sb.AppendLine("Species,Variant,Tier,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived");

            foreach (var key in PerSpeciesMetrics.Keys.OrderBy(k => k))
            {
                var et = PerSpeciesMetrics[key].ExtinctionTiming;
                if (et.NEvents == 0)
                {
                    sb.AppendLine($"{key},{GetVariant(key)},{GetTier(key)},-1,-1,-1,0,{et.NNonEvents}");
                }
                else
                {
                    sb.AppendLine($"{key},{GetVariant(key)},{GetTier(key)},{et.MinDay:F0},{et.MaxDay:F0},{et.MeanDay:F1},{et.NEvents},{et.NNonEvents}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate configuration JSON - NO RESULTS, only config and species
    /// </summary>
    public string ToConfigJson()
    {
        return ConfigExporter.BuildConfigJson(
            DaysPerScenario, TotalScenarios, BiologyStep, RandomSeed,
            CarryingCapacity,
            BaseTemperature, SeasonalAmplitude, ClimateTrend,
            InterannualVariation, VariabilityMagnitude, WarmingBias,
            Autocorrelated, DailyVariationRange, RandomnessGrowthRate,
            TemperatureBoundsMin, TemperatureBoundsMax,
            RunSpecies,
            ConditionDrainRate, ConditionRecoveryRate
        );
    }

    /// <summary>
    /// Generate configuration CSV - NO RESULTS, only config and species
    /// </summary>
    public string ToConfigCsv()
    {
        return ConfigExporter.BuildConfigCsv(
            DaysPerScenario, TotalScenarios, BiologyStep, RandomSeed,
            CarryingCapacity,
            BaseTemperature, SeasonalAmplitude, ClimateTrend,
            InterannualVariation, VariabilityMagnitude, WarmingBias,
            Autocorrelated, DailyVariationRange, RandomnessGrowthRate,
            TemperatureBoundsMin, TemperatureBoundsMax,
            RunSpecies,
            ConditionDrainRate, ConditionRecoveryRate
        );
    }
}

/// <summary>
/// Static helper to generate config JSON.
/// Contains ONLY configuration - NO results.
/// Can be used before simulation runs or after.
/// </summary>
public static class ConfigExporter
{
    /// <summary>
    /// Export SimulationConfig to JSON string.
    /// </summary>
    public static string ToJson(SimulationConfig config)
    {
        if (config == null) return "{ \"error\": \"No configuration available\" }";

        return BuildConfigJson(
            config.DaysPerScenario, config.NumberOfScenarios, config.BiologyStep, config.RandomSeed,
            config.CarryingCapacityTier1,
            config.BaseTemperature, config.SeasonalAmplitude, config.ClimateTrend,
            config.InterannualVariation, config.VariabilityMagnitude, config.WarmingBias,
            config.Autocorrelated, config.DailyVariationRange, config.RandomnessGrowthRate,
            config.TemperatureBoundsMin, config.TemperatureBoundsMax,
            config.RunSpecies,
            config.ConditionDrainRate, config.ConditionRecoveryRate
        );
    }

    /// <summary>
    /// Export SimulationConfig to CSV string.
    /// </summary>
    public static string ToCsv(SimulationConfig config)
    {
        if (config == null) return "# Error,No configuration available";

        return BuildConfigCsv(
            config.DaysPerScenario, config.NumberOfScenarios, config.BiologyStep, config.RandomSeed,
            config.CarryingCapacityTier1,
            config.BaseTemperature, config.SeasonalAmplitude, config.ClimateTrend,
            config.InterannualVariation, config.VariabilityMagnitude, config.WarmingBias,
            config.Autocorrelated, config.DailyVariationRange, config.RandomnessGrowthRate,
            config.TemperatureBoundsMin, config.TemperatureBoundsMax,
            config.RunSpecies,
            config.ConditionDrainRate, config.ConditionRecoveryRate
        );
    }

    /// <summary>
    /// Build config JSON from parameters - NO RESULTS included.
    /// </summary>
    public static string BuildConfigJson(
        int daysPerScenario, int numberOfScenarios, int biologyStep, int randomSeed,
        float carryingCapacity,
        float baseTemperature, float seasonalAmplitude, float climateTrend,
        bool interannualVariation, float variabilityMagnitude, float warmingBias,
        bool autocorrelated, float dailyVariationRange, float randomnessGrowthRate,
        float temperatureBoundsMin, float temperatureBoundsMax,
        RunSpeciesList runSpecies,
        float conditionDrainRate = 0.15f, float conditionRecoveryRate = 0.10f)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"exportedAt\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
        sb.AppendLine();

        // Simulation timing
        sb.AppendLine("  \"simulation\": {");
        sb.AppendLine($"    \"daysPerScenario\": {daysPerScenario},");
        sb.AppendLine($"    \"numberOfScenarios\": {numberOfScenarios},");
        sb.AppendLine($"    \"biologyStep\": {biologyStep},");
        sb.AppendLine($"    \"randomSeed\": {randomSeed}");
        sb.AppendLine("  },");
        sb.AppendLine();

        // Carrying capacity (always on as of v11.1)
        sb.AppendLine("  \"carryingCapacity\": {");
        sb.AppendLine($"    \"tier1Limit\": {carryingCapacity}");
        sb.AppendLine("  },");
        sb.AppendLine();

        // Condition system
        sb.AppendLine("  \"conditionSystem\": {");
        sb.AppendLine($"    \"drainRate\": {conditionDrainRate},");
        sb.AppendLine($"    \"recoveryRate\": {conditionRecoveryRate}");
        sb.AppendLine("  },");
        sb.AppendLine();

        // Temperature settings - ALL of them
        sb.AppendLine("  \"temperature\": {");
        sb.AppendLine($"    \"base\": {baseTemperature},");
        sb.AppendLine($"    \"seasonalAmplitude\": {seasonalAmplitude},");
        sb.AppendLine($"    \"climateTrendPerYear\": {climateTrend},");
        sb.AppendLine($"    \"interannualVariation\": {interannualVariation.ToString().ToLower()},");
        sb.AppendLine($"    \"variabilityMagnitude\": {variabilityMagnitude},");
        sb.AppendLine($"    \"warmingBias\": {warmingBias},");
        sb.AppendLine($"    \"autocorrelated\": {autocorrelated.ToString().ToLower()},");
        sb.AppendLine($"    \"dailyVariationRange\": {dailyVariationRange},");
        sb.AppendLine($"    \"randomnessGrowthRate\": {randomnessGrowthRate},");
        sb.AppendLine($"    \"boundsMin\": {temperatureBoundsMin},");
        sb.AppendLine($"    \"boundsMax\": {temperatureBoundsMax}");
        sb.AppendLine("  },");
        sb.AppendLine();

        // Species data - FULL details
        sb.AppendLine("  \"species\": [");
        if (runSpecies != null && runSpecies.speciesList != null && runSpecies.speciesList.Count > 0)
        {
            for (int i = 0; i < runSpecies.speciesList.Count; i++)
            {
                var species = runSpecies.speciesList[i];
                bool isLast = (i == runSpecies.speciesList.Count - 1);

                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{species.speciesName}\",");
                sb.AppendLine($"      \"variant\": \"{species.variant}\",");
                sb.AppendLine($"      \"displayName\": \"{EscapeJson(species.displayName)}\",");
                sb.AppendLine($"      \"tier\": {species.tier},");
                sb.AppendLine($"      \"initialCount\": {species.count},");
                sb.AppendLine();
                sb.AppendLine("      \"biology\": {");
                sb.AppendLine($"        \"eatingAmount\": {species.eatingAmount},");
                sb.AppendLine($"        \"reproductionMultiplier\": {species.reproductionMultiplier},");
                sb.AppendLine($"        \"deathThreshold\": {species.deathThreshold},");
                sb.AppendLine($"        \"deathRate\": {species.deathRate},");
                sb.AppendLine($"        \"reproThreshold\": {species.reproThreshold},");
                sb.AppendLine($"        \"naturalDeathRate\": {species.naturalDeathRate},");
                sb.AppendLine($"        \"naturalDeathVariance\": {species.naturalDeathVariance},");
                sb.AppendLine($"        \"huntingEfficiency\": {species.huntingEfficiency},");
                sb.AppendLine($"        \"huntingVariance\": {species.huntingVariance}");
                sb.AppendLine("      },");
                sb.AppendLine();
                sb.AppendLine("      \"thermalCurve\": {");
                sb.AppendLine($"        \"optimalTempK\": {species.optimalTempK},");
                sb.AppendLine($"        \"optimalTempC\": {species.optimalTempK - 273.15f:F2},");
                sb.AppendLine($"        \"arrhenBreadth\": {species.arrhenBreadth},");
                sb.AppendLine($"        \"arrhenLower\": {species.arrhenLower},");
                sb.AppendLine($"        \"arrhenUpper\": {species.arrhenUpper},");
                sb.AppendLine($"        \"lowerBoundK\": {species.lowerBoundK},");
                sb.AppendLine($"        \"lowerBoundC\": {species.lowerBoundK - 273.15f:F2},");
                sb.AppendLine($"        \"upperBoundK\": {species.upperBoundK},");
                sb.AppendLine($"        \"upperBoundC\": {species.upperBoundK - 273.15f:F2},");
                sb.AppendLine($"        \"pmax\": {species.pmax},");
                sb.AppendLine($"        \"ctMinC\": {species.ctMinC},");
                sb.AppendLine($"        \"ctMaxC\": {species.ctMaxC},");
                sb.AppendLine($"        \"temperatureDebuff\": {species.TemperatureDebuff}");
                sb.AppendLine("      }");
                sb.AppendLine($"    }}{(isLast ? "" : ",")}");
            }
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Build config CSV from parameters - NO RESULTS included.
    /// </summary>
    public static string BuildConfigCsv(
        int daysPerScenario, int numberOfScenarios, int biologyStep, int randomSeed,
        float carryingCapacity,
        float baseTemperature, float seasonalAmplitude, float climateTrend,
        bool interannualVariation, float variabilityMagnitude, float warmingBias,
        bool autocorrelated, float dailyVariationRange, float randomnessGrowthRate,
        float temperatureBoundsMin, float temperatureBoundsMax,
        RunSpeciesList runSpecies,
        float conditionDrainRate = 0.15f, float conditionRecoveryRate = 0.10f)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== TINYSEA CONFIGURATION ===");
        sb.AppendLine($"# Exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("=== SIMULATION ===");
        sb.AppendLine($"Days Per Scenario,{daysPerScenario}");
        sb.AppendLine($"Number Of Scenarios,{numberOfScenarios}");
        sb.AppendLine($"Biology Step,{biologyStep}");
        sb.AppendLine($"Random Seed,{randomSeed}");
        sb.AppendLine();

        sb.AppendLine("=== CARRYING CAPACITY (always on as of v11.1) ===");
        sb.AppendLine($"Tier 1 Limit,{carryingCapacity}");
        sb.AppendLine();

        sb.AppendLine("=== CONDITION SYSTEM ===");
        sb.AppendLine($"Condition Drain Rate,{conditionDrainRate}");
        sb.AppendLine($"Condition Recovery Rate,{conditionRecoveryRate}");
        sb.AppendLine();

        sb.AppendLine("=== TEMPERATURE ===");
        sb.AppendLine($"Base Temperature,{baseTemperature}");
        sb.AppendLine($"Seasonal Amplitude,{seasonalAmplitude}");
        sb.AppendLine($"Climate Trend Per Year,{climateTrend}");
        sb.AppendLine($"Interannual Variation,{interannualVariation.ToString().ToLower()}");
        sb.AppendLine($"Variability Magnitude,{variabilityMagnitude}");
        sb.AppendLine($"Warming Bias,{warmingBias}");
        sb.AppendLine($"Autocorrelated,{autocorrelated.ToString().ToLower()}");
        sb.AppendLine($"Daily Variation Range,{dailyVariationRange}");
        sb.AppendLine($"Randomness Growth Rate,{randomnessGrowthRate}");
        sb.AppendLine($"Bounds Min,{temperatureBoundsMin}");
        sb.AppendLine($"Bounds Max,{temperatureBoundsMax}");
        sb.AppendLine();

        sb.AppendLine("=== SPECIES ===");
        if (runSpecies != null && runSpecies.speciesList != null && runSpecies.speciesList.Count > 0)
        {
            sb.AppendLine("Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier," +
                "DeathThreshold,DeathRate,ReproThreshold," +
                "NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance," +
                "OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper," +
                "LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC," +
                "Pmax,CTminC,CTmaxC,TemperatureDebuff");

            foreach (var species in runSpecies.speciesList)
            {
                string spName = !string.IsNullOrEmpty(species.displayName) ? species.displayName : species.speciesName.ToString();
                sb.AppendLine($"{spName},{species.variant},{species.tier},{species.count}," +
                    $"{species.eatingAmount},{species.reproductionMultiplier}," +
                    $"{species.deathThreshold},{species.deathRate},{species.reproThreshold}," +
                    $"{species.naturalDeathRate},{species.naturalDeathVariance}," +
                    $"{species.huntingEfficiency},{species.huntingVariance}," +
                    $"{species.optimalTempK},{species.optimalTempK - 273.15f:F2}," +
                    $"{species.arrhenBreadth},{species.arrhenLower},{species.arrhenUpper}," +
                    $"{species.lowerBoundK},{species.lowerBoundK - 273.15f:F2}," +
                    $"{species.upperBoundK},{species.upperBoundK - 273.15f:F2}," +
                    $"{species.pmax:F2},{species.ctMinC:F2},{species.ctMaxC:F2},{species.TemperatureDebuff:F2}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escape special characters for JSON string
    /// </summary>
    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}