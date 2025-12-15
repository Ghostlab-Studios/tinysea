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
    /// Status indicator for UI
    /// </summary>
    public string GetStatusIcon()
    {
        return Crashed ? "✗" : "✓";
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
    public int SurvivedScenarios;       // No crash
    public int CrashedScenarios;        // Had a crash
    
    // Configuration echo
    public int DaysPerScenario;
    public int BiologyStep;
    public float BaseTemperature;
    public float ClimateTrend;
    public bool UseCarryingCapacity;
    public float CarryingCapacity;
    
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
    public float CrashRate;             // Percentage of scenarios that crashed
    public float AvgCrashDay;           // Average day of crash (for crashed scenarios)
    
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
        
        // Averages
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
        
        // Handle edge case where all crashed
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
        return $"✓ {SurvivedScenarios} survived | ✗ {CrashedScenarios} crashed | " +
               $"Avg T1: {AvgFinalTier1Pop:N0} | Avg T2: {AvgFinalTier2Pop:N0}";
    }
    
    /// <summary>
    /// Get configuration summary line
    /// </summary>
    public string GetConfigLine()
    {
        return $"{DaysPerScenario} days × {TotalScenarios} scenarios";
    }
    
    /// <summary>
    /// Generate aggregate CSV for download
    /// </summary>
    public string ToAggregateCsv()
    {
        var sb = new System.Text.StringBuilder();
        
        // Header info
        sb.AppendLine("# TinySea Aggregate Results");
        sb.AppendLine($"# Generated: {CompletedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Configuration: {DaysPerScenario} days x {TotalScenarios} scenarios");
        sb.AppendLine($"# Base Temp: {BaseTemperature}C, Climate Trend: {ClimateTrend}C/year");
        sb.AppendLine();
        
        // Summary stats
        sb.AppendLine("=== SUMMARY ===");
        sb.AppendLine($"Scenarios Run,{TotalScenarios}");
        sb.AppendLine($"Survived,{SurvivedScenarios}");
        sb.AppendLine($"Crashed,{CrashedScenarios}");
        sb.AppendLine($"Crash Rate,{CrashRate:P1}");
        sb.AppendLine($"Avg Crash Day,{AvgCrashDay:F1}");
        sb.AppendLine();
        
        sb.AppendLine("=== POPULATION STATS (Survived Only) ===");
        sb.AppendLine($"Avg Final T1,{AvgFinalTier1Pop:F1}");
        sb.AppendLine($"Avg Final T2,{AvgFinalTier2Pop:F1}");
        sb.AppendLine($"Min Final T1,{MinFinalTier1Pop}");
        sb.AppendLine($"Max Final T1,{MaxFinalTier1Pop}");
        sb.AppendLine($"Min Final T2,{MinFinalTier2Pop}");
        sb.AppendLine($"Max Final T2,{MaxFinalTier2Pop}");
        sb.AppendLine();
        
        // Individual scenario summary table
        sb.AppendLine("=== INDIVIDUAL SCENARIOS ===");
        sb.AppendLine("Scenario,Seed,Crashed,CrashDay,FinalT1,FinalT2,AvgTemp");
        
        foreach (var s in Scenarios)
        {
            sb.AppendLine($"{s.ScenarioIndex},{s.RandomSeed},{s.Crashed},{s.CrashDay}," +
                         $"{s.FinalTier1Pop},{s.FinalTier2Pop},{s.AvgTemperature:F2}");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate configuration JSON for download
    /// </summary>
    public string ToConfigJson()
    {
        // Simple JSON format - can be expanded later
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"timestamp\": \"{CompletedAt:yyyy-MM-dd HH:mm:ss}\",");
        sb.AppendLine($"  \"daysPerScenario\": {DaysPerScenario},");
        sb.AppendLine($"  \"numberOfScenarios\": {TotalScenarios},");
        sb.AppendLine($"  \"biologyStep\": {BiologyStep},");
        sb.AppendLine($"  \"baseTemperature\": {BaseTemperature},");
        sb.AppendLine($"  \"climateTrend\": {ClimateTrend},");
        sb.AppendLine($"  \"useCarryingCapacity\": {UseCarryingCapacity.ToString().ToLower()},");
        sb.AppendLine($"  \"carryingCapacity\": {CarryingCapacity}");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
