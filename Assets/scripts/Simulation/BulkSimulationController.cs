using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Orchestrates bulk CSV batch simulation.
/// Subscribes to CsvUploadHandler.OnRunBulkSimulation, overrides SimulationConfig/RunSpeciesList
/// for each batch, runs all scenarios, and packages results as a ZIP download.
/// Uses ResultsScreenUI for progress display and completion state.
///
/// MEMORY OPTIMIZATION: Uses WebGLZipDownload's progressive API to stream each
/// scenario's CSV data to the ZIP immediately after it finishes running.
/// In WebGL, this moves the string from the 512 MB WASM heap to the JS heap,
/// then nulls the C# reference so GC can reclaim the WASM memory.
/// Peak C# memory = one scenario's CSV at a time (~3-6 MB) instead of all at once.
/// </summary>
public class BulkSimulationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private CsvUploadHandler csvUploadHandler;
    [SerializeField] private ResultsScreenUI resultsScreen;

    [Header("Upload Overlay UI")]
    [SerializeField] private GameObject uploadOverlayPanel;

    private bool _isRunning = false;
    private bool _cancelRequested = false;
    private Coroutine _runCoroutine;

    private void Start()
    {
        if (csvUploadHandler != null)
            csvUploadHandler.OnRunBulkSimulation += OnRunBulkSimulation;

        if (resultsScreen != null)
        {
            resultsScreen.OnCancelRequested += OnCancelRequested;
            resultsScreen.OnCloseRequested += OnBulkResultsClosed;
        }
    }

    private void OnDestroy()
    {
        if (csvUploadHandler != null)
            csvUploadHandler.OnRunBulkSimulation -= OnRunBulkSimulation;

        if (resultsScreen != null)
        {
            resultsScreen.OnCancelRequested -= OnCancelRequested;
            resultsScreen.OnCloseRequested -= OnBulkResultsClosed;
        }
    }

    private void OnRunBulkSimulation(List<BulkBatchConfig> batches)
    {
        if (_isRunning)
        {
            Debug.LogWarning("Bulk simulation already running!");
            return;
        }

        _runCoroutine = StartCoroutine(RunAllBatches(batches));
    }

    private IEnumerator RunAllBatches(List<BulkBatchConfig> batches)
    {
        _isRunning = true;
        _cancelRequested = false;

        // Hide upload overlay
        if (uploadOverlayPanel != null)
            uploadOverlayPanel.SetActive(false);

        // Show ResultsScreenUI with progress
        if (resultsScreen != null)
            resultsScreen.Show();

        yield return null;

        var config = simulationController.Config;
        if (config == null)
        {
            if (resultsScreen != null)
                resultsScreen.UpdateBulkProgress("Error: SimulationConfig not assigned!", 0f);
            _isRunning = false;
            yield break;
        }

        var runSpecies = config.RunSpecies;
        if (runSpecies == null)
        {
            if (resultsScreen != null)
                resultsScreen.UpdateBulkProgress("Error: RunSpeciesList not assigned!", 0f);
            _isRunning = false;
            yield break;
        }

        // Save original SO values so we can restore after bulk run
        int origDays = config.DaysPerScenario;
        int origScenarios = config.NumberOfScenarios;
        float origBaseTemp = config.BaseTemperature;
        float origSeasonalAmp = config.SeasonalAmplitude;
        float origClimateTrend = config.ClimateTrend;
        float origVarMag = config.VariabilityMagnitude;
        float origWarmBias = config.WarmingBias;
        float origDailyVar = config.DailyVariationRange;
        float origRandGrowth = config.RandomnessGrowthRate;
        bool origAutocorr = config.Autocorrelated;
        bool origInterannual = config.InterannualVariation;
        float origTempMin = config.TemperatureBoundsMin;
        float origTempMax = config.TemperatureBoundsMax;
        bool origUseCarry = config.UseCarryingCapacity;
        float origCarryT1 = config.CarryingCapacityTier1;
        float origCondDrain = config.ConditionDrainRate;
        float origCondRecovery = config.ConditionRecoveryRate;
        var origSpeciesList = new List<SpeciesData>(runSpecies.speciesList);

        // Storage strategy: WebGL uses server upload (S3), Editor uses progressive ZIP
        bool useServerUpload = ServerUpload.IsAvailable;

        if (useServerUpload)
        {
            // Create a server session for S3 uploads
            bool sessionOk = false;
            yield return ServerUpload.CreateSession((ok, id) => sessionOk = ok);
            if (!sessionOk)
            {
                Debug.LogWarning("Server upload unavailable, falling back to progressive ZIP");
                useServerUpload = false;
            }
        }

        if (!useServerUpload)
        {
            // Fallback: progressive ZIP (files stream to JS heap or disk)
            string zipFilename = $"tinysea_bulk_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip";
            WebGLZipDownload.InitProgressiveZip(zipFilename);
        }

        // Calculate total scenarios across all batches
        int totalScenarios = 0;
        foreach (var batch in batches)
            totalScenarios += batch.NumScenarios;

        int completedScenarios = 0;

        for (int b = 0; b < batches.Count; b++)
        {
            if (_cancelRequested)
                break;

            var batch = batches[b];
            string batchFolder = batch.BatchName;

            // Update progress at batch start
            if (resultsScreen != null)
            {
                float progress = totalScenarios > 0 ? (float)completedScenarios / totalScenarios : 0f;
                resultsScreen.UpdateBulkProgress(
                    $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) — 0 of {batch.NumScenarios}",
                    progress);
            }
            yield return null;

            if (_cancelRequested)
                break;

            // Override SimulationConfig SO fields
            config.DaysPerScenario = batch.Days;
            config.NumberOfScenarios = batch.NumScenarios;
            config.BaseTemperature = batch.BaseTemp;
            config.SeasonalAmplitude = batch.SeasonalAmp;
            config.ClimateTrend = batch.ClimateTrend;
            config.VariabilityMagnitude = batch.VariabilityMag;
            config.WarmingBias = batch.WarmingBias;
            config.DailyVariationRange = batch.DailyVarRange;
            config.RandomnessGrowthRate = batch.RandomnessGrowth;
            config.Autocorrelated = batch.Autocorrelated;
            config.InterannualVariation = batch.InterannualVariation;
            config.TemperatureBoundsMin = batch.TempMin;
            config.TemperatureBoundsMax = batch.TempMax;
            config.UseCarryingCapacity = batch.UseCarryingCap;
            config.CarryingCapacityTier1 = batch.CarryingCapT1;
            config.ConditionDrainRate = batch.ConditionDrainRate;
            config.ConditionRecoveryRate = batch.ConditionRecoveryRate;

            // Override RunSpeciesList SO
            runSpecies.speciesList.Clear();
            for (int i = 0; i < batch.Species.Count; i++)
                runSpecies.speciesList.Add(ConvertSpecies(batch.Species[i], i));

            // Build AggregateResults for this batch
            var batchResults = new AggregateResults
            {
                TotalScenarios = batch.NumScenarios,
                DaysPerScenario = batch.Days,
                BiologyStep = config.BiologyStep,
                RandomSeed = config.RandomSeed,
                UseCarryingCapacity = batch.UseCarryingCap,
                CarryingCapacity = batch.CarryingCapT1,
                ConditionDrainRate = batch.ConditionDrainRate,
                ConditionRecoveryRate = batch.ConditionRecoveryRate,
                BaseTemperature = batch.BaseTemp,
                SeasonalAmplitude = batch.SeasonalAmp,
                ClimateTrend = batch.ClimateTrend,
                InterannualVariation = batch.InterannualVariation,
                VariabilityMagnitude = batch.VariabilityMag,
                WarmingBias = batch.WarmingBias,
                Autocorrelated = batch.Autocorrelated,
                DailyVariationRange = batch.DailyVarRange,
                RandomnessGrowthRate = batch.RandomnessGrowth,
                TemperatureBoundsMin = batch.TempMin,
                TemperatureBoundsMax = batch.TempMax,
                RunSpecies = runSpecies,
                Scenarios = new List<ScenarioResult>()
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: sequential (single-threaded WASM, must yield for UI)
            for (int s = 0; s < batch.NumScenarios; s++)
            {
                if (_cancelRequested) break;

                int scenarioIndex = s + 1;
                int seed = config.RandomSeed < 0 ? -1 : config.RandomSeed + s;

                if (resultsScreen != null)
                {
                    float progress = totalScenarios > 0 ? (float)completedScenarios / totalScenarios : 0f;
                    resultsScreen.UpdateBulkProgress(
                        $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) — {scenarioIndex} of {batch.NumScenarios}",
                        progress);
                }
                yield return null;
                if (_cancelRequested) break;

                var result = simulationController.RunSingleScenarioPublic(scenarioIndex, seed);
                batchResults.Scenarios.Add(result);

                if (!string.IsNullOrEmpty(result.CsvData))
                {
                    string csvPath = $"{batchFolder}/scenario_{result.ScenarioIndex}.csv";
                    if (useServerUpload)
                        yield return ServerUpload.UploadFile(csvPath, result.CsvData);
                    else
                        WebGLZipDownload.AddFileToProgressiveZip(csvPath, result.CsvData);
                    result.CsvData = null;
                }
                completedScenarios++;
            }
#else
            // Editor/Standalone: parallel scenarios using background threads.
            // Each scenario creates its own SimulationRunner — no shared mutable state.
            int parallelism = Math.Max(1, Environment.ProcessorCount - 1);
            int batchSize = batch.NumScenarios;
            var scenarioResults = new ScenarioResult[batchSize];
            var taskErrors = new Exception[batchSize];

            // Launch all scenarios as parallel tasks
            var tasks = new Task[batchSize];
            for (int s = 0; s < batchSize; s++)
            {
                int scenarioIndex = s + 1;
                int seed = config.RandomSeed < 0 ? -1 : config.RandomSeed + s;
                int taskIndex = s;

                tasks[s] = Task.Run(() =>
                {
                    try
                    {
                        scenarioResults[taskIndex] = simulationController.RunSingleScenarioPublic(scenarioIndex, seed);
                    }
                    catch (Exception ex)
                    {
                        taskErrors[taskIndex] = ex;
                    }
                });
            }

            // Wait for all tasks, yielding to Unity each frame for UI updates
            var allDone = Task.WhenAll(tasks);
            int lastReported = 0;
            while (!allDone.IsCompleted)
            {
                // Count completed tasks for progress
                int done = 0;
                for (int t = 0; t < tasks.Length; t++)
                    if (tasks[t].IsCompleted) done++;

                if (done > lastReported)
                {
                    lastReported = done;
                    if (resultsScreen != null)
                    {
                        float progress = totalScenarios > 0 ? (float)(completedScenarios + done) / totalScenarios : 0f;
                        resultsScreen.UpdateBulkProgress(
                            $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) — {done} of {batchSize} complete ({parallelism} threads)",
                            progress);
                    }
                }
                yield return null;
            }

            // Log any task errors
            for (int s = 0; s < batchSize; s++)
            {
                if (taskErrors[s] != null)
                    Debug.LogError($"Scenario {s + 1} in batch '{batch.BatchName}' failed: {taskErrors[s].Message}\n{taskErrors[s].StackTrace}");
            }

            // Collect results and stream CSVs (main thread for file I/O)
            for (int s = 0; s < batchSize; s++)
            {
                var result = scenarioResults[s];
                if (result == null) continue;

                batchResults.Scenarios.Add(result);

                if (!string.IsNullOrEmpty(result.CsvData))
                {
                    string csvPath = $"{batchFolder}/scenario_{result.ScenarioIndex}.csv";
                    WebGLZipDownload.AddFileToProgressiveZip(csvPath, result.CsvData);
                    result.CsvData = null;
                }
            }
            completedScenarios += batchSize;
#endif

            // Stream aggregate + config CSVs for this batch
            if (batchResults.Scenarios.Count > 0)
            {
                batchResults.CompletedAt = DateTime.Now;
                batchResults.CalculateAggregates();

                string aggCsv = batchResults.ToAggregateCsv();
                string cfgCsv = batchResults.ToConfigCsv();
                if (useServerUpload)
                {
                    yield return ServerUpload.UploadFile($"{batchFolder}/aggregate.csv", aggCsv);
                    yield return ServerUpload.UploadFile($"{batchFolder}/config.csv", cfgCsv);
                }
                else
                {
                    WebGLZipDownload.AddFileToProgressiveZip($"{batchFolder}/aggregate.csv", aggCsv);
                    WebGLZipDownload.AddFileToProgressiveZip($"{batchFolder}/config.csv", cfgCsv);
                }

                batchResults.Scenarios.Clear();
            }
        }

        // Restore original SO values
        config.DaysPerScenario = origDays;
        config.NumberOfScenarios = origScenarios;
        config.BaseTemperature = origBaseTemp;
        config.SeasonalAmplitude = origSeasonalAmp;
        config.ClimateTrend = origClimateTrend;
        config.VariabilityMagnitude = origVarMag;
        config.WarmingBias = origWarmBias;
        config.DailyVariationRange = origDailyVar;
        config.RandomnessGrowthRate = origRandGrowth;
        config.Autocorrelated = origAutocorr;
        config.InterannualVariation = origInterannual;
        config.TemperatureBoundsMin = origTempMin;
        config.TemperatureBoundsMax = origTempMax;
        config.UseCarryingCapacity = origUseCarry;
        config.CarryingCapacityTier1 = origCarryT1;
        config.ConditionDrainRate = origCondDrain;
        config.ConditionRecoveryRate = origCondRecovery;
        runSpecies.speciesList.Clear();
        runSpecies.speciesList.AddRange(origSpeciesList);

        // Switch to results screen with Download All (ZIP) button
        if (resultsScreen != null)
            resultsScreen.DisplayBulkResults(batches.Count, completedScenarios, useServerUpload);

        _isRunning = false;
        _runCoroutine = null;
    }

    private void OnCancelRequested()
    {
        _cancelRequested = true;
    }

    private void OnBulkResultsClosed()
    {
        // If still running, signal cancel so coroutine exits and restores config
        if (_isRunning)
            _cancelRequested = true;

        // Reset upload handler to idle state (ready for next drag-drop)
        if (csvUploadHandler != null)
            csvUploadHandler.ResetToIdle();
    }

    /// <summary>
    /// Convert BulkSpeciesConfig -> SpeciesData with proper enum handling.
    /// Known species names (Hexapod, Sheplik, etc.) map to their enum values.
    /// Unknown names use SpeciesName.Custom. displayName always holds the actual CSV name.
    /// Temperature values convert from Celsius to Kelvin (+273.15).
    /// </summary>
    private SpeciesData ConvertSpecies(BulkSpeciesConfig sp, int index)
    {
        SpeciesName speciesName;
        if (!Enum.TryParse<SpeciesName>(sp.Name, true, out speciesName))
            speciesName = SpeciesName.Custom;

        SpeciesVariant variant;
        if (!Enum.TryParse<SpeciesVariant>(sp.Variant, true, out variant))
            variant = SpeciesVariant.Custom;

        return new SpeciesData
        {
            index = index,
            speciesName = speciesName,
            variant = variant,
            displayName = sp.Name,
            tier = sp.Tier,
            count = sp.Pop,
            eatingAmount = sp.Eating,
            reproductionMultiplier = sp.ReproMult,
            deathThreshold = sp.DeathThresh,
            deathRate = sp.DeathRate,
            reproThreshold = sp.ReproThresh,
            naturalDeathRate = sp.NaturalDeathRate,
            naturalDeathVariance = sp.NaturalDeathVar,
            huntingEfficiency = sp.HuntEff,
            huntingVariance = sp.HuntVar,
            optimalTempK = sp.OptTempC + 273.15f,
            arrhenBreadth = sp.ArrhenBreadth,
            arrhenLower = sp.ArrhenLower,
            arrhenUpper = sp.ArrhenUpper,
            lowerBoundK = sp.LowerBoundC + 273.15f,
            upperBoundK = sp.UpperBoundC + 273.15f,
            pmax = sp.Pmax,
            ctMinC = sp.CTminC,
            ctMaxC = sp.CTmaxC,
            TemperatureDebuff = sp.TempOffset
        };
    }
}
