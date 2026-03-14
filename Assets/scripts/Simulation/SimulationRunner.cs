using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

/// <summary>
/// Data recorded each biology step.
/// 
/// CSV COLUMNS:
/// - Day, Year, Temperature, BiologyCycle
/// - StartPop, EndPop: Total population (T1+T2)
/// - Tier1Pop, Tier2Pop, Tier1Arctic...Tier2Tropical: Populations by tier/variant
/// - EatenT1: Prey eaten (Tier 1 deaths from predation)
/// - TempDeathsT1, TempDeathsT2: Thermal deaths
/// - NaturalDeathsT1, NaturalDeathsT2: Natural mortality deaths
/// - TotalDeaths: All deaths combined
/// - BirthsT1, BirthsT2: New offspring
/// - FedRateT2, AvgHuntingEff: Feeding metrics
/// - BirthAccumT1, BirthAccumT2: Birth accumulator totals
/// - NaturalDeathAccumT1, NaturalDeathAccumT2: Natural death accumulator totals
/// - PredationAccumT1: Predation accumulator total for Tier 1
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

    // Death tracking (using long to prevent overflow)
    public long EatenT1;           // Prey eaten = T1 deaths from predation
    public long TempDeathsT1;      // Temperature deaths T1
    public long TempDeathsT2;      // Temperature deaths T2
    public long NaturalDeathsT1;   // Natural deaths T1
    public long NaturalDeathsT2;   // Natural deaths T2
    public long TotalDeaths;       // ALL deaths combined

    // Birth tracking (using long to prevent overflow)
    public long BirthsT1;
    public long BirthsT2;

    // Feeding tracking
    public float FedRateT2;
    public float AvgHuntingEff;

    // Accumulator tracking (float values for transparency)
    public float BirthAccumT1;
    public float BirthAccumT2;
    public float NaturalDeathAccumT1;
    public float NaturalDeathAccumT2;
    public float PredationAccumT1;

    public string ToCsvLine()
    {
        return $"{Day},{Year},{Temperature:F2},{BiologyCycle}," +
               $"{StartPop},{EndPop}," +
               $"{Tier1Pop},{Tier2Pop}," +
               $"{Tier1Arctic},{Tier1Common},{Tier1Tropical}," +
               $"{Tier2Arctic},{Tier2Common},{Tier2Tropical}," +
               $"{EatenT1},{TempDeathsT1},{TempDeathsT2}," +
               $"{NaturalDeathsT1},{NaturalDeathsT2}," +
               $"{TotalDeaths}," +
               $"{BirthsT1},{BirthsT2}," +
               $"{FedRateT2:F3},{AvgHuntingEff:F3}," +
               $"{BirthAccumT1:F3},{BirthAccumT2:F3}," +
               $"{NaturalDeathAccumT1:F3},{NaturalDeathAccumT2:F3}," +
               $"{PredationAccumT1:F3}";
    }

    public static string CsvHeader()
    {
        return "Day,Year,Temperature,BiologyCycle," +
               "StartPop,EndPop," +
               "Tier1Pop,Tier2Pop," +
               "Tier1Arctic,Tier1Common,Tier1Tropical," +
               "Tier2Arctic,Tier2Common,Tier2Tropical," +
               "EatenT1,TempDeathsT1,TempDeathsT2," +
               "NaturalDeathsT1,NaturalDeathsT2," +
               "TotalDeaths," +
               "BirthsT1,BirthsT2," +
               "FedRateT2,AvgHuntingEff," +
               "BirthAccumT1,BirthAccumT2," +
               "NaturalDeathAccumT1,NaturalDeathAccumT2," +
               "PredationAccumT1";
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

        Debug.Log($"=== Starting Simulation: {TotalDays} days, BiologyStep={BiologyStep} ===");

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
                Debug.LogWarning($"=== ECOSYSTEM CRASH on Day {displayDay} (Year {year}) - Tier {CrashTier} extinct ===");
                break;
            }
        }

        var lastRecord = _records.Count > 0 ? _records[_records.Count - 1] : null;
        Debug.Log($"=== Simulation Complete ===");
        Debug.Log($"Days simulated: {_records.Count}");
        Debug.Log($"Biology cycles: {_biologyCycleCounter}");
        Debug.Log($"Crashed: {HasCrashed} (Day: {CrashDay}, Tier: {CrashTier})");
        if (lastRecord != null)
        {
            Debug.Log($"Final populations: Tier1={lastRecord.Tier1Pop}, Tier2={lastRecord.Tier2Pop}");
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
        float naturalDeathsT1 = biologyRan ? Ecosystem.LastNaturalDeathsT1 : 0f;
        float naturalDeathsT2 = biologyRan ? Ecosystem.LastNaturalDeathsT2 : 0f;

        // TotalDeaths = all death sources combined
        float totalDeaths = eatenT1 + tempDeathsT1 + tempDeathsT2 +
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

            // Death tracking - using long to prevent overflow
            EatenT1 = (long)Math.Round(eatenT1),
            TempDeathsT1 = (long)Math.Round(tempDeathsT1),
            TempDeathsT2 = (long)Math.Round(tempDeathsT2),
            NaturalDeathsT1 = (long)Math.Round(naturalDeathsT1),
            NaturalDeathsT2 = (long)Math.Round(naturalDeathsT2),
            TotalDeaths = (long)Math.Round(totalDeaths),

            // Birth tracking - using long to prevent overflow
            BirthsT1 = biologyRan ? (long)Math.Round(Ecosystem.LastBirthsT1) : 0,
            BirthsT2 = biologyRan ? (long)Math.Round(Ecosystem.LastBirthsT2) : 0,

            // Feeding tracking
            FedRateT2 = biologyRan ? Ecosystem.LastFedRateT2 : 0f,
            AvgHuntingEff = biologyRan ? Ecosystem.LastAvgHuntingEfficiency : 0f,

            // Accumulator tracking
            BirthAccumT1 = Ecosystem.BirthAccumT1,
            BirthAccumT2 = Ecosystem.BirthAccumT2,
            NaturalDeathAccumT1 = Ecosystem.NaturalDeathAccumT1,
            NaturalDeathAccumT2 = Ecosystem.NaturalDeathAccumT2,
            PredationAccumT1 = Ecosystem.PredationAccumT1
        };

        _records.Add(record);
    }

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
        sb.AppendLine($"#config:use_carrying_capacity,{Ecosystem.UseCarryingCapacity.ToString().ToLower()}");
        sb.AppendLine($"#config:carrying_capacity_tier1,{Ecosystem.CarryingCapacityPerTier}");

        sb.AppendLine("#");
        if (RunSpecies != null && RunSpecies.speciesList != null && RunSpecies.speciesList.Count > 0)
        {
            sb.AppendLine("#species:Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier," +
                "DeathThreshold,DeathRate,MinimumDeaths,ReproThreshold," +
                "NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance," +
                "OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper," +
                "LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC," +
                "Pmax,CTminC,CTmaxC,TemperatureDebuff");
            foreach (var sp in RunSpecies.speciesList)
            {
                string spName = !string.IsNullOrEmpty(sp.displayName) ? sp.displayName : sp.speciesName.ToString();
                sb.AppendLine($"#species:{spName},{sp.variant},{sp.tier},{sp.count}," +
                    $"{sp.eatingAmount},{sp.reproductionMultiplier}," +
                    $"{sp.deathThreshold},{sp.deathRate},{sp.minimumDeaths},{sp.reproThreshold}," +
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

        sb.AppendLine(StepRecord.CsvHeader());
        foreach (var record in _records)
        {
            sb.AppendLine(record.ToCsvLine());
        }

        // Append summary statistics and extinction timing
        if (_records.Count > 0)
        {
            var stats = populationStats ?? ComputePopulationStats();

            // Summary statistics block
            sb.AppendLine("#");
            sb.AppendLine("#summary:Statistic," + string.Join(",", ScenarioResult.PopColumns));

            sb.Append("#summary:Mean");
            foreach (var col in ScenarioResult.PopColumns)
                sb.Append($",{stats.Mean[col]:F1}");
            sb.AppendLine();

            sb.Append("#summary:Max");
            foreach (var col in ScenarioResult.PopColumns)
                sb.Append($",{stats.Max[col]}");
            sb.AppendLine();

            sb.Append("#summary:Min");
            foreach (var col in ScenarioResult.PopColumns)
                sb.Append($",{stats.Min[col]}");
            sb.AppendLine();

            sb.Append("#summary:StdDev");
            foreach (var col in ScenarioResult.PopColumns)
                sb.Append($",{stats.StdDev[col]:F1}");
            sb.AppendLine();

            // Extinction timing block
            sb.AppendLine("#");
            sb.AppendLine("#extinction:Variant,DayReachedZero");
            foreach (var variant in ScenarioResult.VariantColumns)
            {
                sb.AppendLine($"#extinction:{variant},{stats.ExtinctionDay[variant]}");
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
