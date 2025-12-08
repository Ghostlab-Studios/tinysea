using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;

/// <summary>
/// Data recorded each day
/// </summary>
public class StepRecord
{
    public int Day;
    public int Year;
    public float Temperature;
    public bool BiologyStepRan;
    public int Tier1Pop;
    public int Tier2Pop;
    public int Tier1Arctic;
    public int Tier1Common;
    public int Tier1Tropical;
    public int Tier2Arctic;
    public int Tier2Common;
    public int Tier2Tropical;
    
    // Tracking (only valid when BiologyStepRan = true)
    public int Eaten;
    public int TempDeaths;
    public int Births;
    public float FedRate;

    public string ToCsvLine()
    {
        return $"{Day},{Year},{Temperature:F2},{(BiologyStepRan ? 1 : 0)}," +
               $"{Tier1Pop},{Tier2Pop}," +
               $"{Tier1Arctic},{Tier1Common},{Tier1Tropical}," +
               $"{Tier2Arctic},{Tier2Common},{Tier2Tropical}," +
               $"{Eaten},{TempDeaths},{Births},{FedRate:F2}";
    }

    public static string CsvHeader()
    {
        return "Day,Year,Temperature,BiologyStep," +
               "Tier1Pop,Tier2Pop," +
               "Tier1Arctic,Tier1Common,Tier1Tropical," +
               "Tier2Arctic,Tier2Common,Tier2Tropical," +
               "Eaten,TempDeaths,Births,FedRate";
    }
}

/// <summary>
/// Main simulation runner.
/// Records temperature EVERY day.
/// Runs biology step every N days.
/// </summary>
public class SimulationRunner
{
    // Components
    public TemperatureCalculator TempCalc { get; private set; }
    public EcosystemSimulator Ecosystem { get; private set; }

    // Settings
    public int MaxYears = 1;
    public int DaysPerBiologyStep = 5;

    // Database reference (set before Run)
    public SpeciesDatabase SpeciesDB { get; set; }

    // Results
    private List<StepRecord> _records = new List<StepRecord>();
    public bool HasCrashed { get; private set; } = false;
    public int CrashDay { get; private set; } = -1;

    public SimulationRunner(int seed = -1)
    {
        TempCalc = new TemperatureCalculator(seed);
        Ecosystem = new EcosystemSimulator();
        Ecosystem.DaysPerStep = DaysPerBiologyStep;
    }

    /// <summary>
    /// Run the simulation
    /// </summary>
    public void Run()
    {
        _records.Clear();
        HasCrashed = false;
        CrashDay = -1;

        // Initialize species from database if provided, otherwise use defaults
        if (SpeciesDB != null)
        {
            Ecosystem.InitializeFromDatabase(SpeciesDB);
        }
        else
        {
            UnityEngine.Debug.LogWarning("No SpeciesDatabase provided, using hardcoded defaults!");
            Ecosystem.InitializeDefaultSpecies();
        }

        int totalDays = MaxYears * TemperatureCalculator.DAYS_PER_YEAR;

        // Run day by day
        for (int day = 0; day < totalDays; day++)
        {
            // Get temperature for this day
            float temp = TempCalc.GetTemperature(day);

            // Check if biology runs today
            bool runBiology = (day > 0) && (day % DaysPerBiologyStep == 0);

            if (runBiology)
            {
                Ecosystem.ProcessBiologyStep(temp);
            }

            // Record EVERY day
            RecordStep(day, temp, runBiology);

            // Check for crash after biology step
            if (runBiology && Ecosystem.HasCrashed())
            {
                HasCrashed = true;
                CrashDay = day;
                break;
            }
        }
    }

    /// <summary>
    /// Record current state
    /// </summary>
    private void RecordStep(int day, float temperature, bool biologyRan)
    {
        var record = new StepRecord
        {
            Day = day,
            Year = TemperatureCalculator.GetYear(day),
            Temperature = temperature,
            BiologyStepRan = biologyRan,
            Tier1Pop = (int)Ecosystem.GetTier1Population(),
            Tier2Pop = (int)Ecosystem.GetTier2Population(),
            Tier1Arctic = (int)Ecosystem.GetVariantPopulation(1, ThermalVariant.Arctic),
            Tier1Common = (int)Ecosystem.GetVariantPopulation(1, ThermalVariant.Common),
            Tier1Tropical = (int)Ecosystem.GetVariantPopulation(1, ThermalVariant.Tropical),
            Tier2Arctic = (int)Ecosystem.GetVariantPopulation(2, ThermalVariant.Arctic),
            Tier2Common = (int)Ecosystem.GetVariantPopulation(2, ThermalVariant.Common),
            Tier2Tropical = (int)Ecosystem.GetVariantPopulation(2, ThermalVariant.Tropical),
            // Tracking values (only meaningful when biologyRan = true)
            Eaten = biologyRan ? Ecosystem.LastTotalEaten : 0,
            TempDeaths = biologyRan ? Ecosystem.LastTotalTempDeaths : 0,
            Births = biologyRan ? Ecosystem.LastTotalBirths : 0,
            FedRate = biologyRan ? Ecosystem.LastFedRate : 1f
        };

        _records.Add(record);
    }

    /// <summary>
    /// Get all records
    /// </summary>
    public List<StepRecord> GetRecords()
    {
        return new List<StepRecord>(_records);
    }

    /// <summary>
    /// Generate CSV string
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
    /// Save to file with timestamp
    /// </summary>
    public string SaveToFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"tinysea_{timestamp}.csv";
        string path = Path.Combine(directory, filename);

        File.WriteAllText(path, ToCsv());

        return path;
    }
}
