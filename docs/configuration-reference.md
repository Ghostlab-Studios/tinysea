# Configuration and ScriptableObject Reference

This document is the parameter reference for the TinySea headless ecosystem simulation. It replaces the old configuration spreadsheet. Every serialized field on the four configuration ScriptableObjects and data classes is listed with its C# type, default value, valid range or validation rule, units, and meaning. It also documents how the ScriptableObjects relate to one another and how the runtime species list is assembled.

Scope note. The shipping simulation runs Tier 1 (prey) only. Tier 2 (predator) fields and behavior remain in the code from the original two tier design. Tier 2 fields are documented here and marked as legacy. The default `SimulationConfig` field `Tier2Enabled` is `false` (`SimulationConfig.cs:130`), and when off, Tier 2 species are dropped before the run (`EcosystemSimulator.cs:336`). The asset shipped under `Assets/Resources/SimulationConfig.asset` sets `Tier2Enabled: 1`, but every species in the shipped species lists is Tier 1, so no Tier 2 species exist to run regardless.

Related documents (do not duplicate, cross reference):

- `simulation-spec.md` for the authoritative simulation specification.
- `biology-and-formulas.md` for the day step biology formulas that consume these parameters.
- `temperature-model.md` for the temperature model that consumes the temperature fields.
- `run-scenario-batch.md` for the day loop, scenario, and run sequencing.
- `bulk-system.md` for the bulk CSV batch path that builds configs from CSV rows.
- `csv-output-formats.md` for how parameters are re-serialized into CSV.
- `data-structures.md` for `SimSpecies` and the per-day record structures.
- `ui-and-io.md` for the editor and runtime UI that writes these fields.

## 1. The four configuration types and how they relate

The simulation reads configuration from four serializable types. Three are `ScriptableObject` assets created from the Unity `TinySea/` create menu. One is a plain static class with no serialized state.

| Type | Kind | Create menu | Asset path | Purpose |
|---|---|---|---|---|
| `SimulationConfig` | ScriptableObject | `TinySea/Simulation Config` (`SimulationConfig.cs:23`) | `Assets/Resources/SimulationConfig.asset` | All global run parameters: timing, carrying capacity, condition rates, temperature model, seed, and a reference to the runtime species list. |
| `RunSpeciesList` | ScriptableObject | `TinySea/RunSpeciesList` (`RunSpeciesList.cs:4`) | `Assets/Resources/RunSpeciesList.asset` | The runtime species list. This is the list the simulator actually loads. Holds a `List<SpeciesData>` and an optional back reference to a `SpeciesDatabase`. |
| `SpeciesDatabase` | ScriptableObject | `TinySea/Species Database` (`SpeciesDatabase.cs:280`) | `Assets/Resources/SpeciesDatabase.asset` | A catalog of `SpeciesData` templates with editor helpers to populate and reset canonical defaults. Not read at simulation runtime. Serves as the source for building a `RunSpeciesList`. |
| `SpeciesEditEvents` | static class (not serialized) | none | none | A static observer event hub used by the species editor UI. No fields to configure. |

`SpeciesData` (`SpeciesDatabase.cs:32-278`) is the `[System.Serializable]` record that holds one species. It is the element type of both `RunSpeciesList.speciesList` and `SpeciesDatabase.speciesList`. It is not a ScriptableObject. All its fields are documented in section 4.

Relationship diagram.

```
SpeciesDatabase.asset                RunSpeciesList.asset                 SimulationConfig.asset
  speciesList: List<SpeciesData>  ->   speciesList: List<SpeciesData>  <-   RunSpecies: RunSpeciesList ref
  (catalog / template source)          SpeciesDatabase: optional ref        (+ global run params)
        (editor copies entries               |
         into the run list)                  | read at runtime
                                             v
                            EcosystemSimulator.InitializeFromRunSpeciesList
                                  builds List<SimSpecies> (see section 5)
```

`SimulationConfig.RunSpecies` is a serialized reference to a `RunSpeciesList` asset (`SimulationConfig.cs:123`). The data flow has a runner hop: `SimulationController.RunSingleScenario` assigns `runner.RunSpecies = config.RunSpecies` (`SimulationController.cs:366`); it does not call the simulator directly. Inside `SimulationRunner.Run`, a null/empty guard checks the list (`SimulationRunner.cs:403`) and then calls `Ecosystem.InitializeFromRunSpeciesList(RunSpecies)` (`SimulationRunner.cs:405`); if the list is null or empty the runner falls back to `InitializeDefaultSpecies` instead (`SimulationRunner.cs:409-410`). The called method is defined at `EcosystemSimulator.cs:317`. `SpeciesDatabase` is never read during a run. The `RunSpeciesList.SpeciesDatabase` back reference (`RunSpeciesList.cs:8`) is an editor convenience link only and is empty (`fileID: 0`) in the shipped asset (`RunSpeciesList.asset:15`).

## 2. SimulationConfig fields

`SimulationConfig` (`SimulationConfig.cs:24-196`). All fields are `public` and serialized by Unity. The "Asset value" column records the value actually stored in `Assets/Resources/SimulationConfig.asset` (lines cited from that file). Where the asset value differs from the C# field initializer, both are shown.

### 2.1 Timing

| Field | Type | C# default | Asset value | Range attribute | Units | Meaning |
|---|---|---|---|---|---|---|
| `BiologyStep` | `int` | `1` (`:29`) | `1` (asset:15) | `[Range(1, 5)]` (`:28`) | days | Days between biology calculations. 1 = daily and most accurate. 5 reproduces the original game step. Consumed as `runner.BiologyStep` (`SimulationController.cs:350`). |
| `DaysPerScenario` | `int` | `365` (`:37`) | `365` (asset:16) | `[Range(1, 182500)]` (`:36`), about 500 years | days | Number of days each scenario runs. Also validated `>= 1` in `IsValid` (`:148-152`). Consumed as `runner.TotalDays` (`SimulationController.cs:349`). |
| `NumberOfScenarios` | `int` | `5` (`:44`) | `5` (asset:17) | `[Range(1, 100)]` (`:43`) | count | How many times the same configuration runs, each with a different seed. Validated `>= 1` in `IsValid` (`:154-158`). Drives the scenario loop (`SimulationController.cs:209`). |

### 2.2 Carrying capacity (Tier 1 shared resource pool)

| Field | Type | C# default | Asset value | Range attribute | Units | Meaning |
|---|---|---|---|---|---|---|
| `CarryingCapacityTier1` | `float` | `5000f` (`:59`) | `5000` (asset:18) | `[Range(100, 100000)]` (`:58`) | individuals (Tier 1 resource pool size) | Size of the shared environmental food/resource pool that all Tier 1 species draw from. Feeds the Tier 1 FedRate calculation `food_density = max(0, 1 - tier1Consumption / capacity)` (defined precisely below). Always on as of v11.1; the former `UseCarryingCapacity` toggle was removed (`:19-21`). `IsValid` hard requires `> 0` (`:166-171`). Consumed as `runner.Ecosystem.CarryingCapacityPerTier` (`SimulationController.cs:369`). |

Scope of the capacity: Tier 1 only. The field name says `Tier1`, but the property it is assigned to is named `CarryingCapacityPerTier` (`EcosystemSimulator.cs:237`, set from `SimulationController.cs:369`). The `PerTier` part of the property name is a legacy misnomer left from the original two tier design. In the current code the capacity drives the Tier 1 supply cap and the search ratio (`EcosystemSimulator.cs:766-782`), and it is never applied to Tier 2: the Tier 2 predator path uses a Holling Type II response driven by the prey:predator ratio, not by this capacity (`EcosystemSimulator.cs:805-865`). So this is a single Tier 1 resource pool, not a shared per-tier limit. The `food_density` formula uses only Tier 1 consumption, consistent with that reading and inconsistent with the `PerTier` name. It also doubles as the overflow guard in Step 10, where population is hard-capped at `100 * CarryingCapacityPerTier` (`EcosystemSimulator.cs:667-668`).

Definition of `tier1Pop`, `tier1Consumption`, and where they are sampled. `tier1Pop` is the sum of the `Population` field of every Tier 1 species (`Tier == 1`), taken from `GetTierPopulation(1)` which is `Species.Where(s => s.Tier == 1).Sum(s => s.Population)` (`EcosystemSimulator.cs:1384`). `tier1Consumption` is the same sum weighted by each individual's appetite, `Population * max(1, eatingAmount)`, over living Tier 1 species (`EcosystemSimulator.cs:774-776`). The summed `Population` values are the fractional (float) populations, not the rounded integers (rounding to integers happens later, in Step 10, `EcosystemSimulator.cs:671-686`). The snapshot is taken once at the top of Step 2 (Feeding), before any feeding, death, or reproduction has run that day, and `tier1Pop` is clamped to be non-negative before use (`EcosystemSimulator.cs:765`). The full day-step formula is:

```
tier1Pop         = sum over Tier 1 species of Population              // fractional, start of Step 2
capSafe          = max(CarryingCapacityPerTier, 1)                    // floor of 1 guards against misconfig
tier1Consumption = sum over live Tier 1 of Population * max(1, eatingAmount)   // appetite floored at 1
food_density     = max(0, 1 - tier1Consumption / capSafe)            // 1 = empty pool, 0 = pool at/over capacity
resourceRatio    = capSafe / max(tier1Pop, 1)                        // Holling search ratio
```

The order of operations within the day is defined by the 10-step biology sequence in `biology-and-formulas.md` (Step 2 is Feeding). This document only fixes which population snapshot feeds the density and ratio; see that document for how `food_density` and `resourceRatio` then drive Tier 1 `FedRate` and the rest of the step.

`IsValid` also logs a non fatal `Debug.LogWarning` when the summed initial Tier 1 population exceeds `CarryingCapacityTier1` (`:177-191`). The over cap start is still allowed because the condition system produces a graceful decline; the warning only flags a likely misconfiguration. The over cap check uses `sp.tier == 0` to identify Tier 1 species (0 based tier, `:181`).

### 2.3 Condition (health) system rates

These two values are the global condition drain and recovery rates. A species may override them per species through `SpeciesData.conditionDrainRate` / `conditionRecoveryRate` (section 4). The inheritance test is a sign test on the species value, not an exact `-1` match. `UpdateCondition` uses `drainRate = sp.ConditionDrainRate >= 0f ? sp.ConditionDrainRate : ConditionDrainRate` and the same form for recovery (`EcosystemSimulator.cs:963-964`). So if the species value is `>= 0f` it is used as-is, including a literal `0f`; only a value `< 0f` (the documented sentinel is `-1`) inherits the global rate here. The `SimSpecies` fields default to `-1f` so an unset species inherits (`SimSpecies.cs:36-37`).

The Condition-update equation (Step 4). `UpdateCondition(sp)` runs once per biology step for every species with `Population >= MIN_ALIVE_POP` (1.0); species below the alive floor are skipped and keep their stored Condition (`EcosystemSimulator.cs:954`). Condition moves a fraction of the way toward a target each step. The target is `RawFinalPerformance = RawThermalPerformance * FedRate` from Step 3, in `[0, 1]` (`EcosystemSimulator.cs:608, 956`); call it `target`. The move is asymmetric (separate drain and recovery rates) and accelerates quadratically as `target` approaches its extreme, and the rates are scaled by `Pmax`. There is no `BiologyStep` factor: Condition tracks its target by the same fraction regardless of step length (`EcosystemSimulator.cs:966-987`). The exact update is:

```
# Inputs:
#   C       = sp.Condition (current, [0,1])
#   target  = sp.RawFinalPerformance ([0,1])     # Step 3 output
#   drain   = per-species or global drain rate    (sign-test inherit, above)
#   recov   = per-species or global recovery rate (sign-test inherit, above)
#   pmaxSafe = max(sp.Pmax, 1e-4)                  # divide-by-zero guard, EcosystemSimulator.cs:960

if C > target:                                    # draining toward a worse target
    severity        = (1 - target) * (1 - target)               # 0 at target=1, 1 at target=0
    effectiveDrain  = drain * (1 + severity) / pmaxSafe          # up to 2x near lethal; /Pmax
    C_new           = C - (C - target) * effectiveDrain          # EcosystemSimulator.cs:972-975
else:                                             # recovering toward a better target
    boost           = target * target                            # 0 at target=0, 1 at target=1
    effectiveRecov  = recov * (1 + boost) * pmaxSafe             # up to 2x near optimal; *Pmax
    C_new           = C + (target - C) * effectiveRecov          # EcosystemSimulator.cs:983-986

sp.Condition = clamp(C_new, 0, 1)                  # EcosystemSimulator.cs:989
```

Read the form as `Condition += rate * (target - Condition)` with a sign-dependent, `Pmax`-scaled, quadratically-accelerated `rate`. Concrete consequences a reconstruction must reproduce: the per-step multiplier `(1 + severity)` and `(1 + boost)` range over `[1, 2]`, so drain and recovery each at most double near the lethal and optimal ends respectively; `Pmax` divides the drain and multiplies the recovery, so a high-`Pmax` specialist both drains slower and recovers faster than a low-`Pmax` generalist at the same target; `Pmax` is applied to the rates only, never to `target`, so the `[0, 1]` Condition scale and the `DeathThreshold`/`ReproThreshold` comparisons stay species-agnostic (`EcosystemSimulator.cs:956, 974, 985`). Condition is the lagged state that drives condition-death (Step 7, section 4.2/4.8) and the reproduction ramp (Step 8, section 4.2). The full per-step context is in `biology-and-formulas.md`.

| Field | Type | C# default | Asset value | Range attribute | Units | Meaning |
|---|---|---|---|---|---|---|
| `ConditionDrainRate` | `float` | `0.15f` (`:68`) | `0.15` (asset:19) | `[Range(0.01f, 1.0f)]` (`:67`) | per biology step (fraction of gap closed) | Speed at which Condition falls toward poor performance. The tooltip states 0.15 gives roughly 8 days from full health to the death threshold at suboptimal temperatures, with drain up to 2x faster near lethal limits (`:64-66`). Consumed as `runner.Ecosystem.ConditionDrainRate` (`SimulationController.cs:372`). |
| `ConditionRecoveryRate` | `float` | `0.10f` (`:74`) | `0.1` (asset:20) | `[Range(0.01f, 1.0f)]` (`:73`) | per biology step (fraction of gap closed) | Speed at which Condition rises toward good performance. Asymmetric and slower than drain. The tooltip states 0.10 gives roughly 10 good days to fully recover (`:70-72`). Consumed as `runner.Ecosystem.ConditionRecoveryRate` (`SimulationController.cs:373`). |

### 2.4 Temperature model parameters

These eleven fields configure the standalone `TemperatureCalculator`. The full model is in `temperature-model.md`; the table below gives each field, default, asset value, units, and which `TemperatureCalculator` property it sets in `SimulationController.RunSingleScenario` (`SimulationController.cs:353-363`). None of these fields carry a Unity `[Range]` attribute, so the inspector allows any value.

| Field | Type | C# default | Asset value | Units | Maps to (TempCalc property) | Meaning |
|---|---|---|---|---|---|---|
| `BaseTemperature` | `float` | `20f` (`:80`) | `20` (asset:21) | degrees C | `BaseTemperature` (`:353`) | Base/mean temperature. |
| `SeasonalAmplitude` | `float` | `5f` (`:84`) | `5` (asset:22) | degrees C | `SeasonalAmplitude` (`:354`) | Amplitude of the seasonal summer/winter swing. |
| `ClimateTrend` | `float` | `1f` (`:88`) | `0` (asset:23) | degrees C per year | `ClimateTrendPerYear` (`:355`) | Warming per year from climate change. The shipped asset disables the trend by setting it to 0. |
| `InterannualVariation` | `bool` | `true` (`:91`) | `0` (false) (asset:24) | flag | `UseInterannualVariation` (`:361`) | Gate for the year to year variation block. When true, the model applies `VariabilityMagnitude` and `WarmingBias`; when false, those two floats have no effect and every year shares the same seasonal baseline. The shipped asset disables it. |
| `VariabilityMagnitude` | `float` | `2f` (`:95`) | `0` (asset:25) | degrees C | `VariabilityMagnitude` (`:356`) | Magnitude of year to year temperature variation. |
| `WarmingBias` | `float` | `1.5f` (`:98`) | `0` (asset:26) | degrees C (positive skew) | `WarmingBias` (`:357`) | Bias toward warmer years. |
| `Autocorrelated` | `bool` | `true` (`:102`) | `1` (true) (asset:27) | flag | `UseAutocorrelation` (`:360`) | Selects how the daily noise (scaled by `DailyVariationRange`/`RandomnessGrowthRate`) is generated. When true, daily variation is smoothed (each day's offset is correlated with the previous day). When false, daily variation is drawn independently (i.i.d.) each day. It does not disable daily noise, only changes its correlation structure. |
| `DailyVariationRange` | `float` | `5f` (`:105`) | `5` (asset:28) | degrees C | `BaseRandomness` (`:358`) | Base daily random variation range. |
| `RandomnessGrowthRate` | `float` | `0.5f` (`:108`) | `0` (asset:29) | degrees C per year | `RandomnessGrowthRate` (`:359`) | How much daily randomness grows per year. |
| `TemperatureBoundsMin` | `float` | `-5f` (`:112`) | `0` (asset:30) | degrees C | `MinTemp` (`:362`) | Minimum possible temperature (clamp floor). |
| `TemperatureBoundsMax` | `float` | `50f` (`:115`) | `40` (asset:31) | degrees C | `MaxTemp` (`:363`) | Maximum possible temperature (clamp ceiling). |

The shipped asset is configured for a constant seasonal climate with no warming trend, no interannual variation, autocorrelated daily noise of +/- 5 C, and a clamp to [0, 40] C. The C# initializers describe a warmer scenario with a 1 C/year trend, which is what a freshly created asset would start with before editing.

Two of these fields are boolean gates over the others. `InterannualVariation` gates the `VariabilityMagnitude` and `WarmingBias` floats: when it is false those two values are inert. `Autocorrelated` does not gate any float; it selects the correlation structure of the daily noise produced from `DailyVariationRange` and `RandomnessGrowthRate` (smoothed day to day when true, independent draws when false).

The temperature draw equations. `TemperatureCalculator.GetTemperature(day)` returns the ambient temperature in Celsius for a 0-based `day` index. It sums five components and clamps to the bounds (`TemperatureCalculator.cs:77-85`). `DAYS_PER_YEAR = 365` (`:30`); `year = day / 365` integer division; the RNG is `System.Random` seeded per scenario (section 2.5). The full reconstructable model (`TemperatureCalculator.cs:61-196`):

```
# Constant: DAYS_PER_YEAR = 365
# Per-day index: day (0-based). year = day / DAYS_PER_YEAR (integer division).
# rng.NextDouble() in [0,1).

# 1. Seasonal sinusoid (zero-mean over a year), TemperatureCalculator.cs:126-129:
seasonal = sin(2*pi * day / DAYS_PER_YEAR) * SeasonalAmplitude
#   (coldest near day 0, warmest near day 182)

# 2. Climate trend (the only non-zero-mean component), TemperatureCalculator.cs:134-138:
years = day / DAYS_PER_YEAR        # float division here
trend = ClimateTrendPerYear * years

# 3. Interannual variation, one value per calendar year, cached, TemperatureCalculator.cs:148-170:
if not UseInterannualVariation:
    interannual = 0
else:
    if year not yet drawn:
        coldPart = rng.NextDouble() * (-VariabilityMagnitude)          # uniform (-mag, 0]
        warmPart = rng.NextDouble() * (VariabilityMagnitude * WarmingBias)   # uniform [0, mag*bias)
        biasMean = VariabilityMagnitude * (WarmingBias - 1) / 4         # removes hidden warm drift
        yearVariation[year] = (coldPart + warmPart) / 2 - biasMean
    interannual = yearVariation[year]                                  # same value all year

# 4. Daily variation, optionally autocorrelated, TemperatureCalculator.cs:175-196:
currentRandomness = BaseRandomness + RandomnessGrowthRate * year
newRandom = (rng.NextDouble() * 2 - 1) * currentRandomness            # uniform [-cR, +cR)
if UseAutocorrelation:
    dailyVar = previousDayVariation * 0.7 + newRandom * 0.3           # 70% yesterday, 30% new
else:
    dailyVar = newRandom
previousDayVariation = dailyVar    # state carried to next day

# 5. Sum and clamp, TemperatureCalculator.cs:77-84:
T = BaseTemperature + seasonal + trend + interannual + dailyVar
return clamp(T, MinTemp, MaxTemp)
```

Field-to-property and name notes a reconstruction must respect. The config field `DailyVariationRange` maps to the calculator property `BaseRandomness` (section 2.4 table), so the daily-noise base half-width is `DailyVariationRange + RandomnessGrowthRate * year`. The autocorrelation coefficients 0.7 and 0.3 are hardcoded, not configurable (`TemperatureCalculator.cs:187`). The interannual block draws exactly once per calendar year and reuses that value for all 365 days of the year; the `biasMean` subtraction makes `WarmingBias` skew only the shape of the distribution, leaving `ClimateTrendPerYear` as the sole owner of long-term mean drift (`:156-166`). `previousDayVariation` starts at 0 and is reset to 0 by `Reset(seed)` at the start of each scenario (`:51-56`), so the daily series is reproducible from the seed. There is also an optional override: if a daily-temperature timeseries has been loaded via `LoadTimeseries`, `GetTemperature` returns the clamped series value for the day (looping when the series is shorter than the run) and the five-component model is bypassed (`:66-75`); the shipped standard run loads no timeseries, so the parametric model above is what executes. The exact draw equations above match `temperature-model.md`.

### 2.5 Species reference, Tier 2 gate, and seed

| Field | Type | C# default | Asset value | Units | Meaning |
|---|---|---|---|---|---|
| `RunSpecies` | `RunSpeciesList` (object ref) | `null` (`:123`) | reference to `RunSpeciesList.asset` (guid `cd179f55...`, asset:32) | reference | The runtime species list. `IsValid` rejects a null or empty list (`:160-164`). Forwarded to the runner and used by `InitializeFromRunSpeciesList` (`SimulationController.cs:366`). |
| `Tier2Enabled` | `bool` | `false` (`:130`) | `1` (true) (asset:33) | flag | Master gate for the legacy Tier 2 (predator) path. When false, Tier 2 species are excluded at load (`EcosystemSimulator.cs:336`), Tier 2 CSV columns are suppressed, and bulk CSV rows with tier != 0 are rejected at parse. The shipped asset sets it true, but no Tier 2 species exist in the shipped lists so the run is Tier 1 only in practice. Consumed as `runner.Ecosystem.Tier2Enabled` (`SimulationController.cs:374`). |
| `RandomSeed` | `int` | `12345` (`:139`) | `12345` (asset:34) | seed | Base seed for reproducibility. The per-scenario seed is computed as `scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i`, where `i` is the 0 based scenario index (`SimulationController.cs:209`). The test is `< 0`, not `== -1`. When `RandomSeed` is negative (the documented sentinel is `-1`), **every** scenario receives `-1`, and each scenario's runner seeds its `System.Random` from system time (`new System.Random()`), so all scenarios are independently non reproducible. When `RandomSeed` is `>= 0`, scenario `i` uses `seed_i = RandomSeed + i` for `i` in `0 .. NumberOfScenarios-1`, giving reproducible per-scenario streams. No overflow guard exists, so a `RandomSeed` within `NumberOfScenarios` of `int.MaxValue` would wrap on the `+ i` addition; keep the base well below `int.MaxValue`. Note the displayed scenario number is `i + 1` while the seed uses the 0 based `i`. |

### 2.6 IsValid validation summary

`SimulationConfig.IsValid(out string errorMessage)` (`:146-195`) returns `false` and sets `errorMessage` on the first failed check, otherwise returns `true` with a null message.

1. `DaysPerScenario < 1` fails with "Days per scenario must be at least 1" (`:148-152`).
2. `NumberOfScenarios < 1` fails with "Number of scenarios must be at least 1" (`:154-158`).
3. `RunSpecies == null` or `RunSpecies.speciesList == null` or `speciesList.Count == 0` fails with "No species configured. Please add species to RunSpeciesList." (`:160-164`).
4. `CarryingCapacityTier1 <= 0f` fails with "CarryingCapacityTier1 must be > 0 (carrying capacity is always on)." (`:166-171`). At run time this field is copied into the property `CarryingCapacityPerTier`; the `PerTier` name is a legacy misnomer and the value is a Tier 1 only pool (see section 2.2).
5. Non fatal: if summed Tier 1 initial population (`sp.tier == 0`) exceeds `CarryingCapacityTier1`, a `Debug.LogWarning` is emitted but validation still passes (`:177-191`).

### 2.7 Runtime overrides of SimulationConfig

`SimulationInputUI` mutates the config asset in place before a run, so the asset values above are starting points, not locked. There are two write paths, and both write the same field set.

1. Reset path. `SimulationConfigDefaults.ApplyTo(config)` (`SimulationInputUI.cs:120-140`) restores the stored defaults. It writes all eleven temperature fields (`BaseTemperature`, `SeasonalAmplitude`, `ClimateTrend`, `InterannualVariation`, `VariabilityMagnitude`, `WarmingBias`, `Autocorrelated`, `DailyVariationRange`, `RandomnessGrowthRate`, `TemperatureBoundsMin`, `TemperatureBoundsMax`), `CarryingCapacityTier1`, both condition rates (`ConditionDrainRate`, `ConditionRecoveryRate`), `DaysPerScenario`, and `NumberOfScenarios` (`SimulationInputUI.cs:124-139`). This path is unconditional.
2. Run path. `OnRunSimulationClicked` (`SimulationInputUI.cs:307-331`) writes back the values read from the UI fields just before starting the run. It writes the same set, but with two conditional cases: the `InterannualVariation` and `Autocorrelated` booleans are written only when their `Toggle` references are non-null (`:311`, `:316`), and the two condition rates are written only when their input field references are non-null (`:327-328`). The remaining fields (temperatures other than the two toggles, `CarryingCapacityTier1`, `DaysPerScenario`, `NumberOfScenarios`) are always written.

The fields `SimulationInputUI` never writes from either path are `RandomSeed`, `Tier2Enabled`, `BiologyStep`, and `RunSpecies`. Those keep their asset values across a UI-launched run.

Timing and blank-field behavior. The run path first reads and validates every assigned field. A field whose Inspector reference is null is skipped (the read helper returns success and leaves the config value untouched, `SimulationInputUI.cs:361-366` and `:387-392`). A field that is assigned but contains text that fails to parse (or an out-of-range integer) sets the validation flag false; the method then logs a warning and returns without writing anything and without starting the run (`:301-305`). So an assigned-but-blank field never clobbers the config with `0`. The writes happen only after all assigned fields parse, and they occur before `SimulationConfig.IsValid` runs (which runs inside `simulationController.StartSimulation()` at `:336`). See `ui-and-io.md`.

## 3. RunSpeciesList fields

`RunSpeciesList` (`RunSpeciesList.cs:4-12`). Two serialized fields.

| Field | Type | C# default | Asset value | Meaning |
|---|---|---|---|---|
| `SpeciesDatabase` | `SpeciesDatabase` (object ref) | `null` | empty `fileID: 0` (asset:15) | Optional back reference to the source catalog. Editor convenience only. Not read by the simulator. |
| `speciesList` | `List<SpeciesData>` | `new List<SpeciesData>()` (`:11`) | 6 entries (asset:16-238) | The actual species the simulator runs. Element type and per field meaning in section 4. |

The shipped `RunSpeciesList.asset` holds six Tier 1 entries (`tier: 0` on every entry). Three are `Hexapod` (specialist, `arrhenBreadth: 5000`) with `variantLabel` Cold/Warm/Hot Specialist, and three are `Gelgi` (generalist, `arrhenBreadth: 7000`) with `variantLabel` Cold/Warm/Hot Generalist. The configured non thermal values are identical across all six. The per entry actual values are summarized in section 6.

`RunSpeciesList` has no `IsValid` of its own. It is validated indirectly by `SimulationConfig.IsValid` (the null/empty checks in section 2.6).

## 4. SpeciesData fields

`SpeciesData` (`SpeciesDatabase.cs:32-278`) is the per species record stored in both list assets. All fields are public and serialized. Defaults below are the C# field initializers; fields with no initializer default to the C# zero value for their type (`0`, `0f`, `null`, `false`). The shipped asset assigns concrete values to most of these (section 6).

### 4.1 Identity

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `index` | `int` | `0` | none | index | Stable index of this species within its list. Used by the editor UI and `SpeciesEditEvents` payloads. |
| `speciesName` | `SpeciesName` enum | `Hexapod` (enum value 0) | enum, see 4.7 | n/a | Legacy enum species identity. Used for UI presets and as a fallback name. Not written to output when `speciesLabel` is set. |
| `variant` | `SpeciesVariant` enum | `ColdSpecialist` (enum value 0) | enum, see 4.7 | n/a | Legacy variant enum. Used for default parameter lookup and legacy bucket columns. The seven values map to four `ThermalVariant` buckets in `ConvertVariant`. |
| `variantLabel` | `string` | `null` | free text | n/a | Free text display variant. When set it is used for `FullName` and all output. When empty, output falls back to `variant.ToString()`. This is the only variant string written to CSV. |
| `speciesLabel` | `string` | `null` | free text | n/a | Free text species name. When set it is used for `FullName`/output and is the raw name written to CSV. When empty, output falls back to `speciesName.ToString()`. |
| `DisplayName` | `string` (computed, get only) | n/a, not serialized | n/a | n/a | Computed UI label `"{speciesLabel or speciesName} {variantLabel or variant}"` (`:46`). Not stored. |
| `icon` | `Sprite` (object ref) | `null` | none | n/a | UI sprite. Preserved through `DeepCopy`/`CopyFrom` (which carry the reference manually because `JsonUtility` does not round trip Unity object references, `:264, :276`). Not used by the simulation core. |
| `count` | `int` | `0` | none | individuals | Initial population for this species. Maps to `SimSpecies.Population` (`EcosystemSimulator.cs:357`). |

### 4.2 Gameplay stats (biology inputs)

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `tier` | `int` | `0` | 0 = Tier 1 prey, 1 = Tier 2 predator (`:51`) | tier index (0 based) | Trophic tier. Converted to the simulator's 1 based `SimSpecies.Tier` via `data.tier + 1` (`EcosystemSimulator.cs:356`). Tier 2 (`tier == 1`) entries are dropped when `Tier2Enabled` is false. |
| `eatingAmount` | `float` | `0f` | none | resource points consumed per creature per biology step | Per-individual consumption, used by both tiers. Tier 2: prey eaten per predator. Tier 1: resource points each individual draws from the shared pool, floored at 1 in the consumption sum (`EcosystemSimulator.cs:776`), so a higher appetite drains the pool faster and supports fewer individuals; at the floored 0 default it behaves as appetite 1 and runs are unchanged. Maps to `SimSpecies.EatingAmount`. |
| `reproductionMultiplier` | `float` | `0f` | none | birth rate multiplier (dimensionless) | Scales births. Maps to `SimSpecies.ReproductionMultiplier`. |
| `deathThreshold` | `float` | `0.3f` (`:54`) | typically (0,1] | dimensionless Condition | Condition death threshold, on the same [0,1] scale as Condition. The graduated condition-death path fires when `Condition < DeathThreshold` (`EcosystemSimulator.cs:1059`), and severity scales with `(DeathThreshold - Condition) / DeathThreshold`. It is compared against `Condition`, not against final performance. The in-code field comment says "FinalPerf below this triggers thermal death," which is stale: thermal death (Step 6) is a separate instant kill keyed on `RawThermalPerformance == 0` and does not read `DeathThreshold` (`:1008`, `:1059`). Maps to `SimSpecies.DeathThreshold`. |
| `deathRate` | `float` | `0f` | none | fraction per biology step | Fraction dying when thermal/condition death triggers. Maps to `SimSpecies.DeathRate`. |
| `TemperatureDebuff` | `float` | `0.0f` (`:56`) | none | degrees C offset | Additional temperature offset applied to the experienced temperature inside the thermal curve. Maps to `SimSpecies.TemperatureDebuff`. |
| `reproThreshold` | `float` | `0.25f` (`:57`) | typically (0,1) | dimensionless Condition | Condition inflection point on the same [0,1] scale as Condition. At or above it reproduction ramps from a struggling rate up to full; below it reproduction is reduced but non zero. Compared against the species `Condition`, not against final performance (`EcosystemSimulator.cs:1179`). Maps to `SimSpecies.ReproThreshold`. |

How "per biology step" rates interact with `BiologyStep`. `BiologyStep` (section 2.1) is the number of days advanced per biology calculation, in the range 1 to 5. The population-changing rates are applied once per biology step and are multiplied by `BiologyStep` inside the formula, so over one step they account for that many days of change. This holds for reproduction (`births = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep`, `EcosystemSimulator.cs:1204`), condition death (`rawDeaths = Population * severity * DeathRate * BiologyStep`, `:1063`), natural death (`deaths = Population * effectiveRate * BiologyStep`, `:1285`), and Tier 2 predator demand (`rawDemand = Population * EatingAmount * ThermalPerformance * BiologyStep`, `:814`). The two exceptions are the condition drain and recovery rates: `UpdateCondition` applies them once per step with no `BiologyStep` factor (`EcosystemSimulator.cs:966-987`), so Condition moves the same fraction toward its target regardless of step length. Thermal death is an instant whole-population kill and carries no rate (`:1015-1016`). Setting `BiologyStep` above 1 therefore scales the per-step birth and death counts by the step length but does not rescale how fast Condition tracks its target.

Interaction of `deathThreshold` and `reproThreshold`. Both thresholds are compared against the same quantity, `Condition`, on the same [0,1] scale, and both checks run every step in fixed order: condition death is Step 7 (`EcosystemSimulator.cs:1056-1099`), reproduction is Step 8 (`:1150-1264`). They are independent, so the two bands can overlap. With the shipped defaults `deathThreshold = 0.3` and `reproThreshold = 0.25`, a species whose `Condition` sits in `[0.25, 0.3)` is below `deathThreshold` (graduated condition deaths fire in Step 7) and at or above `reproThreshold` (it is in the healthy reproduction ramp in Step 8). So that species both loses individuals to condition death and produces births in the same step. Because `deathThreshold > reproThreshold` here, reproduction does not stop at the point deaths begin; it only falls into the reduced "struggling" band once `Condition` drops below `0.25`, and reaches zero births only at `Condition = 0`.

The `reproScale` ramp (Step 8). `reproScale` is the dimensionless `[0, 1]` factor that maps the species `Condition` to a reproduction multiplier. It is a two-region continuous piecewise function joined at the constant `STRUGGLING_REPRO_RATE = 0.10` (`EcosystemSimulator.cs:282`), so there is no discontinuity at the threshold. Above `reproThreshold` it ramps from 0.10 up to 1.0; below it ramps from 0 up to 0.10. Two edge cases handle a threshold pinned at the ends of the range. The full reconstructable function (`EcosystemSimulator.cs:1168-1191`):

```
# Inputs:
#   Cond   = sp.Condition ([0,1])
#   thresh = sp.ReproThreshold (typically in (0,1))
#   S      = STRUGGLING_REPRO_RATE = 0.10

if   thresh >= 1.0:   reproScale = S * Cond                              # EcosystemSimulator.cs:1169-1172
elif thresh <= 0.0:   reproScale = Cond                                  # EcosystemSimulator.cs:1174-1177
elif Cond >= thresh:                                                     # healthy band
    t          = (Cond - thresh) / (1.0 - thresh)                        # 0 at thresh, 1 at Cond=1
    reproScale = S + (1.0 - S) * t                                       # ramps 0.10 -> 1.0
else:                                                                    # struggling band
    reproScale = S * (Cond / thresh)                                     # ramps 0 -> 0.10

reproScale = clamp(reproScale, 0, 1)                                     # EcosystemSimulator.cs:1191
```

At `Cond == thresh` both middle branches yield `S = 0.10` (continuity); at `Cond == 0` the struggling branch yields 0 (only a truly dead group does not reproduce). `reproScale` then enters the birth skeleton already cited in this section:

```
births = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep   # EcosystemSimulator.cs:1204
```

Births below 2 population do not occur at all: `ApplyReproduction` returns early when `Population < MIN_POPULATION_FOR_REPRODUCTION = 2.0` (`EcosystemSimulator.cs:274, 1152`). Fractional `births` accumulate in a per-species birth accumulator; only the whole-number floor is added to the population each step and the remainder carries to the next step (`EcosystemSimulator.cs:1228-1238`).

The `effectiveRate` natural-death draw (Step 9). Natural death is a flat per-step rate with uniform random variance, independent of performance or Condition. For each species with `Population > 0` the rate is drawn and combined as follows (`EcosystemSimulator.cs:1270-1285`):

```
# Inputs:
#   NaturalDeathRate     = sp.NaturalDeathRate     (fraction per step, e.g. 0.02)
#   NaturalDeathVariance = sp.NaturalDeathVariance (± fraction, e.g. 0.01)
#   rng.NextDouble()     in [0,1)                  (System.Random; per-scenario seed)

variance      = (rng.NextDouble() * 2 - 1) * NaturalDeathVariance   # uniform in [-variance, +variance)
baseRate      = max(0, NaturalDeathRate + variance)                 # clamped non-negative, EcosystemSimulator.cs:1279
effectiveRate = baseRate                                            # flat; no performance scaling, EcosystemSimulator.cs:1282
deaths        = Population * effectiveRate * BiologyStep            # EcosystemSimulator.cs:1285
```

So `effectiveRate` is `NaturalDeathRate` perturbed by a symmetric uniform draw of half-width `NaturalDeathVariance`, floored at 0, with no dependence on temperature, food, or Condition (the local name `effectiveRate` is assigned straight from `baseRate` with no further factors). With the shipped values 0.02 and 0.01 the per-step rate is uniform on `[0.01, 0.03)`. Like births, fractional `deaths` accumulate in a per-species natural-death accumulator and only whole deaths are removed each step, capped at the current population (`EcosystemSimulator.cs:1291-1304`).

The condition-death `severity` and `rawDeaths` (Step 7). Condition death fires only when `Condition < DeathThreshold` (`EcosystemSimulator.cs:1059`); at or above the threshold no condition deaths occur. The graduated count is (`EcosystemSimulator.cs:1062-1063`):

```
severity  = (DeathThreshold - Condition) / DeathThreshold   # 0 at threshold, 1 at Condition=0
rawDeaths = Population * severity * DeathRate * BiologyStep
```

After whole deaths are removed (again via a per-species accumulator, `EcosystemSimulator.cs:1067-1071`), the survivors receive a fitness boost that conserves the group's total health pool: `Condition = min(1, oldCondition * oldPop / newPop)` (`EcosystemSimulator.cs:1084-1085`). This pushes Condition back up toward the threshold and damps death spirals. The full step context is in `biology-and-formulas.md`.

### 4.3 Condition timescale (per species)

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `conditionDrainRate` | `float` | `0.15f` (`:63`) | inheritance is a sign test: if the value is `< 0f` the global `SimulationConfig.ConditionDrainRate` is used, otherwise the species value is used including a literal `0f`. The check is `sp.ConditionDrainRate >= 0f ? species : global` (`EcosystemSimulator.cs:963`). The documented sentinel for inherit is `-1`. | per biology step | Per species condition drain rate. The database/UI default is an explicit `0.15` so the inspector never shows a bare `-1` (`:60-62`). The bulk CSV parser keeps its own negative sentinel for blank columns. Maps to `SimSpecies.ConditionDrainRate`. |
| `conditionRecoveryRate` | `float` | `0.10f` (`:64`) | same sign-test inherit rule as above: `< 0f` inherits the global `ConditionRecoveryRate`, `>= 0f` (including `0f`) uses the species value (`EcosystemSimulator.cs:964`) | per biology step | Per species condition recovery rate. Maps to `SimSpecies.ConditionRecoveryRate`. |

### 4.4 Natural mortality

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `naturalDeathRate` | `float` | `0.02f` (`:71`) | none | fraction per biology step | Base background death rate (0.02 = 2%). Maps to `SimSpecies.NaturalDeathRate`. |
| `naturalDeathVariance` | `float` | `0.01f` (`:73`) | none | fraction per biology step (+/- range) | Random variance around the natural death rate (0.01 = +/- 1%). Maps to `SimSpecies.NaturalDeathVariance`. |

### 4.5 Hunting efficiency (shared Holling foraging across both tiers)

These two fields drive a shared Holling Type II foraging response used by every tier through `ComputeForagingSuccess` (`EcosystemSimulator.cs:931-940`). Tier 2 hunts the prey tier; Tier 1 searches the shared resource pool. For Tier 1 the shipped data uses `huntingEfficiency = 1.0` and `huntingVariance = 0`. See `biology-and-formulas.md` for the full per-step use; the Tier 1 chain is summarized here so the formula is in one place.

Tier 1 feeding chain (the only feeding path in the shipping Tier 1 build). For each Tier 1 species with population at or above the alive floor, the per-step feeding satisfaction `FedRate` is the shared Holling foraging success capped by the pool supply:

```
resourceRatio = capSafe / max(tier1Pop, 1)                // search ratio, EcosystemSimulator.cs:782
gatherSuccess = ComputeForagingSuccess(sp, resourceRatio) // Holling II + optional variance, :791
FedRate       = min(1, gatherSuccess * food_density)      // EcosystemSimulator.cs:792
```

where `food_density = max(0, 1 - tier1Consumption / capSafe)` is the shared-pool supply cap defined in section 2.2 (sampled at the start of Step 2). `ComputeForagingSuccess` is `CalculateHollingEfficiency(huntingEfficiency, resourceRatio)`, which returns 1 when `huntingEfficiency >= 1` (`:971`), then a `clamp(eff + (rng*2-1)*huntingVariance, 0, 1)` perturbation only when `huntingVariance > 0` (`:934-938`). `FedRate` is a dimensionless multiplier in [0,1], not a count of consumed individuals: 1 means fully fed, 0 means starving. It feeds Step 3 (`RawFinalPerformance = RawThermalPerformance * FedRate`, `:608`) and through that the Condition update in Step 4. At the shipped Tier 1 values (`huntingEfficiency = 1.0`, `huntingVariance = 0`), `gatherSuccess = 1` and `FedRate` equals `food_density` directly, identical to the old linear model. Lowering `huntingEfficiency` below 1 makes foraging saturate with the resource ratio instead; a positive `huntingVariance` adds a random per-step term. Tier 1 species below the alive floor get `FedRate = 0` (`:798`).

Both fields now affect Tier 1. `huntingEfficiency` drives the Tier 1 Holling foraging curve as above. `eatingAmount` sets each Tier 1 individual's per-step resource consumption from the shared pool, floored at 1: the supply cap is `food_density = max(0, 1 - tier1Consumption / capSafe)` with `tier1Consumption = sum over live Tier 1 of Population * max(1, eatingAmount)` (`:774-777`), so a higher appetite drains the pool faster and supports fewer individuals (a 5000 pool feeds about 1667 at appetite 3). At `eatingAmount = 1` (or the floored 0 default) this equals the former head-count density and runs are unchanged. `eatingAmount` does not enter `resourceRatio`, only `food_density`, mirroring how Tier 2 keeps it out of `preyRatio`. On the Tier 2 path `eatingAmount` is also the predator demand term (`rawDemand = Population * EatingAmount * ThermalPerformance * BiologyStep`, `:814`). `huntingVariance` applies to all tiers (`:934`); `0` makes the foraging draw deterministic and consumes no RNG.

Tier 2 Holling Type II feeding (secondary legacy path; does not execute in the shipped Tier 1 only run). The shipped config has no Tier 2 species (every entry is `tier: 0`), and `Tier2Enabled` has no Tier 2 species to act on, so none of the following runs in a shipped scenario. The code path is fully intact and is documented here for completeness; it is secondary to the Tier 1 description above. The whole block is computed in `ProcessFeedingWithAccumulator` after the Tier 1 FedRate block (`EcosystemSimulator.cs:783-897`), and predators and prey are the live species in each tier (`Population >= MIN_ALIVE_POP = 1.0`, `:730-731`).

Step A, prey:predator ratio. With `availablePrey = sum of live Tier 1 Population` and `totalPredators = sum of live Tier 2 Population` (`:792-793`):

```
preyRatio = totalPredators > 0 ? availablePrey / totalPredators : 0     # EcosystemSimulator.cs:796
```

Step B, per-predator Holling Type II efficiency and demand. For each predator species the base hunting efficiency `HuntingEfficiency` is mapped through a Holling Type II saturating curve whose half-saturation constant is derived from the species' own base efficiency, so the curve always passes through `(NORMAL_PREY_RATIO, HuntingEfficiency)` (`CalculateHollingEfficiency`, `:924-932`). `NORMAL_PREY_RATIO = 20`, `MIN_HUNTING_SUCCESS = 0.0`, `MAX_HUNTING_SUCCESS = 1.0` (`:269-271`):

```
# Inputs per predator:
#   baseEff = pred.HuntingEfficiency
#   preyRatio from Step A
#   HuntingVariance = pred.HuntingVariance
#   rng.NextDouble() in [0,1)

# Holling II efficiency (EcosystemSimulator.cs:924-931):
if preyRatio <= 0 or baseEff <= 0:  hollingEff = 0
elif baseEff >= 1:                  hollingEff = 1
else:
    halfSat    = NORMAL_PREY_RATIO * (1 - baseEff) / baseEff            # half-saturation constant
    hollingEff = preyRatio / (preyRatio + halfSat)                      # saturating in preyRatio

# Apply random variance and clamp (EcosystemSimulator.cs:809-811):
variance      = (rng.NextDouble() * 2 - 1) * HuntingVariance           # uniform [-var, +var)
huntingSuccess = clamp(hollingEff + variance, MIN_HUNTING_SUCCESS, MAX_HUNTING_SUCCESS)

# Demand (EcosystemSimulator.cs:814-818):
rawDemand    = pred.Population * pred.EatingAmount * pred.ThermalPerformance * BiologyStep
actualDemand = rawDemand * huntingSuccess
```

`rawDemand` uses `ThermalPerformance` (the `Pmax`-scaled value from Step 1), not the raw curve. The simulator sums `totalRawDemand` and `totalActualDemand` across all predators (`:815, 819`).

Step C, total eaten and per-predator FedRate (v11). The shared catch is capped by the prey available, then each predator's feeding satisfaction is its own hunting success scaled by a single scarcity factor (`:830-863`):

```
totalEaten     = min(availablePrey, totalActualDemand)                 # EcosystemSimulator.cs:831
scarcityFactor = totalActualDemand > 0 ? min(1, totalEaten / totalActualDemand) : 1   # :849-851
# per predator i:
pred.FedRate   = min(1, huntingSuccess_i * scarcityFactor)            # EcosystemSimulator.cs:857
```

`LastFedRateT2` is the population-weighted average of `pred.FedRate` across predators (`:858-863`). This per-predator form reduces to the older pooled formula when there is exactly one predator species and is unchanged for equal-`HuntingEfficiency` multi-predator runs; it only diverges for mixed-efficiency predator mixes.

Step D, prey partitioning. The realized `totalEaten` is removed from prey species in proportion to each prey species' share of `availablePrey`, with fractional removals carried in a per-prey predation accumulator (`:868-896`):

```
# for each prey species p:
share    = p.Population / availablePrey                                # EcosystemSimulator.cs:872
preyLost = totalEaten * share                                          # :873
# accumulate preyLost, remove whole deaths (floor), cap at p.Population (:879-889)
```

Predator FedRate then feeds the same Steps 3 and 4 as Tier 1 (it becomes the Condition target through `RawFinalPerformance = RawThermalPerformance * FedRate`). When there are no live predators or no live prey, every predator's FedRate is set to 0 and the predation block is skipped (`:783-790`). Because no Tier 2 species ship, the entire block above is dormant in the current simulation.

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `huntingEfficiency` | `float` | `0.75f` (`:78`) | none | fraction (foraging success rate) | Base foraging success at the normal availability ratio; drives the shared Holling II curve for both tiers (`ComputeForagingSuccess`, `EcosystemSimulator.cs:931-940`). Tier 2 hunts prey, Tier 1 searches the resource pool. At >= 1 the curve returns 1 (`:971`); shipped Tier 1 data uses `1.0`. Maps to `SimSpecies.HuntingEfficiency`. |
| `huntingVariance` | `float` | `0.15f` (`:80`) | none | fraction (+/- range) | Random +/- variance on foraging success (0.15 = +/- 15%). Applies to all tiers (`:934`); `0` is deterministic and draws no RNG. Shipped Tier 1 data uses `0`. Maps to `SimSpecies.HuntingVariance`. |

### 4.6 UI only fields (not biology inputs)

These fields drive the inspector and species editor display. They are not read by the simulation core and are not mapped into `SimSpecies` in `InitializeFromRunSpeciesList` (`EcosystemSimulator.cs:351-380`).

| Field | Type | C# default | Units | Meaning |
|---|---|---|---|---|
| `eatingStars` | `int` | `0` | 0 to 5 stars | UI star rating for eating. |
| `reproductionStars` | `int` | `0` | 0 to 5 stars | UI star rating for reproduction. |
| `deathThresholdStars` | `int` | `0` | 0 to 5 stars | UI star rating for death threshold. |
| `deathRateStars` | `int` | `0` | 0 to 5 stars | UI star rating for death rate. |
| `thermalBreadthStars` | `int` | `0` | 0 to 5 stars | UI star rating for thermal breadth. |
| `temperatureThresholdText` | `string` | `null` | n/a | UI label for temperature threshold (for example "High"). |
| `reproductionRateText` | `string` | `null` | n/a | UI label for reproduction rate (for example "Low"). |
| `description` | `string` | `null` | n/a | UI description string. |

### 4.7 Thermal curve parameters (Kelvin)

These feed the Arrhenius thermal performance curve. The formula and the meaning of each Arrhenius term are in `biology-and-formulas.md` (`SimSpecies.CalculatePerformance`, `SimSpecies.cs:95-133`). Inputs marked Kelvin are stored in Kelvin; the ambient temperature comes in as Celsius.

Order of operations for the temperature input. `CalculatePerformance(temperatureCelsius)` first applies the per-species debuff in Celsius, then converts to Kelvin:

```
experiencedC = ambientC + TemperatureDebuff          // SimSpecies.cs:97 (Celsius)
K            = experiencedC + 273.15                  // SimSpecies.cs:116 (Kelvin)
```

So `TemperatureDebuff` is added before the conversion, not after, and the lethal-limit fade (`CTminC`/`CTmaxC`, section 4.8) is also evaluated against `experiencedC` in Celsius (`SimSpecies.cs:99-113`). The simulation thermal path uses `+273.15` throughout (`SimSpecies.cs:116`), which matches the asset `optimalTempK` values (for example 293.15 = 20 C, 295.15 = 22 C). The `+273` convention noted in `CLAUDE.md` applies to the separate interactive-game thermal code (`ThermalCurve`), not to this simulation path; do not use `+273` here.

The Arrhenius core equation. The 0-1 thermal performance is a Sharpe-Schoolfield-style Arrhenius curve with low- and high-temperature deactivation. It is computed in `CalculatePerformance` after the lethal-fade early-out, using the Kelvin temperature `T` (`SimSpecies.cs:124-129`). All six Arrhenius parameters are species fields (section 4.7). The full reconstructable equation is:

```
# Inputs (all per-species), units:
#   T   = experiencedC + 273.15        Kelvin  (debuffed ambient, SimSpecies.cs:97,116)
#   OT  = OptimalTempK                 Kelvin  (optimalTempK)
#   B   = ArrhenBreadth                1/K-basis Arrhenius constant (arrhenBreadth)
#   L   = ArrhenLower                  Arrhenius constant, low-temp deactivation (arrhenLower)
#   U   = ArrhenUpper                  Arrhenius constant, high-temp deactivation (arrhenUpper)
#   LB  = LowerBoundK                  Kelvin reference for L (lowerBoundK)
#   UB  = UpperBoundK                  Kelvin reference for U (upperBoundK)
#   exp = natural exponential, evaluated in double precision

numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))   # SimSpecies.cs:125-126
denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)                          # SimSpecies.cs:127
arrheniusCurve = numerator / denominator                                     # SimSpecies.cs:129
```

The dimensionless result `arrheniusCurve` is then clamped to `[0, 1]` and multiplied by the lethal-fade factor `fade` from section 4.8 to give `RawThermalPerformance` (`SimSpecies.cs:131-132`):

```
RawThermalPerformance = clamp(arrheniusCurve, 0, 1) * fade
```

Properties that fall out of the form: the numerator's constant factor `(1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))` normalizes the curve so that at `T == OT` the ratio equals 1 before the fade and the clamp, which is why peak height is governed externally by `Pmax` (section 4.8) and not by the Arrhenius terms. `B` sets the curve breadth (larger `B` is narrower around `OT`); `L` with `LB` controls the cold-side rolloff and `U` with `UB` the warm-side rolloff. `Pmax` is not present in this equation; it is applied in Step 1 as `ThermalPerformance = RawThermalPerformance * Pmax` (`EcosystemSimulator.cs:594`). This single quantity `RawThermalPerformance` is the input to FedRate's downstream effect (Step 3), the Condition target (Step 4), thermal death (Step 6, fires when it is exactly 0), and reproduction (through Condition). Reproduce it exactly or no downstream numeric behavior matches.

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `optimalTempK` | `float` | `297.0f` (24 C) (`:95`) | none | Kelvin | Optimal temperature (peak of the curve). Maps to `SimSpecies.OptimalTempK`. |
| `arrhenBreadth` | `float` | `8000.0f` (`:96`) | none | Arrhenius constant (1/K basis) | Curve breadth term B. Larger is broader. Maps to `SimSpecies.ArrhenBreadth`. |
| `arrhenLower` | `float` | `3000.0f` (`:97`) | none | Arrhenius constant | Low temperature deactivation term L. Maps to `SimSpecies.ArrhenLower`. |
| `arrhenUpper` | `float` | `35000.0f` (`:98`) | none | Arrhenius constant | High temperature deactivation term U. Maps to `SimSpecies.ArrhenUpper`. |
| `lowerBoundK` | `float` | `296.0f` (23 C) (`:99`) | none | Kelvin | Lower reference bound LB used in the deactivation terms. Maps to `SimSpecies.LowerBoundK`. |
| `upperBoundK` | `float` | `298.0f` (25 C) (`:100`) | none | Kelvin | Upper reference bound UB used in the deactivation terms. Maps to `SimSpecies.UpperBoundK`. |

### 4.8 Thermal curve peak and lethal limits

| Field | Type | C# default | Range/validation | Units | Meaning |
|---|---|---|---|---|---|
| `pmax` | `float` | `0.65f` (`:105`) | `[Range(0f, 1f)]` (`:104`) | dimensionless [0,1] | Maximum performance at the optimal temperature. Scales the curve output. Applied externally to the raw thermal curve (`EcosystemSimulator.cs:594`), not inside `CalculatePerformance`. See the multiplication-order note below for where it sits relative to the lethal fade and the Condition rates. Maps to `SimSpecies.Pmax`. |
| `ctMinC` | `float` | `0.0f` (`:107`) | none | degrees C | Critical thermal minimum. At or below it performance is 0; a cosine fade ramps performance from 0 up to full across the band `[ctMinC, ctMinC + tw]`. Maps to `SimSpecies.CTminC`. |
| `ctMaxC` | `float` | `40.0f` (`:109`) | none | degrees C | Critical thermal maximum. At or above it performance is 0; a cosine fade ramps performance from full down to 0 across the band `[ctMaxC - tw, ctMaxC]`. Maps to `SimSpecies.CTmaxC`. |

The lethal-limit cosine fade. The fade is computed inside `CalculatePerformance` and multiplies the raw Arrhenius curve value (the 0-1 result before `Pmax`), evaluated against the debuffed Celsius temperature `experiencedC` (section 4.7). The transition width is a constant `LETHAL_TRANSITION_WIDTH = 2.0` degrees C (`SimSpecies.cs:57`), clamped down to half the range so it never exceeds `(CTmaxC - CTminC) / 2`:

```
tw = min(2.0, (CTmaxC - CTminC) / 2)                       // SimSpecies.cs:100-101

# lower band
if experiencedC <= CTminC:           fade = 0
elif experiencedC <  CTminC + tw:    fade = 0.5 * (1 + cos(pi * (CTminC + tw - experiencedC) / tw))
else:                                fade = 1

# upper band (multiplies the lower-band result)
if experiencedC >= CTmaxC:           fade = 0
elif experiencedC >  CTmaxC - tw:    fade *= 0.5 * (1 + cos(pi * (experiencedC - (CTmaxC - tw)) / tw))

performance = arrheniusCurve * fade                         # SimSpecies.cs:104-132
```

At `experiencedC = CTminC + tw` the lower expression gives `0.5*(1+cos(0)) = 1`, and at `experiencedC = CTminC` it gives `0.5*(1+cos(pi)) = 0`, so the band is a smooth 0-to-1 ramp over the 2 C (or narrower) window just inside each lethal limit. The `2.0` constant is hardcoded in `SimSpecies`, not configurable. The fade multiplies only the Arrhenius output; `Pmax` is applied afterward (see below).

Multiplication order (raw curve, fade, Pmax, Condition). The order across the day step is:

1. `RawThermalPerformance = arrheniusCurve * fade`, clamped to [0,1] inside `CalculatePerformance` (`SimSpecies.cs:114, 132`). `Pmax` is not applied here.
2. `ThermalPerformance = RawThermalPerformance * Pmax` (`EcosystemSimulator.cs:594`).
3. Condition scaling is separate and does not multiply the performance value: in Step 4, `Pmax` scales the Condition drain and recovery rates (`drain /= Pmax`, `recovery *= Pmax`, `EcosystemSimulator.cs:974, 985`), and the Condition result is clamped to [0,1] (`:989`). `Pmax` is deliberately kept out of the Condition target so the [0,1] Condition scale stays species-agnostic.

The full downstream use (how `RawThermalPerformance` and `Pmax` feed `FedRate`, reproduction, and the death paths) is in `biology-and-formulas.md`.

### 4.9 SpeciesData enums

```
enum SpeciesName  (SpeciesDatabase.cs:7-19)
  Hexapod, Gelgi, Yelloa, Sheplik, Grabbler, Cyplo, Rooda, Sploof, Silu, Custom

enum SpeciesVariant  (SpeciesDatabase.cs:21-30)
  ColdSpecialist, WarmSpecialist, HotSpecialist,
  ColdGeneralist, WarmGeneralist, HotGeneralist, Custom
```

`SpeciesVariant` (7 values) is reduced to the internal `ThermalVariant { Arctic, Common, Tropical, Custom }` (`SimSpecies.cs:3`) by `ConvertVariant` (`EcosystemSimulator.cs:457-478`): `Cold*` to `Arctic`, `Warm*` to `Common`, `Hot*` to `Tropical` (`:470-472`), `Custom` to `Custom` (`:473-474`), and any unmatched value to `Common` via the default case (`:475-476`). The `ThermalVariant` enum names are internal only and are never written to output; output identity uses `variantLabel` exclusively (`SimSpecies.cs:82-89`).

### 4.10 SpeciesData static helpers (label and variant resolution)

These pure static methods drive label normalization and default lookup. They are used by the editor, the CSV importer, and species merging.

| Method | Location | Purpose |
|---|---|---|
| `GetVariantThermalDefaults(variant, out pmax, out ctMinC, out ctMaxC)` | `:115-135` | Returns canonical `pmax`/`ctMinC`/`ctMaxC` per `SpeciesVariant`. Cold/Warm/Hot Specialist give pmax 0.9843/0.972/0.96 and CT ranges [0,35]/[2,37]/[4,39]; the Generalist trio give pmax 0.6616/0.6547/0.6481 with the same CT ranges; Custom/unknown give pmax 0.65, CT [0,40] (`:132-133`). The method's XML comment says it "returns Common defaults" for Custom/unknown (`:113`); that phrasing is a misnomer, since the Common (Warm) values would be pmax 0.972 or 0.6547, not 0.65. The actual fallback is pmax 0.65, CT [0,40] as stated. |
| `NormalizeVariantLabel(raw)` | `:142-147` | Canonical cases a known `SpeciesVariant` name; passes any other free text through unchanged. |
| `DeriveVariantLabel(variant, existing)` | `:156-160` | Keeps an existing label; for `Custom` with no label returns the (empty) existing; otherwise derives a nicified enum name. |
| `NicifyEnumName(s)` | `:167-178` | Inserts a space before each interior capital (`WarmGeneralist` to `Warm Generalist`). |
| `ResolveVariantEnum(label)` | `:187-209` | Maps a label back to a `SpeciesVariant` bucket. Accepts new names and the legacy aliases Cold=Arctic, Warm=Common, Hot=Tropical; unknown labels return `Custom`. |
| `VariantMatchKey(raw)` | `:216-226` | Normalized match key: lowercase, keep only `[a-z0-9]`. Two labels with equal keys are the same variant for merging. |
| `NormalizeSpeciesName(raw)` | `:232-237` | Mirror of `NormalizeVariantLabel` for the species name. |
| `SpeciesNameMatchKey(raw)` | `:243-253` | Mirror of `VariantMatchKey` for the species name. |
| `DeepCopy()` | `:261-266` | JSON round trip clone of every serialized field; copies the `icon` reference manually. |
| `CopyFrom(src)` | `:273-277` | Overwrites every serialized field of this instance from `src` while preserving this object's reference; copies `icon` manually. |

`VariantMatchKey` and `SpeciesNameMatchKey` are the basis of the load time merge described in section 5.

## 5. How the runtime species list is assembled

The simulator does not consume `SpeciesData` directly. `EcosystemSimulator.InitializeFromRunSpeciesList(RunSpeciesList)` (`EcosystemSimulator.cs:317-393`) converts each `SpeciesData` into a `SimSpecies` (`data-structures.md`) with these steps.

1. Clear the existing `Species` list and all accumulators (`:319-320`).
2. If `runSpecies` is null or its list is null or empty, log an error and return (`:322-326`).
3. If `Tier2Enabled` is false and any entry has `tier == 1`, log a warning that Tier 2 species are excluded (`:331-332`).
4. For each `SpeciesData data` in `speciesList`:
   1. If `Tier2Enabled` is false and `data.tier == 1`, skip this entry (`:336`).
   2. Build a merge key from the effective name and variant label. Each component is normalized first, then the two normalized strings are joined with a literal `_` separator: `matchKey = SpeciesData.SpeciesNameMatchKey(mkName) + "_" + SpeciesData.VariantMatchKey(mkLabel)` (`:341-343`). The effective name `mkName` is `speciesLabel` if set else `speciesName.ToString()`; the effective label `mkLabel` is `variantLabel` if set else `variant.ToString()`. Both `SpeciesNameMatchKey` and `VariantMatchKey` lowercase their input and keep only `[a-z0-9]`, dropping every other character including underscores (`SpeciesDatabase.cs:243-253`, `:216-226`). Because neither normalized component can contain `_`, the `_` between them is an unambiguous delimiter: `"Cold_Specialist"` plus `"X"` normalizes to `coldspecialist_x`, which cannot collide with `"Cold"` plus `"SpecialistX"` (which gives `cold_specialistx`).
   3. If the merge key was already seen, add `data.count` to the existing `SimSpecies.Population` and skip creating a new species (`:344-349`). Species whose labels normalize equal merge into the first seen species; unique labels never merge, so legacy single spelling runs stay byte identical.
   4. Otherwise create a new `SimSpecies` (`:351-380`) with the field mapping below, append it to `Species`, initialize its accumulators keyed by `FullName`, and record it in the merge dictionary (`:382-384`).
5. After the loop, set `_tier1WasPopulated`/`_tier2WasPopulated` from the loaded populations (`:391-392`).

`FullName` is the identity string used as the accumulator dictionary key and as the per-species CSV column prefix throughout the simulation. It is a computed property on `SimSpecies`: when `VariantLabel` is empty it is just `Name`, otherwise it is `Name + "_" + VariantLabel` (`SimSpecies.cs:89`). The separator is a literal underscore and the two parts are the mapped `Name` and `VariantLabel` from the table below (not the normalized match-key forms). Note this `_` join is distinct from the merge key in step 4.2: the merge key normalizes each side to `[a-z0-9]` for de-duplication, whereas `FullName` keeps the raw label text for display and keying. The exact CSV-column sanitization applied to `FullName` is in `data-structures.md` and `csv-output-formats.md`.

`SpeciesData` to `SimSpecies` field mapping (`EcosystemSimulator.cs:351-380`):

| SimSpecies field | Source expression |
|---|---|
| `Name` | `data.speciesLabel` if non empty, else `data.speciesName.ToString()` |
| `Variant` | `ConvertVariant(data.variant)` |
| `VariantLabel` | `data.variantLabel` if non empty, else `data.variant.ToString()` |
| `Tier` | `data.tier + 1` (0 based to 1 based) |
| `Population` | `data.count` |
| `EatingAmount` | `data.eatingAmount` |
| `ReproductionMultiplier` | `data.reproductionMultiplier` |
| `DeathThreshold` | `data.deathThreshold` |
| `DeathRate` | `data.deathRate` |
| `ReproThreshold` | `data.reproThreshold` |
| `NaturalDeathRate` | `data.naturalDeathRate` |
| `NaturalDeathVariance` | `data.naturalDeathVariance` |
| `HuntingEfficiency` | `data.huntingEfficiency` |
| `HuntingVariance` | `data.huntingVariance` |
| `OptimalTempK` | `data.optimalTempK` |
| `ArrhenBreadth` | `data.arrhenBreadth` |
| `ArrhenLower` | `data.arrhenLower` |
| `ArrhenUpper` | `data.arrhenUpper` |
| `LowerBoundK` | `data.lowerBoundK` |
| `UpperBoundK` | `data.upperBoundK` |
| `Pmax` | `data.pmax` |
| `CTminC` | `data.ctMinC` |
| `CTmaxC` | `data.ctMaxC` |
| `TemperatureDebuff` | `data.TemperatureDebuff` |
| `ConditionDrainRate` | `data.conditionDrainRate` |
| `ConditionRecoveryRate` | `data.conditionRecoveryRate` |
| `Condition` | `1.0f` (constant; every species starts at full condition) |

The UI only fields (`index`, `icon`, all `*Stars`, the text labels, `description`, `DisplayName`) and the `speciesName`/`variant` enums are not copied into `SimSpecies` beyond their use in the name and merge logic above.

`InitializeFromDatabase(SpeciesDatabase)` (`EcosystemSimulator.cs:399` onward) is a legacy parallel path that reads a `SpeciesDatabase` directly with the same field mapping. New code uses `InitializeFromRunSpeciesList`. The standard run wires `runner.RunSpecies = config.RunSpecies` (`SimulationController.cs:366`), and `SimulationRunner.Run` then calls `InitializeFromRunSpeciesList` (`SimulationRunner.cs:405`), so the database path is not used by the shipping run.

## 6. Shipped asset default values

These are the concrete values stored in the three `.asset` files at the time of writing.

### 6.1 SimulationConfig.asset

Cited from `Assets/Resources/SimulationConfig.asset:15-34`.

```
BiologyStep: 1
DaysPerScenario: 365
NumberOfScenarios: 5
CarryingCapacityTier1: 5000
ConditionDrainRate: 0.15
ConditionRecoveryRate: 0.1
BaseTemperature: 20
SeasonalAmplitude: 5
ClimateTrend: 0
InterannualVariation: 0   (false)
VariabilityMagnitude: 0
WarmingBias: 0
Autocorrelated: 1         (true)
DailyVariationRange: 5
RandomnessGrowthRate: 0
TemperatureBoundsMin: 0
TemperatureBoundsMax: 40
RunSpecies: -> RunSpeciesList.asset (guid cd179f552d2ff62439d8a6b811b28584)
Tier2Enabled: 1           (true)
RandomSeed: 12345
```

Differences from the C# initializers worth noting: `ClimateTrend` 0 vs 1, `InterannualVariation` false vs true, `VariabilityMagnitude` 0 vs 2, `WarmingBias` 0 vs 1.5, `RandomnessGrowthRate` 0 vs 0.5, `TemperatureBoundsMin` 0 vs -5, `TemperatureBoundsMax` 40 vs 50, `Tier2Enabled` true vs false. The configured asset describes a steady seasonal climate with no climate change and no year to year variability.

### 6.2 RunSpeciesList.asset and SpeciesDatabase.asset species

Both assets contain the same six Tier 1 entries (`RunSpeciesList.asset:16-238`, `SpeciesDatabase.asset:15-238`). All six share these non thermal values:

```
tier: 0                       (Tier 1)
count: 20
eatingAmount: 3
reproductionMultiplier: 0.45
deathThreshold: 0.3
deathRate: 0.6
TemperatureDebuff: 0
reproThreshold: 0.25
conditionDrainRate: 0.15
conditionRecoveryRate: 0.1
naturalDeathRate: 0.02
naturalDeathVariance: 0.01
huntingEfficiency: 1
huntingVariance: 0
eatingStars: 0, reproductionStars: 4, deathThresholdStars: 3,
deathRateStars: 2, thermalBreadthStars: 5
temperatureThresholdText: High
reproductionRateText: Low
```

Per species identity and thermal parameters:

| index | speciesLabel | variantLabel | variant (enum) | optimalTempK | arrhenBreadth | arrhenLower | arrhenUpper | lowerBoundK | upperBoundK | pmax | ctMinC | ctMaxC |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Hexapod | Cold Specialist | ColdSpecialist (0) | 293.15 | 5000 | 15998 | 43798 | 292.4 | 293.9 | 0.9843 | 0 | 35 |
| 1 | Hexapod | Warm Specialist | WarmSpecialist (1) | 295.15 | 5000 | 16000 | 43800 | 294.4 | 295.9 | 0.972 | 2 | 37 |
| 2 | Hexapod | Hot Specialist | HotSpecialist (2) | 297.15 | 5000 | 16002 | 43802 | 296.4 | 297.9 | 0.96 | 4 | 39 |
| 3 | Gelgi | Cold Generalist | ColdGeneralist (3) | 293.15 | 7000 | 4998 | 31098 | 292.4 | 293.9 | 0.6616 | 0 | 35 |
| 4 | Gelgi | Warm Generalist | WarmGeneralist (4) | 295.15 | 7000 | 5000 | 31100 | 294.4 | 295.9 | 0.6547 | 2 | 37 |
| 5 | Gelgi | Hot Generalist | HotGeneralist (5) | 297.15 | 7000 | 5002 | 31102 | 296.4 | 297.9 | 0.6481 | 4 | 39 |

Source lines: index 0 (`RunSpeciesList.asset:17-53`), index 1 (`:54-90`), index 2 (`:91-127`), index 3 (`:128-164`), index 4 (`:165-201`), index 5 (`:202-238`). The `SpeciesDatabase.asset` mirrors the same values (`SpeciesDatabase.asset:16-238`).

Because every entry has `tier: 0` and `Tier2Enabled` has no Tier 2 species to act on, the shipped run is Tier 1 only. The two `Hexapod` vs `Gelgi` families differ only in `arrhenBreadth` (5000 specialist vs 7000 generalist) and the resulting `pmax` and Arrhenius L/U terms; the Cold/Warm/Hot variants shift `optimalTempK` by 2 K each (293.15/295.15/297.15) and shift the lethal limits.

## 7. SpeciesDatabase editor helpers (catalog generation)

`SpeciesDatabase` carries lookup methods and `#if UNITY_EDITOR` context menu actions that generate or reset the catalog. None of these run during a simulation; they are authoring tools.

Lookup methods (available in all builds):

| Method | Location | Purpose |
|---|---|---|
| `GetSpecies(name, variant)` | `:290-293` | Find by `speciesName` and `variant` enum pair. |
| `GetSpeciesByName(speciesLabel)` | `:295-298` | Find by `speciesLabel` string. |
| `GetSpeciesByTier(tier)` | `:301-304` | All entries with the given `tier`. |
| `GetVariants(name)` | `:307-310` | All entries with the given `speciesName`. |

Private constants: `DEFAULT_T1_COUNT = 20`, `DEFAULT_T2_COUNT = 4`, `DEFAULT_T3_COUNT = 2` (`:285-287`). Only the Tier 1 count is used by the populate action.

Editor context menus (editor only):

- `[ContextMenu("Populate Default Data")]` `PopulateDefaultData` (`:313-435`) clears the list and adds the six canonical Tier 1 organisms (Hexapod specialist B=5000 and Gelgi generalist B=7000, each Cold/Warm/Hot). It uses shared non thermal constants `SHARED_EATING=3`, `SHARED_REPRO=0.45`, `SHARED_DEATH_THRESH=0.3`, `SHARED_DEATH_RATE=0.6`, `SHARED_REPRO_THRESH=0.25`, `SHARED_NATURAL_DEATH=0.02`, `SHARED_NATURAL_DEATH_VAR=0.01`, `SHARED_HUNT_EFF=1.0`, `SHARED_HUNT_VAR=0`, `SHARED_COND_DRAIN=0.15`, `SHARED_COND_RECOVERY=0.10` (`:331-341`). The per organism thermal numbers it writes match the shipped asset table in section 6.2.
- `[ContextMenu("Reset Values (Preserve Icons)")]` `ResetValuesPreserveIcons` (`:493-545`) rewrites each existing entry's shared Tier 1 defaults and reapplies canonical thermal parameters via `ApplyCanonicalThermal` (`:553-599`), preserving `icon`, `index`, `speciesName`, `variant`, `speciesLabel`, and `count`. Entries whose `variant` is `Custom` or otherwise unknown are skipped with a warning.

`AddSpecies` exists in two overloads, both `private void`. The enum based overload (`:437-491`) is the one used by `PopulateDefaultData`; its six call sites are at `:344`, `:358`, `:372`, `:387`, `:401`, and `:415`. The second string/display name overload (`:601-646`) builds a `Custom` species with placeholder thermal values (`arrhenBreadth 5273.15`, `arrhenLower 10273.15`, `arrhenUpper 21273.15`, `pmax 1.0`, `ctMinC -5`, `ctMaxC 40`), but it has no call sites anywhere in `Assets` (verified across the whole tree), it returns `void`, and it never appends the constructed `data` to `speciesList`. It is therefore unused and unreferenced dead code: no caller relies on it and it has no observable effect.

## 8. SpeciesEditEvents

`SpeciesEditEvents` (`SpeciesEditEvents.cs:11-75`) is a static observer hub with no serialized state. It decouples the species list UI from the species edit panel. It has nothing to configure; it is listed here because it is one of the four required types and because it defines the contract the species editor uses to mutate the `RunSpeciesList`.

Events:

| Member | Signature | Raised by | Meaning |
|---|---|---|---|
| `OnEditRequested` | `event Action<int>` (`:17`) | `RequestEdit(index)` (`:42-45`) | An edit was requested for the species at the given index in the `RunSpeciesList`. |
| `OnEditClosed` | `event Action` (`:23`) | `NotifyEditClosed()` (`:51-54`) | The edit panel closed. |
| `OnSpeciesSaved` | `event Action<int>` (`:29`) | `NotifySpeciesSaved(index)` (`:61-64`) | Species data at the index was saved. |
| `OnSpeciesDeleted` | `event Action<int>` (`:35`) | `NotifySpeciesDeleted(index)` (`:71-74`) | Species at the index was deleted. |

The index in every payload is the position in `RunSpeciesList.speciesList`. Producers and consumers in the simulation UI: `SpeciesUIController` raises `RequestEdit` and listens for `OnSpeciesSaved` (`SpeciesUIController.cs:193, :66`); `EditSpeciesUI` listens for `OnEditRequested` and raises `NotifyEditClosed`/`NotifySpeciesSaved`/`NotifySpeciesDeleted` (`EditSpeciesUI.cs:192, :530, :666, :700`); `SpeciesTierConfig` listens for `OnSpeciesDeleted` (`SpeciesTierConfig.cs:68`). See `ui-and-io.md` for the editor flow.

## 9. Run control flow, scenario statistics, and aggregation

This section documents how the configuration above drives an end-to-end run: the scenario loop and seed derivation, the per-day step loop, how `TemperatureCalculator` and `EcosystemSimulator` interleave, what each `StepRecord` holds, the per-scenario statistics, and the cross-scenario aggregation math. The authoritative narrative spec is `simulation-spec.md`; the per-day biology detail is in `biology-and-formulas.md`; the record structures are in `data-structures.md`. The formulas below are reconstructed from the C# so a developer can rebuild the control flow from this document.

### 9.1 Scenario loop and per-scenario seed

`NumberOfScenarios` (section 2.1) drives a loop in `SimulationController`. Each scenario gets its own seed derived from `RandomSeed` (section 2.5). The 0-based loop index is `i`; the displayed scenario number is `i + 1` (`SimulationController.cs:176, 208`). The per-scenario seed is:

```
scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i      # SimulationController.cs:180, 209
```

The test is `< 0`, not `== -1`: any negative base makes every scenario use `-1`, which seeds `System.Random` from system time and makes the scenario non-reproducible (`EcosystemSimulator.cs:302`, `TemperatureCalculator.cs:45`). A non-negative base gives scenario `i` the reproducible seed `RandomSeed + i`. There is no overflow guard on `RandomSeed + i`. Each scenario builds a fresh `SimulationRunner(scenarioSeed)`, which constructs a `TemperatureCalculator(seed)` and an `EcosystemSimulator(seed)` from the same seed (`SimulationRunner.cs:382-387`), then copies the config fields onto the runner and ecosystem (`SimulationController.cs:343-381`) and calls `runner.Run()`. The two RNGs are independent `System.Random` instances seeded identically; the temperature stream and the biology stream (hunting/natural-death variance) therefore each derive from the same seed but advance separately.

Scenario execution is parallel in Editor/standalone (chunked by `ProcessorCount - 1` via `Task.Run`, `SimulationController.cs:189-258`) and sequential in WebGL (`:170-188`). Parallelism does not change results because each scenario owns its runner and seed. After all scenarios complete, `AggregateResults.CalculateAggregates()` runs (`:262-263`).

### 9.2 Per-day step loop and temperature/biology interleave

`SimulationRunner.Run()` initializes the species (from `RunSpeciesList`, else hardcoded defaults) and then loops one iteration per day for `TotalDays` days (`DaysPerScenario`), with `dayIndex` 0-based (`SimulationRunner.cs:392-448`). The ordered per-day sequence is:

1. Optional cooperative pause/stop check at the day boundary; a paused run spins without consuming RNG so resume is byte-identical, a stopped run breaks the loop (`SimulationRunner.cs:419-423`).
2. Compute `displayDay = dayIndex + 1` and `year = dayIndex / 365 + 1` (`:425-426`).
3. Sample temperature for this day: `temp = TempCalc.GetTemperature(dayIndex)` using the 0-based index (section 2.4 model) (`:428`). Temperature is sampled every day, including days where biology does not run.
4. Decide whether biology runs this day: `runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)` (`:430`). With `BiologyStep = 1` this is every day; with larger steps it is day 1 and every `BiologyStep`-th day thereafter.
5. If `runBiology`: increment the biology-cycle counter and call `Ecosystem.ProcessBiologyStep(temp)`, which runs the 10-step biology sequence at that day's temperature (`:432-436`). The 10 steps are: thermal performance, feeding, raw final performance, update Condition, final performance, thermal death, condition death, reproduction, natural death, population rounding (`EcosystemSimulator.cs:543-700`; the per-step formulas are in sections 4.2, 4.5, 4.7, 4.8 and in `biology-and-formulas.md`).
6. Record the day via `RecordStep(displayDay, year, temp, runBiology)` (`:438`). This always records a `StepRecord`, even on non-biology days; on non-biology days the per-day event counts are 0 but populations, temperature, food density, and accumulator residuals are carried forward (section 9.3).
7. If biology ran and the ecosystem has crashed (total population 0, `EcosystemSimulator.cs:1386-1390`), set the crash flags (`CrashDay = displayDay`, `CrashTier = GetCrashedTier()`) and break out of the day loop early (`:440-447`). A crash ends the scenario before `TotalDays`.

So temperature advances its own daily stream every day; the biology stream advances only on biology days and always reads the temperature already sampled for that day. `BiologyStep` advances the calendar by gating which days call `ProcessBiologyStep`; it also multiplies the per-step birth and death counts by `BiologyStep` inside the biology formulas (section 4.2), so a larger step both runs biology less often and scales each run's magnitudes up.

### 9.3 StepRecord contents

`RecordStep` builds one `StepRecord` per day (`SimulationRunner.cs:464-584`). `long` is used for all population and event counts to avoid 32-bit overflow; non-finite floats are converted to 0 before casting (`SafePopToLong`, `:597-598`). The record holds:

| Group | Fields | Notes |
|---|---|---|
| Time | `Day`, `Year` | 1-based day, 1-based year. |
| Environment | `Temperature` | The day's sampled ambient C (`:484`). |
| Biology marker | `BiologyCycle` | The cycle number if biology ran this day, else 0 (`:485`). |
| Totals | `StartPop`, `EndPop` | T1+T2 start (biology days only) and end populations (`:488-489`). |
| Per tier | `Tier1Pop`, `Tier2Pop` | Rounded tier sums (`:494-495`). |
| Per variant rollup | `TierVariantPop` | Dictionary keyed `Tier{tier}_{SanitizedLabel}`, summed in the per-species loop (`:548-551`). |
| Deaths | `EatenT1`, `TempDeathsT1/T2`, `ConditionDeathsT1/T2`, `NaturalDeathsT1/T2`, `TotalDeaths` | Per-day counts on biology days, else 0; `TotalDeaths` is their sum (`:476-505`). |
| Births | `BirthsT1`, `BirthsT2` | Per-day counts on biology days, else 0 (`:508-509`). |
| Reproduction | `ReproScaleT1`, `ReproScaleT2` | Last reproScale per tier (`:512-513`). |
| Feeding | `FedRateT2`, `AvgHuntingEff`, `FedRateT1`, `FoodDensityT1` | Tier 1 feeding values are carried (not zeroed) on non-biology days because food density is unchanged (`:516-522`). |
| Condition | `AvgConditionT1`, `AvgConditionT2` | Population-weighted average per tier (`:525-526`). |
| Accumulators | `BirthAccumT1/T2`, `NaturalDeathAccumT1/T2`, `ConditionDeathAccumT1/T2`, `PredationAccumT1` | Current fractional residuals carried between days (`:529-535`). |
| Per species | `SpeciesData` | Dictionary keyed by `FullName`, one `PerSpeciesStepData` per species (`:540-581`). |

Each `PerSpeciesStepData` carries `Population`, `Condition`, `ThermalPerf` (raw, no Pmax), `FinalPerf`, `FedRate`, `HuntingEff` (T2 only), the five per-day event counts, `BirthRate = Births / max(startOfDayPop, 1)`, `ReproScale`, and the four accumulator residuals (`SimulationRunner.cs:20-48, 561-580`). The tier-rollup invariant holds: per-species values sum to the matching `Tier{n}_{label}` rollup column, which sum to the `Tier{n}Pop` total.

### 9.4 Per-scenario statistics

At end of run `runner.ToScenarioResult(scenarioIndex, numberOfScenarios)` post-processes the `StepRecord` list into a `ScenarioResult` (`SimulationRunner.cs:982-1037`). The statistics computed per scenario:

- Across-day population stats per column (`Tier1Pop`, `Tier2Pop`, each rollup column, and each species by `FullName`): Mean, Max, Min, StdDev over all recorded days (`ComputePopulationStats`, `:622-736`). StdDev is the population standard deviation `sqrt(sum((x - mean)^2) / N)`.
- Extinction day per rollup column and per species: the first recorded day where population reaches 0 after having been positive, else -1 (`:681-696, 850`).
- Temperature stats: average, min, max over all days (`GetSummary`, `:951-973`).
- Condition stats: full-run mean of the per-day `AvgConditionT1/T2` plus the final-day snapshot (`:992-1003, 1026-1029`).
- Crash outcome: `Crashed`, `CrashDay`, `CrashTier` from the runner (`:1011-1013`).
- Final populations: `FinalTier1Pop`, `FinalTier2Pop`, and `FinalSpeciesPopulations` keyed by `FullName` (`:1014-1016, 1039-1045`).
- Per-species rich metrics `SpeciesMetrics` (`ComputePerSpeciesScenarioMetrics`, `:1060-1186`). For each species, over both the full run and the final-year window (the last `FINAL_YEAR_DAYS = 365` days, or all days if the run is shorter, `:1052, 1067`): mean Condition and mean per-capita birth rate (averaged over alive days only, since Condition is not updated once a species is extinct, `:1128-1132`), population coefficient of variation `CV = StdDev / Mean` (0 when mean is ~0, `:1192-1200`), mean population, final-year death counts by pathway, min/max population, extinction day, and a crash day defined as the first day population drops below `max(CRASH_FLOOR=10, CRASH_FRACTION=0.05 * startPop)` (`:1054-1058, 1111-1117`).

### 9.5 Cross-scenario aggregation

`AggregateResults.CalculateAggregates()` rolls the per-scenario `ScenarioResult` list up into run-level statistics (`ScenarioResult.cs:288-424`). The aggregation math:

- Scenario counts: `SurvivedScenarios` (not crashed) and `CrashedScenarios`; `CrashRate = CrashedScenarios / TotalScenarios` (`:355`).
- Tier-total final populations are averaged over surviving scenarios only: `AvgFinalTier1Pop = sum(FinalTier1Pop over survived) / survivedCount`, with Min/Max also taken over survived scenarios (`:326-344`). If no scenario survived, the Min/Max are forced to 0 (`:357-363`).
- Average crash day is the mean `CrashDay` over crashed scenarios only (`:346-349`).
- Condition: `AvgConditionT1/T2` is the grand mean of each scenario's average condition over all scenarios (crashed and survived); `AvgFinalConditionT1/T2` is the mean final condition over survived scenarios only (`:312-313, 328-329, 342-343, 351-353`).
- Per-species population aggregate over all scenarios (including crashed), keyed by `FullName` from each scenario's `FinalSpeciesPopulations` (`:365-420`): `PerSpeciesAvg` = mean final population across scenarios; `PerSpeciesMin`/`PerSpeciesMax` = min/max final population; `PerSpeciesExtinct`/`PerSpeciesSurvived` = counts of scenarios where the species final pop is `<= 0` / `> 0`; `PerSpeciesSurvivedAvg` = mean final population across surviving scenarios only.
- Per-species rich aggregate `PerSpeciesMetrics` (`BuildPerSpeciesAggregate`, `:431-482`): for each species the per-scenario `PerSpeciesScenarioMetrics` rows are combined with `ComputeAggStat` into Mean/StdDev/Min/Max plus survived-only Mean/StdDev (`:484-520`), and extinction/crash timing into N-events, N-non-events, and min/mean/max day among events (`ComputeExtinctionStat`, `:522-544`). `ComputeAggStat` computes `Mean = sum/N`, `StdDev = sqrt(max(0, sqSum/N - Mean^2))`, and the survived-only variants over the subset of rows where the species' final population was positive.

These run-level aggregates are written to `aggregate.csv` (`ToAggregateCsv`, `ScenarioResult.cs:588-926`). The bulk path (`bulk-system.md`) aggregates one further level across runs. The CSV section layouts are in `csv-output-formats.md`.
