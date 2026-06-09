# TinySea Headless Simulation: Architecture Overview

All paths in this document are relative to `tinysea/Assets/scripts/Simulation/` unless stated otherwise. Line citations point at the current source. A reader can jump straight to the cited file and line to confirm any statement.

## 1. What this is

The TinySea headless simulation is a non-visual ecosystem model used for research and analysis, separate from the interactive Unity game. The core biology runs in plain C# classes (no `MonoBehaviour`); only the entry-point controllers are Unity components. A single execution (one random seed, run for a fixed number of days) is a Scenario. The simulator steps a list of species through a fixed 10-step daily biology sequence (thermal performance, feeding, condition update, several death types, reproduction, population rounding) while a standalone temperature model supplies a daily temperature in Celsius. The current shipping configuration is Tier 1 (prey) only: `EcosystemSimulator.Tier2Enabled` defaults to `false` (`EcosystemSimulator.cs:245`) and `SimulationConfig.Tier2Enabled` defaults to `false` (`SimulationConfig.cs:130`). Tier 2 (predator) logic, including Holling Type II predation, remains in the code from the original two-tier design but is dormant: when the gate is off, Tier-2 species are dropped at load (`EcosystemSimulator.cs:336`), Tier-2 CSV columns are suppressed (`SimulationRunner.cs:216-232`, `SimulationRunner.cs:270-286`), and the bulk CSV parser rejects any Tier-2 row outright (`CsvBatchParser.cs:318-319`). Throughout this document, Tier 1 is described in full; Tier 2 is marked as secondary legacy where it appears.

## 2. Data flow walkthrough: scene entry to CSV output

There are two independent driver chains. Both end in the same scenario engine (`SimulationRunner`) and the same CSV writers.

### 2.1 Standard run (one `SimulationConfig` ScriptableObject)

1. `SimulationController` (a `MonoBehaviour`, the scene entry point) receives a UI button press or a `[ContextMenu]` action and calls `StartSimulation()` (`SimulationController.cs:74`).
2. `StartSimulation()` validates the assigned `SimulationConfig` via `config.IsValid(out err)` (`SimulationController.cs:90`, validation logic at `SimulationConfig.cs:146`), then starts the coroutine `RunAllScenariosCoroutine()` (`SimulationController.cs:96`).
3. `RunAllScenariosCoroutine()` builds one `AggregateResults` and copies every config parameter into it (`SimulationController.cs:129`). It then runs `config.NumberOfScenarios` scenarios. On WebGL the scenarios run sequentially on the main thread; on Editor and standalone they run in `Task.Run` chunks of `ProcessorCount - 1`.
4. For each scenario it calls the private `RunSingleScenario(scenarioIndex, seed)` (`SimulationController.cs:343`). The per-scenario seed is `RandomSeed < 0 ? -1 : RandomSeed + i` (`SimulationController.cs:209`).
5. `RunSingleScenario` constructs `new SimulationRunner(seed)`, which in its constructor creates `new TemperatureCalculator(seed)` and `new EcosystemSimulator(seed)` with the same seed (`SimulationRunner.cs:382-387`). It copies timing, temperature, species, carrying capacity, condition rates, and the Tier-2 gate from the config onto the runner's `TempCalc` and `Ecosystem` (`SimulationController.cs:349-375`), then calls `runner.Run()` and returns `runner.ToScenarioResult(...)` (`SimulationController.cs:378-381`).
6. `SimulationRunner.Run()` (`SimulationRunner.cs:392`) loads species once via `Ecosystem.InitializeFromRunSpeciesList(RunSpecies)` (`SimulationRunner.cs:405`; fallback `InitializeDefaultSpecies` at `SimulationRunner.cs:410`), then loops `dayIndex` from 0 to `TotalDays - 1` (`SimulationRunner.cs:415`).
7. Each day: `float temp = TempCalc.GetTemperature(dayIndex)` (`SimulationRunner.cs:428`); biology runs on day 1 and every `BiologyStep`-th day thereafter (`runBiology` at `SimulationRunner.cs:430`); if `runBiology`, `Ecosystem.ProcessBiologyStep(temp)` runs the 10 steps (`SimulationRunner.cs:435`). Then `RecordStep(displayDay, year, temp, runBiology)` reads the engine's public getters and per-species dictionaries into a `StepRecord` (`SimulationRunner.cs:438`, body at `SimulationRunner.cs:464`). If biology ran and `Ecosystem.HasCrashed()` is true (total population 0), the loop records the crash and breaks (`SimulationRunner.cs:440-447`).
8. After the loop, `ToScenarioResult(idx, n)` (`SimulationRunner.cs:982`) computes population statistics (`ComputePopulationStats`, `SimulationRunner.cs:622`), per-species scenario metrics (`ComputePerSpeciesScenarioMetrics`, `SimulationRunner.cs:1060`), captures final populations, and builds the scenario CSV string via `ToCsvInternal` (`SimulationRunner.cs:743`). The CSV string is stored in `ScenarioResult.CsvData`.
9. Back in the controller, each `ScenarioResult` is added to `AggregateResults.Scenarios`, then `AggregateResults.CalculateAggregates()` (`ScenarioResult.cs:288`) rolls up cross-scenario statistics, and `ResultsScreenUI.DisplayResults(...)` shows them. The aggregate CSV is produced by `AggregateResults.ToAggregateCsv()` (`ScenarioResult.cs:588`) and the config CSV by `ToConfigCsv()` (`ScenarioResult.cs:948`), both on demand from the download buttons.

### 2.2 Bulk run (one uploaded CSV, many runs)

1. `CsvUploadHandler` receives a dropped CSV (or an `L`-key file picker) at `OnCsvFileReceived()` (`CsvUploadHandler.cs:120`), validates it with `ValidateCsv()` (`CsvUploadHandler.cs:158`), and parses it with `CsvBatchParser.TryParse(csv, out batches, out errors)` (`CsvBatchParser.cs:65`). The parser collects all errors and never stops at the first one.
2. When the user clicks Run, `OnRunSimulationClicked()` (`CsvUploadHandler.cs:200`) fires the event `OnRunBulkSimulation(parsedBatches)`.
3. `BulkSimulationController.OnRunBulkSimulation()` (`BulkSimulationController.cs:80`, subscribed in `Start()` at `BulkSimulationController.cs:59`) calls `RunAllBatches(batches)` (`BulkSimulationController.cs:91`). Each batch (a `BulkBatchConfig`) is one run.
4. Per batch, `ConvertSpecies()` (`BulkSimulationController.cs:723`) builds a temporary `RunSpeciesList` that never touches the project ScriptableObject. Per scenario, `SimulationController.RunSingleScenarioFromBatch(batch, tempSpecies, idx, seed)` (`SimulationController.cs:287`) constructs a fresh `SimulationRunner`, applies the batch parameters onto `TempCalc`/`Ecosystem` (`SimulationController.cs:292-311`), runs it, and returns a `ScenarioResult`.
5. Each scenario CSV is streamed out immediately and the `CsvData` field is then nulled to free memory (`BulkSimulationController.cs:226-233`). The output sink is `ServerUpload.UploadFile` (S3, WebGL non-editor only) when `ServerUpload.IsAvailable` is true (`ServerUpload.cs:33`), otherwise `WebGLZipDownload` progressive ZIP.
6. Per batch, `batchResults.CalculateAggregates()` (`BulkSimulationController.cs:324`) runs, then an `aggregate.csv` and `config.csv` are emitted.
7. After all batches, `GenerateBulkSummary(_bulkSummaries)` (`BulkSimulationController.cs:428`) writes `bulk_summary.csv` at the ZIP root, and `ResultsScreenUI.DisplayBulkResults(...)` shows the result.

## 3. The 10-step daily biology sequence

`EcosystemSimulator.ProcessBiologyStep(float temperature)` (`EcosystemSimulator.cs:543`) runs once per biology day. At the top it snapshots `StartPopT1`/`StartPopT2`, resets all `Last*` tier counters, clears all per-species dictionaries, and records `StartPopBySpecies[FullName]` for each species (`EcosystemSimulator.cs:545-584`). It then iterates `Species` (a `List<SimSpecies>`) once per step, in this order:

1. **Thermal performance** (`EcosystemSimulator.cs:589-598`). For each species: `RawThermalPerformance = CalculatePerformance(temperature)` (`SimSpecies.cs:95`); `ThermalPerformance = RawThermalPerformance * Pmax`; `FedRate` and `CurrentHuntingSuccess` reset to 1.
2. **Feeding / predation** (`EcosystemSimulator.cs:600-602`, body `ProcessFeedingWithAccumulator` at `EcosystemSimulator.cs:728`). Tier 1 computes a shared food density and a linear `FedRate`. Tier 2 (legacy) computes Holling Type II demand and removes prey. See section 4.
3. **Raw final performance** (`EcosystemSimulator.cs:604-610`). `RawFinalPerformance = RawThermalPerformance * FedRate`. This is the drain target for Condition.
4. **Update Condition** (`EcosystemSimulator.cs:612-617`, body `UpdateCondition` at `EcosystemSimulator.cs:952`). Condition moves toward `RawFinalPerformance`, asymmetric and Pmax-scaled. See section 5.
5. **Final performance** (`EcosystemSimulator.cs:619-630`). `FinalPerformance = ThermalPerformance * FedRate`. Computed for CSV and logging only; no later step reads it (`EcosystemSimulator.cs:620-624`).
6. **Thermal death** (`EcosystemSimulator.cs:632-637`, body `ApplyThermalDeath` at `EcosystemSimulator.cs:1000`). If `RawThermalPerformance == 0` (at or beyond CTmin/CTmax), the whole population dies and Condition is set to 0 (`EcosystemSimulator.cs:1008-1024`). Otherwise the species survives the step.
7. **Condition death** (`EcosystemSimulator.cs:639-644`, body `ApplyConditionDeath` at `EcosystemSimulator.cs:1056`). Graduated: only fires when `Condition < DeathThreshold`. See section 5.
8. **Reproduction** (`EcosystemSimulator.cs:646-651`, body `ApplyReproduction` at `EcosystemSimulator.cs:1150`). Condition-driven graduated scale, Pmax multiplier, Tier-1 no-predator penalty, birth accumulator. See section 6.
9. **Natural death** (`EcosystemSimulator.cs:653-658`, body `ApplyNaturalDeathWithAccumulator` at `EcosystemSimulator.cs:1270`). Flat rate plus variance, accumulator. See section 6.
10. **Population rounding and overflow guard** (`EcosystemSimulator.cs:660-686`). Each population is capped at `100 * CarryingCapacityPerTier` (`EcosystemSimulator.cs:667-668`) then rounded to the nearest integer with `MidpointRounding.AwayFromZero` (`EcosystemSimulator.cs:681`).

After step 10, `ComputeAverageCondition()` (`EcosystemSimulator.cs:1324`) sets `AvgConditionT1`/`AvgConditionT2`, `EndPopT1`/`EndPopT2` are recorded, and `UpdateAccumulatorTotals()` (`EcosystemSimulator.cs:1343`) sums the residual accumulators for CSV output.

### 3.1 Temperature model

`TemperatureCalculator.GetTemperature(day)` (`TemperatureCalculator.cs:61`) returns the daily temperature in Celsius. If a timeseries was loaded it returns the per-day series value, looping when the series is shorter than the run (`TemperatureCalculator.cs:66-75`). Otherwise it sums five components and clamps to `[MinTemp, MaxTemp]`:

```
T(day) = BaseTemperature
       + Seasonal(day)        // sin(2*pi*day/365) * SeasonalAmplitude   (zero annual mean)
       + ClimateTrend(day)    // ClimateTrendPerYear * (day/365)         (only non-zero long-term mean)
       + Interannual(day)     // per-year offset, de-biased to zero mean
       + DailyVariation(day)  // autocorrelated noise: 0.7*yesterday + 0.3*new
T(day) = clamp(T(day), MinTemp, MaxTemp)
```

Units: all temperatures Celsius, `day` is a zero-based integer day index, `DAYS_PER_YEAR = 365` (`TemperatureCalculator.cs:30`). Seasonal at `TemperatureCalculator.cs:126-129`; climate trend at `TemperatureCalculator.cs:134-138`; interannual draw with the bias-mean subtraction at `TemperatureCalculator.cs:148-170`; daily autocorrelation at `TemperatureCalculator.cs:175-196`. `WarmingBias` skews only the shape of the interannual distribution, not its mean; the long-term trend is owned solely by `ClimateTrendPerYear` (`TemperatureCalculator.cs:145-147`).

## 4. Step 2 feeding in detail

### 4.1 Tier 1 (prey): shared food pool, linear FedRate

Carrying capacity acts as a shared resource pool. The code computes (`EcosystemSimulator.cs:759-780`):

```
tier1Pop     = max(0, total Tier-1 population)
capSafe      = max(CarryingCapacityPerTier, 1)
foodDensity  = max(0, 1 - tier1Pop / capSafe)          // LastFoodDensityT1
per species (Tier 1, alive):
    FedRate  = min(1, HuntingEfficiency * foodDensity)  // linear, not Holling II
LastFedRateT1 = population-weighted mean of FedRate, or 1 if no Tier-1 population
```

Variable units: `foodDensity` is dimensionless in `[0, 1]`; `HuntingEfficiency` is dimensionless and for Tier 1 means resource extraction efficiency (default 1.0 for plankton-style extraction, `SimSpecies.cs:51`, `SimSpecies.cs:155`); `FedRate` is dimensionless in `[0, 1]`. Carrying capacity is always on as of v11.1; there is no toggle (`EcosystemSimulator.cs:121-135`, `EcosystemSimulator.cs:233-237`). Linear is used rather than Holling II because at the common `HuntingEfficiency = 1.0` the Holling half-saturation would be 0 and FedRate would jump to 1 whenever any food exists, defeating the food-pool effect (`EcosystemSimulator.cs:744-751`).

### 4.2 Tier 2 (predator) feeding (secondary legacy)

When predators and prey are both present, each predator's hunting efficiency follows Holling Type II `efficiency = ratio / (ratio + halfSaturation)` where `halfSaturation = NORMAL_PREY_RATIO * (1 - baseEff) / baseEff` and `NORMAL_PREY_RATIO = 20` (`EcosystemSimulator.cs:924-932`, constants `EcosystemSimulator.cs:269-271`). Per-predator `FedRate = min(1, huntingSuccess_i * scarcityFactor)` with `scarcityFactor = totalEaten / totalActualDemand` (`EcosystemSimulator.cs:849-862`). Prey are removed proportionally to each prey species' share of the total prey population, through a predation accumulator (`EcosystemSimulator.cs:867-896`). This entire block runs only when `Tier2Enabled` is true and both tiers have population. Current runs are Tier 1 only.

## 5. Condition system

Condition is a per-species health value in `[0, 1]` that starts at 1.0 (`SimSpecies.cs:80`) and persists across days. `UpdateCondition` (`EcosystemSimulator.cs:952-992`) moves it toward `target = RawFinalPerformance` asymmetrically with quadratic acceleration:

```
target      = RawFinalPerformance                       // dimensionless [0,1]
pmaxSafe    = max(Pmax, 1e-4)
drainRate   = sp.ConditionDrainRate    >= 0 ? sp.ConditionDrainRate    : Ecosystem.ConditionDrainRate
recoveryRate= sp.ConditionRecoveryRate >= 0 ? sp.ConditionRecoveryRate : Ecosystem.ConditionRecoveryRate

if Condition > target:   // draining
    severity      = (1 - target)^2                      // 0 at target=1, 1 at target=0
    effectiveDrain= drainRate * (1 + severity) / pmaxSafe
    Condition    -= (Condition - target) * effectiveDrain
else:                    // recovering
    boost            = target^2                          // 0 at target=0, 1 at target=1
    effectiveRecovery= recoveryRate * (1 + boost) * pmaxSafe
    Condition       += (target - Condition) * effectiveRecovery
Condition = clamp(Condition, 0, 1)
```

Per-species drain and recovery rates default to `-1`, which means "inherit the simulator-global rate" (`SimSpecies.cs:36-37`). Global defaults are `ConditionDrainRate = 0.15` and `ConditionRecoveryRate = 0.10` per day (`EcosystemSimulator.cs:240-241`). Pmax scales the rates but not the target, so Condition stays on a species-agnostic 0-to-1 scale and the thresholds need no per-species tuning (`EcosystemSimulator.cs:943-946`).

Condition death (`ApplyConditionDeath`, `EcosystemSimulator.cs:1056-1099`) fires only when `Condition < DeathThreshold`:

```
severity  = (DeathThreshold - Condition) / DeathThreshold     // 0 at threshold, 1 at Condition=0
rawDeaths = Population * severity * DeathRate * BiologyStep
// rawDeaths is run through the condition-death accumulator; whole deaths removed
// Survivor fitness boost: new Condition = oldCondition * oldPop / newPop, capped at 1
```

The survivor boost models the dead being the weakest individuals and prevents death spirals (`EcosystemSimulator.cs:1079-1086`).

## 6. Reproduction and natural death

Reproduction (`ApplyReproduction`, `EcosystemSimulator.cs:1150-1264`) is driven by Condition through a continuous piecewise scale joined at `STRUGGLING_REPRO_RATE = 0.10` (`EcosystemSimulator.cs:282`):

```
require Population >= MIN_POPULATION_FOR_REPRODUCTION (2)     // else no births
if Condition >= ReproThreshold:                              // healthy
    t        = (Condition - ReproThreshold) / (1 - ReproThreshold)
    reproScale = 0.10 + (1 - 0.10) * t                       // ramps 0.10 -> 1.0
else:                                                        // struggling
    reproScale = 0.10 * (Condition / ReproThreshold)         // ramps 0 -> 0.10
reproScale = clamp(reproScale, 0, 1)

births = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep
if Tier == 1 and Tier-2 population < 1:
    births *= NO_PREDATOR_PENALTY (0.85)                     // 15% reduction
// births run through the birth accumulator; whole births added.
// Newborns inherit the group's current Condition (no separate dilution step).
```

Edge cases for `ReproThreshold >= 1` and `<= 0` are handled at `EcosystemSimulator.cs:1169-1178`. `NO_PREDATOR_PENALTY = 0.85` is defined at `SimSpecies.cs:55` and applied at `EcosystemSimulator.cs:1207-1215`. Note that because current runs are Tier 1 only, the no-predator penalty always applies. The carrying-capacity soft cap on births was removed; Tier 1 throttles indirectly through the Condition pathway (`EcosystemSimulator.cs:1217-1222`).

Natural death (`ApplyNaturalDeathWithAccumulator`, `EcosystemSimulator.cs:1270-1319`) is a flat per-day rate independent of performance:

```
variance      = (rng.NextDouble()*2 - 1) * NaturalDeathVariance
baseRate      = max(0, NaturalDeathRate + variance)
deaths        = Population * baseRate * BiologyStep
// deaths run through the natural-death accumulator; whole deaths removed
```

Defaults: `NaturalDeathRate = 0.02` (2 percent per day) and `NaturalDeathVariance = 0.01` (plus or minus 1 percent) (`SimSpecies.cs:40-41`).

### 6.1 Accumulators

Births, predation, natural deaths, and condition deaths each accumulate a fractional residual in a per-species `Dictionary<string, float>` keyed by `FullName` (`EcosystemSimulator.cs:150-154`). Each step adds the raw fractional amount, extracts `floor(accumulated)` whole individuals with `long` arithmetic to avoid overflow, and carries the fractional remainder to the next step. The residuals are exposed for CSV output through `GetBirthAccum`, `GetNaturalDeathAccum`, `GetConditionDeathAccum`, and `GetPredationAccum` (`EcosystemSimulator.cs:224-231`). The `_thermalDeathAccumulators` dictionary is declared but unused dead code in v11.1 (no accessor, `EcosystemSimulator.cs:153`, `EcosystemSimulator.cs:223`).

## 7. Thermal performance formula

`SimSpecies.CalculatePerformance(temperatureCelsius)` (`SimSpecies.cs:95-133`) returns a dimensionless performance in `[0, 1]`. It first adds the per-species `TemperatureDebuff` offset (`SimSpecies.cs:97`), then applies a cosine lethal fade over `LETHAL_TRANSITION_WIDTH = 2.0` degrees Celsius near CTmin and CTmax (`SimSpecies.cs:99-114`), then evaluates an Arrhenius curve on Kelvin input:

```
T  = temperatureCelsius + 273.15            // Kelvin
OT = OptimalTempK,  B = ArrhenBreadth
L  = ArrhenLower,   U = ArrhenUpper
LB = LowerBoundK,   UB = UpperBoundK
numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)
perf        = numerator / denominator
return clamp(perf, 0, 1) * fadeFactor
```

`Pmax` is applied externally (in step 1), not inside this method (`SimSpecies.cs:131`, `EcosystemSimulator.cs:594`). At or beyond CTmin/CTmax the fade factor is 0 and the method returns 0, which step 6 reads as instant thermal death.

## 8. Glossary

| Term | Code identifier | Meaning |
|------|-----------------|---------|
| Scenario | `SimulationRunner`, `ScenarioResult` (`ScenarioResult.cs:10`) | One simulation execution with one seed, run for `TotalDays` days. The atomic unit. Output file `scenario_N.csv`. |
| Run / Batch | `SimulationController` + `AggregateResults` (std); `BulkBatchConfig` (bulk) | One configuration that generates N scenarios with different seeds. "Batch" and "run" are interchangeable. Output `aggregate.csv`. |
| Bulk | `BulkSimulationController`, `CsvBatchParser` | A collection of runs uploaded as one CSV file; each row is one run. Output is a ZIP with per-run folders plus `bulk_summary.csv`. |
| Bulk summary | `BulkSimulationController.GenerateBulkSummary` (`BulkSimulationController.cs:428`) | Cross-run statistics for the whole upload, written to `bulk_summary.csv`. |
| Day loop | `SimulationRunner.Run()` (`SimulationRunner.cs:392`) | Per-day stepping from day 0 to `TotalDays - 1`. |
| Biology step (cadence) | `BiologyStep`, `runBiology` (`SimulationRunner.cs:430`) | Biology runs on day 1 and every `BiologyStep`-th day; all per-step rates multiply by `BiologyStep`. |
| Biology step (sequence) | `ProcessBiologyStep` (`EcosystemSimulator.cs:543`) | The 10-step daily biology sequence. |
| StepRecord | `StepRecord` (`SimulationRunner.cs:75`) | One day's recorded state. Carries tier totals plus a `PerSpeciesStepData` sub-row per species keyed by `FullName`. |
| PerSpeciesStepData | `PerSpeciesStepData` (`SimulationRunner.cs:20`) | One species' recorded values for one day (Pop, Cond, FedRate, births, deaths, accumulators, etc.). |
| RawThermalPerformance | `SimSpecies.RawThermalPerformance` (`SimSpecies.cs:74`) | Arrhenius performance with lethal fade, before Pmax. `[0, 1]`. |
| ThermalPerformance | `SimSpecies.ThermalPerformance` (`SimSpecies.cs:75`) | `RawThermalPerformance * Pmax`. Used for predator demand and logging. |
| FedRate | `SimSpecies.FedRate` (`SimSpecies.cs:76`) | Feeding satisfaction `[0, 1]`. Tier 1: linear from food density. Tier 2: from Holling II. |
| FoodDensity | local `foodDensity`, `LastFoodDensityT1` (`EcosystemSimulator.cs:761-762`) | Shared Tier-1 resource density `max(0, 1 - tier1Pop/cap)`, `[0, 1]`. |
| RawFinalPerformance | `SimSpecies.RawFinalPerformance` (`SimSpecies.cs:77`) | `RawThermalPerformance * FedRate`. The Condition drain target. |
| FinalPerformance | `SimSpecies.FinalPerformance` (`SimSpecies.cs:78`) | `ThermalPerformance * FedRate`. CSV/logging only; not a biology input. |
| Condition | `SimSpecies.Condition` (`SimSpecies.cs:80`) | Per-species health/energy reserves `[0, 1]`, starts at 1.0, persists across days. |
| Pmax | `SimSpecies.Pmax` (`SimSpecies.cs:68`) | Peak performance height at optimal temperature `[0, 1]`. Scales thermal performance, condition rates, and births. |
| HuntingEfficiency | `SimSpecies.HuntingEfficiency` (`SimSpecies.cs:51`) | Dual semantic: Tier 1 = resource extraction efficiency (default 1.0); Tier 2 = base hunting success (default 0.75). |
| CarryingCapacity | `CarryingCapacityPerTier` (`EcosystemSimulator.cs:237`); `SimulationConfig.CarryingCapacityTier1` (`SimulationConfig.cs:59`) | Tier-1 shared resource ceiling, always on, drives food density. Default 5000. |
| reproScale | local `reproScale`, `LastReproScaleT1` (`EcosystemSimulator.cs:1168`, `EcosystemSimulator.cs:1194`) | Condition-derived reproduction multiplier `[0, 1]`. |
| Accumulator | `_birthAccumulators` etc. (`EcosystemSimulator.cs:150-154`) | Per-species fractional residual that carries births/deaths across days so fractional events are not lost. |
| FullName | `SimSpecies.FullName` (`SimSpecies.cs:89`) | Species identity key. Empty `VariantLabel` gives `Name`; otherwise `"{Name}_{VariantLabel}"`. The dictionary key threaded through everything. |
| variantLabel | `SimSpecies.VariantLabel` (`SimSpecies.cs:18`); `SpeciesData.variantLabel` (`SpeciesDatabase.cs:41`) | Free-text variant string used for identity and all CSV output. The `ThermalVariant` enum name is never written to output. |
| ThermalVariant | enum `ThermalVariant` (`SimSpecies.cs:3`) | Internal enum {Arctic, Common, Tropical, Custom}, used only for default-species curve init and alias resolution. |
| SpeciesData | `SpeciesData` (`SpeciesDatabase.cs:33`) | Authoring record. `tier` is 0-based (0 = prey, 1 = predator) (`SpeciesDatabase.cs:51`). |
| Tier numbering | `SimSpecies.Tier` 1-based (`SimSpecies.cs:19`); `SpeciesData.tier` 0-based (`SpeciesDatabase.cs:51`) | The runtime `SimSpecies.Tier` is `SpeciesData.tier + 1` (`EcosystemSimulator.cs:356`). Be careful: the two differ by 1. |
| SanitizeColumnName | `StepRecord.SanitizeColumnName` (`SimulationRunner.cs:322`) | Maps a `FullName` to an ASCII column prefix: non-alphanumeric to `_`, leading digit gets `_` prefix, empty becomes "Unknown". |
| Tier rollup column | `BuildVariantRollupColumns`, `TierVariantPop` (`SimulationRunner.cs:159`, `SimulationRunner.cs:99`) | Dynamic `Tier{n}_{label}` population column, one per distinct (tier, variantLabel), sorted (tier asc, label asc). |
| RunControl | `RunControl` (`RunControl.cs`) | Volatile `Paused`/`Stopped` flags for cooperative pause/stop. Standard run only; bulk uses a separate pause path. |
| Crash | `HasCrashed`, `CrashDay`, `CrashTier` (`SimulationRunner.cs:440-444`) | Detection condition is total population == 0. |
| ScenarioResult | `ScenarioResult` (`ScenarioResult.cs:10`) | One scenario's summary, per-species metrics, population stats, and CSV string. |
| AggregateResults | `AggregateResults` (`ScenarioResult.cs:204`) | One run's cross-scenario rollup. Produces `aggregate.csv` and `config.csv`. |
| ConfigExporter | static `ConfigExporter` (bottom of `ScenarioResult.cs`) | Builds config CSV/JSON from a `SimulationConfig` or `AggregateResults`. |
| Output sink | `ServerUpload.IsAvailable` (`ServerUpload.cs:33`) vs `WebGLZipDownload` | S3 upload on WebGL non-editor, otherwise a progressive ZIP download. |

## 9. Cross-subsystem data flow

Each value below crosses a subsystem boundary. "Produced" is where the value is set; "consumed" is where another subsystem reads it.

| Value | Produced in | Consumed in |
|-------|-------------|-------------|
| scenario seed | `SimulationController.cs:209` (std) / `BulkSimulationController.cs:259` (bulk) | `SimulationRunner` ctor seeds both calculators (`SimulationRunner.cs:382-387`) |
| daily temperature `temp` (Celsius) | `TemperatureCalculator.GetTemperature` (`TemperatureCalculator.cs:61-85`) | `ProcessBiologyStep` step 1 arg (`SimulationRunner.cs:435`, `EcosystemSimulator.cs:593`); `StepRecord.Temperature` (`SimulationRunner.cs:484`) |
| config parameters (days, BiologyStep, carrying cap, condition rates, temp block) | `SimulationConfig` fields (`SimulationConfig.cs:29-139`) | applied onto `runner.TempCalc`/`runner.Ecosystem` (`SimulationController.cs:349-374`); re-serialized into scenario `#config:` header (`SimulationRunner.cs:749-767`) |
| batch parameters (per CSV row) | `CsvBatchParser.TryParse` (`CsvBatchParser.cs:173-193`) into `BulkBatchConfig` (`BulkBatchConfig.cs:41-64`) | applied in `RunSingleScenarioFromBatch` (`SimulationController.cs:292-311`) |
| `Tier2Enabled` gate | `SimulationConfig.cs:130` / `EcosystemSimulator.cs:245` (both default false) | gates species load (`EcosystemSimulator.cs:336`) and CSV column emission (`SimulationRunner.cs:802`, header/row at `SimulationRunner.cs:216-232`, `SimulationRunner.cs:270-286`) |
| species list (`RunSpecies` / temp `RunSpeciesList`) | `SimulationConfig.RunSpecies` (`SimulationConfig.cs:123`) / `ConvertSpecies` (`BulkSimulationController.cs:723`) | `Ecosystem.InitializeFromRunSpeciesList` (`SimulationRunner.cs:405`, body `EcosystemSimulator.cs:317`) |
| `RawThermalPerformance` | step 1 (`EcosystemSimulator.cs:593`) | `ThermalPerformance` (`EcosystemSimulator.cs:594`), `RawFinalPerformance` (`EcosystemSimulator.cs:608`), thermal-death gate (`EcosystemSimulator.cs:1008`) |
| `FedRate` (Tier 1) | `EcosystemSimulator.cs:769` | `RawFinalPerformance` (`EcosystemSimulator.cs:608`); `LastFedRateBySpecies` (`EcosystemSimulator.cs:778`); `LastFedRateT1` (`EcosystemSimulator.cs:780`) |
| `LastFoodDensityT1` | `EcosystemSimulator.cs:762` | `StepRecord.FoodDensityT1` via `RecordStep`; CSV column `FoodDensityT1` (`SimulationRunner.cs:226`, `SimulationRunner.cs:280`) |
| `RawFinalPerformance` | step 3 (`EcosystemSimulator.cs:608`) | `UpdateCondition` target (`EcosystemSimulator.cs:956`) |
| `Condition` | `UpdateCondition` (`EcosystemSimulator.cs:975`, `EcosystemSimulator.cs:986`) | condition-death gate (`EcosystemSimulator.cs:1059`); reproScale (`EcosystemSimulator.cs:1179-1190`); `AvgConditionT1` (`EcosystemSimulator.cs:1336`) |
| tier `Last*` counters (births, eaten, temp/condition/natural deaths, reproScale, fed rate) | written across steps 2/6/7/8/9 | read by `RecordStep` into `StepRecord` (`SimulationRunner.cs:467-534`) |
| per-species dictionaries (`LastBirthsBySpecies` etc., keyed by `FullName`) | populated in biology steps (`EcosystemSimulator.cs:213-220`) | `RecordStep` builds `StepRecord.SpeciesData` (`SimulationRunner.cs:561`). Invariant: each dictionary sums to its matching tier counter (`EcosystemSimulator.cs:212`) |
| `StartPopBySpecies` | reset/snapshot at step top (`EcosystemSimulator.cs:583`) | per-capita BirthRate in `RecordStep` (`SimulationRunner.cs:565`) |
| `Population` (rounded, capped) | step 10 (`EcosystemSimulator.cs:675-681`) | `StartPopT1/T2` next step (`EcosystemSimulator.cs:546`); `EndPop` (`EcosystemSimulator.cs:692`); `GetTier1/2Population` -> `HasCrashed` (`SimulationRunner.cs:440`) |
| `StepRecord` (per day) | `RecordStep` (`SimulationRunner.cs:464-583`) | `ComputePopulationStats` (`SimulationRunner.cs:622`), `ComputePerSpeciesScenarioMetrics` (`SimulationRunner.cs:1060`), `ToCsvInternal` rows (`SimulationRunner.cs:804-806`) |
| `ScenarioResult` (incl. `CsvData`) | `ToScenarioResult` (`SimulationRunner.cs:982`) | `AggregateResults.Scenarios` (`SimulationController.cs:248` / `BulkSimulationController.cs:305`); CSV streamed then nulled in bulk (`BulkSimulationController.cs:226-233`) |
| `PerSpeciesScenarioMetrics` (keyed by `FullName`) | `ComputePerSpeciesScenarioMetrics` (`SimulationRunner.cs:1060`) | `ScenarioResult.SpeciesMetrics` (`SimulationRunner.cs:1017`) -> `BuildPerSpeciesAggregate` (`ScenarioResult.cs:431`) |
| `AggregateResults` | `CalculateAggregates` (`ScenarioResult.cs:288`) | `ToAggregateCsv` (`ScenarioResult.cs:588`) -> `aggregate.csv`; `ToConfigCsv` (`ScenarioResult.cs:948`) -> `config.csv` |
| `BulkRunSummary` (per run) | added at `BulkSimulationController.cs:346` | `GenerateBulkSummary` -> `bulk_summary.csv` (`BulkSimulationController.cs:428`) |
| parse errors | `CsvBatchParser.TryParse` (`CsvBatchParser.cs:65`) | `ValidateCsv` -> first 10 shown (`CsvUploadHandler.cs:170-177`) |
| `parsedBatches` | `CsvUploadHandler.cs:165` | event payload -> `RunAllBatches` (`BulkSimulationController.cs:91`) |

## 10. Scenario CSV layout

`ToCsvInternal` (`SimulationRunner.cs:743`) writes, in order:

```
#config:model_version,v12-per-species-tracking
#config:<param>,<value>          (one line per config/temperature/condition parameter)
#
#species:Name,Variant,Tier,InitialCount,...,Pmax,CTminC,CTmaxC,TemperatureDebuff   (header)
#species:<row per species>        (temps in both Kelvin and Celsius, Celsius via Kelvin - 273.15, :F2)
#
<daily header line from StepRecord.CsvHeader(orderedSpecies, tier2)>
<one daily data line per recorded day from StepRecord.ToCsvLine(...)>
=== summary statistics and extinction-timing sections ===
```

All `#` lines are ignored by R's `read.csv()`. The species order for the daily columns is sorted `(Tier asc, FullName asc)` once and reused for header and every row (`SimulationRunner.cs:798-806`). Each daily row begins with `Day,Year,Temperature,BiologyCycle,StartPop,EndPop`, then `Tier1Pop` (and `Tier2Pop` only when `tier2`), then the dynamic `Tier{n}_{label}` rollup columns (`SimulationRunner.cs:214-219`, `SimulationRunner.cs:268-273`), then the tier-level event and accumulator columns, then 17 per-species columns per species named `{SanitizedFullName}_{Field}` (`SimulationRunner.cs:234-247`, `SimulationRunner.cs:288-311`). When `Tier2Enabled` is false, all Tier-2 columns are omitted from both header and rows, keeping the Tier-1-only output narrow. Invariant: per-species population columns sum to the matching `Tier{n}_{label}` rollup column, which sum to `Tier{n}Pop`.
