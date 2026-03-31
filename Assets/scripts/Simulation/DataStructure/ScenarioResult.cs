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
        "Tier1Arctic", "Tier1Common", "Tier1Tropical",
        "Tier2Arctic", "Tier2Common", "Tier2Tropical"
    };

    public static readonly string[] VariantColumns = {
        "Tier1Arctic", "Tier1Common", "Tier1Tropical",
        "Tier2Arctic", "Tier2Common", "Tier2Tropical"
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

    // Carrying Capacity
    public bool UseCarryingCapacity;
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

        sb.AppendLine("=== TINYSEA AGGREGATE RESULTS ===");
        sb.AppendLine($"# Generated,{CompletedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Configuration,{DaysPerScenario} days x {TotalScenarios} scenarios");
        sb.AppendLine($"# Base Temp,{BaseTemperature}C");
        sb.AppendLine($"# Climate Trend,{ClimateTrend}C/year");
        sb.AppendLine($"# Carrying Capacity,{(UseCarryingCapacity ? CarryingCapacity.ToString() : "Disabled")}");
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

        sb.AppendLine("=== CONDITION STATS ===");
        sb.AppendLine($"Avg Condition T1 (All Scenarios),{AvgConditionT1:F3}");
        sb.AppendLine($"Avg Condition T2 (All Scenarios),{AvgConditionT2:F3}");
        sb.AppendLine($"Avg Final Condition T1 (Survived),{AvgFinalConditionT1:F3}");
        sb.AppendLine($"Avg Final Condition T2 (Survived),{AvgFinalConditionT2:F3}");
        sb.AppendLine();

        sb.AppendLine("=== INDIVIDUAL SCENARIOS ===");
        sb.AppendLine("Scenario,Seed,Crashed,CrashDay,CrashTier,FinalT1,FinalT2,T1Arctic,T1Common,T1Tropical,T2Arctic,T2Common,T2Tropical,AvgTemp,MinTemp,MaxTemp");

        foreach (var s in Scenarios)
        {
            sb.AppendLine($"{s.ScenarioIndex},{s.RandomSeed},{s.Crashed},{s.CrashDay},{s.CrashTier}," +
                         $"{s.FinalTier1Pop},{s.FinalTier2Pop}," +
                         $"{s.FinalTier1Arctic},{s.FinalTier1Common},{s.FinalTier1Tropical}," +
                         $"{s.FinalTier2Arctic},{s.FinalTier2Common},{s.FinalTier2Tropical}," +
                         $"{s.AvgTemperature:F2},{s.MinTemperature:F2},{s.MaxTemperature:F2}");
        }

        // Grand Mean across all runs
        bool hasPopStats = Scenarios.Any(s => s.PopMean != null);
        if (hasPopStats)
        {
            sb.AppendLine();
            sb.AppendLine("=== SUMMARY STATISTICS (Grand Mean Across All Runs) ===");
            sb.AppendLine("Statistic," + string.Join(",", ScenarioResult.PopColumns));

            string[] statNames = { "Mean", "Max", "Min", "StdDev" };
            foreach (var statName in statNames)
            {
                sb.Append($"GrandMean_{statName}");
                foreach (var col in ScenarioResult.PopColumns)
                {
                    double sum = 0;
                    int count = 0;
                    foreach (var s in Scenarios)
                    {
                        double val;
                        switch (statName)
                        {
                            case "Mean":
                                if (s.PopMean == null) continue;
                                val = s.PopMean.ContainsKey(col) ? s.PopMean[col] : 0; break;
                            case "Max":
                                if (s.PopMax == null) continue;
                                val = s.PopMax.ContainsKey(col) ? s.PopMax[col] : 0; break;
                            case "Min":
                                if (s.PopMin == null) continue;
                                val = s.PopMin.ContainsKey(col) ? s.PopMin[col] : 0; break;
                            case "StdDev":
                                if (s.PopStdDev == null) continue;
                                val = s.PopStdDev.ContainsKey(col) ? s.PopStdDev[col] : 0; break;
                            default: continue;
                        }
                        sum += val;
                        count++;
                    }
                    double grandMean = count > 0 ? sum / count : 0;
                    sb.Append($",{grandMean:F1}");
                }
                sb.AppendLine();
            }
        }

        // Extinction timing across all runs
        bool hasExtinction = Scenarios.Any(s => s.ExtinctionDay != null);
        if (hasExtinction)
        {
            sb.AppendLine();
            sb.AppendLine("=== EXTINCTION TIMING (Across All Runs) ===");
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

        return sb.ToString();
    }

    /// <summary>
    /// Generate configuration JSON - NO RESULTS, only config and species
    /// </summary>
    public string ToConfigJson()
    {
        return ConfigExporter.BuildConfigJson(
            DaysPerScenario, TotalScenarios, BiologyStep, RandomSeed,
            UseCarryingCapacity, CarryingCapacity,
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
            UseCarryingCapacity, CarryingCapacity,
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
            config.UseCarryingCapacity, config.CarryingCapacityTier1,
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
            config.UseCarryingCapacity, config.CarryingCapacityTier1,
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
        bool useCarryingCapacity, float carryingCapacity,
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

        // Carrying capacity
        sb.AppendLine("  \"carryingCapacity\": {");
        sb.AppendLine($"    \"enabled\": {useCarryingCapacity.ToString().ToLower()},");
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
                sb.AppendLine($"        \"minimumDeaths\": {species.minimumDeaths},");
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
        bool useCarryingCapacity, float carryingCapacity,
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

        sb.AppendLine("=== CARRYING CAPACITY ===");
        sb.AppendLine($"Enabled,{useCarryingCapacity.ToString().ToLower()}");
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
                "DeathThreshold,DeathRate,MinimumDeaths,ReproThreshold," +
                "NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance," +
                "OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper," +
                "LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC," +
                "Pmax,CTminC,CTmaxC,TemperatureDebuff");

            foreach (var species in runSpecies.speciesList)
            {
                string spName = !string.IsNullOrEmpty(species.displayName) ? species.displayName : species.speciesName.ToString();
                sb.AppendLine($"{spName},{species.variant},{species.tier},{species.count}," +
                    $"{species.eatingAmount},{species.reproductionMultiplier}," +
                    $"{species.deathThreshold},{species.deathRate},{species.minimumDeaths},{species.reproThreshold}," +
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