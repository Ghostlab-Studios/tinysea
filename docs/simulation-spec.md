# Simulation Specification

This document is the authoritative spine of the TinySea headless ecosystem simulation. It defines the complete per simulated day biology step sequence in exact order, the Scenario then Run then Bulk execution hierarchy, the global invariants the system maintains, and the Tier 1 only scope. It is self contained: a developer can reimplement the shipping Tier 1 behavior, including the temperature driver, the default species, the configuration defaults, and the CSV column layout, from this document alone.

The following topics are specified inline in this document, not in any sibling file:

- The temperature model: Section 8 (the parametric model, its eleven parameters with defaults, the RNG consumption order, and the timeseries override path).
- The default species set and all per species thermal curve values: Section 6.1 and Section 6.2.
- The configuration defaults: Section 9.
- The exact per species CSV column set and its order: Section 7.1.

The bulk mode mechanics and the bulk CSV row schema are summarized in Section 2.3.

All file and line citations point to source under `tinysea/Assets/scripts/Simulation`. The C# source is the only source of truth.

## 1. Scope: Tier 1 Only

The shipping simulation runs Tier 1 (prey) species only. Tier 2 (predator) logic remains in the engine from the original two tier food chain design, but it is gated off by default and current runs never use it.

The gate is `Tier2Enabled`, default `false`, present on both the engine and the config:

| Field | File:line | Default |
|-------|-----------|---------|
| `EcosystemSimulator.Tier2Enabled` | EcosystemSimulator.cs:245 | `false` |
| `SimulationConfig.Tier2Enabled` | SimulationConfig.cs:130 | `false` |

When the gate is off, four things happen:

1. Tier 2 species are dropped at load. `InitializeFromRunSpeciesList` skips any species with `data.tier == 1` (the 0 based tier where 1 means predator) when `!Tier2Enabled` (EcosystemSimulator.cs:336), and logs one warning if any were present (EcosystemSimulator.cs:331-332).
2. Tier 2 CSV columns are suppressed in both header and rows. `SimulationRunner.ToCsvInternal` computes `tier2 = (Ecosystem == null) || Ecosystem.Tier2Enabled` (SimulationRunner.cs:802), so a null `Ecosystem` falls back to `tier2 = true` (Tier 2 columns emitted); otherwise `tier2` equals `Ecosystem.Tier2Enabled`. `StepRecord.CsvHeader` / `StepRecord.ToCsvLine` omit every Tier 2 column when `tier2` is false (SimulationRunner.cs:216-232, 270-286).
3. Bulk CSV input hard rejects Tier 2 rows. The batch parser adds a validation error for any species with `sp.Tier != 0` (CsvBatchParser.cs:318-319).
4. Tier 2 still runs in the engine if explicitly enabled. The biology code for predators is intact (Holling Type II feeding, predator demand, per predator FedRate). It is dormant, not deleted.

Throughout this document, Tier 2 details are marked as legacy and secondary. Tier 1 is documented in full depth. The two tiers differ by an offset of 1 in tier numbering, which is a common source of error:

| Identifier | File:line | Convention |
|------------|-----------|------------|
| `SimSpecies.Tier` | SimSpecies.cs:19 | 1 based. 1 = prey, 2 = predator |
| `SpeciesData.tier` | SpeciesDatabase.cs:51 | 0 based. 0 = prey, 1 = predator |

The conversion is `Tier = tier + 1`, applied at load (EcosystemSimulator.cs:356).

## 2. Execution Hierarchy: Scenario, Run, Bulk

The simulation has three nested aggregation levels. The atomic unit is the Scenario. A Run is a set of Scenarios. A Bulk is a set of Runs.

```
Bulk (one uploaded CSV; each row = one Run)
 └── Run / Batch (one configuration; produces N Scenarios with different seeds)
      └── Scenario (one seeded execution; runs for TotalDays days)
           └── StepRecord (one per simulated day)
                └── PerSpeciesStepData (one per species, keyed by FullName)
```

| Level | Meaning | Driving code | Output artifact |
|-------|---------|--------------|-----------------|
| Scenario | One seeded run of `TotalDays` days. The atomic unit. | `SimulationRunner` (SimulationRunner.cs:347), `ScenarioResult` | `scenario_N.csv` |
| Run / Batch | One configuration that yields N scenarios with distinct seeds. "Batch" and "run" are interchangeable in code. | `SimulationController` (standard) + `AggregateResults`; `BulkBatchConfig` (one bulk row) | `aggregate.csv` |
| Bulk | A collection of runs uploaded as one CSV, each row a run. | `BulkSimulationController` (BulkSimulationController.cs:19) | ZIP plus `bulk_summary.csv` |

### 2.1 Scenario

A Scenario is one execution of `SimulationRunner.Run()` (SimulationRunner.cs:392). It owns one `TemperatureCalculator` and one `EcosystemSimulator`, both constructed with the same seed (SimulationRunner.cs:382-387). It runs a day loop for `TotalDays` days, records one `StepRecord` per day, and converts its records into a `ScenarioResult` with `ToScenarioResult` (SimulationRunner.cs:982). The scenario CSV is produced by `ToCsvInternal` (SimulationRunner.cs:743) and stored on `ScenarioResult.CsvData` (SimulationRunner.cs:1035).

The day loop (SimulationRunner.cs:415-448):

1. Optional cooperative pause or stop check via `RunControl` (SimulationRunner.cs:419-423).
2. Compute the display day (`dayIndex + 1`) and year (`dayIndex / 365 + 1`) (SimulationRunner.cs:425-426).
3. Get the day temperature in degrees Celsius from `TempCalc.GetTemperature(dayIndex)` (SimulationRunner.cs:428). The temperature model is specified in full in Section 8. `dayIndex` is the zero based day, so the first simulated day passes `0`.
4. Decide whether biology runs this day: `runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)` (SimulationRunner.cs:430). The temperature used on a biology day is that single day's temperature from step 3 (`TempCalc.GetTemperature(dayIndex)`), not an aggregate over the skipped interval. Biology is driven by one day's temperature but its rates are scaled as if `BiologyStep` days elapsed (Section 3).
5. If biology runs, increment the biology cycle counter and call `Ecosystem.ProcessBiologyStep(temp)` (SimulationRunner.cs:432-436). This is the per day biology sequence defined in Section 3.
6. Record the day via `RecordStep` (SimulationRunner.cs:438).
7. If biology ran and `Ecosystem.HasCrashed()` is true, mark the crash and break the loop (SimulationRunner.cs:440-447).

`BiologyStep` (default 1, recommended range 1 to 5) controls how often biology runs. At `BiologyStep = 1` biology runs every day. At larger values biology runs on day 1 and every `BiologyStep`th day; on skipped days the record carries the prior population and zeroes the daily event counts (SimulationRunner.cs:467-473, 559-575). `BiologyStep` also multiplies per step rates inside the biology code (see Section 3).

The 1 to 5 range comes from a `[Range(1, 5)]` attribute on `SimulationConfig.BiologyStep` (SimulationConfig.cs:28). That attribute only clamps the Inspector slider. It is not enforced for values set programmatically (for example from a bulk CSV row), and `SimulationConfig.IsValid` does not check `BiologyStep` at all (SimulationConfig.cs:146-195). A supplied value of `0` is therefore not rejected and produces a divide by zero in the `displayDay % BiologyStep` predicate (SimulationRunner.cs:430); values above 5 run without error, biology just fires less often.

The per step rate multiplier is the constant `BiologyStep` on every biology day, including day 1, regardless of the actual elapsed interval. With `BiologyStep = 5` biology runs on day 1, then day 5 (a gap of 4 days), then day 10, day 15, and so on, yet every one of those days multiplies its rates by the constant 5. The first interval (day 1 to day 5) is therefore over counted relative to its true 4 day gap. This is a known boundary effect of keying the schedule on `displayDay == 1`, not a per interval correction.

### 2.2 Run (Batch)

A Run is one configuration that spawns N scenarios with different seeds. In standard mode the configuration is a `SimulationConfig` ScriptableObject and the driver is `SimulationController.RunAllScenariosCoroutine` (SimulationController.cs:111). In bulk mode the configuration is one `BulkBatchConfig` parsed from a CSV row (BulkBatchConfig.cs:41).

Per scenario seeding: `scenarioSeed = RandomSeed < 0 ? -1 : RandomSeed + i`, where `i` is the zero based scenario index (SimulationController.cs:180, 209; BulkSimulationController.cs:259). When `RandomSeed >= 0` each scenario gets the distinct deterministic seed `RandomSeed + i` and the whole run is reproducible.

When `RandomSeed < 0` the expression yields the constant `-1` for every scenario, not `RandomSeed + i`. The value `-1` is a sentinel, not a literal seed. Each `SimulationRunner` passes it to `TemperatureCalculator` and `EcosystemSimulator`, whose constructors both do `_rng = seed < 0 ? new System.Random() : new System.Random(seed)` (TemperatureCalculator.cs:45; EcosystemSimulator.cs:302). The parameterless `System.Random()` seeds from the system clock, so a negative base seed makes every scenario non reproducible. Because the clock based default seed has tick resolution, two scenarios constructed within the same clock tick can receive the same system time seed and collide. Distinct seeds across scenarios are therefore likely but not guaranteed in the negative seed case.

Standard mode applies every config parameter onto a fresh `SimulationRunner` before each scenario (SimulationController.cs:349-374): timing (`TotalDays`, `BiologyStep`), the eleven temperature parameters (the eleven public fields of `TemperatureCalculator` enumerated in Section 8.2), the species list, `CarryingCapacityPerTier`, the two condition rates, and `Tier2Enabled`. The exact field by field copy is `BaseTemperature`, `SeasonalAmplitude`, `ClimateTrend` to `ClimateTrendPerYear`, `VariabilityMagnitude`, `WarmingBias`, `DailyVariationRange` to `BaseRandomness`, `RandomnessGrowthRate`, `Autocorrelated` to `UseAutocorrelation`, `InterannualVariation` to `UseInterannualVariation`, `TemperatureBoundsMin` to `MinTemp`, and `TemperatureBoundsMax` to `MaxTemp` (SimulationController.cs:353-363). After all scenarios finish, `AggregateResults.CalculateAggregates` computes cross scenario statistics (SimulationController.cs:263) and the run produces `aggregate.csv`.

In Editor and standalone builds, scenarios within a run execute in parallel via `Task.Run`, chunked by `ProcessorCount - 1` (SimulationController.cs:190-258). In WebGL non editor they run sequentially because WASM is single threaded (SimulationController.cs:170-188). The seed per scenario is identical in both paths, so results do not depend on the threading mode.

### 2.3 Bulk

A Bulk is a collection of Runs uploaded as a single CSV, one Run per row. `BulkSimulationController` (BulkSimulationController.cs:19) parses the CSV into a list of `BulkBatchConfig`, runs each as a Run (each Run producing its scenarios and its `aggregate.csv`), and finally emits one `bulk_summary.csv` via `GenerateBulkSummary` (BulkSimulationController.cs:386, 428). Each run's scenario CSVs and aggregate are placed in a per run folder inside a ZIP; `bulk_summary.csv` sits at the ZIP root. The full bulk mechanics, the CSV row schema, and the cross run aggregation are documented in [bulk-system.md](bulk-system.md).

## 3. Per Day Biology Step Sequence

The per day biology sequence is `EcosystemSimulator.ProcessBiologyStep(float temperature)` (EcosystemSimulator.cs:543). The argument is the day temperature in degrees Celsius. The method runs ten ordered steps over `Species` (a `List<SimSpecies>`), preceded by a snapshot and reset block and followed by an end of day rollup. This is the canonical biology spine. Each step below gives its exact order, the state it reads and writes, and its formula in full.

The ten steps execute step major. Each step runs its own `foreach (var sp in Species)` loop to completion before the next step starts (EcosystemSimulator.cs:591-686). A step that reads a whole tier total, such as Step 2 reading `GetTierPopulation(1)`, therefore sees one consistent snapshot of that tier for the whole step. No step reorders to species major, so a per-species computation never observes a tier total that some other species already mutated within the same step. The `StartPopBySpecies` snapshot taken in the reset block (Section 3.0) is likewise complete for every species before any step runs.

Most quantities are dimensionless and bounded to `[0, 1]`, but the bounds are enforced by different mechanisms; see Section 5.4 for which quantities are explicitly clamped and which rely only on parameter conventions. Population is stored as `float` during the day and rounded to an integer only at Step 10.

Several distinct population thresholds gate the biology. Each is compared against the fractional intra-day `Population` (a `float`), not a rounded value, since rounding to an integer happens only in Step 10. During the day a species can therefore sit at a sub-1 fractional population, for example 0.5, in which case it is below `MIN_ALIVE_POP` and counts as not alive for feeding, condition, and the chronic-death steps, yet it is not extinct: extinction is defined on the post-Step-10 rounded population being 0 (Section 5.2), and away-from-zero rounding would turn 0.5 into 1.

| Threshold | Value | Comparison in code | Effect | Citation |
|-----------|-------|--------------------|--------|----------|
| `MIN_ALIVE_POP` | `1.0f` | `Population < MIN_ALIVE_POP` (strictly less than 1) | Treated as dead: skips feeding (FedRate set to 0), condition update, thermal death, condition death | EcosystemSimulator.cs:248, 787, 954, 1002, 1058 |
| `MIN_POPULATION_FOR_REPRODUCTION` | `2f` | `Population < MIN_POPULATION_FOR_REPRODUCTION` (strictly less than 2) | Step 8 early-returns, no births | EcosystemSimulator.cs:274, 1152 |
| Natural-death alive guard | `0f` | `Population <= 0f` | Step 9 early-returns (note: this guard is `<= 0`, looser than the `< 1` guard used elsewhere) | EcosystemSimulator.cs:1272 |
| Extinction | `0` | rounded `Population == 0` after Step 10 | Species is extinct; contributes to crash detection | EcosystemSimulator.cs:681; SimulationRunner.cs:597-598 |

### 3.0 Top of step: snapshot and reset (EcosystemSimulator.cs:545-587)

Before any step runs:

1. Snapshot tier start populations: `StartPopT1 = GetTier1Population()`, `StartPopT2 = GetTier2Population()` (EcosystemSimulator.cs:546-547).
2. Reset the tier level `Last*` counters. The death, birth, eaten, and repro-scale counters reset to 0: `LastEatenT1`, the four death-type pairs (`LastTempDeathsT1/T2`, `LastConditionDeathsT1/T2`, `LastNaturalDeathsT1/T2`), the birth pair (`LastBirthsT1/T2`), and the repro-scale pair (`LastReproScaleT1/T2`) (EcosystemSimulator.cs:550-558, 561-562). The two Tier 2 feeding metrics do not reset to 0: they reset to their neutral value `1f`, `LastFedRateT2 = 1f` (EcosystemSimulator.cs:559) and `LastAvgHuntingEfficiency = 1f` (EcosystemSimulator.cs:560). The Tier 1 feeding metrics `LastFedRateT1` and `LastFoodDensityT1` are not touched in this block. They retain the previous day's value and are recomputed every biology day in Step 2 (EcosystemSimulator.cs:778, 803); on a skipped biology day the record reuses those carried-forward values (SimulationRunner.cs:521-522).
3. Clear all per species dictionaries (`LastBirthsBySpecies`, `LastTempDeathsBySpecies`, `LastConditionDeathsBySpecies`, `LastNaturalDeathsBySpecies`, `LastEatenBySpecies`, `LastReproScaleBySpecies`, `LastFedRateBySpecies`, `StartPopBySpecies`) and seed one zero entry per species (EcosystemSimulator.cs:566-584).
4. Set `StartPopBySpecies[FullName] = SafePopToLong(sp.Population)` for each species (EcosystemSimulator.cs:583). This snapshot feeds the per capita `BirthRate` computed later in `RecordStep`.

`SafePopToLong` rounds a finite float to a long and maps any non finite value (NaN, plus or minus infinity) to 0 (EcosystemSimulator.cs:296-297).

### 3.1 Step 1: Thermal performance (EcosystemSimulator.cs:589-598)

For each species:

- `RawThermalPerformance = CalculatePerformance(temperature)` (SimSpecies.cs:95-133). The full formula is given below. `Pmax` is not applied inside it.
- `ThermalPerformance = RawThermalPerformance * Pmax` (EcosystemSimulator.cs:594). `Pmax` is the peak thermal height, the species specific ceiling.
- `FedRate = 1` and `CurrentHuntingSuccess = 1` are initialized (EcosystemSimulator.cs:595-596).

`Pmax` is applied here, not inside `CalculatePerformance` (SimSpecies.cs:131-132). `RawThermalPerformance` is the lethal kill test in Step 6 and the condition target factor in Step 3. `ThermalPerformance` is used for predator demand (legacy Tier 2) and for logging.

`CalculatePerformance(temperatureCelsius)` is the single most important biology formula, so it is given in full here (SimSpecies.cs:95-133). It is an Arrhenius peaked thermal performance curve multiplied by a cosine lethal fade near the critical limits, then clamped to `[0, 1]`. Inputs and constants:

| Symbol | Field / constant | Units | Default | Meaning |
|--------|------------------|-------|---------|---------|
| `temperatureCelsius` | method argument | °C | n/a | The day temperature passed in by Step 1. |
| `TemperatureDebuff` | `SimSpecies.TemperatureDebuff` (SimSpecies.cs:71) | °C | `0f` | Per species offset added to the input temperature. Set from `SpeciesData.TemperatureDebuff` at load (EcosystemSimulator.cs:376). Sign convention: effective temperature is `temperatureCelsius + TemperatureDebuff`, so a positive value raises the experienced temperature and a negative value lowers it. |
| `CTminC` | `SimSpecies.CTminC` (SimSpecies.cs:69) | °C | `-5.0f` | Critical thermal minimum. At or below it performance is 0. |
| `CTmaxC` | `SimSpecies.CTmaxC` (SimSpecies.cs:70) | °C | `40.0f` | Critical thermal maximum. At or above it performance is 0. |
| `LETHAL_TRANSITION_WIDTH` | constant (SimSpecies.cs:57) | °C | `2.0f` | Width of the cosine fade band inside each critical limit. |
| `OptimalTempK` (`OT`) | `SimSpecies.OptimalTempK` | K | per species | Temperature of peak performance. |
| `ArrhenBreadth` (`B`) | `SimSpecies.ArrhenBreadth` | K | per species | Arrhenius activation breadth of the rising limb. |
| `ArrhenLower` (`L`) | `SimSpecies.ArrhenLower` | K | per species | Low temperature deactivation energy term. |
| `ArrhenUpper` (`U`) | `SimSpecies.ArrhenUpper` | K | per species | High temperature deactivation energy term. |
| `LowerBoundK` (`LB`) | `SimSpecies.LowerBoundK` | K | per species | Low temperature half-drop reference. |
| `UpperBoundK` (`UB`) | `SimSpecies.UpperBoundK` | K | per species | High temperature half-drop reference. |

The computation, in order (SimSpecies.cs:97-132):

```
// 1. Apply the per-species debuff in Celsius, then derive the fade band width.
t  = temperatureCelsius + TemperatureDebuff          // effective temperature, °C
halfRange = (CTmaxC - CTminC) / 2
tw = min(LETHAL_TRANSITION_WIDTH, halfRange)         // fade width, °C (caps at half the survivable span)

// 2. Cosine lethal fade. fadeFactor starts at 1.
fadeFactor = 1
if t <= CTminC:            fadeFactor = 0
elif t <  CTminC + tw:     fadeFactor = 0.5 * (1 + cos(PI * (CTminC + tw - t) / tw))
if t >= CTmaxC:            fadeFactor = 0
elif t >  CTmaxC - tw:     fadeFactor *= 0.5 * (1 + cos(PI * (t - (CTmaxC - tw)) / tw))
if fadeFactor <= 0:        return 0                   // early-out at or beyond a lethal limit

// 3. Arrhenius peaked curve, computed in Kelvin.
T  = t + 273.15                                       // °C to Kelvin, simulation uses +273.15
numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)
perf = numerator / denominator

// 4. Clamp the Arrhenius term to [0,1], then multiply by the fade. Pmax is applied later.
return clamp(perf, 0, 1) * fadeFactor
```

The curve returns exactly 0 if and only if the cosine fade drives `fadeFactor` to 0, which happens when the effective temperature `t` is at or below `CTminC` or at or above `CTmaxC` (SimSpecies.cs:104, 109, 114). This is the precise condition that triggers the total kill in Step 6. Between the limits the fade is in `(0, 1]` and the Arrhenius term is positive, so `RawThermalPerformance > 0`. The Kelvin conversion uses `+273.15` (SimSpecies.cs:116). This differs deliberately from the interactive game side and the project CLAUDE.md, which use `+273`. The simulation constant is `+273.15` and is authoritative for the simulation.

### 3.2 Step 2: Feeding and predation (EcosystemSimulator.cs:600-602, body at 728)

`ProcessFeedingWithAccumulator` sets each species' `FedRate` (feeding satisfaction, `[0, 1]`).

Tier 1 (the shipping path), EcosystemSimulator.cs:765-804. Tier 1 uses the same Holling Type II foraging plus optional variance as Tier 2, through the shared helper `ComputeForagingSuccess(species, availabilityRatio)`; the only differences are the availability ratio passed in (the resource pool, not a prey:predator ratio) and the supply cap applied afterward:

- `tier1Pop = max(0, GetTierPopulation(1))` (EcosystemSimulator.cs:765). `GetTierPopulation` sums `Population` over Tier 1 species (EcosystemSimulator.cs:1384). Step 2 runs before any births (Step 8) and before every death step (Steps 6, 7, 9), so `tier1Pop` is the population as it stands at the start of the day, that is the value carried from the previous day's Step 10 rounding, before this day's births and deaths. No step between Step 2 and Step 10 re-reads it, so this single early read drives the density dependent feedback for the whole day.
- `capSafe = max(CarryingCapacityPerTier, 1)` (EcosystemSimulator.cs:766).
- `tier1Consumption = sum over live Tier 1 of Population * max(1, EatingAmount)` (EcosystemSimulator.cs:774-776). Appetite is floored at 1 per individual, so a 0 or unset `EatingAmount` cannot switch off the carrying-capacity limit.
- `foodDensity = max(0, 1 - tier1Consumption / capSafe)`, stored as `LastFoodDensityT1` (EcosystemSimulator.cs:777-778). This is the supply cap, the Tier 1 analog of Tier 2's scarcity factor. It now falls with total consumption (`Population * EatingAmount`), not head count: at `EatingAmount = 1` it equals the former `1 - tier1Pop / capSafe`, while at appetite 3 a 5000 pool supports about 1667 individuals.
- `resourceRatio = capSafe / max(tier1Pop, 1)` (EcosystemSimulator.cs:782). A head count, the land-pool analog of Tier 2's prey:predator ratio; it drives the Holling search efficiency. Appetite does not enter here, only through `foodDensity`, mirroring how Tier 2 keeps `EatingAmount` out of `preyRatio`.
- For each live Tier 1 species: `gatherSuccess = ComputeForagingSuccess(sp, resourceRatio)` then `FedRate = min(1, gatherSuccess * foodDensity)` (EcosystemSimulator.cs:791-792). `ComputeForagingSuccess` is `CalculateHollingEfficiency(HuntingEfficiency, resourceRatio)` (returns 1 when `HuntingEfficiency >= 1`, EcosystemSimulator.cs:971), then, if `HuntingVariance > 0`, a `clamp(eff + (rng*2-1)*HuntingVariance, 0, 1)` perturbation (EcosystemSimulator.cs:931-940). `HuntingVariance == 0` is deterministic and draws no RNG. Dead species, meaning population below `MIN_ALIVE_POP = 1` (`sp.Population < MIN_ALIVE_POP`, evaluated against the intra-day float population), get `FedRate = 0` (EcosystemSimulator.cs:787, 798).
- `LastFedRateT1` is the population weighted mean of Tier 1 FedRate, or `1f` if no Tier 1 species is alive (EcosystemSimulator.cs:803).
- Per species FedRate is recorded into `LastFedRateBySpecies` (EcosystemSimulator.cs:801).

Backward compatibility: at `HuntingEfficiency = 1`, `HuntingVariance = 0`, `EatingAmount = 1` the Holling success is 1 and `FedRate = min(1, 1 * foodDensity) = foodDensity`, identical to the previous linear model, so existing runs are unchanged. New behavior appears only when efficiency < 1, variance > 0, or appetite != 1. The carrying capacity acts as a shared food pool; carrying capacity is always on as of v11.1, and the previous off mode that forced `foodDensity` to 1.0 was removed (EcosystemSimulator.cs:121-129, 759-762).

Tier 2 (legacy, secondary), EcosystemSimulator.cs:783-896: predators use a Holling Type II functional response. Hunting efficiency scales with the prey to predator ratio (`CalculateHollingEfficiency`, EcosystemSimulator.cs:924). A random hunting variance is drawn as `variance = (_rng.NextDouble() * 2 - 1) * HuntingVariance`, which is uniform on `[-HuntingVariance, +HuntingVariance]` and consumes the ecosystem RNG stream `_rng` (EcosystemSimulator.cs:809); the per predator hunting success is then `clamp(hollingEff + variance, MIN_HUNTING_SUCCESS=0, MAX_HUNTING_SUCCESS=1)` (EcosystemSimulator.cs:810). Predator demand uses `ThermalPerformance`, prey are removed proportionally across prey species through the predation accumulator, and each predator's FedRate is its own hunting success scaled by a shared scarcity factor (`fedRate_i = min(1, huntingSuccess_i * scarcityFactor)`, EcosystemSimulator.cs:857). `LastEatenT1` accumulates whole prey removals and `LastEatenBySpecies` tracks them per prey species. None of this fires in Tier 1 only runs because there are no predators.

### 3.3 Step 3: Raw final performance (EcosystemSimulator.cs:604-610)

For each species: `RawFinalPerformance = RawThermalPerformance * FedRate`. This is the target that Condition drains toward in Step 4. It combines thermal stress and feeding stress without `Pmax`.

### 3.4 Step 4: Update Condition (EcosystemSimulator.cs:612-617, body at 952)

`UpdateCondition` moves each live species' `Condition` toward `target = RawFinalPerformance` asymmetrically (EcosystemSimulator.cs:952-992). It is skipped for dead species, guarded by `if (sp.Population < MIN_ALIVE_POP) return` (EcosystemSimulator.cs:954). The "distance from optimal" that drives the quadratic acceleration is the distance of the target from 1.0, not a temperature distance: the target itself is the environmental performance `RawThermalPerformance * FedRate`, and a target of 1.0 is the optimum. `Condition` is then nudged a fraction of the gap `(Condition - target)` or `(target - Condition)` each step.

Let `target = RawFinalPerformance`, `pmaxSafe = max(Pmax, 1e-4)`, and the effective rates `drainRate`/`recoveryRate` (per species when set to a non negative value, otherwise the global rates, EcosystemSimulator.cs:963-964). The update is (EcosystemSimulator.cs:966-989):

```
if Condition > target:                                  // draining
    severity        = (1 - target)^2                    // 0 at target=1, 1 at target=0
    effectiveDrain  = drainRate * (1 + severity) / pmaxSafe
    Condition       = Condition - (Condition - target) * effectiveDrain
else:                                                    // recovering
    boost           = target^2                           // 0 at target=0, 1 at target=1
    effectiveRecovery = recoveryRate * (1 + boost) * pmaxSafe
    Condition       = Condition + (target - Condition) * effectiveRecovery

Condition = clamp(Condition, 0, 1)
```

The quadratic factor `(1 + severity)` ranges from 1 at the optimum to 2 at a lethal target, so drain accelerates as the target worsens; `(1 + boost)` ranges from 1 at a lethal target to 2 at the optimum, so recovery accelerates as the target improves. `Pmax` scales the rate, applied after the quadratic factor and as a divisor for drain and a multiplier for recovery (EcosystemSimulator.cs:974, 985); it does not enter the target, so Condition keeps its species agnostic `[0, 1]` meaning and `ReproThreshold`/`DeathThreshold` need no per species tuning (EcosystemSimulator.cs:943-946). `BiologyStep` does not multiply the drain or recovery rates in Step 4. Unlike Steps 7, 8, and 9, the formulas above contain no `BiologyStep` term (EcosystemSimulator.cs:966-989); Condition advances one logical step per biology day regardless of how many calendar days that biology day represents. `Condition` is clamped to `[0, 1]` and persists across days. Default global rates: `ConditionDrainRate = 0.15`, `ConditionRecoveryRate = 0.10` (EcosystemSimulator.cs:240-241).

### 3.5 Step 5: Final performance (EcosystemSimulator.cs:619-630)

For each species: `FinalPerformance = ThermalPerformance * FedRate`. This is computed for CSV output and logging only. No later step reads it. Reproduction uses Condition, not `FinalPerformance` (EcosystemSimulator.cs:620-624).

### 3.6 Step 6: Thermal death (EcosystemSimulator.cs:632-637, body at 1000)

`ApplyThermalDeath` is an instant total kill at lethal temperature limits. It fires only when `RawThermalPerformance == 0`, which happens at or beyond `CTminC` or `CTmaxC`. When it fires, the entire population dies (`Population = 0`) and `Condition = 0` (EcosystemSimulator.cs:1015-1017). Deaths are added to `LastTempDeathsT1` (Tier 1) or `LastTempDeathsT2` (Tier 2) and to `LastTempDeathsBySpecies` (EcosystemSimulator.cs:1021-1024). Suboptimal but non lethal temperatures do not kill here; they erode Condition instead.

### 3.7 Step 7: Condition death (EcosystemSimulator.cs:639-644, body at 1056)

`ApplyConditionDeath` is graduated chronic mortality. It is skipped for dead species, guarded by `if (sp.Population < MIN_ALIVE_POP) return` (EcosystemSimulator.cs:1058). It then fires only when `Condition < DeathThreshold` (EcosystemSimulator.cs:1059). Severity scales with how far below the threshold Condition has fallen: `severity = (DeathThreshold - Condition) / DeathThreshold`, and `rawDeaths = Population * severity * DeathRate * BiologyStep` (EcosystemSimulator.cs:1062-1063). Fractional deaths accumulate in the condition death accumulator; whole deaths are removed and capped at the current population (EcosystemSimulator.cs:1067-1071). Survivors receive a fitness boost on the assumption the dead were the weakest members. The boost is guarded against division by zero: it is computed only when the post death population is positive, `if (sp.Population > 0f) { newCondition = oldCondition * oldPop / newPop; newCondition = min(1, newCondition); }` (EcosystemSimulator.cs:1082-1086). When condition deaths reduce the population to 0 this step, the boost is skipped and `Condition` keeps the value it held going into Step 7. That residual Condition does not matter for the rest of the day: Step 8 requires `Population >= 2` and is skipped for an extinct species, and Step 10 rounds the 0 population. Deaths feed `LastConditionDeathsT1` / `T2` and `LastConditionDeathsBySpecies` (EcosystemSimulator.cs:1090-1093).

### 3.8 Step 8: Reproduction (EcosystemSimulator.cs:646-651, body at 1150)

`ApplyReproduction` is condition driven graduated reproduction. It requires `Population >= MIN_POPULATION_FOR_REPRODUCTION = 2` (EcosystemSimulator.cs:1152, constant at 274). A reproduction scale `reproScale` in `[0, 1]` is computed from `Condition` relative to `ReproThreshold` as a continuous piecewise function joined at `STRUGGLING_REPRO_RATE = 0.10` (EcosystemSimulator.cs:1168-1191, constant at 282). The code has four branches in this order, including two threshold edge cases that guard the interior divisions against a zero or degenerate denominator:

- If `ReproThreshold >= 1.0`: `reproScale = 0.10 * Condition` (EcosystemSimulator.cs:1169-1172). This guards the interior `1 - ReproThreshold` denominator from reaching 0.
- Else if `ReproThreshold <= 0`: `reproScale = Condition` (EcosystemSimulator.cs:1174-1177). This guards the interior `Condition / ReproThreshold` division from dividing by 0.
- Else if `Condition >= ReproThreshold` (the healthy interior case, `0 < ReproThreshold < 1`): `t = (Condition - ReproThreshold) / (1 - ReproThreshold)`, `reproScale = 0.10 + 0.90 * t` (EcosystemSimulator.cs:1179-1183).
- Else (the struggling interior case): `reproScale = 0.10 * (Condition / ReproThreshold)` (EcosystemSimulator.cs:1185-1189).

`reproScale` is then clamped to `[0, 1]` (EcosystemSimulator.cs:1191). The default species use `ReproThreshold = 0.25`, so only the two interior branches fire in shipping runs; the edge branches matter only for configs that push the threshold to the boundary.

Births: `births = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep` (EcosystemSimulator.cs:1247).

Fractional births accumulate in the birth accumulator; whole births are then added to the population with `sp.Population += wholeBirths` and no cap, unlike the death steps which subtract and cap at the current population (EcosystemSimulator.cs:1228-1238). Births can therefore exceed the pre step population in a single day; the only population ceiling is the defensive `100 * CarryingCapacityPerTier` cap applied later in Step 10. Newborns inherit the species' current group Condition, so no explicit Condition update is needed at birth (EcosystemSimulator.cs:1240-1253). Births feed `LastBirthsT1` / `T2`, `LastBirthsBySpecies`, and `LastReproScaleBySpecies` (EcosystemSimulator.cs:1194-1198, 1258-1263). There is no soft cap on births; throttling happens indirectly through the Condition pathway (high population lowers food density, which lowers FedRate, which drains Condition).

### 3.9 Step 9: Natural death (EcosystemSimulator.cs:653-658, body at 1270)

`ApplyNaturalDeathWithAccumulator` applies a flat per step mortality independent of performance. Its alive guard differs from the other steps: it returns on `if (sp.Population <= 0f)` (EcosystemSimulator.cs:1272), whereas Steps 2, 4, 6, and 7 guard on `Population < MIN_ALIVE_POP` (i.e. strictly less than 1.0). A species with a transient fractional population in `(0, 1)` is therefore treated as alive for natural death but dead for those other steps. This window is only reachable mid day before Step 10 rounding, which rounds away from zero and would turn any such value into 1. The random variance is `variance = (_rng.NextDouble() * 2 - 1) * NaturalDeathVariance`, uniform on `[-NaturalDeathVariance, +NaturalDeathVariance]`, consuming the ecosystem RNG stream `_rng`, then `baseRate = max(0, NaturalDeathRate + variance)` (EcosystemSimulator.cs:1278-1279). The code's `effectiveRate` is just an alias of `baseRate` (EcosystemSimulator.cs:1282). `deaths = Population * baseRate * BiologyStep` (EcosystemSimulator.cs:1285). Fractional deaths accumulate in the natural death accumulator; whole deaths are removed and capped at the population (EcosystemSimulator.cs:1291-1299). Defaults: `NaturalDeathRate = 0.02`, `NaturalDeathVariance = 0.01` (SimSpecies.cs:40-41). Deaths feed `LastNaturalDeathsT1` / `T2` and `LastNaturalDeathsBySpecies` (EcosystemSimulator.cs:1308-1313).

### 3.10 Step 10: Population rounding and overflow guard (EcosystemSimulator.cs:660-686)

Population is guaranteed to be at least 0 entering Step 10. Every death step removes only whole events and caps them at the current population with `wholeDeaths = min(wholeDeaths, (long)Population)` followed by `Population = max(0f, Population - wholeDeaths)` (predation EcosystemSimulator.cs:887-889; condition death 1071, 1077; natural death 1299, 1304), and Step 6 sets `Population = 0` exactly, so no step can drive the float population below 0. For each species:

1. Cap the population at `popCap = 100 * CarryingCapacityPerTier` as a defensive overflow guard. The comparison is `if (sp.Population > popCap) sp.Population = popCap`, an exclusive test, so a population exactly equal to the cap is left unchanged (EcosystemSimulator.cs:667-679). The cap is applied to this day's accumulated population before rounding. This prevents `float` populations from exceeding `long.MaxValue` during the initial transient.
2. Round the population to an integer with `Math.Round(..., MidpointRounding.AwayFromZero)` (EcosystemSimulator.cs:681). Because the input is already at least 0, away from zero rounding only ever rounds up, never toward a negative value.

After this step the population is always a non negative integer valued float.

### 3.11 End of step rollup (EcosystemSimulator.cs:688-699)

1. `ComputeAverageCondition` computes the population weighted mean Condition per tier into `AvgConditionT1` and `AvgConditionT2` (EcosystemSimulator.cs:689, body at 1324).
2. `EndPopT1 = GetTier1Population()`, `EndPopT2 = GetTier2Population()` (EcosystemSimulator.cs:692-693).
3. `UpdateAccumulatorTotals` sums every species' accumulator residuals into the tier level accumulator total fields for CSV output (EcosystemSimulator.cs:694, body at 1343).

### 3.12 Step order summary

| Step | Method | Reads | Writes |
|------|--------|-------|--------|
| 1 | inline + `CalculatePerformance` | day temp, `Pmax` | `RawThermalPerformance`, `ThermalPerformance`, `FedRate=1` |
| 2 | `ProcessFeedingWithAccumulator` | `tier1Pop`, `CarryingCapacityPerTier`, `EatingAmount`, `HuntingEfficiency`, `HuntingVariance` | `FedRate`, `LastFedRateT1`, `LastFoodDensityT1`, predation removals (Tier 2) |
| 3 | inline | `RawThermalPerformance`, `FedRate` | `RawFinalPerformance` |
| 4 | `UpdateCondition` | `RawFinalPerformance`, `Pmax`, drain/recovery rates | `Condition` |
| 5 | inline | `ThermalPerformance`, `FedRate` | `FinalPerformance` (logging only) |
| 6 | `ApplyThermalDeath` | `RawThermalPerformance` | `Population`, `Condition`, temp death counters |
| 7 | `ApplyConditionDeath` | `Condition`, `DeathThreshold`, `DeathRate` | `Population`, `Condition`, condition death counters |
| 8 | `ApplyReproduction` | `Condition`, `ReproThreshold`, `ReproductionMultiplier`, `Pmax` | `Population`, birth counters, repro scale |
| 9 | `ApplyNaturalDeathWithAccumulator` | `NaturalDeathRate`, variance | `Population`, natural death counters |
| 10 | inline | `CarryingCapacityPerTier` | `Population` (capped, rounded integer) |

## 4. Accumulators

Several biology steps produce fractional event counts per day. The simulator carries the fractional residual across days so that, for example, 0.4 births today plus 0.7 births tomorrow yield one whole birth tomorrow with 0.1 carried forward. There are five accumulator dictionaries, keyed by `FullName` (EcosystemSimulator.cs:150-154):

| Accumulator | Field | Step | Accessor |
|-------------|-------|------|----------|
| Birth | `_birthAccumulators` | 8 | `GetBirthAccum(fullName)` (EcosystemSimulator.cs:224) |
| Natural death | `_naturalDeathAccumulators` | 9 | `GetNaturalDeathAccum(fullName)` (EcosystemSimulator.cs:226) |
| Predation | `_predationAccumulators` | 2 (Tier 2) | `GetPredationAccum(fullName)` (EcosystemSimulator.cs:230) |
| Condition death | `_conditionDeathAccumulators` | 7 | `GetConditionDeathAccum(fullName)` (EcosystemSimulator.cs:228) |
| Thermal death | `_thermalDeathAccumulators` | none | dead code, no accessor (EcosystemSimulator.cs:223) |

The first three steps of the extraction pattern are shared by all four live accumulators: add the raw amount to the accumulator, take `wholeEvents = (long)Math.Floor(accumulated)`, then subtract `wholeEvents` from the accumulator. `long` is used rather than `int` to avoid 32 bit overflow at extreme populations. What happens to the population differs by accumulator kind.

The three death and predation accumulators (Steps 2, 7, 9) then cap the whole events at the current population and subtract them: `wholeEvents = min(wholeEvents, (long)Population)`, then `Population = max(0f, Population - wholeEvents)` (predation EcosystemSimulator.cs:887-889; condition death 1071, 1077; natural death 1299, 1304). The cap happens after the full `wholeEvents` was already subtracted from the accumulator, so when deaths exceed the population the clipped excess is not re credited to the accumulator. It is discarded. Residual on the accumulator equals `accumulated - wholeEvents` (the floored fraction only), never `accumulated - appliedEvents`. Near extinction this means deaths beyond the population are lost rather than carried forward to a future day.

The birth accumulator (Step 8) does the opposite. After the floor and subtract it adds the whole births to the population with no cap: `Population += wholeBirths` (EcosystemSimulator.cs:1234-1238). Births are never clipped to the current population, so the "cap at current population" rule does not apply to the birth path. The only population ceiling is the defensive `100 * CarryingCapacityPerTier` cap applied later in Step 10.

Thermal death has no accumulator because it is an all or nothing whole population kill; `_thermalDeathAccumulators` is allocated and cleared but never read.

## 5. Global Invariants

The system maintains the following invariants. A reimplementation must preserve all of them.

### 5.1 Per species values sum to tier totals

The sum to tier invariant applies only to the additive whole event counters: births, the three death types, and eaten. For each of these the sum over species of the per species value equals the matching tier level `Last*` counter (EcosystemSimulator.cs:212). This holds because each per species dictionary entry is incremented from the same code that increments the tier counter, inside the same step. It does not apply to the mean valued metrics. `LastFedRateT1` and `LastFedRateT2` are population weighted means (EcosystemSimulator.cs:780, 863) and `AvgConditionT1`/`AvgConditionT2` are population weighted means (EcosystemSimulator.cs:1336-1337); these do not sum from their per species values and a reimplementation must not try to enforce a sum invariant on them. The additive pairs:

| Per species dictionary | Tier 1 counter | Tier 2 counter |
|------------------------|----------------|----------------|
| `LastBirthsBySpecies` | `LastBirthsT1` | `LastBirthsT2` |
| `LastTempDeathsBySpecies` | `LastTempDeathsT1` | `LastTempDeathsT2` |
| `LastConditionDeathsBySpecies` | `LastConditionDeathsT1` | `LastConditionDeathsT2` |
| `LastNaturalDeathsBySpecies` | `LastNaturalDeathsT1` | `LastNaturalDeathsT2` |
| `LastEatenBySpecies` | `LastEatenT1` | (no Tier 2 counter) |

`LastEatenT1` counts Tier 1 prey individuals removed by Tier 2 predation. It is a Tier 1 victim counter driven by the Tier 2 feeding code, not a count of Tier 2 activity, which is why there is no `LastEatenT2`. In Tier 1 only runs there are no predators, so both `LastEatenT1` and `LastEatenBySpecies` are identically 0 and the sum invariant holds trivially.

This propagates to the CSV. The per species `_Pop` columns sum to the dynamic `Tier{n}_{label}` rollup column for their (tier, label), which in turn sum to `Tier{n}Pop` (SimulationRunner.cs:548-551). The rollup column key is built with the same label rule and `SanitizeColumnName` as the column header, so they always align.

### 5.2 Population is an integer after every day

After Step 10, every species' population is rounded with `MidpointRounding.AwayFromZero` to an integer valued float (EcosystemSimulator.cs:681). All population values written to records and CSV pass through `SafePopToLong`, which rounds and maps non finite values to 0 (SimulationRunner.cs:597-598). A species is extinct exactly when its rounded population is 0.

### 5.3 Population is bounded

Population is capped at `100 * CarryingCapacityPerTier` before rounding (EcosystemSimulator.cs:668, 675-679). This is a defensive guard against overflow, well above any biological overshoot.

### 5.4 Bounded dimensionless quantities

The explicitly clamped quantities are `RawThermalPerformance` (clamped inside `CalculatePerformance`, SimSpecies.cs:132), `FedRate` (Tier 1 at EcosystemSimulator.cs:792, Tier 2 at 857), `Condition` (EcosystemSimulator.cs:989), and `reproScale` (EcosystemSimulator.cs:1191). `RawFinalPerformance = RawThermalPerformance * FedRate` is bounded by `[0, 1]` because both factors are, even though it has no clamp of its own.

`ThermalPerformance` and `FinalPerformance` are not clamped. `ThermalPerformance = RawThermalPerformance * Pmax` (EcosystemSimulator.cs:594) and `FinalPerformance = ThermalPerformance * FedRate` (EcosystemSimulator.cs:628) have no explicit clamp. They stay within `[0, 1]` only by the convention that `Pmax` is configured in `(0, 1]` (`Pmax` default 1.0, SimSpecies.cs:68). No code enforces `Pmax <= 1`, so a config with `Pmax > 1` would let both exceed 1. `Condition` starts at 1.0 and persists across days (SimSpecies.cs:80).

### 5.5 Determinism under the same seed

A scenario is fully determined by its seed when the seed is non negative. The seed initializes both the temperature calculator's RNG and the ecosystem's RNG (SimulationRunner.cs:382-387). Pausing a run spins the day loop without consuming RNG or advancing state, so a paused then resumed run is byte identical to an uninterrupted run with the same seed (RunControl.cs:1-13; SimulationRunner.cs:417-423). Parallel versus sequential scenario execution does not change results because each scenario gets a deterministic seed (`RandomSeed + i`). This determinism guarantee holds only for `RandomSeed >= 0`. When `RandomSeed < 0` every scenario is constructed with the sentinel `-1` and seeds its RNGs from the system clock, so runs are not reproducible (see Section 2.2).

### 5.6 Identity key consistency

Every per species dictionary, CSV column prefix, and metric is keyed by `FullName` (SimSpecies.cs:89). `FullName` is `Name` when `VariantLabel` is empty, otherwise `"{Name}_{VariantLabel}"`. The `ThermalVariant` enum (Arctic, Common, Tropical, Custom; SimSpecies.cs:3) is internal only and is never written to any output. Only the free text `VariantLabel` appears in output.

### 5.7 Crash definition

A scenario has crashed when total population reaches 0. `HasCrashed()` returns `GetTier1Population() + GetTier2Population() == 0` (EcosystemSimulator.cs:1386-1390). The crash is detected only on biology days, after the biology sequence (SimulationRunner.cs:440-447). On crash the loop records `CrashDay` and `CrashTier` and stops. `GetCrashedTier` keys its result off the per tier "was populated at init" flags `_tier1WasPopulated` and `_tier2WasPopulated`, set in `InitializeFromRunSpeciesList` (EcosystemSimulator.cs:391-392), and returns in this order (EcosystemSimulator.cs:1392-1398):

- `0` when Tier 1 was populated and both tiers are now empty (`_tier1WasPopulated && T1 == 0 && T2 == 0`).
- `1` when Tier 1 was populated and Tier 1 is now empty (`_tier1WasPopulated && T1 == 0`).
- `2` when Tier 2 was populated and Tier 2 is now empty (`_tier2WasPopulated && T2 == 0`).
- `-1` otherwise.

Because the `0` and `1` branches gate on `_tier1WasPopulated`, a Tier 2 only ecosystem (no Tier 1 at init) never returns `0` or `1`; when its only populated tier empties it returns `2`. For the shipping Tier 1 only configuration the relevant outcomes are `0` (or equivalently `1`, since `T2` is already 0).

## 6. Species Loading

Species enter a scenario through `EcosystemSimulator.InitializeFromRunSpeciesList(RunSpeciesList)` (EcosystemSimulator.cs:317), the primary path. `SimulationRunner.Run` calls it when a `RunSpeciesList` is present, otherwise it falls back to `InitializeDefaultSpecies` (SimulationRunner.cs:403-411). A legacy `InitializeFromDatabase` path also exists (EcosystemSimulator.cs:399).

During load each `SpeciesData` becomes a `SimSpecies` with `Tier = data.tier + 1` (EcosystemSimulator.cs:356), `Name` from `speciesLabel` or the `speciesName` enum, and `VariantLabel` from `variantLabel` or the `variant` enum (EcosystemSimulator.cs:351-380). Two filters apply during load:

1. Tier 2 drop when the gate is off (EcosystemSimulator.cs:336), see Section 1.
2. Duplicate merge: species whose name and variant labels normalize to the same match key are merged into the first seen species (EcosystemSimulator.cs:338-349). The match key is `SpeciesNameMatchKey(name) + "_" + VariantMatchKey(label)` (EcosystemSimulator.cs:343), where `name` is `speciesLabel` or the `speciesName` enum name and `label` is `variantLabel` or the `variant` enum name (EcosystemSimulator.cs:341-342). Both `SpeciesNameMatchKey` and `VariantMatchKey` normalize identically: lowercase via `char.ToLowerInvariant`, then keep only the characters `[a-z0-9]`, dropping every space, dash, underscore, and other punctuation (SpeciesDatabase.cs:216-226, 243-253). So `"Hexapod Cold"`, `"hexapod_cold"`, and `"HEXAPOD-COLD"` all collide, but `"topic3"` and `"topic4"` do not. The merge is destructive and not symmetric across parameters: only `Population` is summed onto the first seen species (`existingSp.Population += data.count`, EcosystemSimulator.cs:346); every other biology parameter (`Pmax`, the rates, the thresholds, the thermal curve fields) is taken from the first seen species and the duplicate's values are dropped. Unique keys never merge.

Each loaded species gets its five accumulators initialized to 0 (EcosystemSimulator.cs:383, 489-496).

### 6.1 Default species set (`InitializeDefaultSpecies`)

When `SimulationRunner.Run` is called with no usable `RunSpeciesList` (the reference is null, its `speciesList` is null, or the list is empty), it logs a warning and falls back to `InitializeDefaultSpecies` (SimulationRunner.cs:403-411). This fallback builds a fixed six species set (EcosystemSimulator.cs:501-523):

1. Clear `Species` and clear all accumulators (EcosystemSimulator.cs:503-504).
2. Add three Tier 1 Hexapod species, one per `ThermalVariant`, each seeded with 20 individuals: `CreateHexapod(Arctic, 20)`, `CreateHexapod(Common, 20)`, `CreateHexapod(Tropical, 20)` (EcosystemSimulator.cs:507-509).
3. Add three Tier 2 Sheplik species, one per `ThermalVariant`, each seeded with 4 individuals: `CreateSheplik(Arctic, 4)`, `CreateSheplik(Common, 4)`, `CreateSheplik(Tropical, 4)` (EcosystemSimulator.cs:511-513).
4. Initialize each species' five accumulators to 0, keyed by `FullName` (EcosystemSimulator.cs:516-519).
5. Set `_tier1WasPopulated` and `_tier2WasPopulated` from the resulting tier populations (EcosystemSimulator.cs:521-522).

The factory methods build Hexapod with `VariantLabel` set to `"Cold"`, `"Warm"`, `"Hot"` for the `Arctic`, `Common`, `Tropical` enum slots respectively (SimSpecies.cs:169, 180, 189). Sheplik does not set a `VariantLabel`, so its `FullName` is just `"Sheplik"` for all three enum variants, which means all three Sheplik collide to one identity key in any per species dictionary. This default set predates the Tier 1 only scope. In a shipping Tier 1 only run the three Sheplik are dropped at load because `Tier2Enabled` is false (Section 1), leaving the three Hexapod species `Hexapod_Cold`, `Hexapod_Warm`, `Hexapod_Hot` with 20 individuals each.

This fallback is a hardcoded safety net. The shipping configuration provides a `RunSpeciesList` asset and never takes this path under normal operation.

### 6.2 Default species parameter values

Both factory methods set every biology parameter explicitly. The values below are the canonical defaults baked into the source. Tier 1 (Hexapod) is the shipping path and is given in full. Tier 2 (Sheplik) is legacy and secondary; its values are listed for completeness because the default set constructs them, but they are inert in Tier 1 only runs.

#### 6.2.1 Tier 1 Hexapod (`CreateHexapod`, SimSpecies.cs:140-202)

Shared parameters, identical across all three Hexapod variants (SimSpecies.cs:142-163):

| Field | Value | Meaning |
|-------|-------|---------|
| `Name` | `"Hexapod"` | Species name. |
| `Tier` | `1` | Prey tier (1 based). |
| `EatingAmount` | `0f` | Per-individual draw from the shared food pool, floored at 1 in the consumption sum (EcosystemSimulator.cs:776), so this default behaves as appetite 1 (one resource point each). |
| `ReproductionMultiplier` | `0.45f` | Birth rate multiplier in Step 8. |
| `DeathThreshold` | `0.3f` | Condition below this triggers condition death in Step 7. |
| `DeathRate` | `0.6f` | Fraction scaling for condition death severity. |
| `ReproThreshold` | `0.25f` | Condition inflection point for the reproduction scale in Step 8. |
| `NaturalDeathRate` | `0.02f` | Flat per step natural mortality (2 percent) in Step 9. |
| `NaturalDeathVariance` | `0.01f` | Uniform variance band for natural death (plus or minus 1 percent). |
| `HuntingEfficiency` | `1.0f` | Base foraging success; at >= 1 the shared Holling curve returns 1, so `FedRate` equals `foodDensity` (EcosystemSimulator.cs:971). |
| `HuntingVariance` | `0f` | No foraging variance, so the Tier 1 foraging draw is deterministic and consumes no RNG (EcosystemSimulator.cs:934). |
| `ArrhenBreadth` (`B`) | `5000f` | Arrhenius breadth (before the per variant override, which leaves it at 5000). |
| `Pmax` (default field) | `1.0f` | Peak height before the per variant override below replaces it. |

Per variant parameters. Each Hexapod variant shares the curve shape and shifts only the optimum, the half drop bounds, the critical limits, and the peak height. The `ArrhenLower` (`L`) and `ArrhenUpper` (`U`) fields are set twice: the shared block sets `L = 16000`, `U = 43800` (SimSpecies.cs:161-162), then the Cold and Hot variant blocks translate them by plus or minus 2 (the Warm block does not, so Warm keeps the shared values). The effective per variant values are (SimSpecies.cs:168-198):

| Field | Cold (`Arctic` slot) | Warm (`Common` slot) | Hot (`Tropical` slot) |
|-------|----------------------|----------------------|-----------------------|
| `VariantLabel` | `"Cold"` | `"Warm"` | `"Hot"` |
| `OptimalTempK` (`OT`) | `293.15f` (20 C) | `295.15f` (22 C) | `297.15f` (24 C) |
| `LowerBoundK` (`LB`) | `292.40f` (19.25 C) | `294.40f` (21.25 C) | `296.40f` (23.25 C) |
| `UpperBoundK` (`UB`) | `293.90f` (20.75 C) | `295.90f` (22.75 C) | `297.90f` (24.75 C) |
| `ArrhenLower` (`L`) | `15998f` | `16000f` (shared, unchanged) | `16002f` |
| `ArrhenUpper` (`U`) | `43798f` | `43800f` (shared, unchanged) | `43802f` |
| `Pmax` | `0.9843f` | `0.972f` | `0.96f` |
| `CTminC` | `0f` | `2f` | `4f` |
| `CTmaxC` | `35f` | `37f` | `39f` |
| `ArrhenBreadth` (`B`) | `5000f` | `5000f` | `5000f` |

These are the values consumed by `CalculatePerformance` (Section 3.1). The remaining `SimSpecies` defaults that the factory does not override stay at their field initializer values: `ConditionDrainRate = -1f` and `ConditionRecoveryRate = -1f` (negative means inherit the simulator global rates, SimSpecies.cs:36-37), `TemperatureDebuff = 0f` (SimSpecies.cs:71), and `Condition = 1.0f` starting value (SimSpecies.cs:80).

#### 6.2.2 Tier 2 Sheplik (`CreateSheplik`, SimSpecies.cs:207-258), legacy and secondary

Sheplik is the predator tier from the original two tier design. It is dropped at load in every shipping Tier 1 only run, so these values never drive a shipping simulation. They are recorded only because `InitializeDefaultSpecies` constructs them.

Shared parameters (SimSpecies.cs:209-227): `Name = "Sheplik"`, `Tier = 2`, `EatingAmount = 1.5f`, `ReproductionMultiplier = 0.1f`, `DeathThreshold = 0.3f`, `DeathRate = 0.3f`, `ReproThreshold = 0.25f`, `NaturalDeathRate = 0.01f`, `NaturalDeathVariance = 0.005f`, `HuntingEfficiency = 0.75f`, `HuntingVariance = 0.15f`, `ArrhenBreadth = 5273.15f`, `ArrhenLower = 10273.15f`, `ArrhenUpper = 21273.15f`. Sheplik sets no `VariantLabel`.

Per variant parameters (SimSpecies.cs:229-255):

| Field | `Arctic` | `Common` | `Tropical` |
|-------|----------|----------|------------|
| `OptimalTempK` | `278.15f` | `293.15f` | `308.65f` |
| `LowerBoundK` | `270.15f` | `285.15f` | `300.15f` |
| `UpperBoundK` | `280.15f` | `295.15f` | `310.15f` |
| `Pmax` | `1.0f` | `0.9f` | `1.0f` |
| `CTminC` | `-30f` | `-5f` | `0f` |
| `CTmaxC` | `20f` | `40f` | `80f` |

## 7. Per Day Record and Crash Handling

After each day, `RecordStep` builds one `StepRecord` (SimulationRunner.cs:464). The record holds the day, year, temperature, biology cycle index, start and end total populations, per tier populations, the dynamic per variant rollup populations, all death and birth counters, feeding and condition metrics, accumulator totals, and a `SpeciesData` dictionary of `PerSpeciesStepData` keyed by `FullName` (SimulationRunner.cs:480-581). On non biology days the daily event counts are zeroed while populations and physiology carry forward (SimulationRunner.cs:467-473, 559-575). The `PerSpeciesStepData` struct and the exact CSV column layout it produces are specified in Section 7.1.

`PerSpeciesStepData.BirthRate` is computed by guarded division, not by a `max(.., 1)` denominator: `BirthRate = startPop > 0 ? (float)Births / startPop : 0` (SimulationRunner.cs:574). When `startPop` is 0 it returns 0, not `Births / 1`. (A struct comment at SimulationRunner.cs:37 still reads `Births / max(StartOfDayPop, 1)`; that comment is stale and the implementation diverges from it.) `startPop` is `StartPopBySpecies[FullName]`, the start of step snapshot taken in the reset block (Section 3.0), with a defensive fallback: if the snapshot is 0 but the current rounded population is positive, `startPop` is replaced with the current rounded population (`if (startPop == 0L && sp.Population > 0f) startPop = SafePopToLong(sp.Population)`, SimulationRunner.cs:553-557).

On a non biology day the reset block does not run, `Births` is 0 (it is read as 0 unless `biologyRan`, SimulationRunner.cs:559), and `StartPopBySpecies` still holds the stale snapshot from the last biology day. With `Births == 0` the numerator is 0, so `BirthRate` is 0 regardless of the denominator. The per capita rate feeds the final year metrics in `ScenarioResult`.

### 7.1 Per species CSV columns

Each scenario CSV appends 17 per species columns to every daily row, one block per species. The fields, their order, their `StepRecord.PerSpeciesStepData` source field, and their formatting are fixed. The header builder (`StepRecord.CsvHeader`, SimulationRunner.cs:264-313), the row builder (`StepRecord.ToCsvLine`, SimulationRunner.cs:209-249), and the `PerSpeciesStepData` struct (SimulationRunner.cs:20-48) must all agree on this order or rows misalign with the header.

Species order. The block order is the scenario's stable species order, sorted by `Tier` ascending then `FullName` ascending, built once in `ToCsvInternal` and reused for the header and every row (SimulationRunner.cs:798-806). In a Tier 1 only run only Tier 1 species appear; Tier 2 species were dropped at load.

Column naming. Each column is named `{prefix}_{Field}` where `prefix = SanitizeColumnName(FullName)` (SimulationRunner.cs:295, 305-309). `SanitizeColumnName` replaces every character outside `[A-Za-z0-9_]` with `_` and prepends `_` if the result starts with a digit (SimulationRunner.cs:322-337). If two species sanitize to the same prefix, the second and later get a `_2`, `_3`, ... suffix (SimulationRunner.cs:292-303). The header writes the column names; the row writes only the values in the same order with no per cell name.

The 17 fields, in exact emission order (header names from SimulationRunner.cs:305-309; values and formats from SimulationRunner.cs:241-245):

| # | Column suffix | `PerSpeciesStepData` field | Format | Meaning |
|---|---------------|----------------------------|--------|---------|
| 1 | `_Pop` | `Population` (`long`) | integer | Rounded population after Step 10. |
| 2 | `_Cond` | `Condition` (`float`) | `:F3` | Condition after Step 7 boost, `[0, 1]`. |
| 3 | `_ThermalPerf` | `ThermalPerf` (`float`) | `:F3` | `RawThermalPerformance` from Step 1, without `Pmax`. |
| 4 | `_FinalPerf` | `FinalPerf` (`float`) | `:F3` | `FinalPerformance` from Step 5, logging only. |
| 5 | `_FedRate` | `FedRate` (`float`) | `:F3` | Step 2 feeding satisfaction, `[0, 1]`. |
| 6 | `_HuntingEff` | `HuntingEff` (`float`) | `:F3` | Tier 2 `CurrentHuntingSuccess`; written as 0 for Tier 1 (SimulationRunner.cs:568). |
| 7 | `_Births` | `Births` (`long`) | integer | Whole births added in Step 8 this day. |
| 8 | `_TempDeaths` | `TempDeaths` (`long`) | integer | Whole thermal deaths in Step 6 this day. |
| 9 | `_CondDeaths` | `ConditionDeaths` (`long`) | integer | Whole condition deaths in Step 7 this day. |
| 10 | `_NatDeaths` | `NaturalDeaths` (`long`) | integer | Whole natural deaths in Step 9 this day. |
| 11 | `_Eaten` | `Eaten` (`long`) | integer | Tier 1 prey eaten by predation this day; 0 for Tier 2 and 0 in Tier 1 only runs. |
| 12 | `_BirthRate` | `BirthRate` (`float`) | `:F4` | Per capita `Births / startPop` (Section 7), guarded to 0 when `startPop` is 0. |
| 13 | `_ReproScale` | `ReproScale` (`float`) | `:F3` | Step 8 reproduction scale, `[0, 1]`. |
| 14 | `_BirthAccum` | `BirthAccum` (`float`) | `:F3` | Birth accumulator residual after this day. |
| 15 | `_NatDeathAccum` | `NaturalDeathAccum` (`float`) | `:F3` | Natural death accumulator residual after this day. |
| 16 | `_CondDeathAccum` | `ConditionDeathAccum` (`float`) | `:F3` | Condition death accumulator residual after this day. |
| 17 | `_PredAccum` | `PredationAccum` (`float`) | `:F3` | Predation accumulator residual; Tier 1 only, 0 in Tier 1 only runs. |

The row writes the block as a leading comma then the 17 values in this order (SimulationRunner.cs:241-245), so for a species with sanitized prefix `Hexapod_Cold` the header segment is `,Hexapod_Cold_Pop,Hexapod_Cold_Cond,Hexapod_Cold_ThermalPerf,Hexapod_Cold_FinalPerf,Hexapod_Cold_FedRate,Hexapod_Cold_HuntingEff,Hexapod_Cold_Births,Hexapod_Cold_TempDeaths,Hexapod_Cold_CondDeaths,Hexapod_Cold_NatDeaths,Hexapod_Cold_Eaten,Hexapod_Cold_BirthRate,Hexapod_Cold_ReproScale,Hexapod_Cold_BirthAccum,Hexapod_Cold_NatDeathAccum,Hexapod_Cold_CondDeathAccum,Hexapod_Cold_PredAccum`.

These per species columns follow the tier level columns. The tier level layout precedes them on the same row and is described by the same two builders; the per species block is appended after `ReproScaleT1` (and `ReproScaleT2` when Tier 2 is enabled) (SimulationRunner.cs:232-247, 286-310).

## 8. Temperature Model

`TemperatureCalculator` is the standalone temperature driver for the simulation (TemperatureCalculator.cs:15-205). It is a separate class from the interactive game's `Temperature`. Each scenario owns one instance, constructed with the scenario seed (SimulationRunner.cs:385). The biology loop reads one temperature per day from `GetTemperature(dayIndex)` (Section 2.1, step 3), where `dayIndex` is zero based. All temperatures are in degrees Celsius.

There are two modes. The default is the parametric five component model (Section 8.1). If a daily timeseries is loaded, it overrides the parametric model (Section 8.4).

### 8.1 Parametric model

`GetTemperature(day)` for the parametric path is (TemperatureCalculator.cs:77-84):

```
T(day) = BaseTemperature
       + Seasonal(day)
       + ClimateTrend(day)
       + InterannualVariation(day)
       + DailyVariation(day)
T(day) = clamp(T(day), MinTemp, MaxTemp)        // hard floor/ceiling, applied last
```

`day` is the zero based day index. `DAYS_PER_YEAR = 365` is a constant (TemperatureCalculator.cs:30). The five components:

1. Base. The constant offset `BaseTemperature` (TemperatureCalculator.cs:77). It is the only fixed term.

2. Seasonal (TemperatureCalculator.cs:126-129). A sine wave with a one year period and zero annual mean:

```
Seasonal(day) = sin(2 * PI * day / 365) * SeasonalAmplitude     // degrees Celsius
```

`SeasonalAmplitude` is the peak seasonal swing in degrees Celsius. The wave is 0 at day 0, rises to `+SeasonalAmplitude` near day 91, returns to 0 at day 182, and so on. The header comment "coldest at day 0" is inaccurate for this sign convention; day 0 is the zero crossing, not the minimum.

3. Climate trend (TemperatureCalculator.cs:134-138). A linear ramp, the only component with a nonzero long term mean:

```
years = day / 365.0
ClimateTrend(day) = ClimateTrendPerYear * years                 // degrees Celsius
```

`ClimateTrendPerYear` is the warming in degrees Celsius added per elapsed year. The division uses `(float)DAYS_PER_YEAR`, so `years` is fractional.

4. Interannual variation (TemperatureCalculator.cs:148-170). One zero mean offset per year, held constant for the whole year. It is disabled entirely when `UseInterannualVariation` is false, returning 0. Otherwise the year is `year = day / 365` (integer division). The first time a given year is seen, one draw is computed and memoized in a per year dictionary (`_yearVariations`); subsequent days in the same year reuse it. The draw is:

```
coldPart = rng.NextDouble() * (-VariabilityMagnitude)              // uniform on [-VariabilityMagnitude, 0]
warmPart = rng.NextDouble() * (VariabilityMagnitude * WarmingBias) // uniform on [0, VariabilityMagnitude * WarmingBias]
biasMean = VariabilityMagnitude * (WarmingBias - 1) / 4            // shape-correction constant
value    = (coldPart + warmPart) / 2 - biasMean                   // memoized for this year
```

`VariabilityMagnitude` is the year to year variation range in degrees Celsius. `WarmingBias` skews the shape of the distribution toward warm years when greater than 1, while `biasMean` subtracts the mean that the skew would otherwise introduce so that the long term trend stays owned entirely by `ClimateTrendPerYear` (TemperatureCalculator.cs:156-166). Each year that is drawn consumes exactly two `rng.NextDouble()` values, `coldPart` then `warmPart`, in that order.

5. Daily variation (TemperatureCalculator.cs:175-196). Fresh noise every day, optionally smoothed:

```
year = day / 365                                                  // integer division
currentRandomness = BaseRandomness + RandomnessGrowthRate * year  // degrees Celsius
newRandom = (rng.NextDouble() * 2 - 1) * currentRandomness        // uniform on [-currentRandomness, +currentRandomness]
if UseAutocorrelation:
    variation = previousDayVariation * 0.7 + newRandom * 0.3       // AR(1)-style smoothing, 0.7/0.3 fixed
else:
    variation = newRandom
previousDayVariation = variation                                  // carried to the next day
DailyVariation(day) = variation
```

`BaseRandomness` is the base daily noise half range in degrees Celsius. `RandomnessGrowthRate` widens it by that many degrees per elapsed year (integer year). When `UseAutocorrelation` is true the day blends 70 percent of yesterday's variation with 30 percent of the new draw, which produces smoother day to day transitions; the 0.7 and 0.3 weights are fixed in code. `previousDayVariation` is state that persists across days within a scenario and resets to 0 on `Reset` (TemperatureCalculator.cs:55). Daily variation consumes exactly one `rng.NextDouble()` value every day, whether or not autocorrelation is on.

### 8.2 The eleven parameters and their defaults

The model exposes eleven public fields. The defaults below are the C# field initializers in `TemperatureCalculator` (TemperatureCalculator.cs:18-28). `SimulationController` overwrites every one of them from the `SimulationConfig` before each scenario (Section 2.2 and SimulationController.cs:353-363), so the effective values in a real run come from the config (Section 9), not from these initializers. The two are listed side by side because the config field names differ from the calculator field names.

| `TemperatureCalculator` field | Type | Calculator default | `SimulationConfig` source field | Config default (Section 9) | Units / meaning |
|-------------------------------|------|--------------------|---------------------------------|----------------------------|-----------------|
| `BaseTemperature` | float | `20f` | `BaseTemperature` | `20f` | Base mean temperature, degrees Celsius. |
| `SeasonalAmplitude` | float | `5f` | `SeasonalAmplitude` | `5f` | Seasonal peak swing, degrees Celsius. |
| `ClimateTrendPerYear` | float | `1f` | `ClimateTrend` | `1f` | Warming per year, degrees Celsius. |
| `VariabilityMagnitude` | float | `2f` | `VariabilityMagnitude` | `2f` | Year to year variation range, degrees Celsius. |
| `WarmingBias` | float | `1.5f` | `WarmingBias` | `1.5f` | Warm year shape skew (1 = symmetric). |
| `BaseRandomness` | float | `5f` | `DailyVariationRange` | `5f` | Daily noise half range, degrees Celsius. |
| `RandomnessGrowthRate` | float | `0.5f` | `RandomnessGrowthRate` | `0.5f` | Daily noise growth per year, degrees Celsius. |
| `UseInterannualVariation` | bool | `true` | `InterannualVariation` | `true` | Enable the per year offset. |
| `UseAutocorrelation` | bool | `true` | `Autocorrelated` | `true` | Smooth daily noise with the 0.7/0.3 blend. |
| `MinTemp` | float | `-5f` | `TemperatureBoundsMin` | `-5f` | Hard floor of the final clamp, degrees Celsius. |
| `MaxTemp` | float | `40f` | `TemperatureBoundsMax` | `50f` | Hard ceiling of the final clamp, degrees Celsius. |

The shipping `SimulationConfig.asset` overrides several of these config defaults to different serialized values (Section 9 documents the asset values). The clamp to `[MinTemp, MaxTemp]` is always applied last, after summing all five components, in both the parametric and the timeseries paths (TemperatureCalculator.cs:74, 84).

### 8.3 RNG consumption order and determinism

The calculator's RNG is `_rng`, a `System.Random` seeded in the constructor as `_rng = (seed < 0) ? new Random() : new Random(seed)` (TemperatureCalculator.cs:43-46). A negative seed draws from the system clock and is non reproducible; a non negative seed is reproducible (Section 5.5). This is a separate RNG instance from the ecosystem's `_rng`, so the temperature stream and the biology stream never interleave.

Within one call to `GetTemperature(day)` on the parametric path, RNG is consumed in this order:

1. Interannual variation. If `UseInterannualVariation` is true and `day`'s year has not been drawn yet, two `NextDouble()` calls are consumed (`coldPart` then `warmPart`). On every later day of an already drawn year, and whenever `UseInterannualVariation` is false, no RNG is consumed here.
2. Daily variation. Exactly one `NextDouble()` call, every day, regardless of `UseAutocorrelation`.

So the first day of each new year consumes three random draws (two interannual then one daily); every other day consumes one. Components 1 (base), 2 (seasonal), and 3 (climate trend) are deterministic and consume no RNG. A reimplementation must preserve this draw count and order to reproduce a seeded scenario byte for byte. Because the days are read in strictly increasing order by the day loop (Section 2.1), the year draws also occur in increasing year order.

### 8.4 Timeseries override path

A scenario can replace the parametric model with an explicit daily temperature series (TemperatureCalculator.cs:37-41, 60-97). When a non empty series is loaded, `GetTemperature(day)` returns `clamp(series[day mod seriesCount], MinTemp, MaxTemp)` and never touches the parametric components or the RNG (TemperatureCalculator.cs:66-75). The series is indexed by day and loops if it is shorter than the run, with a single warning logged the first time the index passes the end (TemperatureCalculator.cs:68-73). `HasTimeseries` reports whether one is loaded (TemperatureCalculator.cs:97).

The series is loaded with `LoadTimeseries(List<float>)`, which stores the list only when it is non null and non empty, otherwise clears the override back to the parametric model (TemperatureCalculator.cs:91-95). `ParseTimeseriesCsv(string)` builds the list from CSV text (TemperatureCalculator.cs:104-121): it normalizes line endings, skips blank lines and lines starting with `#`, splits each remaining line on commas, and parses the last comma separated cell as the temperature using invariant culture float parsing. The last column rule lets it read both a `Day,Temperature_C` two column file and a bare single column file. Rows that fail to parse, including a header row, are skipped. It returns null when no numeric rows are found.

In standard `SimulationController` runs no timeseries is loaded, so the parametric model always applies. The timeseries path is wired only for bulk batches: when a `BulkBatchConfig` provides a `TemperatureTimeseriesFile` path, `RunSingleScenarioFromBatch` reads the file in Editor and standalone builds, parses it, and loads it on the runner's `TempCalc`; a missing file, a WebGL build, or a parse failure logs a warning and falls back to the parametric model (SimulationController.cs:317-334).

## 9. Configuration Defaults

The scenario, run, and engine parameters come from `SimulationConfig`, a ScriptableObject (SimulationConfig.cs:24). Two layers of defaults exist and they differ:

- The C# field initializers in `SimulationConfig.cs` (the values a freshly created config asset starts with).
- The serialized values in the shipping `Assets/Resources/SimulationConfig.asset`, which override several initializers and are the values an actual shipping run uses.

Both are documented below because the gap closed here is "the defaults are not given." A reimplementation that wants to match a shipping run must use the asset column; a reimplementation that wants to match the source level defaults must use the initializer column.

### 9.1 SimulationConfig fields

| Config field | Type | C# initializer (SimulationConfig.cs) | Shipping asset value (SimulationConfig.asset) | Meaning |
|--------------|------|--------------------------------------|-----------------------------------------------|---------|
| `BiologyStep` | int | `1` (cs:29) | `1` (asset:15) | Days between biology runs (Section 2.1). `[Range(1, 5)]` on the slider only. |
| `DaysPerScenario` | int | `365` (cs:37) | `365` (asset:16) | Days per scenario; copied to `SimulationRunner.TotalDays`. `[Range(1, 182500)]`. |
| `NumberOfScenarios` | int | `5` (cs:44) | `5` (asset:17) | Scenarios per run, each with its own seed. `[Range(1, 100)]`. |
| `CarryingCapacityTier1` | float | `5000f` (cs:59) | `5000` (asset:18) | Tier 1 shared food pool capacity; copied to `EcosystemSimulator.CarryingCapacityPerTier`. `[Range(100, 100000)]`. |
| `ConditionDrainRate` | float | `0.15f` (cs:68) | `0.15` (asset:19) | Global condition drain rate (Step 4). `[Range(0.01, 1.0)]`. |
| `ConditionRecoveryRate` | float | `0.10f` (cs:74) | `0.1` (asset:20) | Global condition recovery rate (Step 4). `[Range(0.01, 1.0)]`. |
| `BaseTemperature` | float | `20f` (cs:80) | `20` (asset:21) | Temperature base (Section 8). |
| `SeasonalAmplitude` | float | `5f` (cs:84) | `5` (asset:22) | Seasonal amplitude (Section 8). |
| `ClimateTrend` | float | `1f` (cs:88) | `0` (asset:23) | Warming per year (Section 8). Asset disables the trend. |
| `InterannualVariation` | bool | `true` (cs:91) | `0` / false (asset:24) | Enable per year variation (Section 8). Asset disables it. |
| `VariabilityMagnitude` | float | `2f` (cs:95) | `0` (asset:25) | Interannual range (Section 8). Asset sets it to 0. |
| `WarmingBias` | float | `1.5f` (cs:98) | `0` (asset:26) | Warm year shape skew (Section 8). |
| `Autocorrelated` | bool | `true` (cs:102) | `1` / true (asset:27) | Smooth daily noise (Section 8). |
| `DailyVariationRange` | float | `5f` (cs:105) | `5` (asset:28) | Daily noise half range; copied to `BaseRandomness`. |
| `RandomnessGrowthRate` | float | `0.5f` (cs:108) | `0` (asset:29) | Daily noise growth per year (Section 8). |
| `TemperatureBoundsMin` | float | `-5f` (cs:112) | `0` (asset:30) | Clamp floor; copied to `MinTemp`. |
| `TemperatureBoundsMax` | float | `50f` (cs:115) | `40` (asset:31) | Clamp ceiling; copied to `MaxTemp`. |
| `RunSpecies` | `RunSpeciesList` | null (cs:123) | asset reference (asset:32) | The runtime species list. When usable, this replaces the default species (Section 6). |
| `Tier2Enabled` | bool | `false` (cs:130) | `0` / false (asset:33) | Tier 2 gate (Section 1). Both the C# default and the shipped asset are off, so the shipped run is Tier-1-only. |
| `RandomSeed` | int | `12345` (cs:139) | `12345` (asset:34) | Base seed. Per scenario seed is `RandomSeed + i` when non negative, sentinel `-1` per scenario when negative (Section 2.2). |

The asset values above are the literal serialized contents of `Assets/Resources/SimulationConfig.asset` as inspected; they are editable in the Unity inspector and represent one saved configuration, not a hard default. A reimplementer reproducing a specific CSV should read the `#config:` comment lines that every scenario CSV embeds (SimulationRunner.cs:749-767), which record the exact values that produced that file.

### 9.2 Engine and runner defaults not exposed on the config

A few parameters live on the engine or runner with their own defaults and are not separate config fields:

| Field | Owner | Default | Citation | Meaning |
|-------|-------|---------|----------|---------|
| `TotalDays` | `SimulationRunner` | `365` | SimulationRunner.cs:354 | Set from `DaysPerScenario` before each scenario; the standalone default matches. |
| `BiologyStep` | `SimulationRunner` | `1` | SimulationRunner.cs:355 | Set from config before each scenario. |
| `CarryingCapacityPerTier` | `EcosystemSimulator` | `5000f` | EcosystemSimulator.cs:237 | Set from `CarryingCapacityTier1`. Drives food density in Step 2 (`foodDensity = max(0, 1 - tier1Pop / capSafe)`), the `capSafe = max(cap, 1)` floor, and the Step 10 overflow cap `100 * CarryingCapacityPerTier` (with the default that ceiling is 500000). |
| `ConditionDrainRate` | `EcosystemSimulator` | `0.15f` | EcosystemSimulator.cs:240 | Global drain rate; set from config. |
| `ConditionRecoveryRate` | `EcosystemSimulator` | `0.10f` | EcosystemSimulator.cs:241 | Global recovery rate; set from config. |
| `Tier2Enabled` | `EcosystemSimulator` | `false` | EcosystemSimulator.cs:245 | Tier 2 gate; set from config. |
| `MAX_POP_MULTIPLE_OF_K` | `EcosystemSimulator` | `100f` | EcosystemSimulator.cs:667 | Multiplier for the Step 10 population overflow cap. |

`RandomSeed` validity. `SimulationConfig.IsValid` checks `DaysPerScenario >= 1`, `NumberOfScenarios >= 1`, a non empty `RunSpecies`, and `CarryingCapacityTier1 > 0`; it warns but does not fail when the initial Tier 1 population exceeds the cap (SimulationConfig.cs:146-195). It does not validate `BiologyStep`, the temperature parameters, or `RandomSeed`, so out of slider range values supplied programmatically (for example from a bulk CSV row) are accepted as is (Section 2.1 covers the `BiologyStep = 0` divide by zero).
