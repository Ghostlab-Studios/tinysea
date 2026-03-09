using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates bulk CSV batch simulation.
/// Subscribes to CsvUploadHandler.OnRunBulkSimulation, overrides SimulationConfig/RunSpeciesList
/// for each batch, runs all scenarios, and packages results as a ZIP download.
/// Uses ResultsScreenUI for progress display and completion state.
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
        var origSpeciesList = new List<SpeciesData>(runSpecies.speciesList);

        var allFiles = new List<(string name, string content)>();

        // Calculate total scenarios across all batches
        int totalScenarios = 0;
        foreach (var batch in batches)
            totalScenarios += batch.NumScenarios;

        int completedScenarios = 0;

        for (int b = 0; b < batches.Count; b++)
        {
            if (_cancelRequested)
            {
                Debug.Log($"Bulk simulation cancelled after {b} batches");
                break;
            }

            var batch = batches[b];
            string batchFolder = batch.BatchName;

            // Update progress at batch start
            if (resultsScreen != null)
            {
                float progress = totalScenarios > 0 ? (float)completedScenarios / totalScenarios : 0f;
                resultsScreen.UpdateBulkProgress(
                    $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) \u2014 Scenario 0 of {batch.NumScenarios}...",
                    progress);
            }
            yield return null;

            // Re-check cancel after yield (catches clicks processed during yield frame)
            if (_cancelRequested)
            {
                Debug.Log($"Bulk simulation cancelled before batch {b + 1} setup");
                break;
            }

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

            // Run each scenario in this batch
            for (int s = 0; s < batch.NumScenarios; s++)
            {
                if (_cancelRequested)
                {
                    Debug.Log($"Bulk simulation cancelled during batch {b + 1}");
                    break;
                }

                int scenarioIndex = s + 1;
                int seed = config.RandomSeed < 0 ? -1 : config.RandomSeed + s;

                // Update progress for each scenario
                if (resultsScreen != null)
                {
                    float progress = totalScenarios > 0 ? (float)completedScenarios / totalScenarios : 0f;
                    resultsScreen.UpdateBulkProgress(
                        $"Batch {b + 1} of {batches.Count} ({batch.BatchName}) \u2014 Scenario {scenarioIndex} of {batch.NumScenarios}...",
                        progress);
                }

                // Yield before the blocking scenario call so Unity can process
                // pending UI events (cancel button clicks) from the previous frame
                yield return null;

                // Re-check cancel after yield — catches clicks queued during
                // the previous scenario's synchronous execution
                if (_cancelRequested)
                {
                    Debug.Log($"Bulk simulation cancelled during batch {b + 1}");
                    break;
                }

                var result = simulationController.RunSingleScenarioPublic(scenarioIndex, seed);
                batchResults.Scenarios.Add(result);

                completedScenarios++;
            }

            // Save batch files (even if partially completed due to cancel)
            if (batchResults.Scenarios.Count > 0)
            {
                batchResults.CompletedAt = DateTime.Now;
                batchResults.CalculateAggregates();

                allFiles.Add(($"{batchFolder}/aggregate.csv", batchResults.ToAggregateCsv()));
                allFiles.Add(($"{batchFolder}/config.csv", batchResults.ToConfigCsv()));

                foreach (var scenario in batchResults.Scenarios)
                {
                    if (!string.IsNullOrEmpty(scenario.CsvData))
                        allFiles.Add(($"{batchFolder}/scenario_{scenario.ScenarioIndex}.csv", scenario.CsvData));
                }

                Debug.Log($"Batch '{batch.BatchName}' complete: {batchResults.SurvivedScenarios} survived, " +
                          $"{batchResults.CrashedScenarios} crashed");
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
        runSpecies.speciesList.Clear();
        runSpecies.speciesList.AddRange(origSpeciesList);

        // Switch to results screen with Download All (ZIP) button
        // ZIP is NOT auto-downloaded — user clicks the button
        if (resultsScreen != null)
            resultsScreen.DisplayBulkResults(batches.Count, completedScenarios, allFiles);

        Debug.Log($"Bulk simulation complete. {allFiles.Count} files ready for download.");

        _isRunning = false;
        _runCoroutine = null;
    }

    private void OnCancelRequested()
    {
        _cancelRequested = true;
        Debug.Log("Bulk simulation cancel requested");
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
            minimumDeaths = sp.MinDeaths,
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
            upperBoundK = sp.UpperBoundC + 273.15f
        };
    }
}
