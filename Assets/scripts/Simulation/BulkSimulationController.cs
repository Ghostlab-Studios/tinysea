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
        public Dictionary<string, float> SurvivedSpeciesPop; // avg pop only from survived scenarios
        // v12: Per-species rich aggregate snapshot for cross-run aggregation in GenerateBulkSummary.
        public Dictionary<string, PerSpeciesAggregate> PerSpeciesMetrics;
        // v12.2: Per-species (Tier, Variant) lookup keyed by FullName. Populated in
        // RunAllBatches from tempSpecies. Lets GenerateBulkSummary annotate every
        // per-species row with explicit Variant + Tier columns.
        public Dictionary<string, (int Tier, string Variant)> SpeciesInfo;
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
                batchResults.BatchName = batch.BatchName;   // Change 4: stamp run name into aggregate.csv
                batchResults.CalculateAggregates();

                // v12.2: Build per-species (Tier, Variant) lookup from tempSpecies so
                // GenerateBulkSummary can annotate per-species rows with explicit
                // Variant + Tier columns.
                var speciesInfo = new Dictionary<string, (int Tier, string Variant)>();
                if (tempSpecies != null && tempSpecies.speciesList != null)
                {
                    foreach (var sp in tempSpecies.speciesList)
                    {
                        string name = !string.IsNullOrEmpty(sp.displayName)
                            ? sp.displayName
                            : sp.speciesName.ToString();
                        // Batch 1A: use the free-text variant label (falls back to the
                        // enum name) so FullName matches SimSpecies.FullName.
                        string vlabel = !string.IsNullOrEmpty(sp.variantLabel) ? sp.variantLabel : sp.variant.ToString();
                        string fullName = $"{name}_{vlabel}";
                        // SpeciesData.tier is 0/1; internal Tier is 1/2.
                        speciesInfo[fullName] = (sp.tier + 1, vlabel);
                    }
                }

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
                        : new Dictionary<string, float>(),
                    SurvivedSpeciesPop = batchResults.PerSpeciesSurvivedAvg != null
                        ? new Dictionary<string, float>(batchResults.PerSpeciesSurvivedAvg)
                        : new Dictionary<string, float>(),
                    // v12: snapshot the rich aggregate. Reference is fine — batchResults
                    // is discarded after Scenarios.Clear() below; we keep the dict alive.
                    PerSpeciesMetrics = batchResults.PerSpeciesMetrics ?? new Dictionary<string, PerSpeciesAggregate>(),
                    SpeciesInfo = speciesInfo
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

        // v12.2: Build a unified (Tier, Variant) lookup across all runs. Some
        // species may appear only in some runs; merge all SpeciesInfo dicts
        // into one for use in per-species CSV rows.
        var bulkSpeciesMeta = new Dictionary<string, (int Tier, string Variant)>();
        foreach (var run in summaries)
        {
            if (run.SpeciesInfo == null) continue;
            foreach (var kv in run.SpeciesInfo)
                bulkSpeciesMeta[kv.Key] = kv.Value; // last-write-wins; fine since species shared across runs have identical metadata
        }
        string GetVariant(string fn) => bulkSpeciesMeta.TryGetValue(fn, out var m) ? m.Variant : "Unknown";
        string GetTier(string fn) => bulkSpeciesMeta.TryGetValue(fn, out var m) ? m.Tier.ToString() : "?";

        sb.AppendLine("=== TINYSEA BULK SUMMARY (Across All Runs) ===");
        sb.AppendLine($"# Model Version,v12-per-species-tracking");
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

        // Per-run results table — TIER LEVEL (kept wide-format, includes per-species
        // average columns for backward compatibility with existing analysis scripts).
        sb.AppendLine("=== PER-RUN RESULTS - TIER LEVEL ===");
        sb.Append("Run,Scenarios,Survived,Crashed,CrashRate,BaseTemp,ClimateTrend");
        foreach (var sp in allSpecies)
            sb.Append($",{sp}");
        sb.AppendLine();

        foreach (var run in summaries)
        {
            int total = run.Survived + run.Crashed;
            float crashRate = total > 0 ? (float)run.Crashed / total : 0;
            sb.Append($"{run.BatchName},{run.NumScenarios},{run.Survived},{run.Crashed},{crashRate:P1},{run.BaseTemp:F2},{run.ClimateTrend:F4}");
            foreach (var sp in allSpecies)
            {
                float val = run.AvgSpeciesPop != null && run.AvgSpeciesPop.ContainsKey(sp) ? run.AvgSpeciesPop[sp] : 0;
                sb.Append($",{val:F1}");
            }
            sb.AppendLine();
        }
        sb.AppendLine();

        // v12.2: Per-run per-species long-format table. One row per (run, species)
        // with explicit Variant + Tier columns. Easier to consume than the wide
        // format above, especially when there are many species.
        sb.AppendLine("=== PER-RUN RESULTS - PER SPECIES ===");
        sb.AppendLine("Run,Species,Variant,Tier,AvgPop,SurvivedAvgPop");
        foreach (var run in summaries)
        {
            if (run.AvgSpeciesPop == null) continue;
            foreach (var sp in allSpecies)
            {
                float avgPop = run.AvgSpeciesPop.TryGetValue(sp, out var av) ? av : 0f;
                float survivedAvgPop = run.SurvivedSpeciesPop != null && run.SurvivedSpeciesPop.TryGetValue(sp, out var sv) ? sv : 0f;
                sb.AppendLine($"{run.BatchName},{sp},{GetVariant(sp)},{GetTier(sp)},{avgPop:F1},{survivedAvgPop:F1}");
            }
        }
        sb.AppendLine();

        // Per-species aggregate across all runs
        // GrandMean = avg of run-level avgs (all scenarios, including extinct).
        // SurvivedMean = avg of run-level survived avgs (only scenarios where species lived).
        // Min/Max of averages are not meaningful population values — use the per-run table.
        sb.AppendLine("=== PER-SPECIES AGGREGATE (Across All Runs) ===");
        sb.AppendLine("Species,Variant,Tier,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate");

        foreach (var sp in allSpecies)
        {
            float sum = 0;
            int count = 0;
            int runsExtinct = 0;
            int runsSurvived = 0;
            float survivedSum = 0;
            int survivedCount = 0;

            foreach (var run in summaries)
            {
                if (run.AvgSpeciesPop == null || !run.AvgSpeciesPop.ContainsKey(sp)) continue;
                float val = run.AvgSpeciesPop[sp];
                sum += val;
                count++;
                if (val <= 0)
                {
                    runsExtinct++;
                }
                else
                {
                    runsSurvived++;
                    // Use survived-only avg if available, otherwise fall back to run avg
                    float survivedVal = run.SurvivedSpeciesPop != null && run.SurvivedSpeciesPop.ContainsKey(sp)
                        ? run.SurvivedSpeciesPop[sp] : val;
                    survivedSum += survivedVal;
                    survivedCount++;
                }
            }

            float grandMean = count > 0 ? sum / count : 0;
            float survivedMean = survivedCount > 0 ? survivedSum / survivedCount : 0;
            float extinctionRate = count > 0 ? (float)runsExtinct / count : 0;
            sb.AppendLine($"{sp},{GetVariant(sp)},{GetTier(sp)},{grandMean:F1},{survivedMean:F1},{runsExtinct},{runsSurvived},{extinctionRate:P1}");
        }

        // ====================================================================
        // v12: Per-species rich aggregate sections — final-year metrics, full-run
        // metrics, and stability across all runs in the bulk batch.
        // ====================================================================

        // Build the cross-run species union from PerSpeciesMetrics (richer than AvgSpeciesPop)
        var allSpeciesRich = new SortedSet<string>();
        foreach (var run in summaries)
        {
            if (run.PerSpeciesMetrics == null) continue;
            foreach (var k in run.PerSpeciesMetrics.Keys) allSpeciesRich.Add(k);
        }

        if (allSpeciesRich.Count > 0)
        {
            // Per-run × per-species final-year detail table
            sb.AppendLine();
            sb.AppendLine("=== PER-RUN PER-SPECIES FINAL YEAR ===");
            sb.AppendLine("Run,Species,Variant,Tier,N,NSurvived,MeanCondition,MeanBirthRate,PopCv,MeanPop,MeanFinalYear_TempDeaths,MeanFinalYear_ConditionDeaths,MeanFinalYear_NaturalDeaths,MeanFinalYear_PredationDeaths");
            foreach (var run in summaries)
            {
                if (run.PerSpeciesMetrics == null) continue;
                foreach (var sp in allSpeciesRich)
                {
                    if (!run.PerSpeciesMetrics.TryGetValue(sp, out var a)) continue;
                    sb.AppendLine($"{run.BatchName},{sp},{GetVariant(sp)},{GetTier(sp)},{a.N},{a.NSurvived}," +
                        $"{a.MeanConditionFinalYear.Mean:F3}," +
                        $"{a.MeanBirthRateFinalYear.Mean:F4}," +
                        $"{a.PopCvFinalYear.Mean:F3}," +
                        $"{a.MeanPopulationFinalYear.Mean:F1}," +
                        $"{a.FinalYearTempDeaths.Mean:F1},{a.FinalYearConditionDeaths.Mean:F1}," +
                        $"{a.FinalYearNaturalDeaths.Mean:F1},{a.FinalYearPredationDeaths.Mean:F1}");
                }
            }
            sb.AppendLine();

            // Cross-run grand-mean (mean of per-run means — equal weight per run).
            //
            // Population is averaged across ALL runs for GrandMeanPop (zero-pop runs
            // contribute a real 0 — meaningful for "typical population including
            // failures") and across surviving runs only for GrandMeanPop_SurvivedMean.
            //
            // Condition / BirthRate / PopCv are averaged across surviving runs ONLY
            // (NSurvived > 0). For non-surviving runs the per-run "mean" of these
            // metrics is either 0 (post-fix, when no day had Population > 0) or a
            // sentinel value (Condition stuck at its initial 1.0 because biology
            // never updated it). Including those samples produced misleading aggregates
            // — fixed in v12.3. We also use the per-run SurvivedMean (not Mean) for
            // these three so partial-survival runs contribute their cleanest
            // representative value.
            sb.AppendLine("=== CROSS-RUN PER-SPECIES FINAL YEAR (Mean of per-run means) ===");
            sb.AppendLine("Species,Variant,Tier,Runs,RunsSurvived,GrandMeanCondition,GrandMeanCondition_StdDev,GrandMeanBirthRate,GrandMeanBirthRate_StdDev,GrandMeanPopCv,GrandMeanPop,GrandMeanPop_SurvivedMean");
            foreach (var sp in allSpeciesRich)
            {
                int runs = 0, runsSurvivedCount = 0;
                float condSum = 0f, condSqSum = 0f;
                float brSum = 0f, brSqSum = 0f;
                float cvSum = 0f;
                float popSum = 0f;
                float popSurvivedSum = 0f;
                int popSurvivedCount = 0;

                foreach (var run in summaries)
                {
                    if (run.PerSpeciesMetrics == null) continue;
                    if (!run.PerSpeciesMetrics.TryGetValue(sp, out var a)) continue;

                    // Population: include every run (zero is a real datum here).
                    popSum += a.MeanPopulationFinalYear.Mean;
                    runs++;

                    // Condition / BirthRate / PopCv: only include runs where the
                    // species had at least one surviving scenario, and use the
                    // per-run SurvivedMean (cleaned of dead-scenario samples).
                    if (a.NSurvived > 0)
                    {
                        runsSurvivedCount++;
                        float cond = a.MeanConditionFinalYear.SurvivedMean;
                        float br   = a.MeanBirthRateFinalYear.SurvivedMean;
                        float cv   = a.PopCvFinalYear.SurvivedMean;
                        condSum += cond; condSqSum += cond * cond;
                        brSum   += br;   brSqSum   += br * br;
                        cvSum   += cv;
                        popSurvivedSum += a.MeanPopulationFinalYear.SurvivedMean;
                        popSurvivedCount++;
                    }
                }

                if (runs == 0) continue;

                // Grand means for condition / birth-rate / popCv are over surviving
                // runs only; if no runs survived, emit 0 (consistent with how
                // GrandMeanPop_SurvivedMean handles the same edge case).
                float gmCond = runsSurvivedCount > 0 ? condSum / runsSurvivedCount : 0f;
                float gmBr   = runsSurvivedCount > 0 ? brSum   / runsSurvivedCount : 0f;
                float gmCv   = runsSurvivedCount > 0 ? cvSum   / runsSurvivedCount : 0f;
                float gmPop  = popSum / runs;

                // StdDev across the same surviving-run sample.
                float condVar = runsSurvivedCount > 0 ? (condSqSum / runsSurvivedCount) - (gmCond * gmCond) : 0f;
                float brVar   = runsSurvivedCount > 0 ? (brSqSum   / runsSurvivedCount) - (gmBr   * gmBr)   : 0f;
                float gmCondStd = condVar > 0f ? (float)Math.Sqrt(condVar) : 0f;
                float gmBrStd   = brVar   > 0f ? (float)Math.Sqrt(brVar)   : 0f;

                float gmPopSurvived = popSurvivedCount > 0 ? popSurvivedSum / popSurvivedCount : 0f;

                sb.AppendLine($"{sp},{GetVariant(sp)},{GetTier(sp)},{runs},{runsSurvivedCount}," +
                    $"{gmCond:F3},{gmCondStd:F3}," +
                    $"{gmBr:F4},{gmBrStd:F4}," +
                    $"{gmCv:F3}," +
                    $"{gmPop:F1},{gmPopSurvived:F1}");
            }
            sb.AppendLine();

            // Cross-run stability summary
            sb.AppendLine("=== CROSS-RUN STABILITY ===");
            sb.AppendLine("Species,Variant,Tier,Runs,RunsSurvived,MinPop_Mean,MaxPop_Mean,FinalPop_Mean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay");
            foreach (var sp in allSpeciesRich)
            {
                int runs = 0, runsSurvivedCount = 0;
                float minSum = 0f, maxSum = 0f, finalSum = 0f;
                int totalScenarios = 0;
                int totalExtinctions = 0;
                int totalCrashes = 0;
                float extDaySum = 0f;
                int extDayCount = 0;
                float crashDaySum = 0f;
                int crashDayCount = 0;

                foreach (var run in summaries)
                {
                    if (run.PerSpeciesMetrics == null) continue;
                    if (!run.PerSpeciesMetrics.TryGetValue(sp, out var a)) continue;

                    minSum += a.MinPopulation.Mean;
                    maxSum += a.MaxPopulation.Mean;
                    finalSum += a.FinalPopulation.Mean;
                    runs++;
                    if (a.NSurvived > 0) runsSurvivedCount++;

                    totalScenarios   += a.N;
                    totalExtinctions += a.ExtinctionTiming.NEvents;
                    totalCrashes     += a.CrashTiming.NEvents;

                    if (a.ExtinctionTiming.NEvents > 0)
                    {
                        extDaySum   += a.ExtinctionTiming.MeanDay * a.ExtinctionTiming.NEvents;
                        extDayCount += a.ExtinctionTiming.NEvents;
                    }
                    if (a.CrashTiming.NEvents > 0)
                    {
                        crashDaySum   += a.CrashTiming.MeanDay * a.CrashTiming.NEvents;
                        crashDayCount += a.CrashTiming.NEvents;
                    }
                }

                if (runs == 0) continue;
                float minMean = minSum / runs;
                float maxMean = maxSum / runs;
                float finalMean = finalSum / runs;
                float extinctionRate2 = totalScenarios > 0 ? (float)totalExtinctions / totalScenarios : 0f;
                float crashRate2 = totalScenarios > 0 ? (float)totalCrashes / totalScenarios : 0f;
                float meanExtDay = extDayCount > 0 ? extDaySum / extDayCount : -1f;
                float meanCrashDay = crashDayCount > 0 ? crashDaySum / crashDayCount : -1f;

                sb.AppendLine($"{sp},{GetVariant(sp)},{GetTier(sp)},{runs},{runsSurvivedCount}," +
                    $"{minMean:F1},{maxMean:F1},{finalMean:F1}," +
                    $"{extinctionRate2:P1},{meanExtDay:F1}," +
                    $"{crashRate2:P1},{meanCrashDay:F1}");
            }
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

        // Batch 1B: resolve Cold/Warm/Hot + legacy aliases to the enum bucket; the
        // free-text display label is preserved separately via variantLabel.
        SpeciesVariant variant = SpeciesData.ResolveVariantEnum(sp.Variant);

        return new SpeciesData
        {
            index = index,
            speciesName = speciesName,
            variant = variant,
            variantLabel = SpeciesData.NormalizeVariantLabel(sp.Variant),
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
