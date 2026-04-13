using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Unity MonoBehaviour to run TinySea simulation v6.
/// Reads configuration from SimulationConfig ScriptableObject.
/// 
/// v6 CHANGES:
/// - Runs multiple scenarios based on NumberOfScenarios
/// - Uses DaysPerScenario instead of MaxYears
/// - Connects to ResultsScreenUI for progress and results display
/// - Builds AggregateResults for statistical analysis
/// - Populates ALL config fields for export
/// </summary>
public class SimulationController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SimulationConfig config;

    [Header("Results Screen")]
    [SerializeField] private ResultsScreenUI resultsScreen;

    [Header("Output (Editor Only)")]
    [SerializeField] private string outputFolderName = "TinySeaResults";

    // State tracking
    private bool _isRunning = false;
    private bool _cancelRequested = false;
    private AggregateResults _currentResults;

    // Cached output directory (Editor only)
    private string OutputDirectory => Path.Combine(SavePaths.ResultsFolder, outputFolderName);

    /// <summary>
    /// Get the current SimulationConfig (for config export before simulation)
    /// </summary>
    public SimulationConfig Config => config;

    private void Awake()
    {
        // Force windowed mode on macOS standalone
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            Screen.fullScreen = false;
            Screen.SetResolution(1280, 720, false);
        }

        // Subscribe to results screen events
        if (resultsScreen != null)
        {
            resultsScreen.OnCancelRequested += OnCancelRequested;
            resultsScreen.OnCloseRequested += OnResultsClosed;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (resultsScreen != null)
        {
            resultsScreen.OnCancelRequested -= OnCancelRequested;
            resultsScreen.OnCloseRequested -= OnResultsClosed;
        }
    }

    /// <summary>
    /// Public method to start simulation - call this from UI buttons.
    /// </summary>
    public void StartSimulation()
    {
        if (_isRunning)
        {
            Debug.LogWarning("Simulation already running!");
            return;
        }

        // Validate config
        if (config == null)
        {
            Debug.LogError("SimulationConfig not assigned! Please assign it in the Inspector.");
            return;
        }

        string errorMessage;
        if (!config.IsValid(out errorMessage))
        {
            Debug.LogError($"Invalid configuration: {errorMessage}");
            return;
        }

        StartCoroutine(RunAllScenariosCoroutine());
    }

    /// <summary>
    /// Context menu for Editor testing
    /// </summary>
    [ContextMenu("Run Simulation")]
    public void RunSimulation()
    {
        StartSimulation();
    }

    /// <summary>
    /// Main coroutine that runs all scenarios
    /// </summary>
    private IEnumerator RunAllScenariosCoroutine()
    {
        _isRunning = true;
        _cancelRequested = false;

        // Show results screen in progress mode
        if (resultsScreen != null)
        {
            resultsScreen.Show();
        }

        // Wait one frame so the UI renders
        yield return null;

        // Config is already shown in UI — no console logging needed

        // Initialize aggregate results with ALL config parameters
        _currentResults = new AggregateResults
        {
            // Simulation timing
            TotalScenarios = config.NumberOfScenarios,
            DaysPerScenario = config.DaysPerScenario,
            BiologyStep = config.BiologyStep,
            RandomSeed = config.RandomSeed,

            // Carrying capacity
            UseCarryingCapacity = config.UseCarryingCapacity,
            CarryingCapacity = config.CarryingCapacityTier1,

            // Condition system
            ConditionDrainRate = config.ConditionDrainRate,
            ConditionRecoveryRate = config.ConditionRecoveryRate,

            // Temperature - Base
            BaseTemperature = config.BaseTemperature,
            SeasonalAmplitude = config.SeasonalAmplitude,

            // Temperature - Climate Trend
            ClimateTrend = config.ClimateTrend,
            InterannualVariation = config.InterannualVariation,
            VariabilityMagnitude = config.VariabilityMagnitude,
            WarmingBias = config.WarmingBias,

            // Temperature - Daily Variation
            Autocorrelated = config.Autocorrelated,
            DailyVariationRange = config.DailyVariationRange,
            RandomnessGrowthRate = config.RandomnessGrowthRate,

            // Temperature - Bounds
            TemperatureBoundsMin = config.TemperatureBoundsMin,
            TemperatureBoundsMax = config.TemperatureBoundsMax,

            // Species reference
            RunSpecies = config.RunSpecies,

            // Initialize scenarios list
            Scenarios = new List<ScenarioResult>()
        };

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: sequential (single-threaded WASM, must yield for UI)
        for (int i = 0; i < config.NumberOfScenarios; i++)
        {
            if (_cancelRequested) break;

            int scenarioIndex = i + 1;
            if (resultsScreen != null)
                resultsScreen.UpdateProgress(scenarioIndex, config.NumberOfScenarios);

            int scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i;
            var result = RunSingleScenario(scenarioIndex, scenarioSeed);
            _currentResults.Scenarios.Add(result);

            if (resultsScreen != null)
                resultsScreen.OnScenarioCompleted(result);

            yield return null;
        }
#else
        // Editor/Standalone: parallel scenarios using Task.Run for maximum speed.
        // Chunks by ProcessorCount to bound memory and allow UI updates.
        int totalScenarios = config.NumberOfScenarios;
        int parallelism = Math.Max(1, Environment.ProcessorCount - 1);
        var allResults = new ScenarioResult[totalScenarios];
        var taskErrors = new Exception[totalScenarios];

        for (int chunk = 0; chunk < totalScenarios; chunk += parallelism)
        {
            if (_cancelRequested) break;

            int chunkEnd = Math.Min(chunk + parallelism, totalScenarios);
            int chunkSize = chunkEnd - chunk;
            var tasks = new Task[chunkSize];

            for (int t = 0; t < chunkSize; t++)
            {
                int i = chunk + t;
                int scenarioIndex = i + 1;
                int scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i;
                int taskIndex = i;

                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        allResults[taskIndex] = RunSingleScenario(scenarioIndex, scenarioSeed);
                    }
                    catch (Exception ex)
                    {
                        taskErrors[taskIndex] = ex;
                    }
                });
            }

            // Wait for this chunk, updating UI
            var chunkDone = Task.WhenAll(tasks);
            while (!chunkDone.IsCompleted)
            {
                int done = 0;
                for (int t = 0; t < tasks.Length; t++)
                    if (tasks[t].IsCompleted) done++;

                if (resultsScreen != null)
                    resultsScreen.UpdateProgress(chunk + done, totalScenarios);

                yield return null;
            }

            // Collect results from this chunk
            for (int t = 0; t < chunkSize; t++)
            {
                int i = chunk + t;
                if (taskErrors[i] != null)
                    Debug.LogError($"Scenario {i + 1} failed: {taskErrors[i].Message}\n{taskErrors[i].StackTrace}");

                if (allResults[i] != null)
                {
                    _currentResults.Scenarios.Add(allResults[i]);
                    if (resultsScreen != null)
                        resultsScreen.OnScenarioCompleted(allResults[i]);
                }
            }

            if (resultsScreen != null)
                resultsScreen.UpdateProgress(chunkEnd, totalScenarios);

            yield return null;
        }
#endif

        // Calculate aggregates
        _currentResults.CompletedAt = System.DateTime.Now;
        _currentResults.CalculateAggregates();

        // Show results
        if (resultsScreen != null)
        {
            resultsScreen.DisplayResults(_currentResults);
        }

        _isRunning = false;
    }

    /// <summary>
    /// Public entry point for running a single scenario (used by BulkSimulationController).
    /// Reads parameters from the current SimulationConfig SO.
    /// </summary>
    public ScenarioResult RunSingleScenarioPublic(int scenarioIndex, int seed)
    {
        return RunSingleScenario(scenarioIndex, seed);
    }

    /// <summary>
    /// Run a single scenario with explicit batch parameters — does NOT touch any ScriptableObject.
    /// Used by BulkSimulationController to avoid mutating shared SOs.
    /// </summary>
    public ScenarioResult RunSingleScenarioFromBatch(
        BulkBatchConfig batch, RunSpeciesList tempSpecies, int scenarioIndex, int seed)
    {
        var runner = new SimulationRunner(seed);

        runner.TotalDays = batch.Days;
        runner.BiologyStep = config.BiologyStep;

        runner.TempCalc.BaseTemperature = batch.BaseTemp;
        runner.TempCalc.SeasonalAmplitude = batch.SeasonalAmp;
        runner.TempCalc.ClimateTrendPerYear = batch.ClimateTrend;
        runner.TempCalc.VariabilityMagnitude = batch.VariabilityMag;
        runner.TempCalc.WarmingBias = batch.WarmingBias;
        runner.TempCalc.BaseRandomness = batch.DailyVarRange;
        runner.TempCalc.RandomnessGrowthRate = batch.RandomnessGrowth;
        runner.TempCalc.UseAutocorrelation = batch.Autocorrelated;
        runner.TempCalc.MinTemp = batch.TempMin;
        runner.TempCalc.MaxTemp = batch.TempMax;

        runner.RunSpecies = tempSpecies;

        runner.Ecosystem.UseCarryingCapacity = batch.UseCarryingCap;
        runner.Ecosystem.CarryingCapacityPerTier = batch.CarryingCapT1;
        runner.Ecosystem.ConditionDrainRate = batch.ConditionDrainRate;
        runner.Ecosystem.ConditionRecoveryRate = batch.ConditionRecoveryRate;

        runner.Run();
        return runner.ToScenarioResult(scenarioIndex, batch.NumScenarios);
    }

    /// <summary>
    /// Run a single scenario and return results
    /// </summary>
    private ScenarioResult RunSingleScenario(int scenarioIndex, int seed)
    {
        // Create runner with seed
        var runner = new SimulationRunner(seed);

        // Apply simulation parameters
        runner.TotalDays = config.DaysPerScenario;
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

        // Pass species list
        runner.RunSpecies = config.RunSpecies;

        // Apply carrying capacity settings
        runner.Ecosystem.UseCarryingCapacity = config.UseCarryingCapacity;
        runner.Ecosystem.CarryingCapacityPerTier = config.CarryingCapacityTier1;

        // Apply condition system settings
        runner.Ecosystem.ConditionDrainRate = config.ConditionDrainRate;
        runner.Ecosystem.ConditionRecoveryRate = config.ConditionRecoveryRate;

        // Run the simulation
        runner.Run();

        // Convert to ScenarioResult
        return runner.ToScenarioResult(scenarioIndex, config.NumberOfScenarios);
    }

    /// <summary>
    /// Handle cancel request from results screen
    /// </summary>
    private void OnCancelRequested()
    {
        _cancelRequested = true;
    }

    private void OnResultsClosed()
    {
        _currentResults = null;
    }

    /// <summary>
    /// Check if simulation is currently running
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Get current aggregate results (null if not available)
    /// </summary>
    public AggregateResults GetCurrentResults() => _currentResults;

    // ==================== EDITOR UTILITIES ====================

    [ContextMenu("Open Output Folder")]
    public void OpenOutputFolder()
    {
#if UNITY_EDITOR
        if (!Directory.Exists(OutputDirectory))
        {
            Directory.CreateDirectory(OutputDirectory);
        }

        Debug.Log($"Opening folder: {OutputDirectory}");

#if UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start("explorer.exe", OutputDirectory.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX
        System.Diagnostics.Process.Start("open", OutputDirectory);
#endif
#endif
    }

    [ContextMenu("Log Config")]
    public void LogConfig()
    {
        if (config == null)
        {
            Debug.Log("No config assigned");
            return;
        }

        Debug.Log($"=== SimulationConfig: {config.name} ===");
        Debug.Log($"Days per Scenario: {config.DaysPerScenario}");
        Debug.Log($"Number of Scenarios: {config.NumberOfScenarios}");
        Debug.Log($"Biology Step: {config.BiologyStep}");
        Debug.Log($"Base Temperature: {config.BaseTemperature}C");
        Debug.Log($"Climate Trend: {config.ClimateTrend}C/year");
        Debug.Log($"Carrying Capacity: {config.UseCarryingCapacity} ({config.CarryingCapacityTier1})");
        Debug.Log($"Random Seed: {config.RandomSeed}");

        if (config.RunSpecies != null)
        {
            Debug.Log($"Species: {config.RunSpecies.speciesList?.Count ?? 0} configured");
        }
        else
        {
            Debug.Log("Species: None assigned!");
        }
    }

    [ContextMenu("Export Config JSON")]
    public void ExportConfigJson()
    {
        if (config == null)
        {
            Debug.LogError("No config assigned");
            return;
        }

        string json = ConfigExporter.ToJson(config);
        string filename = $"tinysea_config_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        string path = Path.Combine(SavePaths.ResultsFolder, filename);

        File.WriteAllText(path, json);
        Debug.Log($"Config exported to: {path}");

#if UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path.Replace("/", "\\"),
            UseShellExecute = true
        });
#elif UNITY_EDITOR_OSX
        System.Diagnostics.Process.Start("open", path);
#endif
    }
}