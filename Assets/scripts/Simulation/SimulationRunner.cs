using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;

/// <summary>
/// Data recorded each day of simulation.
/// All population and tracking values are FLOAT for research precision.
/// </summary>
public class StepRecord
{
    // Time
    public int Day;                 // Day number (1-365 for year 1)
    public int Year;                // Year number (1, 2, 3...)

    // Environment
    public float Temperature;       // Temperature in °C

    // Biology tracking
    public int BiologyCycle;        // Cumulative biology step counter (0 if no biology this day)

    // Population (FLOAT values for precision)
    public float Tier1Pop;
    public float Tier2Pop;
    public float Tier1Arctic;
    public float Tier1Common;
    public float Tier1Tropical;
    public float Tier2Arctic;
    public float Tier2Common;
    public float Tier2Tropical;

    // Biology step tracking (FLOAT, only valid when BiologyCycle > 0 for this day)
    public float Eaten;
    public float Deaths;
    public float Births;
    public float FedRate;

    public string ToCsvLine()
    {
        return $"{Day},{Year},{Temperature:F2},{BiologyCycle}," +
               $"{Tier1Pop:F2},{Tier2Pop:F2}," +
               $"{Tier1Arctic:F2},{Tier1Common:F2},{Tier1Tropical:F2}," +
               $"{Tier2Arctic:F2},{Tier2Common:F2},{Tier2Tropical:F2}," +
               $"{Eaten:F2},{Deaths:F2},{Births:F2},{FedRate:F2}";
    }

    public static string CsvHeader()
    {
        return "Day,Year,Temperature,BiologyCycle," +
               "Tier1Pop,Tier2Pop," +
               "Tier1Arctic,Tier1Common,Tier1Tropical," +
               "Tier2Arctic,Tier2Common,Tier2Tropical," +
               "Eaten,Deaths,Births,FedRate";
    }
}

/// <summary>
/// Main simulation runner.
/// 
/// Design:
/// - Temperature calculated EVERY day
/// - Biology runs every BiologyStep days (default: 1 = daily)
/// - Day numbering starts at 1 (Day 1-365 for Year 1)
/// - All populations are FLOAT for precision
/// - Species extinct when population < 1.0
/// - Modular design for easy multi-year extension
/// </summary>
public class SimulationRunner
{
    // Components
    public TemperatureCalculator TempCalc { get; private set; }
    public EcosystemSimulator Ecosystem { get; private set; }

    // Settings
    public int MaxYears = 1;
    public int BiologyStep = 1;  // Default: daily biology

    // Database reference (set before Run)
    public SpeciesDatabase SpeciesDB { get; set; }

    // Results
    private List<StepRecord> _records = new List<StepRecord>();
    private int _biologyCycleCounter = 0;

    // Crash tracking
    public bool HasCrashed { get; private set; } = false;
    public int CrashDay { get; private set; } = -1;
    public int CrashTier { get; private set; } = -1;

    public SimulationRunner(int seed = -1)
    {
        TempCalc = new TemperatureCalculator(seed);
        Ecosystem = new EcosystemSimulator();
    }

    /// <summary>
    /// Run the simulation for MaxYears.
    /// </summary>
    public void Run()
    {
        _records.Clear();
        _biologyCycleCounter = 0;
        HasCrashed = false;
        CrashDay = -1;
        CrashTier = -1;

        // Apply BiologyStep to ecosystem
        Ecosystem.BiologyStep = BiologyStep;

        // Initialize species from database if provided
        if (SpeciesDB != null)
        {
            Ecosystem.InitializeFromDatabase(SpeciesDB);
        }
        else
        {
            Debug.LogWarning("No SpeciesDatabase provided, using hardcoded defaults!");
            Ecosystem.InitializeDefaultSpecies();
        }

        int totalDays = MaxYears * TemperatureCalculator.DAYS_PER_YEAR;

        Debug.Log($"=== Starting Simulation: {MaxYears} year(s), {totalDays} days, BiologyStep={BiologyStep} ===");

        // Run day by day (0-indexed internally, but recorded as 1-indexed)
        for (int dayIndex = 0; dayIndex < totalDays; dayIndex++)
        {
            int displayDay = dayIndex + 1;  // Day 1-365
            int year = (dayIndex / TemperatureCalculator.DAYS_PER_YEAR) + 1;

            // Get temperature for this day
            float temp = TempCalc.GetTemperature(dayIndex);

            // Check if biology runs today
            // Day 1 runs biology, then every BiologyStep days after
            bool runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0);

            if (runBiology)
            {
                _biologyCycleCounter++;
                Ecosystem.ProcessBiologyStep(temp);
            }

            // Record this day
            RecordStep(displayDay, year, temp, runBiology);

            // Check for crash after biology step
            if (runBiology && Ecosystem.HasCrashed())
            {
                HasCrashed = true;
                CrashDay = displayDay;
                CrashTier = Ecosystem.GetCrashedTier();
                Debug.LogWarning($"=== ECOSYSTEM CRASH on Day {displayDay} (Year {year}) - Tier {CrashTier} extinct ===");
                break;
            }
        }

        // Log summary
        var lastRecord = _records.Count > 0 ? _records[_records.Count - 1] : null;
        Debug.Log($"=== Simulation Complete ===");
        Debug.Log($"Days simulated: {_records.Count}");
        Debug.Log($"Biology cycles: {_biologyCycleCounter}");
        Debug.Log($"Crashed: {HasCrashed} (Day: {CrashDay}, Tier: {CrashTier})");
        if (lastRecord != null)
        {
            Debug.Log($"Final populations: Tier1={lastRecord.Tier1Pop:F2}, Tier2={lastRecord.Tier2Pop:F2}");
        }
    }

    /// <summary>
    /// Record current state for a day.
    /// </summary>
    private void RecordStep(int day, int year, float temperature, bool biologyRan)
    {
        var record = new StepRecord
        {
            Day = day,
            Year = year,
            Temperature = temperature,
            BiologyCycle = biologyRan ? _biologyCycleCounter : 0,

            // Population values (FLOAT)
            Tier1Pop = Ecosystem.GetTier1Population(),
            Tier2Pop = Ecosystem.GetTier2Population(),
            Tier1Arctic = Ecosystem.GetVariantPopulation(1, ThermalVariant.Arctic),
            Tier1Common = Ecosystem.GetVariantPopulation(1, ThermalVariant.Common),
            Tier1Tropical = Ecosystem.GetVariantPopulation(1, ThermalVariant.Tropical),
            Tier2Arctic = Ecosystem.GetVariantPopulation(2, ThermalVariant.Arctic),
            Tier2Common = Ecosystem.GetVariantPopulation(2, ThermalVariant.Common),
            Tier2Tropical = Ecosystem.GetVariantPopulation(2, ThermalVariant.Tropical),

            // Tracking values (only meaningful when biologyRan = true)
            Eaten = biologyRan ? Ecosystem.LastTotalEaten : 0f,
            Deaths = biologyRan ? Ecosystem.LastTotalDeaths : 0f,
            Births = biologyRan ? Ecosystem.LastTotalBirths : 0f,
            FedRate = biologyRan ? Ecosystem.LastFedRate : 0f
        };

        _records.Add(record);
    }

    /// <summary>
    /// Get all records.
    /// </summary>
    public List<StepRecord> GetRecords()
    {
        return new List<StepRecord>(_records);
    }

    /// <summary>
    /// Generate CSV string.
    /// </summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(StepRecord.CsvHeader());

        foreach (var record in _records)
        {
            sb.AppendLine(record.ToCsvLine());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Save to file with timestamp.
    /// </summary>
    public string SaveToFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string crashSuffix = HasCrashed ? $"_crash_day{CrashDay}" : "";
        string filename = $"tinysea_{timestamp}{crashSuffix}.csv";
        string path = Path.Combine(directory, filename);

        File.WriteAllText(path, ToCsv());

        return path;
    }

    /// <summary>
    /// Get summary statistics for the simulation.
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

        // Calculate statistics
        var lastRecord = _records[_records.Count - 1];
        summary.FinalTier1Pop = lastRecord.Tier1Pop;
        summary.FinalTier2Pop = lastRecord.Tier2Pop;

        float maxT1 = 0, minT1 = float.MaxValue;
        float maxT2 = 0, minT2 = float.MaxValue;
        float tempSum = 0;

        foreach (var r in _records)
        {
            tempSum += r.Temperature;
            if (r.Tier1Pop > maxT1) maxT1 = r.Tier1Pop;
            if (r.Tier1Pop < minT1 && r.Tier1Pop >= 1f) minT1 = r.Tier1Pop;
            if (r.Tier2Pop > maxT2) maxT2 = r.Tier2Pop;
            if (r.Tier2Pop < minT2 && r.Tier2Pop >= 1f) minT2 = r.Tier2Pop;
        }

        summary.MaxTier1Pop = maxT1;
        summary.MinTier1Pop = minT1 == float.MaxValue ? 0 : minT1;
        summary.MaxTier2Pop = maxT2;
        summary.MinTier2Pop = minT2 == float.MaxValue ? 0 : minT2;
        summary.AvgTemperature = tempSum / _records.Count;

        return summary;
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
    public float FinalTier1Pop;
    public float FinalTier2Pop;
    public float MaxTier1Pop;
    public float MinTier1Pop;
    public float MaxTier2Pop;
    public float MinTier2Pop;
    public float AvgTemperature;

    public override string ToString()
    {
        return $"Days: {TotalDays}, Cycles: {TotalBiologyCycles}, " +
               $"Crashed: {Crashed} (Day {CrashDay}, Tier {CrashTier}), " +
               $"Final T1: {FinalTier1Pop:F2}, Final T2: {FinalTier2Pop:F2}, " +
               $"Max T1: {MaxTier1Pop:F2}, Max T2: {MaxTier2Pop:F2}, " +
               $"Avg Temp: {AvgTemperature:F1}°C";
    }
}
