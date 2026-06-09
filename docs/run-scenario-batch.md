# Runs, Scenarios, and the Day Loop

This document defines the three execution units of the TinySea headless simulation (Scenario, Run, Bulk), the per day loop that drives the biology, how each day is recorded, how crash and extinction are detected, where the day and scenario counts are configured, and the coroutine and threading structure that surrounds all of it.

Scope note: the current simulation runs Tier 1 (prey) only. `SimulationConfig.Tier2Enabled` defaults to `false` (SimulationConfig.cs:130) and `EcosystemSimulator.Tier2Enabled` defaults to `false` (EcosystemSimulator.cs:245). The Tier 2 (predator) code path still exists from the original two tier design. Where Tier 2 appears below it is marked as secondary legacy. Population queries, crash detection, and record fields still carry both tiers, but with the gate off the Tier 2 population is 0 and Tier 2 contributes nothing.

Related documents (do not duplicate, cross reference by filename):

- The 10 step biology sequence executed once per biology day: `biology-and-formulas.md`.
- The daily temperature value passed into the day loop: `temperature-model.md`.
- The CSV that a scenario serializes into and the aggregate and bulk CSV layouts: `csv-output-formats.md`.
- `StepRecord`, `PerSpeciesStepData`, `ScenarioResult`, `AggregateResults`, `PerSpeciesScenarioMetrics`: `data-structures.md`.
- The bulk upload, parse, and per run output flow in full: `bulk-system.md`.
- Every `SimulationConfig` and `BulkBatchConfig` field with defaults and ranges: `configuration-reference.md`.
- The results screen, progress callbacks, and output sinks: `ui-and-io.md`.
- The authoritative end to end spec: `simulation-spec.md`.

---

## 1. Definitions: Scenario vs Run vs Bulk

The three units are nested. A Bulk contains many Runs. A Run contains many Scenarios. A Scenario contains many days.

| Unit | Code identity | What varies inside it | Output artifact |
|------|---------------|-----------------------|-----------------|
| Scenario | `SimulationRunner` -> `ScenarioResult` (SimulationRunner.cs:347, ScenarioResult.cs:10) | One fixed seed; `TotalDays` days advance | `scenario_N.csv` |
| Run (also called batch) | `SimulationController` + `AggregateResults` (standard) or one `BulkBatchConfig` (bulk) | `NumberOfScenarios` seeds, same parameters | `aggregate.csv` |
| Bulk | `BulkSimulationController` driving many `BulkBatchConfig` (BulkSimulationController.cs) | Many runs, each from one uploaded CSV row | ZIP plus `bulk_summary.csv` |

### 1.1 Scenario

A Scenario is one execution of `SimulationRunner.Run()` with a single random seed (SimulationRunner.cs:392). It advances day by day for `TotalDays` days, calling the biology step on biology days, and records one `StepRecord` per day. It is the atomic unit of the simulation: every statistic above it is computed by aggregating scenarios. The result object is a `ScenarioResult` produced by `ToScenarioResult` (SimulationRunner.cs:982), which carries the per day CSV plus summary statistics and per species metrics.

A Scenario owns one `TemperatureCalculator` and one `EcosystemSimulator`, both seeded from the same scenario seed in the `SimulationRunner` constructor (SimulationRunner.cs:382-387):

```csharp
public SimulationRunner(int seed = -1)
{
    UsedSeed = seed;
    TempCalc   = new TemperatureCalculator(seed);
    Ecosystem  = new EcosystemSimulator(seed);
}
```

Because both random sources are seeded from the same value, two scenarios with the same non negative seed and the same parameters produce byte identical output on the same build and platform. "Parameters" here means every field copied onto the runner before `Run()`: `TotalDays`, `BiologyStep`, every `TempCalc.*` field, every `Ecosystem.*` field, and `RunSpecies` (Section 2.4). The guarantee assumes a fixed seed; a `-1` seed is time seeded and not reproducible (Section 8). The simulation uses `float`/`double` arithmetic and runs on different execution paths per platform (parallel `Task.Run` on Editor and standalone, single threaded WASM on WebGL, Section 2), so cross platform byte identity between a WebGL build and a standalone build is not guaranteed; the byte identical claim is for the same build on the same platform.

### 1.2 Run (batch)

A Run is one parameter set (species list plus environment parameters) executed `NumberOfScenarios` times, each scenario with a different seed. The seeds are derived from a base seed (Section 4). The standard entry point is `SimulationController.RunAllScenariosCoroutine` (SimulationController.cs:111), which loops scenario indices, collects each `ScenarioResult` into `AggregateResults.Scenarios` (SimulationController.cs:167, 182, 248), and at the end calls `CalculateAggregates` (SimulationController.cs:263) to roll the scenarios up into cross scenario statistics. The Run output is `aggregate.csv`.

In bulk mode, a Run is one row of the uploaded CSV, represented as a `BulkBatchConfig`. The word "batch" in the bulk code means exactly one Run. `BulkBatchConfig.NumScenarios` is that run's scenario count, and `BulkBatchConfig.Days` is that run's day count (used at SimulationController.cs:292 and BulkSimulationController.cs:241).

### 1.3 Bulk

A Bulk is a list of Runs uploaded as a single CSV file, parsed into a `List<BulkBatchConfig>`. `BulkSimulationController.RunBulkCoroutine` iterates the batches (BulkSimulationController.cs:148, `for (int b = 0; b < batches.Count; b++)`), runs each batch's scenarios, streams each batch's `scenario_N.csv`, `aggregate.csv`, and `config.csv` into a per run folder, and at the end emits a `bulk_summary.csv` at the ZIP root. The full bulk flow, CSV parsing, and summary generation are documented in `bulk-system.md`. This document covers only how a Bulk schedules and seeds the scenarios it contains.

---

## 2. How one Run executes N scenarios

The driver is the same shape in both standard and bulk mode: an outer loop over scenario indices, each index mapped to a seed, each seed handed to a fresh `SimulationRunner`. The two modes differ only in their concurrency strategy, which is selected by the `UNITY_WEBGL && !UNITY_EDITOR` compile flag.

### 2.1 Standard mode, WebGL (sequential)

On a non editor WebGL build, WASM is single threaded, so scenarios run one at a time and the coroutine yields a frame between each so the UI repaints (SimulationController.cs:170-188):

```csharp
for (int i = 0; i < config.NumberOfScenarios; i++)
{
    if (_cancelRequested) break;

    int scenarioIndex = i + 1;
    resultsScreen.UpdateProgress(scenarioIndex, config.NumberOfScenarios);

    int scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i;
    var result = RunSingleScenario(scenarioIndex, scenarioSeed);
    _currentResults.Scenarios.Add(result);

    resultsScreen.OnScenarioCompleted(result);
    yield return null;   // one frame so the UI renders
}
```

Note the index convention: the loop counter `i` is 0 based, but the `scenarioIndex` reported and stored is `i + 1` (1 based).

### 2.2 Standard mode, Editor and standalone (parallel)

On Editor and standalone builds, scenarios run in parallel using `Task.Run`, chunked by processor count to bound memory and to give the coroutine a chance to update the UI between chunks (SimulationController.cs:189-258). The degree of parallelism is `Math.Max(1, Environment.ProcessorCount - 1)` (SimulationController.cs:193), leaving one core for the main thread.

The structure is:

1. Walk the scenario range in steps of `parallelism` (SimulationController.cs:197).
2. For each scenario in the chunk, spawn a `Task.Run` that calls `RunSingleScenario` and stores the result into a preallocated `allResults[taskIndex]` slot, capturing any exception into `taskErrors[taskIndex]` instead of throwing (SimulationController.cs:212-222). `taskIndex` is the global 0 based scenario index `i = chunk + t` (SimulationController.cs:207, 210), not the within chunk offset `t`. Both `allResults` and `taskErrors` are sized to `totalScenarios` (SimulationController.cs:194-195), so each scenario has a fixed slot for the whole run and workers never write the same slot.
3. Await the chunk with `Task.WhenAll`, polling completion and calling `resultsScreen.UpdateProgress` while it runs, yielding a frame each poll (SimulationController.cs:226-237).
4. After the chunk completes, log any captured errors and append the successful results to `_currentResults.Scenarios` (SimulationController.cs:240-252). Collection walks the chunk in ascending `i` order and appends `allResults[i]` only when it is non null (SimulationController.cs:240-251), so the final `Scenarios` list is in ascending scenario index order with failed (null) slots skipped, not in task completion order.

Each task constructs its own `SimulationRunner` (SimulationController.cs:346), so the parallel scenarios share no mutable simulation state. Per scenario errors are isolated: one scenario throwing does not abort the others; its slot stays null and is skipped during collection (SimulationController.cs:243-246). Because collection is ordered by index and chunks are processed in ascending order, the assembled `Scenarios` list order is reproducible regardless of which worker finishes first.

### 2.3 Bulk mode

`BulkSimulationController` mirrors the standard structure for each batch. WebGL runs the batch's scenarios sequentially (BulkSimulationController.cs:204-236); Editor and standalone run them in `Task.Run` chunks of `Math.Max(1, Environment.ProcessorCount - 1)` (BulkSimulationController.cs:240, 245-315). Each scenario calls `simulationController.RunSingleScenarioFromBatch(batch, tempSpecies, scenarioIndex, seed)` (BulkSimulationController.cs:266), which builds a `SimulationRunner` from the batch's parameters without mutating any ScriptableObject (SimulationController.cs:287-338). After each chunk, the chunk's CSVs are streamed to the ZIP and the in memory `ScenarioResult.CsvData` is nulled to free memory (BulkSimulationController.cs:307-313).

### 2.4 The single scenario entry points

Three methods on `SimulationController` start one scenario:

| Method | Source of parameters | Caller |
|--------|----------------------|--------|
| `RunSingleScenario(int scenarioIndex, int seed)` | the assigned `SimulationConfig` ScriptableObject | standard Run loop (SimulationController.cs:343) |
| `RunSingleScenarioPublic(int scenarioIndex, int seed)` | same; thin public wrapper | external callers (SimulationController.cs:278) |
| `RunSingleScenarioFromBatch(BulkBatchConfig batch, RunSpeciesList tempSpecies, int scenarioIndex, int seed)` | the `BulkBatchConfig` row, no ScriptableObject mutation | bulk Run loop (SimulationController.cs:287) |

All three do the same thing: construct a `SimulationRunner(seed)`, copy timing, temperature, condition, carrying capacity, and species parameters onto the runner, call `runner.Run()`, then return `runner.ToScenarioResult(scenarioIndex, numberOfScenarios)`. The exact field by field copy differs between the standard path (`RunSingleScenario`, source = `SimulationConfig`) and the bulk path (`RunSingleScenarioFromBatch`, source = `BulkBatchConfig` row), and several copies rename the field. The two copy blocks are enumerated in Sections 2.4.1 and 2.4.2.

One parameter is sourced differently by `RunSingleScenarioFromBatch`: `runner.BiologyStep` is NOT taken from the `BulkBatchConfig` row. The bulk method reads `runner.BiologyStep = config.BiologyStep` from the controller's assigned `SimulationConfig` ScriptableObject (SimulationController.cs:293), while it takes the day count from the row (`runner.TotalDays = batch.Days`, SimulationController.cs:292). The standard methods take both from `config` (SimulationController.cs:349-350). Section 7 covers this asymmetry in detail.

`RunSingleScenario` also wires the cooperative pause and stop signal into the runner: `runner.Control = _runControl` (SimulationController.cs:375). `RunSingleScenarioFromBatch` does not set `Control`; bulk runs are paused at coarser boundaries instead (Section 6).

#### 2.4.1 Standard path: RunSingleScenario field copy

`RunSingleScenario(int scenarioIndex, int seed)` copies from the controller's serialized `SimulationConfig` field named `config` (SimulationController.cs:343-382). It sets the two timing fields, then 11 `runner.TempCalc.*` fields, then `runner.RunSpecies`, then 4 `runner.Ecosystem.*` fields, then `runner.Control`. Three of the temperature copies rename the field between `SimulationConfig` and `TemperatureCalculator`; those rows are marked "remap" below.

| Runner field | Assigned from | Remap | Source line |
|--------------|---------------|-------|-------------|
| `runner.TotalDays` | `config.DaysPerScenario` | yes | SimulationController.cs:349 |
| `runner.BiologyStep` | `config.BiologyStep` | no | SimulationController.cs:350 |
| `runner.TempCalc.BaseTemperature` | `config.BaseTemperature` | no | SimulationController.cs:353 |
| `runner.TempCalc.SeasonalAmplitude` | `config.SeasonalAmplitude` | no | SimulationController.cs:354 |
| `runner.TempCalc.ClimateTrendPerYear` | `config.ClimateTrend` | yes (`ClimateTrend` to `ClimateTrendPerYear`) | SimulationController.cs:355 |
| `runner.TempCalc.VariabilityMagnitude` | `config.VariabilityMagnitude` | no | SimulationController.cs:356 |
| `runner.TempCalc.WarmingBias` | `config.WarmingBias` | no | SimulationController.cs:357 |
| `runner.TempCalc.BaseRandomness` | `config.DailyVariationRange` | yes (`DailyVariationRange` to `BaseRandomness`) | SimulationController.cs:358 |
| `runner.TempCalc.RandomnessGrowthRate` | `config.RandomnessGrowthRate` | no | SimulationController.cs:359 |
| `runner.TempCalc.UseAutocorrelation` | `config.Autocorrelated` | yes (`Autocorrelated` to `UseAutocorrelation`) | SimulationController.cs:360 |
| `runner.TempCalc.UseInterannualVariation` | `config.InterannualVariation` | no | SimulationController.cs:361 |
| `runner.TempCalc.MinTemp` | `config.TemperatureBoundsMin` | yes (`TemperatureBoundsMin` to `MinTemp`) | SimulationController.cs:362 |
| `runner.TempCalc.MaxTemp` | `config.TemperatureBoundsMax` | yes (`TemperatureBoundsMax` to `MaxTemp`) | SimulationController.cs:363 |
| `runner.RunSpecies` | `config.RunSpecies` | no | SimulationController.cs:366 |
| `runner.Ecosystem.CarryingCapacityPerTier` | `config.CarryingCapacityTier1` | yes (`CarryingCapacityTier1` to `CarryingCapacityPerTier`) | SimulationController.cs:369 |
| `runner.Ecosystem.ConditionDrainRate` | `config.ConditionDrainRate` | no | SimulationController.cs:372 |
| `runner.Ecosystem.ConditionRecoveryRate` | `config.ConditionRecoveryRate` | no | SimulationController.cs:373 |
| `runner.Ecosystem.Tier2Enabled` | `config.Tier2Enabled` | no | SimulationController.cs:374 |
| `runner.Control` | `_runControl` | no (Section 6) | SimulationController.cs:375 |

`SimulationConfig` source field defaults and ranges (SimulationConfig.cs): `DaysPerScenario = 365` (cs:37), `NumberOfScenarios = 5` (cs:44), `BiologyStep = 1` (cs:29), `BaseTemperature = 20f` (cs:80), `SeasonalAmplitude = 5f` (cs:84), `ClimateTrend = 1f` (cs:88), `InterannualVariation = true` (cs:91), `VariabilityMagnitude = 2f` (cs:95), `WarmingBias = 1.5f` (cs:98), `Autocorrelated = true` (cs:102), `DailyVariationRange = 5f` (cs:105), `RandomnessGrowthRate = 0.5f` (cs:108), `TemperatureBoundsMin = -5f` (cs:112), `TemperatureBoundsMax = 50f` (cs:115), `CarryingCapacityTier1 = 5000f` (cs:59), `ConditionDrainRate = 0.15f` (cs:68), `ConditionRecoveryRate = 0.10f` (cs:74), `Tier2Enabled = false` (cs:130). The 17 `runner` targets above are the only fields touched; everything else on `TemperatureCalculator` and `EcosystemSimulator` keeps its constructor default. `RunSingleScenarioPublic(int scenarioIndex, int seed)` is a thin wrapper that calls `RunSingleScenario` (SimulationController.cs:278-281), so it copies the identical set.

#### 2.4.2 Bulk path: RunSingleScenarioFromBatch field copy

`RunSingleScenarioFromBatch(BulkBatchConfig batch, RunSpeciesList tempSpecies, int scenarioIndex, int seed)` copies from one parsed `BulkBatchConfig` row plus two values from the controller's `config` SO, and never mutates any ScriptableObject (SimulationController.cs:287-338). It sets `runner.TotalDays` and `runner.Tier2Enabled` from different sources than the standard path, and adds a temperature timeseries override branch the standard path does not have.

| Runner field | Assigned from | Remap | Source line |
|--------------|---------------|-------|-------------|
| `runner.TotalDays` | `batch.Days` | no | SimulationController.cs:292 |
| `runner.BiologyStep` | `config.BiologyStep` (the SO, not the row) | no | SimulationController.cs:293 |
| `runner.TempCalc.BaseTemperature` | `batch.BaseTemp` | yes (`BaseTemp` to `BaseTemperature`) | SimulationController.cs:295 |
| `runner.TempCalc.SeasonalAmplitude` | `batch.SeasonalAmp` | yes (`SeasonalAmp` to `SeasonalAmplitude`) | SimulationController.cs:296 |
| `runner.TempCalc.ClimateTrendPerYear` | `batch.ClimateTrend` | yes (`ClimateTrend` to `ClimateTrendPerYear`) | SimulationController.cs:297 |
| `runner.TempCalc.VariabilityMagnitude` | `batch.VariabilityMag` | yes (`VariabilityMag` to `VariabilityMagnitude`) | SimulationController.cs:298 |
| `runner.TempCalc.WarmingBias` | `batch.WarmingBias` | no | SimulationController.cs:299 |
| `runner.TempCalc.BaseRandomness` | `batch.DailyVarRange` | yes (`DailyVarRange` to `BaseRandomness`) | SimulationController.cs:300 |
| `runner.TempCalc.RandomnessGrowthRate` | `batch.RandomnessGrowth` | yes (`RandomnessGrowth` to `RandomnessGrowthRate`) | SimulationController.cs:301 |
| `runner.TempCalc.UseAutocorrelation` | `batch.Autocorrelated` | yes (`Autocorrelated` to `UseAutocorrelation`) | SimulationController.cs:302 |
| `runner.TempCalc.UseInterannualVariation` | `batch.InterannualVariation` | no | SimulationController.cs:303 |
| `runner.TempCalc.MinTemp` | `batch.TempMin` | yes (`TempMin` to `MinTemp`) | SimulationController.cs:304 |
| `runner.TempCalc.MaxTemp` | `batch.TempMax` | yes (`TempMax` to `MaxTemp`) | SimulationController.cs:305 |
| `runner.RunSpecies` | `tempSpecies` (method argument) | no | SimulationController.cs:307 |
| `runner.Ecosystem.CarryingCapacityPerTier` | `batch.CarryingCapT1` | yes (`CarryingCapT1` to `CarryingCapacityPerTier`) | SimulationController.cs:309 |
| `runner.Ecosystem.ConditionDrainRate` | `batch.ConditionDrainRate` | no | SimulationController.cs:310 |
| `runner.Ecosystem.ConditionRecoveryRate` | `batch.ConditionRecoveryRate` | no | SimulationController.cs:311 |
| `runner.Ecosystem.Tier2Enabled` | `config.Tier2Enabled` (the SO, not the row) | no | SimulationController.cs:312 |

`runner.Control` is not set here, so bulk scenarios are paused at coroutine boundaries instead (Section 6).

`BulkBatchConfig` is the per row record parsed from the uploaded CSV (BulkBatchConfig.cs:41-64). Its full field set, with the defaults the parser leaves when a column is absent, is: `BatchName` (`string`), `Days` (`int`), `NumScenarios` (`int`), `BaseTemp` (`float`), `SeasonalAmp` (`float`), `ClimateTrend` (`float`), `VariabilityMag` (`float`), `WarmingBias` (`float`), `DailyVarRange` (`float`), `RandomnessGrowth` (`float`), `Autocorrelated` (`bool`), `InterannualVariation` (`bool`), `TempMin` (`float`), `TempMax` (`float`), `CarryingCapT1` (`float`), `ConditionDrainRate` (`float`, default `0.15f`, BulkBatchConfig.cs:59), `ConditionRecoveryRate` (`float`, default `0.10f`, BulkBatchConfig.cs:60), `TemperatureTimeseriesFile` (`string`, default `""`, BulkBatchConfig.cs:62), and `Species` (`List<BulkSpeciesConfig>`, BulkBatchConfig.cs:63). The full CSV parse that fills these is in `bulk-system.md`; this document covers only how the filled record maps onto the runner. There is no per row `BiologyStep` or per row `Tier2Enabled` column, which is why both come from the controller's `SimulationConfig` SO (Section 7).

##### Temperature timeseries override branch

After the parametric copies, `RunSingleScenarioFromBatch` optionally replaces the parametric temperature model with a fixed daily timeseries loaded from a file (SimulationController.cs:317-334). The branch runs only when `batch.TemperatureTimeseriesFile` is non null and not whitespace (SimulationController.cs:317). The pseudocode:

```text
if not string.IsNullOrWhiteSpace(batch.TemperatureTimeseriesFile):
    try:
        tsPath = batch.TemperatureTimeseriesFile
        if File.Exists(tsPath):
            series = TemperatureCalculator.ParseTimeseriesCsv(File.ReadAllText(tsPath))
            if series != null:
                runner.TempCalc.LoadTimeseries(series)      # overrides parametric model
            else:
                warn "had no numeric rows; using parametric model"
        else:
            warn "file not found; using parametric model"
    catch (Exception e):
        warn "failed to load; using parametric model"
```

`ParseTimeseriesCsv(string content)` is a static method on `TemperatureCalculator` (TemperatureCalculator.cs:104-121). It splits the file on any of `\r\n`, `\r`, or `\n` (cs:108), trims each line, skips blank lines and lines beginning with `#` (cs:112), splits the remaining line on commas, and parses the last comma separated cell as a `float` using `NumberStyles.Float` and `CultureInfo.InvariantCulture` (cs:115-117). Reading the last cell handles both a `Day,Temperature_C` two column file and a bare `Temperature_C` single column file. Rows that do not parse, including a header row, are skipped. It returns the list of parsed temperatures in file order, or `null` when no numeric row was found (cs:120). `LoadTimeseries(List<float> dailyTempsC)` stores the list as the active timeseries when it is non null and non empty, else clears it to `null` (TemperatureCalculator.cs:91-95). When a timeseries is loaded, `GetTemperature` returns the stored value indexed by day instead of evaluating the parametric model; the indexing and end of series behavior are documented in `temperature-model.md`. The file read uses `System.IO.File`, so on WebGL (no synchronous file system) the `File.Exists` check is false and the run falls back to the parametric model with a warning. Every failure mode in the branch logs a `Debug.LogWarning` and leaves the parametric model in place; none of them aborts the scenario.

---

## 3. The per day loop

The day loop lives entirely in `SimulationRunner.Run()` (SimulationRunner.cs:392-459). It is plain synchronous C#; the only concurrency around it is the `Task.Run` wrapper that the controllers place around the whole scenario (Section 2). The loop does not allocate threads of its own.

### 3.1 Setup before the loop

`Run()` first clears prior state and initializes the species list (SimulationRunner.cs:394-411):

1. Clear `_records`, reset `_biologyCycleCounter` to 0, reset `HasCrashed`, `CrashDay`, `CrashTier` to their initial values (`false`, `-1`, `-1`).
2. Copy `BiologyStep` onto `Ecosystem.BiologyStep`.
3. If `RunSpecies` is non null and has a non empty `speciesList`, call `Ecosystem.InitializeFromRunSpeciesList(RunSpecies)`; otherwise log a warning and call `Ecosystem.InitializeDefaultSpecies()`.

Species initialization is where the `_tier1WasPopulated` and `_tier2WasPopulated` latches are set, which crash tier reporting later depends on (Section 5.2).

### 3.2 The loop body

```csharp
for (int dayIndex = 0; dayIndex < TotalDays; dayIndex++)
{
    // Cooperative per-day pause/stop (Section 6).
    if (Control != null)
    {
        while (Control.Paused && !Control.Stopped) System.Threading.Thread.Sleep(10);
        if (Control.Stopped) break;
    }

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
        CrashDay   = displayDay;
        CrashTier  = Ecosystem.GetCrashedTier();
        // (Debug.LogWarning of the crash omitted here; see SimulationRunner.cs:445.)
        break;
    }
}
```

The crash branch also emits `Debug.LogWarning($"=== ECOSYSTEM CRASH on Day {displayDay} (Year {year}) - All populations extinct ===")` before the `break` (SimulationRunner.cs:445). It is a log statement only and mutates no state, so it is elided from the reproduced blocks above and below.

The ordered steps each iteration (SimulationRunner.cs:415-447):

1. Honor the optional pause and stop signal (Section 6). When `Control` is null this branch is skipped entirely.
2. Compute `displayDay = dayIndex + 1` (the 1 based day number written to CSV) and `year = (dayIndex / 365) + 1`. `TemperatureCalculator.DAYS_PER_YEAR` is the constant `365` (TemperatureCalculator.cs:30). Year is 1 based: days 1 through 365 are year 1, days 366 through 730 are year 2.
3. Fetch the day's temperature in Celsius: `temp = TempCalc.GetTemperature(dayIndex)`. The temperature calculator is indexed by the 0 based `dayIndex`, not `displayDay`. The temperature model and the timeseries override are documented in `temperature-model.md`.
4. Decide whether biology runs this day: `runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)`. The predicate is evaluated independently each day against the calendar day number `displayDay`; it does not track the last biology day. Day 1 is an additional biology day that does not reset or phase shift the modulo schedule. The modulo is absolute on `displayDay`, so day 1 never moves the next scheduled biology day. For `BiologyStep == 5` the biology days are {1, 5, 10, 15, ...}: day 1 plus every multiple of 5, and the day after day 1 that runs biology is day 5, not day 6.
5. If biology runs, increment `_biologyCycleCounter` and call `Ecosystem.ProcessBiologyStep(temp)`. That call executes the full 10 step biology sequence for the day; see `biology-and-formulas.md`. The biology cycle counter is written into each record's `BiologyCycle` field on biology days, and 0 on skipped days (SimulationRunner.cs:485).
6. Always call `RecordStep(displayDay, year, temp, runBiology)` to capture the day (Section 4).
7. If biology ran and the ecosystem crashed, latch the crash and break the loop (Section 5).

### 3.3 The BiologyStep skip pattern

`BiologyStep` controls how often the biology runs. It is an integer in `[1, 5]` (`SimulationConfig.BiologyStep`, `[Range(1, 5)]`, SimulationConfig.cs:28-29), default 1.

- `BiologyStep == 1`: biology runs every day. This is the default and the most accurate mode.
- `BiologyStep == 5`: biology runs on day 1, then on days 5, 10, 15, and every multiple of 5 thereafter. This reproduces the original game's turn cadence.

The schedule is `displayDay == 1 OR (displayDay mod BiologyStep) == 0`, evaluated fresh each day on the absolute calendar day number. Day 1 is a one off extra biology day; it does not reset a counter or shift the modulo phase. So for `BiologyStep == 5` the day after the day 1 special case that runs biology is day 5, not day 6, and the full set of biology days is {1, 5, 10, 15, ...}.

On a skipped day, `ProcessBiologyStep` is not called, so no species `Population`, `Condition`, performance value, accumulator, or `Last*` field changes. `RecordStep` still writes a record for that day (SimulationRunner.cs:464-584). Every field in that record falls into one of three rules, and the rule is fixed by whether the field's right hand side is guarded by `biologyRan`, read live from the ecosystem, or read from a `Last*`/`StartPopBySpecies` cache that only `ProcessBiologyStep` rewrites.

The three rules for a skipped day:

1. Zeroed: the field is written `biologyRan ? value : 0`, so it is 0 on a skipped day.
2. Live: the field is read directly from current ecosystem state with no `biologyRan` guard. Because no biology ran, the live value equals the last biology day's value, but it is read fresh from the ecosystem each day, not copied from the previous record.
3. Cached: the field is read from a `Last*` or `StartPopBySpecies` collection that is cleared and repopulated only inside `ProcessBiologyStep` (EcosystemSimulator.cs:550-584). On a skipped day those collections still hold the last biology day's values, so the field reuses the last computed value.

Tier level fields on a skipped day (SimulationRunner.cs:480-536):

| Field(s) | Rule | Skipped day value | Source |
|----------|------|-------------------|--------|
| `Day`, `Year`, `Temperature` | live (loop args) | the day's own values | SimulationRunner.cs:482-484 |
| `BiologyCycle` | zeroed | `0` | SimulationRunner.cs:485 |
| `StartPop` | zeroed | `0` | SimulationRunner.cs:488 |
| `EndPop`, `Tier1Pop`, `Tier2Pop` | live, then `SafePopToLong` | current rounded populations (equal last biology day) | SimulationRunner.cs:489, 494-495 |
| `EatenT1`, `TempDeathsT1/T2`, `ConditionDeathsT1/T2`, `NaturalDeathsT1/T2`, `TotalDeaths` | zeroed, biology day value `SafePopToLong` rounded | `0` | SimulationRunner.cs:467-478, 498-505 |
| `BirthsT1`, `BirthsT2` | zeroed, biology day value `SafePopToLong` rounded | `0` | SimulationRunner.cs:508-509 |
| `ReproScaleT1` (from `Ecosystem.LastReproScaleT1`), `ReproScaleT2` (from `Ecosystem.LastReproScaleT2`) | zeroed | `0` | SimulationRunner.cs:512-513 |
| `FedRateT2` (from `Ecosystem.LastFedRateT2`), `AvgHuntingEff` (from `Ecosystem.LastAvgHuntingEfficiency`) | zeroed | `0` | SimulationRunner.cs:516-517 |
| `FedRateT1`, `FoodDensityT1` | cached | last `Ecosystem.LastFedRateT1` / `LastFoodDensityT1` | SimulationRunner.cs:521-522 |
| `AvgConditionT1`, `AvgConditionT2` | live | current `Ecosystem.AvgConditionT1/T2` | SimulationRunner.cs:525-526 |
| `BirthAccumT1/T2`, `NaturalDeathAccumT1/T2`, `ConditionDeathAccumT1/T2`, `PredationAccumT1` | live | current ecosystem accumulator state (unchanged on a skipped day) | SimulationRunner.cs:529-535 |

Per species fields on a skipped day (SimulationRunner.cs:559-579):

| Field(s) | Rule | Skipped day value | Source |
|----------|------|-------------------|--------|
| `Population`, `Condition`, `ThermalPerf`, `FinalPerf` | live | current `sp.*` values (equal last biology day) | SimulationRunner.cs:563-566 |
| `HuntingEff` | live | `sp.CurrentHuntingSuccess` for Tier 2, else `0` | SimulationRunner.cs:568 |
| `FedRate` | cached | last `Ecosystem.LastFedRateBySpecies[fn]`, falling back to `sp.FedRate` | SimulationRunner.cs:567 |
| `Births`, `TempDeaths`, `ConditionDeaths`, `NaturalDeaths`, `Eaten` | zeroed | `0` | SimulationRunner.cs:569-573 |
| `ReproScale` | zeroed (biology day from `Ecosystem.LastReproScaleBySpecies[fn]`, fallback `0f`) | `0` | SimulationRunner.cs:575 |
| `BirthRate` | derived | `0`, because `births == 0` on a skipped day makes the numerator 0 | SimulationRunner.cs:574 |
| `BirthAccum`, `NaturalDeathAccum`, `ConditionDeathAccum`, `PredationAccum` | live | current accumulator state via `Ecosystem.Get*Accum(fn)` (unchanged on a skipped day) | SimulationRunner.cs:576-579 |

Note the rollup column `TierVariantPop` is also live: it is summed from each species' current `SafePopToLong(sp.Population)` in the same loop (SimulationRunner.cs:548-551), so on a skipped day it equals the last biology day's rollup, and each contribution is banker's rounded per species before the sum.

Source fields for the zeroed per species event counts: on a biology day each is read from the matching per species dictionary on `EcosystemSimulator`, all keyed by `sp.FullName` and declared at EcosystemSimulator.cs:213-220. `Births` reads `LastBirthsBySpecies` (SimulationRunner.cs:559), `TempDeaths` reads `LastTempDeathsBySpecies` (SimulationRunner.cs:570), `ConditionDeaths` reads `LastConditionDeathsBySpecies` (SimulationRunner.cs:571), `NaturalDeaths` reads `LastNaturalDeathsBySpecies` (SimulationRunner.cs:572), and `Eaten` reads `LastEatenBySpecies` (SimulationRunner.cs:573). These five dictionaries hold `long` values whose per species sums equal the corresponding tier level `Last*` float counters (the v12 rollup invariant, EcosystemSimulator.cs:212), so the per species event counts are stored directly as `long` and are not re rounded by `SafePopToLong`. `ReproScale` reads `LastReproScaleBySpecies` (a `Dictionary<string, float>`, EcosystemSimulator.cs:218) with fallback `0f`, and `FedRate` reads `LastFedRateBySpecies` (a `Dictionary<string, float>`, EcosystemSimulator.cs:219) with fallback to the live `sp.FedRate`. The dictionary reads use the helpers `GetOrZeroLong` (returns `0L` when the key is absent, SimulationRunner.cs:588-589) and `GetOrFallbackFloat` (returns the supplied fallback when the key is absent, SimulationRunner.cs:591-592). The four accumulator residual fields read `Ecosystem.GetBirthAccum(fn)`, `GetNaturalDeathAccum(fn)`, `GetConditionDeathAccum(fn)`, and `GetPredationAccum(fn)` (SimulationRunner.cs:576-579), each of which returns `0f` for an absent key (EcosystemSimulator.cs:224-231). Only `Population` on the per species record runs through `SafePopToLong` (SimulationRunner.cs:563).

### 3.4 Loop termination

The loop ends when any of these occurs:

1. `dayIndex` reaches `TotalDays` (the normal end).
2. A crash is detected on a biology day; the loop breaks immediately after recording that day. The `break` statement is at SimulationRunner.cs:446 (the whole crash block is SimulationRunner.cs:440-447, with the closing brace on line 447).
3. The cooperative stop signal is set; the loop breaks at the top of the next iteration before any work (`if (Control.Stopped) break;`, SimulationRunner.cs:422).

Because the crash and stop both `break`, the number of records (`_records.Count`) can be less than the configured `TotalDays`. The output field also named `TotalDays` does not hold the configured day count: `GetSummary` sets `SimulationSummary.TotalDays = _records.Count` (SimulationRunner.cs:940) and `ToScenarioResult` copies that into `ScenarioResult.TotalDays` (SimulationRunner.cs:1009), so both output fields carry the actual record count, which after a crash or stop is less than the configured length. The originally configured day count is preserved separately in the scenario CSV's `#config:days_per_scenario` line, which writes `runner.TotalDays` (the configured value, SimulationRunner.cs:750). A consumer that needs the intended length reads the `#config` line; a consumer that needs the actual recorded length reads the `TotalDays` output field.

---

## 4. How each day is recorded: StepRecord

`RecordStep(int day, int year, float temperature, bool biologyRan)` builds one `StepRecord` and appends it to `_records` (SimulationRunner.cs:464-584). The full field list of `StepRecord` and `PerSpeciesStepData` is in `data-structures.md`; this section covers how the day loop populates them.

### 4.1 Tier level capture

The record's tier level fields are read directly from the `EcosystemSimulator` after the biology step (SimulationRunner.cs:480-536):

- Time and environment: `Day`, `Year`, `Temperature`, `BiologyCycle` (the last is `_biologyCycleCounter` on biology days, else 0).
- Start and end populations: `StartPop` is `SafePopToLong(StartPopT1 + StartPopT2)`, where `StartPopT1`/`StartPopT2` are captured as the first two executable lines of `ProcessBiologyStep` (EcosystemSimulator.cs:546-547), on biology days, else 0 (SimulationRunner.cs:488); `EndPop` is `SafePopToLong(GetTier1Population() + GetTier2Population())`, the current combined population (SimulationRunner.cs:489). For these two combined fields the two tier sums are added in `float` first and the single sum is rounded. `SafePopToLong` is the central rounding and guard used by every population, death, and birth field stored on the record. Its definition is `float.IsFinite(pop) ? (long)Math.Round(pop) : 0L` (SimulationRunner.cs:597-598). The `float` argument widens to `double` and `Math.Round(double)` uses the default `MidpointRounding.ToEven` (banker's rounding), so a value of exactly `.5` rounds to the nearest even integer (2.5 to 2, 3.5 to 4); this is not away from zero rounding. Non finite values (NaN, positive or negative infinity) become `0L` to avoid the `long.MinValue` cast sentinel. There is no clamp on negative values: a finite negative population would round and cast to a negative `long`. Under normal biology populations stay at or above 0, so this path is not exercised, but `SafePopToLong` itself does not floor at 0.

The complete set of record fields that are stored through `SafePopToLong`, and how they combine before rounding, is:

| Stored field | Pre round expression | Combine then round, or round each part | Source line |
|--------------|----------------------|----------------------------------------|-------------|
| `StartPop` | `StartPopT1 + StartPopT2` | combine then round (one round) | SimulationRunner.cs:488 |
| `EndPop` | `GetTier1Population() + GetTier2Population()` | combine then round (one round) | SimulationRunner.cs:489 |
| `Tier1Pop` | `GetTier1Population()` | rounded on its own | SimulationRunner.cs:494 |
| `Tier2Pop` | `GetTier2Population()` | rounded on its own | SimulationRunner.cs:495 |
| `EatenT1` | `eatenT1` float local | rounded on its own | SimulationRunner.cs:498 |
| `TempDeathsT1` | `tempDeathsT1` float local | rounded on its own | SimulationRunner.cs:499 |
| `TempDeathsT2` | `tempDeathsT2` float local | rounded on its own | SimulationRunner.cs:500 |
| `ConditionDeathsT1` | `conditionDeathsT1` float local | rounded on its own | SimulationRunner.cs:501 |
| `ConditionDeathsT2` | `conditionDeathsT2` float local | rounded on its own | SimulationRunner.cs:502 |
| `NaturalDeathsT1` | `naturalDeathsT1` float local | rounded on its own | SimulationRunner.cs:503 |
| `NaturalDeathsT2` | `naturalDeathsT2` float local | rounded on its own | SimulationRunner.cs:504 |
| `TotalDeaths` | `totalDeaths` float local (sum of the seven death floats) | sum the seven floats, then round once | SimulationRunner.cs:505 |
| `BirthsT1` | `Ecosystem.LastBirthsT1` (when `biologyRan`) | rounded on its own | SimulationRunner.cs:508 |
| `BirthsT2` | `Ecosystem.LastBirthsT2` (when `biologyRan`) | rounded on its own | SimulationRunner.cs:509 |

Per species `Population` is also stored through `SafePopToLong(sp.Population)` (SimulationRunner.cs:563), and the per species event counts (`Births`, `TempDeaths`, `ConditionDeaths`, `NaturalDeaths`, `Eaten`) are already `long` dictionary values and are stored without re rounding (Section 4.2). A reimplementer must apply `SafePopToLong` to exactly the fields in this table, with the combine then round versus round each part distinction shown, or the daily CSV integers will diverge from the real output whenever an accumulator carries a fractional part.
- Per tier populations: `Tier1Pop`, `Tier2Pop`. Each is routed individually through `SafePopToLong`: `Tier1Pop = SafePopToLong(Ecosystem.GetTier1Population())` and `Tier2Pop = SafePopToLong(Ecosystem.GetTier2Population())` (SimulationRunner.cs:494-495). The two tiers are rounded separately before storage, not summed first, so each tier total is banker's rounded on its own and a non finite tier population becomes `0L`. `GetTier1Population()` and `GetTier2Population()` each return a `float` sum of the `Population` of every species in that tier (EcosystemSimulator.cs:1382-1383); with Tier 2 gated off `GetTier2Population()` is `0f` and `Tier2Pop` is `0`.
- Death counts by pathway: `EatenT1`, `TempDeathsT1`, `TempDeathsT2`, `ConditionDeathsT1`, `ConditionDeathsT2`, `NaturalDeathsT1`, `NaturalDeathsT2`. Each is first read into a `float` local from the matching `Ecosystem.Last*` accumulator on biology days, else `0f` (SimulationRunner.cs:467-473), then each `float` local is passed through `SafePopToLong` before it is stored on the record (SimulationRunner.cs:498-504). `TotalDeaths` is the `float` sum of all seven locals (SimulationRunner.cs:476-478) and is then itself passed through `SafePopToLong` for storage (SimulationRunner.cs:505). So every death field on the record is a banker's rounded `long`, not the raw float accumulator value: a fractional accumulator such as `2.5` stores `2`, `3.5` stores `4`, and a non finite accumulator stores `0L`. The matching ecosystem fields are `LastEatenT1`, `LastTempDeathsT1`, `LastTempDeathsT2`, `LastConditionDeathsT1`, `LastConditionDeathsT2`, `LastNaturalDeathsT1`, `LastNaturalDeathsT2`, all `float` properties on `EcosystemSimulator` (EcosystemSimulator.cs:164-175). Because the round is applied to each pathway separately and again to the float sum, `TotalDeaths` is the round of the float sum of the seven raw floats, which can differ from the sum of the seven separately rounded `long` fields by up to a few units when several accumulators carry fractions.
- Births: `BirthsT1`, `BirthsT2`. Each is `biologyRan ? SafePopToLong(Ecosystem.LastBirthsT1) : 0` and `biologyRan ? SafePopToLong(Ecosystem.LastBirthsT2) : 0` (SimulationRunner.cs:508-509). `LastBirthsT1` and `LastBirthsT2` are `float` properties on `EcosystemSimulator` (EcosystemSimulator.cs:167, 175), so each stored birth count is a banker's rounded `long` of the raw float birth quantity, with a non finite value mapping to `0L`.
- Feeding, condition, reproduction scale, and the four accumulator families, each read from the corresponding `Ecosystem` field. These remain `float` on the record and are not rounded. Their exact source fields and zeroing rules are given in the skipped day tables in Section 3.3 and the `float` field list below.

The exact source field for each `float` (non rounded) tier level record field is fixed in code as follows (SimulationRunner.cs:512-535):

| Record field | Source on a biology day | Source on a skipped day | Source line |
|--------------|-------------------------|-------------------------|-------------|
| `ReproScaleT1` | `Ecosystem.LastReproScaleT1` | `0f` | SimulationRunner.cs:512 |
| `ReproScaleT2` | `Ecosystem.LastReproScaleT2` | `0f` | SimulationRunner.cs:513 |
| `FedRateT2` | `Ecosystem.LastFedRateT2` | `0f` | SimulationRunner.cs:516 |
| `AvgHuntingEff` | `Ecosystem.LastAvgHuntingEfficiency` | `0f` | SimulationRunner.cs:517 |
| `FedRateT1` | `Ecosystem.LastFedRateT1` | `Ecosystem.LastFedRateT1` (same field, cached) | SimulationRunner.cs:521 |
| `FoodDensityT1` | `Ecosystem.LastFoodDensityT1` | `Ecosystem.LastFoodDensityT1` (same field, cached) | SimulationRunner.cs:522 |
| `AvgConditionT1` | `Ecosystem.AvgConditionT1` | `Ecosystem.AvgConditionT1` (live) | SimulationRunner.cs:525 |
| `AvgConditionT2` | `Ecosystem.AvgConditionT2` | `Ecosystem.AvgConditionT2` (live) | SimulationRunner.cs:526 |
| `BirthAccumT1` / `BirthAccumT2` | `Ecosystem.BirthAccumT1` / `BirthAccumT2` | same (live) | SimulationRunner.cs:529-530 |
| `NaturalDeathAccumT1` / `NaturalDeathAccumT2` | `Ecosystem.NaturalDeathAccumT1` / `NaturalDeathAccumT2` | same (live) | SimulationRunner.cs:531-532 |
| `ConditionDeathAccumT1` / `ConditionDeathAccumT2` | `Ecosystem.ConditionDeathAccumT1` / `ConditionDeathAccumT2` | same (live) | SimulationRunner.cs:533-534 |
| `PredationAccumT1` | `Ecosystem.PredationAccumT1` | same (live) | SimulationRunner.cs:535 |

`LastReproScaleT1`, `LastReproScaleT2`, `LastFedRateT2`, `LastAvgHuntingEfficiency`, `LastFedRateT1`, and `LastFoodDensityT1` are all `float` properties on `EcosystemSimulator` (EcosystemSimulator.cs:176-186). `FedRateT2`, `AvgHuntingEff`, `ReproScaleT2`, `AvgConditionT2`, and the Tier 2 accumulator fields belong to the secondary legacy Tier 2 path and read 0 in a Tier 1 only run.

`StartPop` reflects the population before the day's biology; `EndPop` reflects the population after the day's rounding step. On a skipped biology day, `StartPop` is 0 but `EndPop` still reports the current carried forward population. The tier `StartPopT1`/`StartPopT2` capture (EcosystemSimulator.cs:546-547) is separate from the per species `StartPopBySpecies` snapshot that feeds per species `BirthRate`; the latter is taken later in the same reset block (EcosystemSimulator.cs:573-583), one entry per species.

### 4.2 Per species capture

After the tier level fields, `RecordStep` loops over `Ecosystem.Species` and writes one `PerSpeciesStepData` per species into `record.SpeciesData`, keyed by `sp.FullName` (SimulationRunner.cs:540-581). In the same loop it accumulates the dynamic tier variant rollup column `Tier{tier}_{SanitizeColumnName(label)}` into `record.TierVariantPop` (SimulationRunner.cs:548-551). The label is `string.IsNullOrEmpty(sp.VariantLabel) ? sp.Name : sp.VariantLabel` (SimulationRunner.cs:548), so "empty" means null or the zero length string. A whitespace only `VariantLabel` is not empty by this test and is used as is. `SanitizeColumnName` replaces every character outside `[A-Za-z0-9_]` with `_` and prepends `_` if the result starts with a digit (SimulationRunner.cs:322-337); the same transform and the `_2`/`_3` collision suffix rule must be applied identically wherever this column key is reproduced. The full sanitization rules and the per species to tier rollup invariant are documented in `csv-output-formats.md`.

Each per species entry reads:

- `Population` (via `SafePopToLong(sp.Population)`), `Condition` (`sp.Condition`), `ThermalPerf` (the species `RawThermalPerformance`), `FinalPerf` (the species `FinalPerformance`), `FedRate` (read from `Ecosystem.LastFedRateBySpecies[FullName]`, falling back to the live `sp.FedRate` when the key is absent, SimulationRunner.cs:567), `HuntingEff` (`sp.CurrentHuntingSuccess` for Tier 2, `0f` for Tier 1, SimulationRunner.cs:568).
- Event counts pulled from per species dictionaries by `FullName` on biology days, else 0: `Births`, `TempDeaths`, `ConditionDeaths`, `NaturalDeaths`, `Eaten` (SimulationRunner.cs:559-573).
- `BirthRate` is the per capita birth count for the day, computed as `startPop > 0L ? (float)births / startPop : 0f` (SimulationRunner.cs:574). Both operands are `long`: `births` is the day's per species birth count read from `Ecosystem.LastBirthsBySpecies[FullName]` (0 on a skipped day), and `startPop` is the species' start of day population. The division is floating point because `births` is cast to `float` before dividing. The guard means `BirthRate` is exactly `0f` whenever `startPop == 0L`; there is no NaN or infinity, and the result is never an integer division. `startPop` is read from `Ecosystem.StartPopBySpecies[FullName]`, with a defensive fallback to the current rounded population when that entry is 0 but the species currently has a positive population (SimulationRunner.cs:553-557). On a skipped day `StartPopBySpecies` still holds the last biology day's snapshot, but `births` is 0, so `BirthRate` is 0 regardless of that snapshot.
- `ReproScale`, read from `Ecosystem.LastReproScaleBySpecies[FullName]` on biology days with fallback `0f`, and `0f` on skipped days (SimulationRunner.cs:575), and the four accumulator residuals (`BirthAccum`, `NaturalDeathAccum`, `ConditionDeathAccum`, `PredationAccum`), read via `Ecosystem.GetBirthAccum(fn)`, `GetNaturalDeathAccum(fn)`, `GetConditionDeathAccum(fn)`, and `GetPredationAccum(fn)` (SimulationRunner.cs:576-579), each returning `0f` for an absent key.

The accumulator residuals are the fractional carry over each biology step uses to convert fractional birth and death quantities into whole population changes; they are described in `biology-and-formulas.md` and `data-structures.md`.

### 4.3 From records to ScenarioResult

After the loop, `ToScenarioResult` (SimulationRunner.cs:982) folds `_records` into a `ScenarioResult`:

- `GetSummary()` computes whole run scalars into a `SimulationSummary` (SimulationRunner.cs:934-977). It returns `null` when `_records.Count == 0` (SimulationRunner.cs:936). The `SimulationSummary` fields and how each is filled are listed in the table below. The non obvious behaviors are: (a) `MinTier1Pop` and `MinTier2Pop` use a `>= 1` guard, so a day on which the tier population is 0 is excluded from the running minimum; the minimum is taken only over days where the tier had at least one individual (SimulationRunner.cs:963, 965). (b) If a tier was never populated above 0 on any recorded day, its `minT1`/`minT2` accumulator stays at its `long.MaxValue` seed and is converted to `0` at the end (`minT1 == long.MaxValue ? 0 : minT1`, SimulationRunner.cs:969, 971). (c) `FinalTier1Pop` and `FinalTier2Pop` are taken from the last record (`_records[_records.Count - 1]`, SimulationRunner.cs:947-949), so after a crash or stop they are the populations on the last recorded day, not on the configured final day. (d) The maxima use a `0` seed (`maxT1 = 0`, SimulationRunner.cs:951), so a tier whose population never exceeds 0 reports `MaxTier1Pop == 0`; there is no `>= 1` guard on the maxima.

`SimulationSummary` fields (SimulationRunner.cs:1206-1230):

| Field | Type | Value | Source line |
|-------|------|-------|-------------|
| `TotalDays` | `int` | `_records.Count` (actual recorded days, not the configured length) | SimulationRunner.cs:940 |
| `TotalBiologyCycles` | `int` | `_biologyCycleCounter` | SimulationRunner.cs:941 |
| `Crashed` | `bool` | `HasCrashed` | SimulationRunner.cs:942 |
| `CrashDay` | `int` | `CrashDay` (`-1` if no crash) | SimulationRunner.cs:943 |
| `CrashTier` | `int` | `CrashTier` (`-1` if no crash; see Section 5.2) | SimulationRunner.cs:944 |
| `FinalTier1Pop` | `long` | `lastRecord.Tier1Pop` | SimulationRunner.cs:948 |
| `FinalTier2Pop` | `long` | `lastRecord.Tier2Pop` | SimulationRunner.cs:949 |
| `MaxTier1Pop` | `long` | max of `r.Tier1Pop` over all records, seeded at `0` | SimulationRunner.cs:962, 968 |
| `MinTier1Pop` | `long` | min of `r.Tier1Pop` over records with `r.Tier1Pop >= 1`, `0` if none | SimulationRunner.cs:963, 969 |
| `MaxTier2Pop` | `long` | max of `r.Tier2Pop` over all records, seeded at `0` | SimulationRunner.cs:964, 970 |
| `MinTier2Pop` | `long` | min of `r.Tier2Pop` over records with `r.Tier2Pop >= 1`, `0` if none | SimulationRunner.cs:965, 971 |
| `AvgTemperature` | `float` | `tempSum / _records.Count` (mean of every recorded day's `Temperature`) | SimulationRunner.cs:972 |
| `MinTemperature` | `float` | min of `r.Temperature` over all records (seeded `float.MaxValue`) | SimulationRunner.cs:973 |
| `MaxTemperature` | `float` | max of `r.Temperature` over all records (seeded `float.MinValue`) | SimulationRunner.cs:974 |

All thirteen statistics are computed in a single `foreach (var r in _records)` pass (SimulationRunner.cs:957-966) plus the final conversions (SimulationRunner.cs:968-974). `ToScenarioResult` copies each of these onto the matching `ScenarioResult` field, substituting `0` (or `0f` for temperatures) when `GetSummary()` returned `null` (SimulationRunner.cs:1009-1024). A reimplementer that computes a plain unguarded minimum will report `0` for `MinTier1Pop` on any run where the population touched 0 on some day but recovered, which disagrees with the real output; the guard keeps the minimum at the smallest nonzero daily population.
- `ComputePopulationStats()` computes per column mean, max, min, standard deviation, and extinction day for `Tier1Pop`, `Tier2Pop`, the dynamic rollup columns, and each species by `FullName` (SimulationRunner.cs:622-736). The standard deviation is the population standard deviation (divide by N, not N minus 1): `Math.Sqrt(varianceSum / _records.Count)` for the tier and rollup columns (SimulationRunner.cs:673) and `Math.Sqrt(variance)` from `(sqSum / dayCount) - mean^2` for the per species columns (SimulationRunner.cs:853-858). N is the count of all recorded days (`_records.Count`), including zero population days. Extinction day here is computed for the rollup columns only; the per species block in this method computes mean, max, min, and standard deviation but no extinction day (Section 5.3).
- `ComputePerSpeciesScenarioMetrics()` computes per species final year and full run metrics (Section 5.4).
- `CsvData` is the full scenario CSV from `ToCsvInternal` (SimulationRunner.cs:1035). In bulk mode this string is streamed to the ZIP and then nulled to free memory (BulkSimulationController.cs:311).

---

## 5. Crash and extinction detection

The simulation distinguishes a whole ecosystem crash (every species dead) from a single species extinction (one species reaches 0 while others survive). They are detected at different scopes and recorded differently.

How Tier 2 is excluded (secondary legacy): with `Tier2Enabled = false` (the default), `InitializeFromRunSpeciesList` skips any species whose 0 based database tier is 1 (`if (!Tier2Enabled && data.tier == 1) continue;`, EcosystemSimulator.cs:336) and logs a warning if the list contained any (EcosystemSimulator.cs:331-332). Bulk CSV input with `tier != 0` is rejected at parse (per the `Tier2Enabled` tooltip, SimulationConfig.cs:127-129). So in a current Tier 1 only run `Species` holds no Tier 2 entries, `GetTier2Population()` returns 0, and every Tier 2 term below contributes nothing. The Tier 2 references in the crash and extinction code are retained from the original two tier design.

### 5.1 Ecosystem crash: total population zero

`EcosystemSimulator.HasCrashed()` returns true when the combined population of all tiers is exactly 0 (EcosystemSimulator.cs:1386-1390):

```csharp
public bool HasCrashed()
{
    float totalPop = GetTier1Population() + GetTier2Population();
    return totalPop == 0;
}
```

`GetTier1Population()` and `GetTier2Population()` sum the `Population` of every species in the respective tier (EcosystemSimulator.cs:1382-1383). The `Population` values are integer valued when this comparison runs: step 10 of the biology sequence rounds every species' `Population` to a whole number via `Math.Round(sp.Population, MidpointRounding.AwayFromZero)` (EcosystemSimulator.cs:681), and the crash check runs only on biology days immediately after `ProcessBiologyStep` returns. The summed `float` is therefore an exact non negative integer, so the `== 0` float equality test is safe and reaches 0 only when every species rounded to 0; there is no fractional residue that could sit just above 0. With Tier 2 gated off there are no Tier 2 species, so `HasCrashed()` is effectively "all Tier 1 species are extinct."

The day loop checks this only on biology days, immediately after `RecordStep`, and only then latches the crash and breaks (SimulationRunner.cs:440-447):

```csharp
if (runBiology && Ecosystem.HasCrashed())
{
    HasCrashed = true;
    CrashDay   = displayDay;
    CrashTier  = Ecosystem.GetCrashedTier();
    break;
}
```

The crash check is guarded by `runBiology` because on skipped days the populations did not change, so re evaluating the crash would be redundant. The crashed day's record is already in `_records` (it was appended by `RecordStep` before the check), so the CSV always contains the day on which the population reached 0.

### 5.2 Which tier crashed: GetCrashedTier

`GetCrashedTier()` reports a coarse code for which tier emptied (EcosystemSimulator.cs:1392-1398):

```csharp
public int GetCrashedTier()
{
    if (_tier1WasPopulated && GetTier1Population() == 0 && GetTier2Population() == 0) return 0; // all dead
    if (_tier1WasPopulated && GetTier1Population() == 0) return 1;
    if (_tier2WasPopulated && GetTier2Population() == 0) return 2;
    return -1;
}
```

| Return value | Meaning |
|--------------|---------|
| `0` | Both tiers started populated and both are now empty (total ecosystem death) |
| `1` | Tier 1 started populated and is now empty |
| `2` | Tier 2 started populated and is now empty |
| `-1` | No tracked tier reached zero from a populated start |

`_tier1WasPopulated` and `_tier2WasPopulated` are set once by whichever species initialization method runs. There are three such methods and exactly one executes per scenario: `InitializeFromRunSpeciesList` (the primary path, EcosystemSimulator.cs:391-392), the legacy `InitializeFromDatabase` overload (EcosystemSimulator.cs:453-454), and the fallback `InitializeDefaultSpecies` (EcosystemSimulator.cs:521-522). All three set `_tier1WasPopulated = GetTier1Population() > 0` and `_tier2WasPopulated = GetTier2Population() > 0`. `ProcessBiologyStep` (EcosystemSimulator.cs:543) never rewrites them, so they retain the run's initial condition, and the comparison reports which initially populated tier emptied. In a Tier 1 only run, `_tier2WasPopulated` is false, so a crash returns `1` (Tier 1 emptied), never `2`.

`Run()` calls one of these initialization methods on every invocation (SimulationRunner.cs:403-411), so the two latches are reassigned each time `Run()` starts. A `SimulationRunner` reused across runs is therefore re-initialized cleanly. In normal flow each scenario already constructs a fresh `SimulationRunner` (Section 1.1), so the latches always reflect the current scenario's starting populations.

`CrashTier` is stored on the `SimulationRunner` and copied into `ScenarioResult.CrashTier` (SimulationRunner.cs:1013) and `SimulationSummary.CrashTier` (SimulationRunner.cs:944).

### 5.3 Per species extinction day

Extinction of an individual species is computed during result roll up, not during the loop, and does not stop the run. In `ComputePopulationStats`, a species or rollup column's extinction day is the first day its population hits 0 after having been positive (SimulationRunner.cs:681-696 for rollup columns; SimulationRunner.cs:840-859 for species in the summary block):

```csharp
int extinctionDay = -1;
bool wasAlive = false;
foreach (var r in _records)
{
    long pop = GetPopColumn(r, variant);
    if (pop > 0) wasAlive = true;
    if (wasAlive && pop == 0) { extinctionDay = r.Day; break; }
}
```

A species that is never alive (always 0) and a species that survives to the end both get `extinctionDay == -1`. The window is always the full run: the loops at SimulationRunner.cs:681-696 and 840-859 iterate every record in `_records`, so extinction day is never a final year quantity.

Two distinct outputs carry extinction days, and they use different key sets:

- The scenario CSV's `#extinction:` block lists per species extinction days keyed by `FullName`. These are computed in the CSV summary block as `spExtinctionDay` (SimulationRunner.cs:840-859) and written at SimulationRunner.cs:901-908.
- `ScenarioResult.ExtinctionDay` (assigned `popStats.ExtinctionDay` at SimulationRunner.cs:1034) does NOT carry per species `FullName` keys. `ComputePopulationStats` populates `stats.ExtinctionDay` only for the dynamic tier variant rollup columns (the `foreach (var variant in rollupCols)` loop at SimulationRunner.cs:681-696). The per species block that follows (SimulationRunner.cs:698-733) computes only Mean, Max, Min, and StdDev keyed by `FullName` and writes no `ExtinctionDay` entry. So `ScenarioResult.ExtinctionDay` is keyed by rollup column names such as `Tier1_Cold`, not by species `FullName`.

### 5.4 Per species crash day

`ComputePerSpeciesScenarioMetrics` also computes a per species crash day distinct from extinction (SimulationRunner.cs:1060-1186). A species "crashes" on the first day its population drops below a threshold relative to its start population. The loop walks `_records` in order and the assignment is gated so it records the first crossing, not the last (SimulationRunner.cs:1111-1117):

```csharp
// crashDay starts at -1; startPop is the species' day-0 population (set when i == 0).
if (crashDay < 0 && startPop > 0L)
{
    long threshold = Math.Max(CRASH_FLOOR, (long)(startPop * CRASH_FRACTION));
    if (d.Population < threshold)
        crashDay = rec.Day;
}
```

The `crashDay < 0` guard means once a crash day is set it is never overwritten, so `crashDay` is the day of the first below threshold crossing. `startPop` is the species' day 0 population: it is captured as `startPop = d.Population` only on the first record (`if (i == 0) startPop = d.Population;`, SimulationRunner.cs:1099), so the threshold is the population at run start, not the window start or the full run aggregate. The threshold is recomputed each day from that fixed `startPop`, but since `startPop` does not change, the threshold value is constant for a given species. `(long)(startPop * CRASH_FRACTION)` truncates toward zero (C# `float`-to-`long` cast), and `Math.Max(CRASH_FLOOR, ...)` then takes the larger of that truncated value and `CRASH_FLOOR`. The constants are `CRASH_FRACTION = 0.05f` and `CRASH_FLOOR = 10L` (SimulationRunner.cs:1057-1058), so a species crashes when it falls below the larger of 5 percent of its start population (truncated) or 10 individuals. The source comments mark these as placeholder defaults that may be tuned. This crash day is recorded only in the per species metrics; it does not affect the scenario loop or the ecosystem level crash.

Window: this per species crash day is a full run quantity. The crash check runs inside the single `for (int i = 0; i < _records.Count; i++)` pass over every record (SimulationRunner.cs:1093-1117), outside the `if (i >= finalYearStart)` final year block, so it scans the whole run and produces one `CrashDay` per species. Extinction day (Section 5.3) is likewise always full run. The final year window (Section 5.5) splits only the condition, birth rate, population, and death sum aggregates; it does not produce a separate final year crash day or extinction day.

### 5.5 Final year window

The per species metrics split into a full run window (all recorded days) and a final year window (the last `FINAL_YEAR_DAYS = 365` days, SimulationRunner.cs:1052). The window start is `finalYearStart = Math.Max(0, totalDays - FINAL_YEAR_DAYS)` where `totalDays = _records.Count` (SimulationRunner.cs:1066-1067), and a record is in the final year window when its loop index `i >= finalYearStart` (SimulationRunner.cs:1138). For runs shorter than 365 days `finalYearStart` is 0, so the final year window covers all days and the final year metrics equal the full run metrics.

`FINAL_YEAR_DAYS = 365` (SimulationRunner.cs:1052) and `TemperatureCalculator.DAYS_PER_YEAR = 365` (TemperatureCalculator.cs:30) are two independent constants that both happen to equal 365. `FINAL_YEAR_DAYS` sizes the final year metric window; `DAYS_PER_YEAR` drives the `Year` column and the temperature model. They are not linked in code, so changing one would not move the other. The final year window is hard coded to `FINAL_YEAR_DAYS` and does not track `DAYS_PER_YEAR`.

Condition and birth rate means are computed over only the days a species was alive (population greater than 0), because once a species hits 0 its condition stops updating and would otherwise pollute the mean (SimulationRunner.cs:1119-1133 for the full run window; SimulationRunner.cs:1138-1144 for the final year window, both guarded by `if (d.Population > 0L)`). The divisor is the count of alive days in the window, not the window length: `condSumYear / condCountYear` and `brSumYear / brCountYear`, where `condCountYear`/`brCountYear` increment only on alive days (SimulationRunner.cs:1166-1169). The birth rate metric is the mean of the per day `BirthRate` field (Section 4.2), not a separately recomputed per capita rate: the loop accumulates `d.BirthRate` directly (SimulationRunner.cs:1131, 1143).

When a species is never alive in a window the divisor would be 0; the code substitutes a fallback rather than dividing by zero. For the final year metrics, if `condCountYear == 0` (or `brCountYear == 0`) the value falls back to the full run mean, and if the full run count is also 0 the value is `0f` (SimulationRunner.cs:1167, 1169). For the full run metrics, a never alive species yields `0f` directly (`condCountFull > 0 ? ... : 0f`, SimulationRunner.cs:1166, 1168). Population statistics include the zero days, because zero is a real datum for an extinct species; the population mean divides by every recorded day in the window (`popCountFull`/`popCountYear`, SimulationRunner.cs:1133-1134, 1145-1146).

### 5.6 The complete PerSpeciesScenarioMetrics field set

`ComputePerSpeciesScenarioMetrics` returns `Dictionary<string, PerSpeciesScenarioMetrics>` keyed by `sp.FullName` (SimulationRunner.cs:1060-1186) and is assigned to `ScenarioResult.SpeciesMetrics` (SimulationRunner.cs:1017). It returns an empty dictionary when there are no records or no species (SimulationRunner.cs:1063-1064), and skips any species that never appeared in any `StepRecord` (`if (popCountFull == 0) continue;`, SimulationRunner.cs:1157). Every metric for one species is computed in a single `for (int i = 0; i < _records.Count; i++)` pass (SimulationRunner.cs:1093-1153) using running accumulators, so no per species sublist is materialized. These metrics feed the v12 `PER-SPECIES FINAL YEAR METRICS`, `PER-SPECIES FULL-RUN METRICS`, and `PER-SPECIES STABILITY METRICS` aggregate sections documented in `csv-output-formats.md`.

The complete field list of `PerSpeciesScenarioMetrics` (ScenarioResult.cs:102-138) and how each is produced:

| Field | Type | Definition | Source line |
|-------|------|------------|-------------|
| `FullName` | `string` | the species key `sp.FullName` | SimulationRunner.cs:1164 |
| `FinalPopulation` | `long` | `d.Population` on the last record the species appeared in (`finalPop`, updated every iteration) | SimulationRunner.cs:1100, 1165 |
| `MeanConditionFullRun` | `float` | `condSumFull / condCountFull` over alive days, else `0f` | SimulationRunner.cs:1166 |
| `MeanConditionFinalYear` | `float` | `condSumYear / condCountYear` over final year alive days; falls back to the full run mean when `condCountYear == 0`, then to `0f` | SimulationRunner.cs:1167 |
| `MeanBirthRateFullRun` | `float` | `brSumFull / brCountFull` over alive days, else `0f` | SimulationRunner.cs:1168 |
| `MeanBirthRateFinalYear` | `float` | `brSumYear / brCountYear` over final year alive days; same fallback chain as condition | SimulationRunner.cs:1169 |
| `PopCvFullRun` | `float` | `ComputeCvFromSums(popSumFull, popSqSumFull, popCountFull)` (see formula below) | SimulationRunner.cs:1170 |
| `PopCvFinalYear` | `float` | `ComputeCvFromSums(popSumYear, popSqSumYear, popCountYear)` when `popCountYear > 0`, else the full run CV | SimulationRunner.cs:1171-1173 |
| `MeanPopulationFinalYear` | `float` | `popSumYear / popCountYear` (population mean over the final year, including zero days) when `popCountYear > 0`, else `popSumFull / popCountFull` | SimulationRunner.cs:1160, 1174 |
| `FinalYearTempDeaths` | `float` | sum of per species `d.TempDeaths` over final year records | SimulationRunner.cs:1148, 1175 |
| `FinalYearConditionDeaths` | `float` | sum of per species `d.ConditionDeaths` over final year records | SimulationRunner.cs:1149, 1176 |
| `FinalYearNaturalDeaths` | `float` | sum of per species `d.NaturalDeaths` over final year records | SimulationRunner.cs:1150, 1177 |
| `FinalYearPredationDeaths` | `float` | sum of per species `d.Eaten` over final year records (predation deaths; Tier 1 only) | SimulationRunner.cs:1151, 1178 |
| `MinPopulation` | `long` | minimum `d.Population` over all records the species appeared in; `0L` if the species never appeared (`minPop` seeded `long.MaxValue`) | SimulationRunner.cs:1103, 1179 |
| `MaxPopulation` | `long` | maximum `d.Population` over all records; `0L` if never appeared (`maxPop` seeded `long.MinValue`) | SimulationRunner.cs:1104, 1180 |
| `ExtinctionDay` | `int` | first `rec.Day` where `d.Population == 0` after having been positive; `-1` if never extinct or never alive | SimulationRunner.cs:1107-1109, 1181 |
| `CrashDay` | `int` | first `rec.Day` below the crash threshold (Section 5.4); `-1` if never crossed | SimulationRunner.cs:1112-1116, 1182 |
| `Survived` | `bool` (computed property) | `FinalPopulation > 0` | ScenarioResult.cs:138 |

This `ExtinctionDay` is per species and is distinct from `ComputePopulationStats.ExtinctionDay` (Section 5.3), which is keyed by rollup column name. The two are computed by separate passes with the same first hit logic but different key sets. `MinPopulation`, `MaxPopulation`, `PopCvFullRun`, `PopCvFinalYear`, and the four `FinalYear*Deaths` fields are produced only here and have no counterpart in `ComputePopulationStats`.

`ComputeCvFromSums` computes a population coefficient of variation from running sums (SimulationRunner.cs:1192-1200):

```text
ComputeCvFromSums(sum, sqSum, count):
    if count <= 0:          return 0
    mean = sum / count
    if mean <= 1e-4:        return 0      # CV undefined for ~zero mean
    variance = (sqSum / count) - (mean * mean)
    if variance <= 0.0:     return 0      # numerical guard against tiny negatives
    return sqrt(variance) / mean
```

Variables: `sum` is the sum of daily populations in the window, `sqSum` is the sum of squared daily populations (`(double)d.Population * d.Population` accumulated per day, SimulationRunner.cs:1133, 1145), and `count` is the number of days in the window (population counts include zero days). `mean` and `variance` are computed in `double`; the result casts to `float`. The variance is the population variance (divide by `count`, not `count - 1`), matching `ComputePopulationStats`. Two guards return exactly `0f`: a mean at or below `1e-4` (the CV is undefined as the mean approaches zero) and a variance at or below `0.0` (which can occur from floating point cancellation when all daily populations are equal). A reimplementer that omits the `1e-4` mean floor or the negative variance clamp, or that uses sample variance, will produce different CV values than the real output. `MeanPopulationFinalYear` and the four `FinalYear*Deaths` fields all include zero population days in their sums, unlike the condition and birth rate means which filter to alive days.

---

## 6. Pause, resume, and cancel

Two distinct mechanisms exist because standard scenarios run on background `Task.Run` threads while bulk pause is checked at coarser coroutine boundaries.

### 6.1 Standard mode: RunControl

`RunControl` is a small class with two `volatile bool` fields, `Paused` and `Stopped` (RunControl.cs:9-13). The fields are `volatile` because the scenario runs on a `Task.Run` background thread while the main thread toggles them from UI callbacks. A fresh `RunControl` is created at the start of each Run (SimulationController.cs:115) and assigned to every scenario's `runner.Control` (SimulationController.cs:375).

Inside the day loop, the control is honored at the top of each iteration (SimulationRunner.cs:419-423):

```csharp
if (Control != null)
{
    while (Control.Paused && !Control.Stopped) System.Threading.Thread.Sleep(10);
    if (Control.Stopped) break;
}
```

Pausing spins the loop with a 10 millisecond sleep without consuming any RNG or advancing any state, so a paused then resumed run is byte identical to an uninterrupted run with the same seed (the design intent stated in RunControl.cs:2-5). Stopping breaks the day loop, ending the scenario early.

`Paused` and `Stopped` are two independent `volatile bool` fields (RunControl.cs:11-12), each read on its own. The spin condition `while (Control.Paused && !Control.Stopped)` re reads both every iteration, and the very next statement is `if (Control.Stopped) break;` (SimulationRunner.cs:421-422). So if `Stopped` is set while the loop is paused, the spin exits on its next read and the immediately following `Stopped` check breaks before `displayDay`, `GetTemperature`, or `ProcessBiologyStep` run for that day. No biology or RNG advances between the stop being observed and the break, which is why a stop during a pause does not perturb determinism. The two reads are not a single atomic operation, but the loop never advances state between them, so their ordering is sufficient here. `OnCancelRequested` sets `Stopped = true` and clears `Paused` together (SimulationController.cs:390) so a paused run can be broken out of.

The controller exposes `PauseSimulation()`, `ResumeSimulation()`, and `IsPaused` that set or read `_runControl.Paused` (SimulationController.cs:394-400). `OnCancelRequested()` sets both `_cancelRequested` and `_runControl.Stopped = true` and clears `Paused` so a paused run can be broken out of (SimulationController.cs:387-391). `_cancelRequested` is the outer signal that stops the scenario scheduling loop between scenarios and chunks (SimulationController.cs:174, 199); `_runControl.Stopped` is the inner signal that breaks the day loop of an in flight scenario.

### 6.2 Bulk mode: IsPaused poll

Bulk scenarios use `RunSingleScenarioFromBatch`, which does not set `runner.Control`, so the day loop's pause branch is inert. Instead, the bulk coroutine yields on `WaitWhilePaused()` at three boundaries: the batch boundary, between sequential WebGL scenarios, and between parallel chunks (BulkSimulationController.cs:166, 220, 248). `WaitWhilePaused` spins the coroutine while the results screen reports paused (BulkSimulationController.cs:403-405):

```csharp
private IEnumerator WaitWhilePaused()
{
    while (resultsScreen != null && resultsScreen.IsPaused && !_cancelRequested)
        yield return null;
}
```

Bulk pause therefore takes effect between scenarios or chunks, not mid scenario. The pause source is `ResultsScreenUI.IsPaused`, polled on the main thread, rather than the `RunControl` flags. The results screen is documented in `ui-and-io.md`.

---

## 7. Where the counts are configured

| What | Field | Default | Range or validation | Source |
|------|-------|---------|---------------------|--------|
| Days per scenario (standard) | `SimulationConfig.DaysPerScenario` | `365` | `[Range(1, 182500)]`, and `IsValid` rejects `< 1` | SimulationConfig.cs:37, 148-152 |
| Scenarios per run (standard) | `SimulationConfig.NumberOfScenarios` | `5` | `[Range(1, 100)]`, and `IsValid` rejects `< 1` | SimulationConfig.cs:44, 154-158 |
| Biology cadence | `SimulationConfig.BiologyStep` | `1` | `[Range(1, 5)]` | SimulationConfig.cs:29 |
| Base random seed | `SimulationConfig.RandomSeed` | `12345` | `-1` means system time (non reproducible) | SimulationConfig.cs:139 |
| Days per scenario (bulk) | `BulkBatchConfig.Days` | per CSV row | parsed from the uploaded CSV | SimulationController.cs:292, BulkSimulationController.cs:241 |
| Scenarios per run (bulk) | `BulkBatchConfig.NumScenarios` | per CSV row | parsed from the uploaded CSV | BulkSimulationController.cs:241, 258 |
| Runner day count | `SimulationRunner.TotalDays` | `365` | set from config or batch before `Run()` | SimulationRunner.cs:354 |
| Runner cadence | `SimulationRunner.BiologyStep` | `1` | set from config before `Run()` | SimulationRunner.cs:355 |

`SimulationController.RunSingleScenario` copies `config.DaysPerScenario` onto `runner.TotalDays` and `config.BiologyStep` onto `runner.BiologyStep` before calling `Run()` (SimulationController.cs:349-350).

In bulk mode the two counts come from different sources. `RunSingleScenarioFromBatch` copies `batch.Days` onto `runner.TotalDays` and `batch.NumScenarios` flows to `ToScenarioResult`, both from the `BulkBatchConfig` row. But `runner.BiologyStep` is taken from `config.BiologyStep`, the controller's serialized `SimulationConfig` ScriptableObject field (`SimulationController.config`, SimulationController.cs:293), not from the row. The bulk CSV has no per row BiologyStep column: `BulkBatchConfig` defines only `Days` and `NumScenarios` for timing (BulkBatchConfig.cs:44-45) and no biology cadence field. So every run in a bulk upload runs at the same cadence, the one set on the controller's `SimulationConfig`, regardless of the row. The aggregate also records that same value (`AggregateResults.BiologyStep = config.BiologyStep`, BulkSimulationController.cs:182). To reproduce a bulk run you need both the CSV row (for `Days` and `NumScenarios`) and the controller's `SimulationConfig.BiologyStep`.

Year length is the compile time constant `TemperatureCalculator.DAYS_PER_YEAR = 365` (TemperatureCalculator.cs:30); the year column and the final year window both derive from it.

### 7.1 Validation gate

`SimulationController.StartSimulation` refuses to start unless `config` is assigned and `config.IsValid(out errorMessage)` returns true (SimulationController.cs:83-94). `IsValid` enforces (SimulationConfig.cs:146-195):

1. `DaysPerScenario >= 1`.
2. `NumberOfScenarios >= 1`.
3. `RunSpecies` is non null with a non empty `speciesList`.
4. `CarryingCapacityTier1 > 0` (carrying capacity is always on as of v11.1).

A separate non fatal warning fires when the summed initial Tier 1 population exceeds `CarryingCapacityTier1`; the run still proceeds (SimulationConfig.cs:173-191).

---

## 8. Seed assignment

Seeds are derived deterministically from the base seed so a Run reproduces exactly when rerun with the same configuration.

Standard mode (SimulationController.cs:180, 209):

```csharp
int scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i;   // i is 0-based
```

Bulk mode (BulkSimulationController.cs:259):

```csharp
int seed = config.RandomSeed < 0 ? -1 : config.RandomSeed + s;           // s is 0-based
```

So scenario `i` (0 based) uses seed `RandomSeed + i`. With the default `RandomSeed = 12345` and 5 scenarios, the seeds are 12345, 12346, 12347, 12348, 12349.

When `RandomSeed` is `-1`, every scenario receives `-1`, and the two RNGs inside the scenario each turn `-1` into their own independent time seeded RNG at construction. The `SimulationRunner` constructor passes the scenario seed to both `new TemperatureCalculator(seed)` and `new EcosystemSimulator(seed)` (SimulationRunner.cs:382-387). Each constructor branches on the sign of the seed:

```csharp
// TemperatureCalculator(int seed)  (TemperatureCalculator.cs:43-46)
_rng = (seed < 0) ? new Random() : new Random(seed);

// EcosystemSimulator(int seed)     (EcosystemSimulator.cs:299-303)
_rng = seed < 0 ? new System.Random() : new System.Random(seed);
```

For a negative seed each constructor calls the parameterless `System.Random()`, whose default seed is time dependent. These are two separate `new System.Random()` calls, so the temperature RNG and the ecosystem RNG within one scenario get independent default seeds and are not correlated. The temperature and ecosystem random streams therefore diverge from each other and from run to run, which is what makes a `-1` run non reproducible (the meaning documented at SimulationConfig.cs:135-138). Because the actual seed each RNG uses is not captured anywhere, a `-1` run cannot be reproduced. The byte identical guarantee in Section 1.1 holds only for `RandomSeed >= 0`, where both RNGs are seeded from the same fixed integer and `new System.Random(seed)` is deterministic.

For a non negative seed both RNGs are constructed from the same value, so within one scenario the temperature and ecosystem streams are both deterministic functions of that single integer. The chosen seed is stored on `SimulationRunner.UsedSeed` (SimulationRunner.cs:384) and surfaced as `ScenarioResult.RandomSeed` (SimulationRunner.cs:1008) and the `#config:random_seed` CSV line (SimulationRunner.cs:753); for a `-1` run these fields record `-1`, not the time derived value the RNGs actually used.

---

## 9. Concurrency and threading summary

| Layer | Concurrency | Where |
|-------|-------------|-------|
| Run scheduling (standard) | Unity coroutine, one frame yields | `RunAllScenariosCoroutine` (SimulationController.cs:111) |
| Run scheduling (bulk) | Unity coroutine, one frame yields | `RunBulkCoroutine` (BulkSimulationController.cs) |
| Scenario execution (WebGL) | sequential on the main thread | SimulationController.cs:170-188 |
| Scenario execution (Editor/standalone) | `Task.Run`, chunked by `ProcessorCount - 1` | SimulationController.cs:189-258, BulkSimulationController.cs:240-315 |
| Day loop | synchronous, no threads of its own | `SimulationRunner.Run` (SimulationRunner.cs:392) |
| Pause/stop (standard) | `volatile` flags read on the worker thread | `RunControl` (RunControl.cs) |
| Pause (bulk) | `IsPaused` poll on the main thread | `WaitWhilePaused` (BulkSimulationController.cs:403) |

Key invariants that make parallel scenarios safe:

1. Each scenario constructs its own `SimulationRunner`, `TemperatureCalculator`, and `EcosystemSimulator`; no simulation state is shared across scenarios (SimulationController.cs:346, SimulationRunner.cs:382-387).
2. Results are written into a preallocated per index array slot, so workers never contend on a shared list; collection into the shared `Scenarios` list happens on the main thread after the chunk completes (SimulationController.cs:194, 216, 248).
3. Exceptions are captured per scenario into a parallel `taskErrors` array rather than thrown, so one failing scenario does not abort the chunk (SimulationController.cs:218-221).
4. Bulk pause and stop are honored only at coroutine boundaries (batch, scenario, chunk), never inside a worker, avoiding cross thread coordination inside the day loop (BulkSimulationController.cs:166, 220, 248).
