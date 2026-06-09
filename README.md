# TinySea

TinySea is a Unity 6 project built by Northeastern University GhostLab. This repository now centers on a **headless ecosystem simulation** that models how a population of marine prey organisms grows, shrinks, and sometimes collapses as water temperature changes over time. The simulation runs many days of biology, records a row of numbers per day, and writes the results to CSV files for analysis in R, Python, or a spreadsheet.

This document is written for two readers at once:

- A **marine scientist or researcher** who wants to understand what the simulation models, how to run it, and what the output files mean. Plain-language explanations are given for every technical term.
- A **software developer** who may be handed the code. Every non-trivial statement cites the source file and line range so the behavior can be located and reimplemented exactly. The authoritative source of truth is the C# code under `tinysea/Assets/scripts/Simulation/` and the asset files under `tinysea/Assets/`.

All file paths in this document are relative to the repository root unless stated otherwise. Citations look like `(EcosystemSimulator.cs:543-700)`, meaning lines 543 to 700 of that file inside `tinysea/Assets/scripts/Simulation/` (or its `DataStructure/` and `UI/` subfolders).

---

## 1. Status and scope

### 1.1 The interactive game is deprecated

TinySea originally shipped as an interactive, turn-based game where a player bought and sold organisms across a food chain. That game still exists in the code under `tinysea/Assets/scripts/`, but it is **deprecated and not built**. The Unity build scene list enables only the simulation scene:

```
Assets/scenes/simulation.unity   enabled: 1   <- the only active scene
Assets/scenes/main_menu.unity    enabled: 0
Assets/scenes/new_ui_scene.unity enabled: 0
Assets/scenes/tutorial.unity     enabled: 0
Assets/scenes/mainscene.unity    enabled: 0
Assets/scenes/tutorialscene.unity enabled: 0
```

Source: `tinysea/ProjectSettings/EditorBuildSettings.asset`. Only `Assets/scenes/simulation.unity` has `enabled: 1`; every game scene has `enabled: 0`. When you open the project and press Play, or when you make a build, the simulation is what runs.

### 1.2 Build targets: Windows and macOS standalone. WebGL was canceled

The project ships two active standalone build profiles:

| Profile file | Platform | Unity `m_BuildTarget` | Architecture note |
|---|---|---|---|
| `Assets/Settings/Build Profiles/Windows.asset` | Windows 64-bit standalone | `19` (StandaloneWindows64) | `WindowsPlatformSettings`, `m_Subtarget: 2` |
| `Assets/Settings/Build Profiles/macOS.asset` | macOS standalone | `2` (StandaloneOSX) | `OSXStandaloneBuildProfile`, `m_Architecture: 2` |

Sources: `Windows.asset:16-31`, `macOS.asset:16-31`.

A third profile, `Assets/Settings/Build Profiles/Web - Desktop - Development.asset`, exists in the folder, but **WebGL is canceled and is not a supported target**. The code still contains WebGL-only branches (browser file picker, browser download, S3 upload). Those branches are dormant on the standalone targets. On Windows and macOS the equivalent work is done with native file dialogs and direct disk writes (`CsvUploadHandler.cs:279-300`, `CsvUploadHandler.cs:324-340`, `SavePaths.cs:14-60`).

On macOS the simulation forces a windowed 1280x720 resolution at startup (`SimulationController.cs:44-51`).

### 1.3 Tier 1 only; Tier 2 is secondary legacy

The biology engine was written for a two-level food chain: **Tier 1** (prey) and **Tier 2** (predator). The current model is **Tier 1 only**. Tier 2 code remains in the engine from the original design and still functions, but it is secondary legacy. Throughout this document, Tier 1 is documented completely and precisely; Tier 2 is documented briefly and marked as legacy wherever it appears.

Plain language: "Tier 1" means the small organisms at the bottom of the food chain (think plankton or small grazers) that feed on a shared environmental resource pool rather than hunting. "Tier 2" means larger animals that would eat the Tier 1 organisms. The active research model studies only the Tier 1 prey under temperature stress.

Two independent switches control whether Tier 2 participates, and they do not agree in the shipped project, so read this carefully:

- The **bulk CSV upload path is hard Tier-1-only**. The bulk parser rejects any species row whose `tier` is not `0` (prey) with an explicit error (`CsvBatchParser.cs:316-319`). You cannot upload a predator through the bulk workflow.
- The **single-config path is governed by a `Tier2Enabled` flag** on the config (`SimulationConfig.cs:126-130`). The code default for this flag is `false` (`EcosystemSimulator.cs:245`), but the **shipped config asset sets it to `1` (true)**: `Tier2Enabled: 1` in `Assets/Resources/SimulationConfig.asset:33`. So a default single-config run as shipped will still process any Tier 2 species present in the species list, and will emit Tier 2 output columns.

When `Tier2Enabled` is false, Tier 2 species are dropped at load (`EcosystemSimulator.cs:331-337`) and every Tier 2 output column is suppressed (`SimulationRunner.cs:802`, header/row logic in `SimulationRunner.cs:209-313`). For a clean Tier-1-only study, set `Tier2Enabled` to false on the config and include only `tier 0` species.

---

## 2. What the simulation models

### 2.1 The big picture

Each simulated **day**, the engine does the following for every species, in a fixed order:

1. Compute how well the species performs at today's temperature (a number from 0 to 1).
2. Compute how well-fed it is, based on crowding against a shared food pool.
3. Update its **Condition** (a slow-moving health/energy reserve from 0 to 1).
4. Kill individuals that hit lethal temperature limits.
5. Kill individuals whose Condition has fallen too low (graduated, not all-or-nothing).
6. Produce offspring, scaled by Condition.
7. Apply a flat background death rate (old age, disease, accidents).
8. Round the population to a whole number.

Temperature is driven by a separate model that adds a seasonal cycle, a long-term warming trend, year-to-year noise, and day-to-day noise, then clamps to a min/max. The whole point of the simulation is to see how a population responds to that temperature signal over months or years: does it find a stable level, oscillate, or crash to zero.

### 2.2 The temperature model

Temperature is produced by `TemperatureCalculator` (`TemperatureCalculator.cs`), which is independent of the deprecated game's temperature class. For a given zero-based `day`, the temperature in degrees Celsius is the sum of five components, then clamped (`TemperatureCalculator.cs:61-85`):

```
T(day) = Base + Seasonal(day) + Trend(day) + Interannual(day) + Daily(day)
T(day) = clamp(T(day), MinTemp, MaxTemp)
```

| Term | Formula | Meaning | Source |
|---|---|---|---|
| Base | `BaseTemperature` | Mean temperature in degrees C. | `TemperatureCalculator.cs:18` |
| Seasonal(day) | `sin(2*pi*day / 365) * SeasonalAmplitude` | Summer/winter swing. Coldest at day 0, warmest near day 182. Averages to 0 over a year. | `TemperatureCalculator.cs:126-129` |
| Trend(day) | `ClimateTrendPerYear * (day / 365)` | Linear climate warming. This is the **only** component with a non-zero long-term mean. | `TemperatureCalculator.cs:134-138` |
| Interannual(day) | per-year random offset, zero-mean | One offset per year, reused all year. See below. | `TemperatureCalculator.cs:148-170` |
| Daily(day) | autocorrelated random noise | Day-to-day weather wobble. See below. | `TemperatureCalculator.cs:175-196` |

Units: `day` is an integer count of days; one year is `DAYS_PER_YEAR = 365` (`TemperatureCalculator.cs:30`). All temperatures are degrees Celsius.

**Interannual variation** draws a cold part `~ uniform(-VariabilityMagnitude, 0)` and a warm part `~ uniform(0, VariabilityMagnitude * WarmingBias)`, averages them, then subtracts the expected bias mean `VariabilityMagnitude * (WarmingBias - 1) / 4` so that `WarmingBias` skews only the *shape* of the distribution and not its mean (`TemperatureCalculator.cs:148-170`). The long-term trend is owned solely by `ClimateTrendPerYear`. Set by `UseInterannualVariation` (`TemperatureCalculator.cs:25`); returns 0 when off (`TemperatureCalculator.cs:150`).

**Daily variation** grows with time: `currentRandomness = BaseRandomness + RandomnessGrowthRate * year` (`TemperatureCalculator.cs:177-178`). A fresh value `newRandom ~ uniform(-currentRandomness, +currentRandomness)` is drawn each day. When `UseAutocorrelation` is true, the day's value is `0.7 * yesterday + 0.3 * newRandom`, producing smooth transitions; when false it is just `newRandom` (`TemperatureCalculator.cs:183-195`).

**Optional temperature timeseries.** Instead of the five-component model, a per-day temperature series can be loaded from a `Day,Temperature_C` CSV (`TemperatureCalculator.cs:104-121`, `TemperatureCalculator.cs:91-97`). When a series is loaded, `GetTemperature(day)` returns `series[day % length]`, looping with a one-time warning if the series is shorter than the run, still clamped to min/max (`TemperatureCalculator.cs:66-75`). In the bulk workflow this is wired through `BulkBatchConfig.TemperatureTimeseriesFile` (`BulkBatchConfig.cs:61-62`) and loaded from disk in `SimulationController.cs:317-334`. Loading from a file path is an Editor/standalone feature; on a missing file or parse failure the engine logs a warning and falls back to the parametric model.

### 2.3 Thermal performance (the temperature response curve)

Each species has a thermal performance curve: a function that returns how well it performs (0 = cannot function, 1 = peak) at any temperature. This is computed by `SimSpecies.CalculatePerformance(temperatureCelsius)` (`SimSpecies.cs:95-133`).

Plain language: every organism has a temperature it likes best and a range it can tolerate. Outside that range it does poorly; past hard limits it dies. This curve encodes that.

The calculation has three parts:

1. **Per-species temperature offset.** The species' `TemperatureDebuff` is added to the input temperature first, so a species can experience a shifted temperature relative to the water (`SimSpecies.cs:97`).

2. **Lethal-limit fade.** Two critical limits bound the curve: `CTminC` (critical thermal minimum, deg C) and `CTmaxC` (critical thermal maximum, deg C). Performance is exactly 0 at or beyond either limit. Just inside each limit, performance fades smoothly to/from zero over a cosine transition whose width is `min(2.0, (CTmaxC - CTminC)/2)` degrees (`SimSpecies.cs:99-114`). "CT" stands for critical thermal: the temperatures at which the organism can no longer survive.

3. **Arrhenius curve.** Between the limits, performance follows an Arrhenius-type thermal performance curve. Temperature is converted to Kelvin as `T = temperatureCelsius + 273.15` (`SimSpecies.cs:116`). With `OT = OptimalTempK`, `B = ArrhenBreadth`, `L = ArrhenLower`, `U = ArrhenUpper`, `LB = LowerBoundK`, `UB = UpperBoundK` (all Kelvin), the formula is (`SimSpecies.cs:124-132`):

```
numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)
perf        = numerator / denominator
result      = clamp(perf, 0, 1) * fadeFactor
```

The result is the **raw thermal performance**, clamped to [0, 1] and multiplied by the lethal fade. `Pmax` (peak height) is *not* applied here; it is applied separately by the engine (next section). The Arrhenius parameters are named per their role:

| Parameter | Units | Meaning |
|---|---|---|
| `OptimalTempK` | Kelvin | Temperature of peak performance. |
| `ArrhenBreadth` | Kelvin (rate constant) | Controls how broad the curve is around the optimum. |
| `ArrhenLower` / `ArrhenUpper` | Kelvin (rate constants) | Govern the low-temperature and high-temperature shoulders. |
| `LowerBoundK` / `UpperBoundK` | Kelvin | Reference temperatures for the lower/upper deactivation terms. |
| `Pmax` | dimensionless [0,1] | Peak height; applied by the engine, not by this function. |
| `CTminC` / `CTmaxC` | degrees C | Hard lethal limits. |
| `TemperatureDebuff` | degrees C | Per-species offset added to experienced temperature. |

### 2.4 The daily biology sequence (Tier 1)

The engine is `EcosystemSimulator.ProcessBiologyStep(temperature)` (`EcosystemSimulator.cs:543-700`). It runs ten ordered steps every biology day. The list below documents each step for Tier 1. Tier 2 additions are noted as legacy.

At the top of the step, the engine snapshots each tier's starting population, resets all per-step counters to zero, and snapshots per-species starting populations (`EcosystemSimulator.cs:545-584`).

1. **Thermal performance.** For each species, `RawThermalPerformance = CalculatePerformance(temperature)` and `ThermalPerformance = RawThermalPerformance * Pmax`. `FedRate` is initialized to 1 (`EcosystemSimulator.cs:589-598`). `RawThermalPerformance` excludes `Pmax`; `ThermalPerformance` includes it.

2. **Feeding.** Tier 1 feeding is density-dependent extraction from a shared food pool (`EcosystemSimulator.cs:728-781`). Define:

   ```
   tier1Pop     = max(0, total Tier 1 population)
   capSafe      = max(CarryingCapacityPerTier, 1)
   foodDensity  = max(0, 1 - tier1Pop / capSafe)        // 1 = empty pool, 0 = full pool
   FedRate_i    = min(1, HuntingEfficiency_i * foodDensity)   // per Tier 1 species i
   ```

   Plain language: there is one shared pool of food. The more crowded the prey are, the less food each one gets. `foodDensity` is the fraction of the pool still available; it falls linearly from 1 (empty, plenty of food) toward 0 as the population approaches the carrying capacity. Each species converts available food into a feeding satisfaction `FedRate` between 0 and 1, scaled by its own `HuntingEfficiency` (for Tier 1, this means *resource extraction efficiency*; the default 1.0 represents perfect plankton-style passive uptake, `SimSpecies.cs:43-52`, `SimSpecies.cs:155`). A species below the minimum-alive population of 1.0 gets `FedRate = 0` (`EcosystemSimulator.cs:767-776`). The relationship is intentionally **linear**, not a Holling saturating curve, because passive extractors have no search/handling phases and because the Holling form collapses to 1 at the common `HuntingEfficiency = 1` setting (`EcosystemSimulator.cs:736-757`). The population-weighted average Tier 1 FedRate and the day's `foodDensity` are recorded for output (`EcosystemSimulator.cs:780-781`).

   Carrying capacity is **always on**. `CarryingCapacityPerTier` defaults to 5000 (`EcosystemSimulator.cs:237`) and must be greater than 0 (validated at config and parse time). Without a ceiling, Tier 1 grows without bound, which is biologically meaningless and overflows the birth counters (`EcosystemSimulator.cs:121-135`, `SimulationConfig.cs:166-171`).

3. **Raw final performance.** `RawFinalPerformance = RawThermalPerformance * FedRate` (`EcosystemSimulator.cs:604-610`). This is the *target* that Condition drifts toward. It combines temperature stress and food stress.

4. **Update Condition.** Condition is a per-species health reserve in [0, 1] that starts at 1.0 and moves slowly toward `RawFinalPerformance` (`EcosystemSimulator.cs:952-992`). It drains when above target and recovers when below, asymmetrically (drain is faster than recovery). With `target = RawFinalPerformance` and `pmaxSafe = max(Pmax, 1e-4)`:

   ```
   drainRate    = species ConditionDrainRate    if >= 0 else global ConditionDrainRate
   recoveryRate = species ConditionRecoveryRate  if >= 0 else global ConditionRecoveryRate

   if Condition > target:                      // draining
       severity        = (1 - target)^2        // 0 at perfect target, 1 at zero target
       effectiveDrain  = drainRate * (1 + severity) / pmaxSafe
       Condition      -= (Condition - target) * effectiveDrain
   else:                                        // recovering
       boost           = target^2
       effectiveRecov  = recoveryRate * (1 + boost) * pmaxSafe
       Condition      += (target - Condition) * effectiveRecov

   Condition = clamp(Condition, 0, 1)
   ```

   Plain language: Condition is like body fat or energy reserves. It does not crash the instant the weather turns bad; it draws down gradually, which lets a healthy summer carry an animal partway through a hard winter. The drain accelerates (up to twice as fast) as the target approaches lethal performance, and `Pmax` makes high-peak species drain slower and recover faster. Global defaults: drain 0.15/day, recovery 0.10/day (`EcosystemSimulator.cs:240-241`). A per-species rate of `-1` means "inherit the global rate" (`SimSpecies.cs:36-37`).

5. **Final performance (logging only).** `FinalPerformance = ThermalPerformance * FedRate` is computed for output but is **not consumed by any later step** (`EcosystemSimulator.cs:619-630`). Reproduction uses Condition, not this value.

6. **Thermal death (instant at lethal limits).** If `RawThermalPerformance == 0` (the species is at or beyond `CTmin`/`CTmax`), the entire population dies and Condition is set to 0 (`EcosystemSimulator.cs:1000-1025`). Otherwise nothing happens here. There is no Condition buffer against lethal temperature; suboptimal-but-survivable temperatures are handled entirely by the Condition system.

7. **Condition death (graduated).** If `Condition < DeathThreshold`, some individuals die, scaled by how far below the threshold Condition has fallen (`EcosystemSimulator.cs:1056-1099`):

   ```
   severity  = (DeathThreshold - Condition) / DeathThreshold   // 0 at threshold, 1 at Condition=0
   rawDeaths = Population * severity * DeathRate * BiologyStep
   ```

   Plain language: instead of a cliff where everyone below a line dies at once, deaths scale with severity. Barely below the threshold means a few weak individuals die; severely depleted means a mass die-off. After deaths, survivors get a **fitness boost**: their average Condition is recomputed as `oldCondition * oldPop / newPop` (capped at 1.0), on the assumption that the dead were the weakest (`EcosystemSimulator.cs:1079-1086`). This conserves the population's total health pool among fewer, healthier survivors and prevents runaway death spirals. `DeathThreshold` defaults to 0.3 and `ReproThreshold` to 0.25 in the data model (`SimSpecies.cs:27-29`).

8. **Reproduction (Condition-driven).** Births are scaled by a continuous function of Condition, joined at a "struggling" rate so there is no cliff (`EcosystemSimulator.cs:1150-1264`). With `STRUGGLING_REPRO_RATE = 0.10` (`EcosystemSimulator.cs:282`):

   ```
   if Condition >= ReproThreshold:    // healthy: ramps 0.10 -> 1.0
       t         = (Condition - ReproThreshold) / (1 - ReproThreshold)
       reproScale = 0.10 + 0.90 * t
   else:                              // struggling: ramps 0 -> 0.10
       reproScale = 0.10 * (Condition / ReproThreshold)

   reproScale = clamp(reproScale, 0, 1)
   births     = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep
   ```

   (Edge cases when `ReproThreshold >= 1` or `<= 0` are handled at `EcosystemSimulator.cs:1169-1178`.) A species needs at least `MIN_POPULATION_FOR_REPRODUCTION = 2` to reproduce (`EcosystemSimulator.cs:274`, `EcosystemSimulator.cs:1152-1156`). Plain language: reproduction tracks the slow Condition reserve rather than instantaneous performance, so animals with reserves keep breeding (at reduced rates) through harsh stretches instead of stopping dead in winter. `Pmax` makes high-peak species convert health into offspring more efficiently. Newborns inherit the group's current Condition, so no separate dilution step is applied (`EcosystemSimulator.cs:1240-1253`).

   **Tier 1 throttling is indirect.** There is no hard cap on Tier 1 births. High population lowers food density (step 2), which lowers FedRate, which lowers the Condition target (step 3), which drains Condition (step 4), which both shrinks `reproScale` and fires condition deaths (step 7). Logistic-overshoot dynamics emerge from this feedback rather than from a births cap (`EcosystemSimulator.cs:1132-1138`, `EcosystemSimulator.cs:1217-1222`). One consequence: equilibrium populations under this model oscillate around roughly 80 to 95 percent of the carrying capacity rather than sitting exactly at it (`SimulationConfig.cs:55-56`).

   **No-predator penalty (interacts with legacy Tier 2).** If a Tier 1 species exists with no live Tier 2 population, its births are multiplied by `NO_PREDATOR_PENALTY = 0.85` (a 15 percent reduction) (`SimSpecies.cs:55`, `EcosystemSimulator.cs:1207-1215`). In a Tier-1-only run this penalty is always in effect.

9. **Natural death (flat rate).** Independent of performance, each species loses `Population * effectiveRate * BiologyStep`, where `effectiveRate = max(0, NaturalDeathRate + variance)` and `variance ~ uniform(-NaturalDeathVariance, +NaturalDeathVariance)` (`EcosystemSimulator.cs:1270-1319`). Defaults: `NaturalDeathRate = 0.02` (2 percent) with `NaturalDeathVariance = 0.01` (plus or minus 1 percent) (`SimSpecies.cs:40-41`). This represents old age, disease, and accidents.

10. **Population rounding and overflow guard.** Each population is hard-capped at `100 * CarryingCapacityPerTier` as a defensive guard against transient overshoot, then rounded to a whole number away from zero (`EcosystemSimulator.cs:660-686`). After rounding, the engine recomputes average Condition per tier and records end populations (`EcosystemSimulator.cs:688-699`).

**Fractional accumulators.** Births and the three death types (predation, condition, natural) are tracked with fractional accumulators per species. Each day the fractional part carries over and only whole individuals are added or removed (`EcosystemSimulator.cs:150-154`, and within steps 2/7/8/9). This prevents small daily fractions from being lost to rounding and keeps long runs accurate. All death and birth tallies are stored as `long` to avoid integer overflow at large populations.

**Crash detection.** A run is considered crashed when the total population (Tier 1 plus Tier 2) reaches exactly 0 (`EcosystemSimulator.cs:1386-1390`). The runner records the crash day and stops early (`SimulationRunner.cs:440-447`).

### 2.5 Tier 2 (predator) logic — secondary legacy

Tier 2 is not part of the active model. It is documented here briefly for completeness; it does not reduce the precision of the Tier 1 description above.

When Tier 2 species are present and enabled, step 2 also runs a predator feeding model (`EcosystemSimulator.cs:783-897`). Predators hunt Tier 1 prey using a **Holling Type II functional response** (Holling 1959): hunting efficiency scales with the prey-to-predator ratio, equaling the species' base `HuntingEfficiency` at a reference ratio of 20:1, approaching 1.0 when prey are abundant and falling toward 0 when prey are scarce (`EcosystemSimulator.cs:924-932`). Each predator's feeding satisfaction is its own hunting success scaled by an overall scarcity factor `totalEaten / totalActualDemand` (`EcosystemSimulator.cs:849-863`). Prey are removed proportionally across Tier 1 species with a predation accumulator (`EcosystemSimulator.cs:867-896`). Tier 2 has its own thermal death, condition death, reproduction (with `ReproductionMultiplier` defaulting much lower), and natural death, sharing the same step machinery.

Current runs consider **Tier 1 only**. Treat all Tier 2 behavior as legacy.

---

## 3. Configuration

### 3.1 Single-config parameters (`SimulationConfig`)

The single-config run reads a `SimulationConfig` ScriptableObject (`DataStructure/SimulationConfig.cs`). The shipped asset is `Assets/Resources/SimulationConfig.asset`. The table gives each field's default, valid range, and meaning. Ranges shown as `[a, b]` are enforced by Unity `[Range]` attributes.

| Field | Type | Default (code) | Shipped asset value | Range | Meaning |
|---|---|---|---|---|---|
| `BiologyStep` | int | 1 | 1 | [1, 5] | Days between biology calculations. 1 = daily (most accurate). (`SimulationConfig.cs:26-29`) |
| `DaysPerScenario` | int | 365 | 365 | [1, 182500] | Length of one scenario in days (max approx. 500 years). (`SimulationConfig.cs:31-37`) |
| `NumberOfScenarios` | int | 5 | 5 | [1, 100] | How many times to repeat the scenario with different random seeds. (`SimulationConfig.cs:39-44`) |
| `CarryingCapacityTier1` | float | 5000 | 5000 | [100, 100000] | Shared Tier 1 food-pool capacity. Always on; must be > 0. (`SimulationConfig.cs:46-59`) |
| `ConditionDrainRate` | float | 0.15 | 0.15 | [0.01, 1.0] | Global Condition drain speed. (`SimulationConfig.cs:63-68`) |
| `ConditionRecoveryRate` | float | 0.10 | 0.10 | [0.01, 1.0] | Global Condition recovery speed. (`SimulationConfig.cs:70-74`) |
| `BaseTemperature` | float | 20 | 20 | none | Mean temperature, deg C. (`SimulationConfig.cs:78-80`) |
| `SeasonalAmplitude` | float | 5 | 5 | none | Summer/winter swing, deg C. (`SimulationConfig.cs:82-84`) |
| `ClimateTrend` | float | 1 | 0 | none | Warming per year, deg C. (`SimulationConfig.cs:86-88`) |
| `InterannualVariation` | bool | true | 0 (off) | none | Enable year-to-year variation. (`SimulationConfig.cs:90-91`) |
| `VariabilityMagnitude` | float | 2 | 0 | none | Magnitude of year-to-year variation. (`SimulationConfig.cs:93-95`) |
| `WarmingBias` | float | 1.5 | 0 | none | Skews the *shape* of interannual variation, not its mean. (`SimulationConfig.cs:97-98`) |
| `Autocorrelated` | bool | true | 1 (on) | none | Smooth day-to-day temperature transitions. (`SimulationConfig.cs:100-102`) |
| `DailyVariationRange` | float | 5 | 5 | none | Base daily random range, deg C. (`SimulationConfig.cs:104-105`) |
| `RandomnessGrowthRate` | float | 0.5 | 0 | none | How much daily randomness grows per year. (`SimulationConfig.cs:107-108`) |
| `TemperatureBoundsMin` | float | -5 | 0 | none | Hard floor, deg C. (`SimulationConfig.cs:110-112`) |
| `TemperatureBoundsMax` | float | 50 | 40 | none | Hard ceiling, deg C. (`SimulationConfig.cs:114-115`) |
| `RunSpecies` | reference | none | `RunSpeciesList.asset` | none | The list of species to simulate. (`SimulationConfig.cs:119-123`) |
| `Tier2Enabled` | bool | false | 1 (on) | none | Whether legacy Tier 2 predators participate. (`SimulationConfig.cs:126-130`) |
| `RandomSeed` | int | 12345 | 12345 | none | Base seed. `-1` = system time (non-reproducible). Scenario `i` uses `RandomSeed + i`. (`SimulationConfig.cs:134-139`) |

Source for shipped values: `Assets/Resources/SimulationConfig.asset:15-34`. Note again that the shipped asset enables Tier 2 (`Tier2Enabled: 1`) even though the field's code default is false.

**Validation** runs before a single-config simulation starts (`SimulationConfig.cs:146-195`, called from `SimulationController.cs:90-94`). It fails if `DaysPerScenario < 1`, if `NumberOfScenarios < 1`, if no species are configured, or if `CarryingCapacityTier1 <= 0`. It warns (non-fatal) if the initial Tier 1 population exceeds the carrying capacity, since that is a valid but often unintended over-seeding (`SimulationConfig.cs:173-191`).

### 3.2 Species parameters (`SpeciesData`)

Each species in a `RunSpeciesList` is a `SpeciesData` (`DataStructure/SpeciesDatabase.cs:32-278`). The biology-relevant fields, with defaults, are:

| Field | Default | Units / meaning |
|---|---|---|
| `tier` | 0 | 0 = Tier 1 prey, 1 = Tier 2 predator (legacy). (`SpeciesDatabase.cs:51`) |
| `count` | 0 | Initial population (whole individuals). (`SpeciesDatabase.cs:48`) |
| `eatingAmount` | 0 | Prey consumed per predator per step; 0 for Tier 1. (`SpeciesDatabase.cs:52`) |
| `reproductionMultiplier` | 0 | Birth-rate multiplier. (`SpeciesDatabase.cs:53`) |
| `deathThreshold` | 0.3 | Condition below this triggers graduated condition death. (`SpeciesDatabase.cs:54`) |
| `deathRate` | 0 | Fraction dying at maximum condition severity. (`SpeciesDatabase.cs:55`) |
| `reproThreshold` | 0.25 | Condition inflection between "healthy" and "struggling" reproduction. (`SpeciesDatabase.cs:57`) |
| `conditionDrainRate` | 0.15 | Per-species drain speed (database default explicit; bulk CSV uses `-1` to inherit global). (`SpeciesDatabase.cs:63`) |
| `conditionRecoveryRate` | 0.10 | Per-species recovery speed. (`SpeciesDatabase.cs:64`) |
| `naturalDeathRate` | 0.02 | Flat background death fraction per step. (`SpeciesDatabase.cs:71`) |
| `naturalDeathVariance` | 0.01 | Plus/minus range on natural death. (`SpeciesDatabase.cs:73`) |
| `huntingEfficiency` | 0.75 | Tier 2: base hunting success. Tier 1: resource extraction efficiency (use 1.0). (`SpeciesDatabase.cs:78`) |
| `huntingVariance` | 0.15 | Plus/minus range on hunting (Tier 2 only). (`SpeciesDatabase.cs:80`) |
| `optimalTempK` | 297.0 | Peak-performance temperature, Kelvin. (`SpeciesDatabase.cs:95`) |
| `arrhenBreadth` | 8000 | Arrhenius breadth. (`SpeciesDatabase.cs:96`) |
| `arrhenLower` / `arrhenUpper` | 3000 / 35000 | Arrhenius shoulder constants. (`SpeciesDatabase.cs:97-98`) |
| `lowerBoundK` / `upperBoundK` | 296.0 / 298.0 | Arrhenius reference temperatures, Kelvin. (`SpeciesDatabase.cs:99-100`) |
| `pmax` | 0.65 | Peak height [0, 1]. (`SpeciesDatabase.cs:104-105`) |
| `ctMinC` / `ctMaxC` | 0.0 / 40.0 | Lethal limits, deg C. (`SpeciesDatabase.cs:106-109`) |
| `TemperatureDebuff` | 0.0 | Per-species experienced-temperature offset, deg C. (`SpeciesDatabase.cs:56`) |

**Species and variant identity.** A species carries both enum fields and free-text labels. `speciesLabel` and `variantLabel` are free-text strings used for output identity; when blank they fall back to the `speciesName` and `variant` enums (`SpeciesDatabase.cs:37-46`). The output display name of a species is its `FullName`: `Name_VariantLabel`, or just `Name` when the variant label is empty (`SimSpecies.cs:82-89`). The internal `SpeciesVariant` enum has seven values (Cold/Warm/Hot in Specialist and Generalist forms, plus Custom) (`SpeciesDatabase.cs:21-30`); the legacy `ThermalVariant` enum has four (Arctic/Common/Tropical/Custom) (`SimSpecies.cs:3`). These enums are used only for default-parameter lookup and never written to output CSV; all output identity flows through the free-text labels. The legacy names Arctic/Common/Tropical are accepted as aliases for Cold/Warm/Hot when resolving a label to its parameter bucket (`SpeciesDatabase.cs:187-209`).

When loading, species whose name and variant labels normalize to the same key (ignoring case, spaces, and punctuation) are merged into one, summing their populations (`EcosystemSimulator.cs:330-349`, match keys at `SpeciesDatabase.cs:216-253`).

The shipped default species list is `Assets/Resources/RunSpeciesList.asset`, referenced by the shipped config. The fallback hard-coded defaults (used only when no list is provided) are three Hexapod (Tier 1) and three Sheplik (Tier 2) variants (`EcosystemSimulator.cs:501-523`, factory methods `SimSpecies.cs:140-258`).

---

## 4. Running the simulation

### 4.1 Open the project

1. Install **Unity 6 (6000.3.8f1)**. This exact editor version is required.
2. Open the `tinysea/` folder as a Unity project (the repository root contains `tinysea/` and the unused `tinysea webiste/` folder; open `tinysea/`).
3. Unity loads `Assets/scenes/simulation.unity`, the only active scene.

There is no command-line build or test pipeline. The project compiles as standard Unity C# (Assembly-CSharp). The Unity Test Framework package is present but there are no test classes.

### 4.2 What a Run is

A **Run** is one configuration (one set of environment parameters plus one species list) executed `NumberOfScenarios` times, each time with a different random seed. Each individual execution is a **scenario**.

Plain language: because the temperature and the death/hunting rates contain randomness, a single execution is just one possible future. A Run repeats the same setup several times with different random seeds so you can see the spread of outcomes and compute statistics. Each repeat is a scenario; the Run is the whole batch of repeats for one configuration.

In code:

- A **scenario** is one `SimulationRunner.Run()` (`SimulationRunner.cs:392-459`) over `TotalDays` days, producing one `ScenarioResult` and one `scenario_N.csv`.
- A **Run** is driven by `SimulationController` (`SimulationController.cs`). It builds an `AggregateResults`, runs scenario 1..N (seed `RandomSeed + i`), and aggregates them (`SimulationController.cs:111-272`). Scenarios run in parallel on background threads on standalone/Editor and sequentially on WebGL (dormant) (`SimulationController.cs:170-259`).

To start a single-config Run from the Editor: select the `SimulationController` component and use its **Run Simulation** context-menu action (`SimulationController.cs:102-106`), or trigger `StartSimulation()` from the scene's UI (`SimulationController.cs:74-97`). The `SimulationConfig` inspector also offers context-menu actions: Run Simulation, Log Config, Export Config JSON, and Open Output Folder.

### 4.3 What a Bulk upload is

A **Bulk upload** is a single CSV file where **each row is one complete Run**. You upload the file, the app validates it, then it executes every row as its own Run (its own species list, environment, and `num_scenarios` repeats) and packages all results into one ZIP.

Plain language: instead of editing parameters in the Unity inspector and running one configuration at a time, you describe many configurations as rows in a spreadsheet, upload it once, and get back a ZIP containing every configuration's results. This is the intended workflow for sweeping across temperatures, species traits, or carrying capacities.

The Bulk workflow (`CsvUploadHandler.cs`, `BulkSimulationController.cs`, `CsvBatchParser.cs`):

1. Press **L** in the simulation scene to open a file dialog and choose a CSV. On Windows/macOS this is a native OS dialog (`CsvUploadHandler.cs:279-300`). A **Download Template** button writes a ready-to-edit template (`CsvUploadHandler.cs:308-341`).
2. The CSV is validated by `CsvBatchParser.TryParse` (`CsvBatchParser.cs:65-...`). All errors are collected and the first ten are shown; the run button only appears if the file is valid (`CsvUploadHandler.cs:158-193`).
3. On confirm, `BulkSimulationController` runs every row and streams results into a ZIP named `tinysea_bulk_<timestamp>.zip` (`BulkSimulationController.cs:80-...`, `BulkSimulationController.cs:134-135`).

**Bulk CSV format.** The header has 15 required global columns, 4 optional global columns, and per-species blocks prefixed `sp1_`, `sp2_`, ... The number of species is detected from the header (`CsvBatchParser.cs:97-112`). Required global columns (`CsvBatchParser.cs:18-27`):

```
batch_name, days, num_scenarios,
base_temp, seasonal_amp, climate_trend,
variability_mag, warming_bias,
daily_var_range, randomness_growth, autocorrelated,
interannual_variation,
temp_min, temp_max,
carrying_cap_t1
```

Optional global columns, with defaults applied when absent (`CsvBatchParser.cs:35-40`): `condition_drain_rate`, `condition_recovery_rate`, `temperature_timeseries_file`, and the deprecated `use_carrying_cap` (ignored with a warning; carrying capacity is always on).

Required per-species columns (each prefixed `spN_`) (`CsvBatchParser.cs:42-52`):

```
name, variant, tier, pop,
eating, repro_mult,
death_thresh, death_rate, repro_thresh,
natural_death_rate, natural_death_var,
hunt_eff, hunt_var,
opt_temp_c,
arrhen_breadth, arrhen_lower, arrhen_upper,
lower_bound_c, upper_bound_c
```

Optional per-species columns, with defaults (`CsvBatchParser.cs:55-59`): `pmax`, `ctmin`, `ctmax`, `temp_offset`, `condition_drain_rate`, `condition_recovery_rate`.

In the bulk CSV, temperatures are given in **Celsius** in the `*_c` and `opt_temp_c` columns and converted to Kelvin internally. **Every species row must have `tier = 0`**; a non-zero tier is rejected at parse with a clear error, because Bulk is Tier-1-only (`CsvBatchParser.cs:316-319`). The downloaded template mirrors the live default species list plus one fully custom example row, all `tier 0`, so a freshly downloaded template re-uploads cleanly (`CsvBatchParser.cs:436-522`).

### 4.4 Where output goes

On Windows and macOS standalone, results are written to a `TinySeaResults` folder. The location is next to the executable when writable, otherwise the OS per-user persistent data path (`SavePaths.cs:14-60`). In the Editor, output goes to the persistent data path. A single-config Run's "Download All (ZIP)" writes a folder named `tinysea_results_<timestamp>` (`ResultsScreenUI.cs:712-765`); Bulk writes per-run subfolders plus a `bulk_summary.csv` at the root.

---

## 5. Output CSV files

The simulation produces several CSV types. All "config" and "species" header lines in the per-scenario file begin with `#`, which R's `read.csv()` treats as comments and ignores by default, so the daily data parses directly.

### 5.1 Scenario CSV (`scenario_N.csv`) — one per scenario

This is the daily record of one scenario. Structure (`SimulationRunner.cs:738-807`):

1. **Config comment lines**, one per parameter, prefixed `#config:`, beginning with `#config:model_version,v12-per-species-tracking` (`SimulationRunner.cs:749-767`).
2. **Species table**, prefixed `#species:`, listing each species' initial parameters with temperatures in both Kelvin and Celsius (`SimulationRunner.cs:770-794`).
3. **Daily data**, one row per day, with a header row produced by `StepRecord.CsvHeader` (`SimulationRunner.cs:803-806`).

Each daily row begins with tier-level columns (`SimulationRunner.cs:264-313` for the header, `:209-249` for the row):

```
Day, Year, Temperature, BiologyCycle,
StartPop, EndPop,
Tier1Pop, [Tier2Pop,]
Tier{n}_{variantLabel} ... (one column per distinct tier+variant present),
EatenT1, TempDeathsT1, [TempDeathsT2,]
ConditionDeathsT1, [ConditionDeathsT2,]
NaturalDeathsT1, [NaturalDeathsT2,]
TotalDeaths,
BirthsT1, [BirthsT2,]
[FedRateT2, AvgHuntingEff,]
FedRateT1, FoodDensityT1,
AvgConditionT1, [AvgConditionT2,]
BirthAccumT1, [BirthAccumT2,]
NaturalDeathAccumT1, [NaturalDeathAccumT2,]
ConditionDeathAccumT1, [ConditionDeathAccumT2,]
PredationAccumT1,
ReproScaleT1 [, ReproScaleT2]
```

Columns in brackets are present only when Tier 2 is enabled (`SimulationRunner.cs:802`). The `Tier{n}_{label}` rollup columns are generated dynamically, one per distinct (tier, variant label) present in the run, sorted by tier then label, with name collisions disambiguated by a `_2`, `_3` suffix (`SimulationRunner.cs:159-199`). Plain meaning of the key tier-level columns: `Tier1Pop` is total prey population that day; `TempDeathsT1`/`ConditionDeathsT1`/`NaturalDeathsT1`/`EatenT1` are the day's deaths by cause; `BirthsT1` is the day's births; `FedRateT1` and `FoodDensityT1` are the average feeding satisfaction and the remaining food fraction; `AvgConditionT1` is the population-weighted average health; `ReproScaleT1` is the reproduction scale factor.

**Per-species columns.** After the tier-level columns, each species contributes **17 columns** named `{SanitizedFullName}_{Field}`, in `(Tier ascending, FullName ascending)` order (`SimulationRunner.cs:234-248`, header `:288-310`). The fields are:

```
Pop, Cond, ThermalPerf, FinalPerf, FedRate, HuntingEff,
Births, TempDeaths, CondDeaths, NatDeaths, Eaten,
BirthRate, ReproScale,
BirthAccum, NatDeathAccum, CondDeathAccum, PredAccum
```

`BirthRate` is births divided by start-of-day population (`PerSpeciesStepData` definition, `SimulationRunner.cs:20-48`). Names are ASCII-sanitized: any character outside `[A-Za-z0-9_]` becomes `_`, and a leading digit gets a `_` prefix (`SimulationRunner.cs:322-337`). **Tier-rollup invariant**: the per-species values sum to the matching `Tier{n}_{label}` rollup column, which sum to the `Tier{n}Pop` total.

After the daily rows, the scenario file appends a per-species summary statistics block (mean, max, min, standard deviation, extinction day) computed over the run (`SimulationRunner.cs:809-857` onward).

### 5.2 Aggregate CSV (`aggregate.csv`) — one per Run

This summarizes the scenarios within one Run. It uses `=== SECTION ===` headers and `# Key,Value` metadata lines, produced by `ScenarioResult.ToAggregateCsv()` (in `DataStructure/ScenarioResult.cs`, invoked at `ResultsScreenUI.cs:722`). It contains per-species population statistics (average, average across surviving scenarios only, min, max, extinction count, extinction rate) and a per-scenario table. The "final year" metrics use the last 365 days. Plain language: this file answers "across all the repeats of this configuration, how did each species typically do, how often did it go extinct, and how variable was it."

### 5.3 Config CSV (`config.csv`) — one per Run

A parameters-only file (no daily results) describing exactly how the Run was configured, in the same `=== SECTION ===` style with a species table. Produced by `ScenarioResult.ToConfigCsv()` (invoked at `ResultsScreenUI.cs:728`). It is also downloadable on its own via a "Download Config" button. Use it to record the precise inputs that generated a result set.

### 5.4 Bulk Summary CSV (`bulk_summary.csv`) — one per Bulk upload, at the ZIP root

This compares Runs against each other. Produced by `BulkSimulationController.GenerateBulkSummary` (`BulkSimulationController.cs:428` onward). It has a per-run results table (one row per CSV row, with each species' average population and the crash rate) and a per-species cross-run aggregate (grand mean and survived-only mean across Runs, plus extinction rate). Min and max are intentionally not reported at this level because they would be min/max of per-run averages rather than of real populations. Plain language: this is the top-level scoreboard across every configuration you uploaded.

### 5.5 ZIP layout

A Bulk upload yields one ZIP whose root holds `bulk_summary.csv` and one subfolder per Run (named by `batch_name`). Each Run subfolder holds that Run's `aggregate.csv`, `config.csv`, and one `scenario_N.csv` per scenario (`BulkSimulationController.cs:154`, `:228-232`, `:370-376`, `:386-390`). A single-config "Download All (ZIP)" yields `aggregate.csv`, `config.csv`, and the per-scenario files in one folder (`ResultsScreenUI.cs:717-741`).

---

## 6. Where the detailed developer documentation lives

In-depth developer documentation is under `tinysea/docs/`. Start at the documentation index:

- **`tinysea/docs/README.md`** — documentation index and entry point.

Key documents (all under `tinysea/docs/`):

| Document | Covers |
|---|---|
| `simulation-spec.md` | The authoritative biology specification: the ten-step daily sequence, exact formulas, and invariants. |
| `biology-and-formulas.md` | Detailed derivations of the thermal curve, Condition system, feeding, reproduction, and death formulas. |
| `temperature-model.md` | The five-component temperature model and the optional timeseries override. |
| `architecture-overview.md` | How the classes fit together: controllers, runner, simulator, data structures, UI. |
| `data-structures.md` | `SimSpecies`, `SpeciesData`, `StepRecord`, `SimulationConfig`, and related types field by field. |
| `configuration-reference.md` | Every configuration parameter, default, range, and validation rule. |
| `run-scenario-batch.md` | The Run and scenario execution paths and terminology. |
| `bulk-system.md` | The Bulk CSV upload format, parser, and output packaging. |
| `csv-output-formats.md` | Exact column layouts of every CSV the simulation writes. |
| `ui-and-io.md` | The simulation UI, file dialogs, downloads, and disk/output locations. |

If any document disagrees with the code, the code in `tinysea/Assets/scripts/Simulation/` and the asset files in `tinysea/Assets/` are authoritative.

---

## 7. Repository layout

```
tinysea/                        Unity 6 project (open this in the editor)
  Assets/
    scenes/simulation.unity      The only active scene
    scripts/Simulation/          Headless simulation engine and UI (the active code)
      EcosystemSimulator.cs       Ten-step daily biology
      SimulationRunner.cs         Per-scenario day loop and CSV writer
      SimulationController.cs      Single-config Run orchestration
      TemperatureCalculator.cs    Temperature model
      SimSpecies.cs               Per-species simulation state and thermal curve
      BulkSimulationController.cs  Bulk upload orchestration and ZIP packaging
      CsvBatchParser.cs           Bulk CSV parser, validator, template generator
      CsvUploadHandler.cs         Bulk upload UI and file dialogs
      SavePaths.cs                Output folder resolution
      DataStructure/              SimulationConfig, SpeciesDatabase/Data, RunSpeciesList, ScenarioResult
      UI/                         Results screen, species editor, thermal graph UI
    Resources/
      SimulationConfig.asset       Shipped single-config defaults
      RunSpeciesList.asset         Shipped default species list
      SpeciesDatabase.asset        Species data store
    Settings/Build Profiles/       Windows.asset, macOS.asset (active), Web (canceled)
    scripts/                       Deprecated interactive game (not built)
  docs/                            Developer documentation (see section 6)
tinysea webiste/                  Unused PHP site (folder name has a typo)
```

---

This README describes the simulation as implemented in the C# source and the shipped asset files. For exact behavior, read the cited code.
