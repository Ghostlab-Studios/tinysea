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
    private List<BulkRunSummary> _bulkSummaries;

    private struct BulkRunSummary
    {
        public string BatchName;
        public int NumScenarios;
        public int Survived;
        public int Crashed;
        public float BaseTemp;
        public float ClimateTrend;
        public Dictionary<string, float> AvgSpeciesPop;
    }

    // ETA — recalculated once per minute, cached between updates
    private float _lastEtaUpdateTime;
    private string _cachedEta = "";

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
        _bulkSummaries = new List<BulkRunSummary>();

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
        float startTime = Time.realtimeSinceStartup;
        _lastEtaUpdateTime = 0f;
        _cachedEta = "";

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
                string eta = GetETA(completedScenarios, totalScenarios, startTime);
                resultsScreen.UpdateBulkProgress(
                    $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) — 0 of {batch.NumScenarios}{eta}",
                    progress);
            }
            yield return null;

            if (_cancelRequested)
                break;

            // Create a temporary RunSpeciesList for this batch — never touches the real SO
            var tempSpecies = ScriptableObject.CreateInstance<RunSpeciesList>();
            tempSpecies.speciesList = new List<SpeciesData>();
            for (int i = 0; i < batch.Species.Count; i++)
                tempSpecies.speciesList.Add(ConvertSpecies(batch.Species[i], i));

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
                RunSpecies = tempSpecies,
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
                    string eta = GetETA(completedScenarios, totalScenarios, startTime);
                    resultsScreen.UpdateBulkProgress(
                        $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) — {scenarioIndex} of {batch.NumScenarios}{eta}",
                        progress);
                }
                yield return null;
                if (_cancelRequested) break;

                var result = simulationController.RunSingleScenarioFromBatch(batch, tempSpecies, scenarioIndex, seed);
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
            // Editor/Standalone: parallel scenarios in chunks to bound memory.
            // Each chunk runs ProcessorCount scenarios, writes CSVs, then frees memory.
            int parallelism = Math.Max(1, Environment.ProcessorCount - 1);
            int batchSize = batch.NumScenarios;
            var scenarioResults = new ScenarioResult[batchSize];
            var taskErrors = new Exception[batchSize];

            for (int chunk = 0; chunk < batchSize; chunk += parallelism)
            {
                if (_cancelRequested) break;

                int chunkEnd = Math.Min(chunk + parallelism, batchSize);
                int chunkSize = chunkEnd - chunk;
                var tasks = new Task[chunkSize];

                for (int i = 0; i < chunkSize; i++)
                {
                    int s = chunk + i;
                    int scenarioIndex = s + 1;
                    int seed = config.RandomSeed < 0 ? -1 : config.RandomSeed + s;
                    int taskIndex = s;

                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            scenarioResults[taskIndex] = simulationController.RunSingleScenarioFromBatch(batch, tempSpecies, scenarioIndex, seed);
                        }
                        catch (Exception ex)
                        {
                            taskErrors[taskIndex] = ex;
                        }
                    });
                }

                // Wait for this chunk to finish
                var chunkDone = Task.WhenAll(tasks);
                while (!chunkDone.IsCompleted)
                {
                    int done = 0;
                    for (int t = 0; t < tasks.Length; t++)
                        if (tasks[t].IsCompleted) done++;

                    if (resultsScreen != null)
                    {
                        int totalDone = completedScenarios + chunk + done;
                        float progress = totalScenarios > 0 ? (float)totalDone / totalScenarios : 0f;
                        string eta = GetETA(totalDone, totalScenarios, startTime);
                        resultsScreen.UpdateBulkProgress(
                            $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) — {chunk + done} of {batchSize}{eta}",
                            progress);
                    }
                    yield return null;
                }

                // Log errors and stream CSVs for this chunk immediately (frees memory)
                for (int i = 0; i < chunkSize; i++)
                {
                    int s = chunk + i;
                    if (taskErrors[s] != null)
                        Debug.LogError($"Scenario {s + 1} in batch '{batch.BatchName}' failed: {taskErrors[s].Message}\n{taskErrors[s].StackTrace}");

                    var result = scenarioResults[s];
                    if (result == null) continue;

                    batchResults.Scenarios.Add(result);

                    if (!string.IsNullOrEmpty(result.CsvData))
                    {
                        string csvPath = $"{batchFolder}/scenario_{result.ScenarioIndex}.csv";
                        WebGLZipDownload.AddFileToProgressiveZip(csvPath, result.CsvData);
                        result.CsvData = null;
                    }
                    scenarioResults[s] = null; // Free memory
                }
            }
            completedScenarios += batchSize;
#endif

            // Stream aggregate + config CSVs for this batch
            if (batchResults.Scenarios.Count > 0)
            {
                batchResults.CompletedAt = DateTime.Now;
                batchResults.CalculateAggregates();

                _bulkSummaries.Add(new BulkRunSummary
                {
                    BatchName = batch.BatchName,
                    NumScenarios = batch.NumScenarios,
                    Survived = batchResults.SurvivedScenarios,
                    Crashed = batchResults.CrashedScenarios,
                    BaseTemp = batch.BaseTemp,
                    ClimateTrend = batch.ClimateTrend,
                    AvgSpeciesPop = batchResults.PerSpeciesAvg != null
                        ? new Dictionary<string, float>(batchResults.PerSpeciesAvg)
                        : new Dictionary<string, float>()
                });

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

        // Generate and stream bulk summary CSV
        if (_bulkSummaries.Count > 0)
        {
            string bulkSummary = GenerateBulkSummary(_bulkSummaries);
            if (useServerUpload)
                yield return ServerUpload.UploadFile("bulk_summary.csv", bulkSummary);
            else
                WebGLZipDownload.AddFileToProgressiveZip("bulk_summary.csv", bulkSummary);
        }

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
    /// Generate a bulk summary CSV aggregating per-species stats across all runs.
    /// </summary>
    private string GenerateBulkSummary(List<BulkRunSummary> summaries)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== TINYSEA BULK SUMMARY (Across All Runs) ===");
        sb.AppendLine($"# Total Runs,{summaries.Count}");
        sb.AppendLine($"# Generated,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Collect all species names across all runs
        var allSpecies = new SortedSet<string>();
        foreach (var run in summaries)
        {
            if (run.AvgSpeciesPop != null)
                foreach (var key in run.AvgSpeciesPop.Keys)
                    allSpecies.Add(key);
        }

        // Per-run results table
        sb.AppendLine("=== PER-RUN RESULTS ===");
        sb.Append("Run,Scenarios,Survived,Crashed,BaseTemp,ClimateTrend");
        foreach (var sp in allSpecies)
            sb.Append($",{sp}");
        sb.AppendLine();

        foreach (var run in summaries)
        {
            sb.Append($"{run.BatchName},{run.NumScenarios},{run.Survived},{run.Crashed},{run.BaseTemp:F2},{run.ClimateTrend:F4}");
            foreach (var sp in allSpecies)
            {
                float val = run.AvgSpeciesPop != null && run.AvgSpeciesPop.ContainsKey(sp) ? run.AvgSpeciesPop[sp] : 0;
                sb.Append($",{val:F1}");
            }
            sb.AppendLine();
        }
        sb.AppendLine();

        // Per-species aggregate across all runs
        sb.AppendLine("=== PER-SPECIES AGGREGATE (Across All Runs) ===");
        sb.AppendLine("Species,Avg,Min,Max");

        foreach (var sp in allSpecies)
        {
            float sum = 0;
            float min = float.MaxValue;
            float max = float.MinValue;
            int count = 0;

            foreach (var run in summaries)
            {
                if (run.AvgSpeciesPop == null || !run.AvgSpeciesPop.ContainsKey(sp)) continue;
                float val = run.AvgSpeciesPop[sp];
                sum += val;
                count++;
                if (val < min) min = val;
                if (val > max) max = val;
            }

            float avg = count > 0 ? sum / count : 0;
            if (min == float.MaxValue) min = 0;
            if (max == float.MinValue) max = 0;
            sb.AppendLine($"{sp},{avg:F1},{min:F1},{max:F1}");
        }

        return sb.ToString();
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

    /// <summary>
    /// Returns cached ETA string. Recalculates only once per minute.
    /// </summary>
    private string GetETA(int completed, int total, float startTime)
    {
        float now = Time.realtimeSinceStartup;
        if (completed <= 0) return "";
        if (now - _lastEtaUpdateTime < 60f && _cachedEta.Length > 0) return _cachedEta;

        _lastEtaUpdateTime = now;
        float elapsed = now - startTime;
        float perScenario = elapsed / completed;
        float remaining = perScenario * (total - completed);

        if (remaining < 60) _cachedEta = $" — ~{remaining:F0}s left";
        else if (remaining < 3600) _cachedEta = $" — ~{remaining / 60:F0}m left";
        else
        {
            float hours = remaining / 3600;
            float mins = (remaining % 3600) / 60;
            _cachedEta = $" — ~{hours:F0}h {mins:F0}m left";
        }
        return _cachedEta;
    }
}
