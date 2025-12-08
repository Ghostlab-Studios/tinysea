using UnityEngine;
using System.IO;
using System.Diagnostics;

/// <summary>
/// Unity MonoBehaviour to run TinySea simulation.
/// </summary>
public class SimulationController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SimulationConfig config;

    [Header("Simulation Settings")]
    [SerializeField] private int maxYears = 1;
    [SerializeField] private int daysPerBiologyStep = 5;
    [SerializeField] private int randomSeed = 12345;

    [Header("Output")]
    [SerializeField] private string outputFolderName = "TinySeaResults";
    [SerializeField] private bool openFileOnComplete = true;

    [Header("Status (Read Only)")]
    [SerializeField] private string lastOutputPath = "";
    [SerializeField] private bool lastRunCrashed = false;
    [SerializeField] private int lastCrashDay = -1;

    // Cached output directory
    private string OutputDirectory => Path.Combine(Application.persistentDataPath, outputFolderName);

    /// <summary>
    /// Run simulation using SimulationConfig values
    /// </summary>
    [ContextMenu("Run Simulation")]
    public void RunSimulation()
    {
        if (config == null)
        {
            UnityEngine.Debug.LogError("SimulationConfig not assigned! Please assign it in the Inspector.");
            return;
        }

        UnityEngine.Debug.Log("=== TinySea Simulation Starting ===");
        UnityEngine.Debug.Log($"Using config: {config.name}");
        UnityEngine.Debug.Log($"Output will be saved to: {OutputDirectory}");

        // Create runner
        var runner = new SimulationRunner(randomSeed);
        runner.MaxYears = maxYears;
        runner.DaysPerBiologyStep = daysPerBiologyStep;

        // Apply ALL values from SimulationConfig
        runner.TempCalc.BaseTemperature = config.BaseTemperature;
        runner.TempCalc.SeasonalAmplitude = config.SeasonalAmplitude;
        runner.TempCalc.ClimateTrendPerYear = config.ClimateTrend;
        runner.TempCalc.VariabilityMagnitude = config.variabilityMagnitude;
        runner.TempCalc.WarmingBias = config.warmingBias;
        runner.TempCalc.BaseRandomness = Mathf.Abs(config.RandomRangeMax - config.RandomRangeMin) / 2f;
        runner.TempCalc.RandomnessGrowthRate = config.randomnessGrowthRate;
        runner.TempCalc.UseAutocorrelation = config.Autocorrelated;
        runner.TempCalc.MinTemp = config.TempratureBoundsMin;
        runner.TempCalc.MaxTemp = config.TempratureBoundsMax;

        // Pass species database from config
        runner.SpeciesDB = config.Database;

        // Log config values being used
        UnityEngine.Debug.Log($"Config values: BaseTemp={config.BaseTemperature}, Seasonal={config.SeasonalAmplitude}, " +
                              $"Trend={config.ClimateTrend}, Bounds=[{config.TempratureBoundsMin}, {config.TempratureBoundsMax}]");
        
        if (config.Database != null)
        {
            UnityEngine.Debug.Log($"Using SpeciesDatabase: {config.Database.name} with {config.Database.speciesList.Count} species");
        }
        else
        {
            UnityEngine.Debug.LogWarning("No SpeciesDatabase assigned in config!");
        }

        // Run
        runner.Run();

        // Save
        lastOutputPath = runner.SaveToFile(OutputDirectory);

        // Update status
        lastRunCrashed = runner.HasCrashed;
        lastCrashDay = runner.CrashDay;

        // Log results
        var records = runner.GetRecords();
        UnityEngine.Debug.Log($"=== Simulation Complete ===");
        UnityEngine.Debug.Log($"Days recorded: {records.Count}");
        UnityEngine.Debug.Log($"Crashed: {lastRunCrashed} (Day: {lastCrashDay})");
        UnityEngine.Debug.Log($"File: {lastOutputPath}");

        // Print first 10 records
        UnityEngine.Debug.Log("=== First 10 days ===");
        for (int i = 0; i < Mathf.Min(10, records.Count); i++)
        {
            var r = records[i];
            string bio = r.BiologyStepRan ? " [BIO]" : "";
            UnityEngine.Debug.Log($"Day {r.Day}: Temp={r.Temperature:F1}C, T1={r.Tier1Pop:F1}, T2={r.Tier2Pop:F1}{bio}");
        }

        // Open file
        if (openFileOnComplete && !string.IsNullOrEmpty(lastOutputPath))
        {
            OpenFile(lastOutputPath);
        }
    }

    [ContextMenu("Open Last Results")]
    public void OpenLastResults()
    {
        if (string.IsNullOrEmpty(lastOutputPath) || !File.Exists(lastOutputPath))
        {
            UnityEngine.Debug.LogWarning("No results file found. Run simulation first.");
            return;
        }
        OpenFile(lastOutputPath);
    }

    [ContextMenu("Open Output Folder")]
    public void OpenOutputFolder()
    {
        if (!Directory.Exists(OutputDirectory))
        {
            Directory.CreateDirectory(OutputDirectory);
        }
        
        UnityEngine.Debug.Log($"Opening folder: {OutputDirectory}");
        
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Windows: Use explorer with the full path
        Process.Start("explorer.exe", OutputDirectory.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        Process.Start("open", OutputDirectory);
#else
        UnityEngine.Debug.Log($"Folder path: {OutputDirectory}");
#endif
    }

    [ContextMenu("Log Output Path")]
    public void LogOutputPath()
    {
        UnityEngine.Debug.Log($"Application.persistentDataPath: {Application.persistentDataPath}");
        UnityEngine.Debug.Log($"Output directory: {OutputDirectory}");
    }

    private void OpenFile(string path)
    {
        UnityEngine.Debug.Log($"Opening file: {path}");
        
        try
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Process.Start(new ProcessStartInfo 
            { 
                FileName = path.Replace("/", "\\"), 
                UseShellExecute = true 
            });
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            Process.Start("open", path);
#else
            UnityEngine.Debug.Log($"File path: {path}");
#endif
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to open file: {e.Message}");
        }
    }
}
