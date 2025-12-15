using System;
using System.Collections.Generic;

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
        int survivedCount = 0;
        int crashedCount = 0;

        MinFinalTier1Pop = float.MaxValue;
        MaxFinalTier1Pop = float.MinValue;
        MinFinalTier2Pop = float.MaxValue;
        MaxFinalTier2Pop = float.MinValue;

        foreach (var scenario in Scenarios)
        {
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
        }

        if (crashedCount > 0)
        {
            AvgCrashDay = sumCrashDay / crashedCount;
        }

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

        sb.AppendLine("# TinySea Aggregate Results");
        sb.AppendLine($"# Generated: {CompletedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Configuration: {DaysPerScenario} days x {TotalScenarios} scenarios");
        sb.AppendLine($"# Base Temp: {BaseTemperature}C, Climate Trend: {ClimateTrend}C/year");
        sb.AppendLine($"# Carrying Capacity: {(UseCarryingCapacity ? CarryingCapacity.ToString() : "Disabled")}");
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
            RunSpecies
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
            config.RunSpecies
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
        RunSpeciesList runSpecies)
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
                sb.AppendLine($"        \"upperBoundC\": {species.upperBoundK - 273.15f:F2}");
                sb.AppendLine("      }");
                sb.AppendLine($"    }}{(isLast ? "" : ",")}");
            }
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

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