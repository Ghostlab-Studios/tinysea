using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Collections;

/// <summary>
/// Unity MonoBehaviour to run TinySea simulation v5.
/// Reads configuration from SimulationConfig ScriptableObject.
/// 
/// v5 CHANGES:
/// - Now uses RunSpeciesList instead of SpeciesDatabase
/// </summary>
public class SimulationController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SimulationConfig config;

    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingScreen;

    [Header("Output")]
    [SerializeField] private string outputFolderName = "TinySeaResults";
    [SerializeField] private bool openFileOnComplete = true;

    [Header("Status (Read Only)")]
    [SerializeField] private string lastOutputPath = "";
    [SerializeField] private bool lastRunCrashed = false;
    [SerializeField] private int lastCrashDay = -1;
    [SerializeField] private int lastCrashTier = -1;
    [SerializeField] private float lastFinalTier1Pop = 0f;
    [SerializeField] private float lastFinalTier2Pop = 0f;

    // Cached output directory
    private string OutputDirectory => Path.Combine(Application.persistentDataPath, outputFolderName);

    /// <summary>
    /// Public method to start simulation - call this from UI buttons.
    /// Uses coroutine to allow loading screen to render.
    /// </summary>
    public void StartSimulation()
    {
        StartCoroutine(RunSimulationCoroutine());
    }

    /// <summary>
    /// Run simulation using SimulationConfig values (coroutine version)
    /// </summary>
    [ContextMenu("Run Simulation")]
    public void RunSimulation()
    {
        // For Editor/ContextMenu use - starts the coroutine
        StartCoroutine(RunSimulationCoroutine());
    }

    /// <summary>
    /// Coroutine that runs simulation with loading screen support
    /// </summary>
    private IEnumerator RunSimulationCoroutine()
    {
        if (config == null)
        {
            UnityEngine.Debug.LogError("SimulationConfig not assigned! Please assign it in the Inspector.");
            yield break;
        }

        // Show loading screen
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        // Wait one frame so the loading screen actually renders
        yield return null;

        UnityEngine.Debug.Log("=== TinySea Simulation v5 Starting ===");
        UnityEngine.Debug.Log($"Using config: {config.name}");
        UnityEngine.Debug.Log($"Output will be saved to: {OutputDirectory}");

        // Create runner with seed from config
        var runner = new SimulationRunner(config.RandomSeed);

        // Apply simulation parameters from config
        runner.MaxYears = config.MaxYears;
        runner.BiologyStep = config.BiologyStep;

        // Apply temperature parameters
        runner.TempCalc.BaseTemperature = config.BaseTemperature;
        runner.TempCalc.SeasonalAmplitude = config.SeasonalAmplitude;
        runner.TempCalc.ClimateTrendPerYear = config.ClimateTrend;
        runner.TempCalc.VariabilityMagnitude = config.VariabilityMagnitude;
        runner.TempCalc.WarmingBias = config.WarmingBias;
        runner.TempCalc.BaseRandomness = config.DailyVariationRange;
        runner.TempCalc.RandomnessGrowthRate = config.RandomnessGrowthRate;
        runner.TempCalc.UseAutocorrelation = config.Autocorrelated;
        runner.TempCalc.MinTemp = config.TemperatureBoundsMin;
        runner.TempCalc.MaxTemp = config.TemperatureBoundsMax;

        // Pass RunSpeciesList from config (changed from Database)
        runner.RunSpecies = config.RunSpecies;

        // Apply carrying capacity settings
        runner.Ecosystem.UseCarryingCapacity = config.UseCarryingCapacity;
        runner.Ecosystem.CarryingCapacityPerTier = config.CarryingCapacityTier1;

        // Log config values being used
        UnityEngine.Debug.Log($"Config: BiologyStep={config.BiologyStep}, MaxYears={config.MaxYears}");
        UnityEngine.Debug.Log($"Carrying Capacity (Tier 1 only): {config.UseCarryingCapacity} (limit={config.CarryingCapacityTier1})");
        UnityEngine.Debug.Log($"Temperature: Base={config.BaseTemperature}°C, Seasonal=±{config.SeasonalAmplitude}°C, " +
                              $"Trend={config.ClimateTrend}°C/year, Bounds=[{config.TemperatureBoundsMin}, {config.TemperatureBoundsMax}]");

        if (config.RunSpecies != null)
        {
            UnityEngine.Debug.Log($"Using RunSpeciesList: {config.RunSpecies.name} with {config.RunSpecies.speciesList.Count} species");
        }
        else
        {
            UnityEngine.Debug.LogWarning("No RunSpeciesList assigned in config - using defaults!");
        }

        // Run simulation
        runner.Run();

        // Hide loading screen before file save/download
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(false);
        }

        // Save results
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string crashSuffix = runner.HasCrashed ? "_crash_day" + runner.CrashDay : "";
        string filename = "tinysea_v5_" + timestamp + crashSuffix + ".csv";

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: trigger browser download instead of writing to persistentDataPath
        string csv = runner.ToCsv();
        WebGLDownload.DownloadCsv(filename, csv);
        lastOutputPath = filename; // just store the name for UI/status
#else
        // Desktop: keep your current behavior
        lastOutputPath = runner.SaveToFile(OutputDirectory);
#endif

        // Update status
        lastRunCrashed = runner.HasCrashed;
        lastCrashDay = runner.CrashDay;
        lastCrashTier = runner.CrashTier;

        // Get summary
        var summary = runner.GetSummary();
        if (summary != null)
        {
            lastFinalTier1Pop = summary.FinalTier1Pop;
            lastFinalTier2Pop = summary.FinalTier2Pop;
        }

        // Log results
        var records = runner.GetRecords();
        UnityEngine.Debug.Log($"=== Simulation Complete ===");
        UnityEngine.Debug.Log($"Days recorded: {records.Count}");
        UnityEngine.Debug.Log($"Biology cycles: {summary?.TotalBiologyCycles ?? 0}");
        UnityEngine.Debug.Log($"Crashed: {lastRunCrashed} (Day: {lastCrashDay}, Tier: {lastCrashTier})");
        UnityEngine.Debug.Log($"Final populations: Tier1={lastFinalTier1Pop:F2}, Tier2={lastFinalTier2Pop:F2}");
        UnityEngine.Debug.Log($"File: {lastOutputPath}");

        // Print first and last few records
        PrintRecordSamples(records);

        // Open file if requested
        if (openFileOnComplete && !string.IsNullOrEmpty(lastOutputPath))
        {
            OpenFile(lastOutputPath);
        }
    }

    private void PrintRecordSamples(System.Collections.Generic.List<StepRecord> records)
    {
        if (records.Count == 0) return;

        UnityEngine.Debug.Log("=== First 5 days ===");
        for (int i = 0; i < Mathf.Min(5, records.Count); i++)
        {
            var r = records[i];
            string bio = r.BiologyCycle > 0 ? $" [Cycle {r.BiologyCycle}]" : "";
            UnityEngine.Debug.Log($"Day {r.Day}: Temp={r.Temperature:F1}°C, T1={r.Tier1Pop}, T2={r.Tier2Pop}{bio}");
        }

        if (records.Count > 10)
        {
            UnityEngine.Debug.Log("...");
            UnityEngine.Debug.Log("=== Last 5 days ===");
            for (int i = records.Count - 5; i < records.Count; i++)
            {
                var r = records[i];
                string bio = r.BiologyCycle > 0 ? $" [Cycle {r.BiologyCycle}]" : "";
                UnityEngine.Debug.Log($"Day {r.Day}: Temp={r.Temperature:F1}°C, T1={r.Tier1Pop}, T2={r.Tier2Pop}{bio}");
            }
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