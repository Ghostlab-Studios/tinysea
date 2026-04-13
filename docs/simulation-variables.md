# TinySea Simulation Variable Reference

Complete reference for every configurable parameter, runtime variable, constant, and output field in the simulation system. Cross-referenced with source code as of the current `simulation` branch.

---

## Table of Contents

1. [SimulationConfig](#simulationconfig) - Top-level simulation parameters
2. [Species Parameters](#species-parameters) - Per-species biology & thermal data
3. [Temperature Model](#temperature-model) - Temperature calculation parameters
4. [Ecosystem Simulator](#ecosystem-simulator) - Runtime state & constants
5. [Bulk CSV Import](#bulk-csv-import) - Bulk batch configuration fields
6. [Output: Scenario CSV](#output-scenario-csv) - Per-day step record columns
7. [Output: ScenarioResult](#output-scenarioresult) - Per-scenario summary stats
8. [Output: AggregateResults](#output-aggregateresults) - Cross-scenario aggregate stats
9. [Output: Bulk Summary](#output-bulk-summary) - Cross-run bulk stats
10. [Output: SimulationSummary](#output-simulationsummary) - Runner-level summary
11. [Enums](#enums) - All enum types
12. [Constants](#constants) - All hardcoded constants

---

## SimulationConfig

**Source:** `Assets/scripts/Simulation/DataStructure/SimulationConfig.cs`
**Type:** ScriptableObject (editable in Unity Inspector)

### Simulation Timing

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| `BiologyStep` | int | 1 | 1-5 | Days between biology calculations. 1 = daily (most accurate), 5 = original game behavior. |
| `DaysPerScenario` | int | 365 | 1-182500 | Number of days per scenario. 35 = quick test, 365 = 1 year, 3650 = 10 years. |
| `NumberOfScenarios` | int | 5 | 1-100 | How many times to run the scenario with different random seeds. Enables statistical analysis. |

### Carrying Capacity

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| `UseCarryingCapacity` | bool | true | | Enable soft population limit for Tier 1. Reduces birth rate as population approaches the limit. Does NOT kill creatures. Formula: `births = rawBirths * (1 - tierPop/capacity)`. |
| `CarryingCapacityTier1` | float | 5000 | 100-100000 | Maximum sustainable Tier 1 population. At 50% capacity, birth rate is halved. At 100%, births stop. |

### Condition (Health) System

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| `ConditionDrainRate` | float | 0.15 | 0.01-1.0 | How fast Condition drains toward poor performance. 0.15 = ~8 days from full health to death threshold at suboptimal temps. Drain accelerates up to 5x near lethal limits. |
| `ConditionRecoveryRate` | float | 0.10 | 0.01-1.0 | How fast Condition recovers toward good performance. Intentionally slower than drain (asymmetric recovery). 0.10 = ~10 good days to fully recover. |

### Temperature: Base & Seasonal

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `BaseTemperature` | float | 20 | Base/mean temperature in Celsius. |
| `SeasonalAmplitude` | float | 10 | Amplitude of seasonal variation. Produces a sinusoidal summer/winter swing of +/- this value. |

### Temperature: Climate Trend

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ClimateTrend` | float | 1 | Degrees Celsius warming per year (linear climate change). |
| `InterannualVariation` | bool | true | Enable year-to-year random temperature variation. |

### Temperature: Interannual Variation

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `VariabilityMagnitude` | float | 2 | Magnitude of year-to-year temperature variation. Each year gets a random offset drawn from this range. |
| `WarmingBias` | float | 1.5 | Bias towards warmer years (positive skew). Higher = warm years more likely than cold years. |

### Temperature: Daily Variation

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Autocorrelated` | bool | true | Enable autocorrelated (smooth) daily variation. When true, each day's random variation is 70% of yesterday's + 30% new random, creating realistic weather transitions. |
| `DailyVariationRange` | float | 5 | Base daily random variation range in Celsius. |
| `RandomnessGrowthRate` | float | 0.5 | How much daily randomness increases per year. Models increasing climate instability. |

### Temperature: Bounds

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `TemperatureBoundsMin` | float | -5 | Minimum possible temperature (hard floor, Celsius). |
| `TemperatureBoundsMax` | float | 50 | Maximum possible temperature (hard ceiling, Celsius). |

### Species & Randomness

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `RunSpecies` | RunSpeciesList | | Runtime species list ScriptableObject. Contains the actual species that will be simulated. |
| `RandomSeed` | int | 12345 | Base random seed. -1 = use system time (non-reproducible). Any other value = reproducible results. Each scenario uses `BaseSeed + ScenarioIndex`. |

---

## Species Parameters

Species are defined in two parallel systems that share the same fields.

### SimSpecies (Simulation Runtime)

**Source:** `Assets/scripts/Simulation/SimSpecies.cs`
Used by the simulation engine during execution. Population stored as float for fractional precision.

### SpeciesData (Database/Inspector)

**Source:** `Assets/scripts/Simulation/DataStructure/SpeciesDatabase.cs`
Used for Unity Inspector editing and stored in ScriptableObjects. Mapped to `SimSpecies` at simulation start.

**Note:** `SpeciesData.tier` uses 0-indexed (0 = prey, 1 = predator), while `SimSpecies.Tier` uses 1-indexed (1 = prey, 2 = predator).

### Identity

| SimSpecies Field | SpeciesData Field | Type | Description |
|-----------------|------------------|------|-------------|
| `Name` | `displayName` / `speciesName` | string / enum | Species name. SpeciesData has both an enum (`SpeciesName`) and display string. |
| `Variant` | `variant` | enum | Thermal variant: Arctic, Common, Tropical, or Custom. |
| `Tier` | `tier` | int | Food chain level. SimSpecies: 1 = prey, 2 = predator. SpeciesData: 0 = prey, 1 = predator. |
| `Population` | `count` | float / int | Initial population. SimSpecies uses float for fractional tracking during simulation. |
| *(computed)* `FullName` | | string | `"{Name}_{Variant}"` (e.g., "Hexapod_Arctic"). Used as dictionary key in results. |

### Biological Parameters

| Field | Type | T1 Default | T2 Default | Description |
|-------|------|-----------|-----------|-------------|
| `EatingAmount` | float | 0 | 1.5 | Prey consumed per creature per biology step. Tier 1 = 0 (they don't eat other species). |
| `ReproductionMultiplier` | float | 0.45 | 0.1 | Birth rate multiplier. T2 reproduces 4.5x slower than T1. |
| `DeathThreshold` | float | 0.3 | 0.3 | Condition value below which condition deaths begin. Threshold-gated: no deaths above this. |
| `DeathRate` | float | 0.6 | 0.3 | Maximum fraction of population dying per day when below DeathThreshold. T1 higher due to allometric scaling (smaller prey have less physiological buffering). |
| `ReproThreshold` | float | 0.25 | 0.25 | Condition inflection point for reproduction curve. Above: healthy ramp (0.10 to 1.0). Below: struggling but non-zero (0 to 0.10). Not a gate -- reproduction occurs whenever Condition > 0. |

### Natural Mortality

| Field | Type | T1 Default | T2 Default | Description |
|-------|------|-----------|-----------|-------------|
| `NaturalDeathRate` | float | 0.02 (2%) | 0.01 (1%) | Base natural death rate per biology step. Represents old age, disease, accidents. Always active, independent of performance. T2 lower due to allometric scaling (larger animals have lower background mortality). |
| `NaturalDeathVariance` | float | 0.01 (+-1%) | 0.005 (+-0.5%) | Random daily fluctuation around the base rate. Actual rate on any day = base +/- variance. |

### Hunting Efficiency (Tier 2 Only)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `HuntingEfficiency` | float | 0.75 (75%) | Base hunting success rate at normal prey:predator ratio (20:1). Scaled by Holling Type II functional response. T1 ignores this. |
| `HuntingVariance` | float | 0.15 (+-15%) | Random daily variance in hunting success. |

### Thermal Curve Parameters

All temperature parameters stored in Kelvin internally. Celsius values are computed for display (`K - 273.15`).

| Field | Type | Description |
|-------|------|-------------|
| `OptimalTempK` | float | Temperature in Kelvin where the species performs best. The peak of the Arrhenius curve. |
| `ArrhenBreadth` | float | Arrhenius breadth parameter. Controls the width of the thermal performance curve. |
| `ArrhenLower` | float | Arrhenius lower parameter. Controls the left (cold) side drop-off rate. |
| `ArrhenUpper` | float | Arrhenius upper parameter. Controls the right (warm) side drop-off rate. |
| `LowerBoundK` | float | Lower thermal bound in Kelvin. Inflection point for cold-side performance decline. |
| `UpperBoundK` | float | Upper thermal bound in Kelvin. Inflection point for warm-side performance decline. |

### Peak Height & Lethal Limits

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Pmax` | float | 0.65 (Common), 0.85 (Arctic/Tropical), 1.0 (SimSpecies factory) | Maximum performance at optimal temperature (0-1). Scales the Arrhenius output. A Pmax of 0.65 means even at the perfect temperature, performance caps at 65%. |
| `CTminC` | float | -5 to 0 (varies) | Critical thermal minimum in Celsius. At or below this temperature, performance = 0 (instant death). A smooth cosine fade of 2 degrees applies near the boundary. |
| `CTmaxC` | float | 20 to 80 (varies) | Critical thermal maximum in Celsius. At or above this temperature, performance = 0 (instant death). Same cosine fade applies. |
| `TemperatureDebuff` | float | 0 | Per-species temperature offset. Shifts the experienced temperature before thermal calculation. Positive = species "feels" warmer than ambient. |

### Runtime Values (Calculated Each Step)

These are set by `EcosystemSimulator` during the biology sequence. Not configurable.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `RawThermalPerformance` | float | | Arrhenius curve output with CTmin/CTmax cosine fade applied, WITHOUT Pmax scaling. Range: 0-1. |
| `ThermalPerformance` | float | | `RawThermalPerformance * Pmax`. Used for reproduction calculations. |
| `FedRate` | float | 1.0 | Feeding satisfaction (0-1). Tier 1 always 1.0. Tier 2 depends on hunting success and prey availability. |
| `RawFinalPerformance` | float | | `RawThermalPerformance * FedRate`. Target for Condition drain/recovery. Used for death checks (without Pmax). |
| `FinalPerformance` | float | | `ThermalPerformance * FedRate`. Used for reproduction threshold and birth calculations. |
| `CurrentHuntingSuccess` | float | | This step's actual hunting success rate after Holling Type II + variance. For tracking/output only. |
| `Condition` | float | 1.0 | Health/energy reserves [0-1]. Starts at 1.0. Drains toward RawFinalPerformance when environment is poor, recovers when good. Drives both reproduction scaling and condition death. |

---

## Temperature Model

**Source:** `Assets/scripts/Simulation/TemperatureCalculator.cs`

Formula: `T(day) = Base + Seasonal + Trend + InterannualVar + DailyVar`, then clamped to bounds.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `BaseTemperature` | float | 20 | Starting temperature in Celsius. |
| `SeasonalAmplitude` | float | 10 | Seasonal swing via `sin(2*PI*day/365) * amplitude`. Coldest at day 0, warmest at day 182. |
| `ClimateTrendPerYear` | float | 1 | Linear warming: `trend * (day / 365)`. |
| `VariabilityMagnitude` | float | 2 | Year-to-year random offset range. Each year gets one value, constant for all 365 days. |
| `WarmingBias` | float | 1.5 | Skews interannual variation warm. Formula: `(coldPart + warmPart*bias) / 2`. |
| `BaseRandomness` | float | 5 | Daily random variation range in Celsius. |
| `RandomnessGrowthRate` | float | 0.5 | Daily randomness increases per year: `currentRandomness = base + growth * yearNumber`. |
| `UseAutocorrelation` | bool | true | When true: `variation = 0.7 * yesterday + 0.3 * newRandom` (smooth weather). When false: pure random each day. |
| `MinTemp` | float | -5 | Hard floor -- temperature never goes below this. |
| `MaxTemp` | float | 40 | Hard ceiling -- temperature never exceeds this. |

**Constant:** `DAYS_PER_YEAR` = 365

---

## Ecosystem Simulator

**Source:** `Assets/scripts/Simulation/EcosystemSimulator.cs`

### Configurable Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BiologyStep` | int | 1 | Days between biology calculations (mirrors SimulationConfig). |
| `UseCarryingCapacity` | bool | true | Enable soft Tier 1 population limit. |
| `CarryingCapacityPerTier` | float | 5000 | Tier 1 population limit. |
| `ConditionDrainRate` | float | 0.15 | How fast Condition drains toward poor performance. |
| `ConditionRecoveryRate` | float | 0.10 | How fast Condition recovers toward good performance. |

### Population Tracking (Read-Only, Per Step)

| Property | Type | Description |
|----------|------|-------------|
| `StartPopT1` | float | Tier 1 population at start of this biology step. |
| `StartPopT2` | float | Tier 2 population at start of this biology step. |
| `EndPopT1` | float | Tier 1 population at end of this biology step. |
| `EndPopT2` | float | Tier 2 population at end of this biology step. |

### Death/Birth Tracking (Read-Only, Per Step)

| Property | Type | Description |
|----------|------|-------------|
| `LastEatenT1` | float | Prey eaten this step (Tier 1 deaths from predation). |
| `LastTempDeathsT1` | float | Thermal deaths Tier 1 (instant kill at lethal limits). |
| `LastTempDeathsT2` | float | Thermal deaths Tier 2. |
| `LastConditionDeathsT1` | float | Condition deaths Tier 1 (chronic stress). |
| `LastConditionDeathsT2` | float | Condition deaths Tier 2. |
| `LastNaturalDeathsT1` | float | Natural deaths Tier 1 (flat rate). |
| `LastNaturalDeathsT2` | float | Natural deaths Tier 2. |
| `LastBirthsT1` | float | Births Tier 1 this step. |
| `LastBirthsT2` | float | Births Tier 2 this step. |
| `LastFedRateT2` | float | Tier 2 feeding satisfaction this step. |
| `LastAvgHuntingEfficiency` | float | Average hunting success rate this step. |
| `LastReproScaleT1` | float | Condition-based reproduction scale factor for T1 [0-1]. |
| `LastReproScaleT2` | float | Condition-based reproduction scale factor for T2 [0-1]. |

### Accumulator Totals (Read-Only, For CSV Output)

Accumulators carry fractional remainders between steps so no births or deaths are lost to rounding.

| Property | Type | Description |
|----------|------|-------------|
| `BirthAccumT1` | float | Birth accumulator residual for Tier 1. |
| `BirthAccumT2` | float | Birth accumulator residual for Tier 2. |
| `NaturalDeathAccumT1` | float | Natural death accumulator residual for Tier 1. |
| `NaturalDeathAccumT2` | float | Natural death accumulator residual for Tier 2. |
| `PredationAccumT1` | float | Predation accumulator residual for Tier 1. |
| `ConditionDeathAccumT1` | float | Condition death accumulator residual for Tier 1. |
| `ConditionDeathAccumT2` | float | Condition death accumulator residual for Tier 2. |

### Condition Tracking

| Property | Type | Description |
|----------|------|-------------|
| `AvgConditionT1` | float | Population-weighted average Condition for Tier 1. |
| `AvgConditionT2` | float | Population-weighted average Condition for Tier 2. |

### Biology Sequence (10 Steps)

Executed each biology step in this order:

1. **Thermal Performance** - Arrhenius formula with CTmin/CTmax cosine fade
2. **Feeding/Predation** - Holling Type II functional response + predation accumulator
3. **Raw Final Performance** - `RawThermalPerf * FedRate` (Condition drain target)
4. **Update Condition** - Drain or recover toward RawFinalPerformance
5. **Final Performance** - `ThermalPerf * FedRate` (Condition NOT used here)
6. **Thermal Death** - Instant kill when `RawThermalPerformance == 0`
7. **Condition Death** - Graduated severity when `Condition < DeathThreshold`, survivor fitness boost
8. **Reproduction** - Condition-based graduated scale + birth accumulator + carrying capacity
9. **Natural Death** - Flat rate + natural death accumulator
10. **Population Rounding** - All populations become integers (floor)

---

## Bulk CSV Import

**Source:** `Assets/scripts/Simulation/BulkBatchConfig.cs`

Each row in a bulk CSV file defines one run. These are the parsed column names.

### BulkBatchConfig (Per-Run Environment)

| Field | CSV Column | Type | Default | Description |
|-------|-----------|------|---------|-------------|
| `BatchName` | `batch_name` | string | | Name/label for this run. |
| `Days` | `days` | int | | Days per scenario. |
| `NumScenarios` | `num_scenarios` | int | | Number of scenarios per run. |
| `BaseTemp` | `base_temp` | float | | Base temperature (Celsius). |
| `SeasonalAmp` | `seasonal_amp` | float | | Seasonal amplitude. |
| `ClimateTrend` | `climate_trend` | float | | Climate warming per year. |
| `VariabilityMag` | `variability_mag` | float | | Year-to-year variation magnitude. |
| `WarmingBias` | `warming_bias` | float | | Warm year bias. |
| `DailyVarRange` | `daily_var_range` | float | | Daily variation range. |
| `RandomnessGrowth` | `randomness_growth` | float | | Daily randomness growth per year. |
| `Autocorrelated` | `autocorrelated` | bool | | Enable smooth daily transitions. |
| `InterannualVariation` | `interannual_variation` | bool | | Enable year-to-year variation. |
| `TempMin` | `temp_min` | float | | Temperature floor. |
| `TempMax` | `temp_max` | float | | Temperature ceiling. |
| `UseCarryingCap` | `use_carrying_cap` | bool | | Enable carrying capacity. |
| `CarryingCapT1` | `carrying_cap_t1` | float | | Tier 1 carrying capacity. |
| `ConditionDrainRate` | `condition_drain_rate` | float | 0.15 | Condition drain rate (optional column, backward compatible). |
| `ConditionRecoveryRate` | `condition_recovery_rate` | float | 0.10 | Condition recovery rate (optional column, backward compatible). |

### BulkSpeciesConfig (Per-Species, Prefixed)

Species columns use a prefix pattern: `sp1_`, `sp2_`, `sp3_`, etc. All temperatures in Celsius.

| Field | CSV Suffix | Type | Default | Description |
|-------|-----------|------|---------|-------------|
| `Name` | `name` | string | | Species name (e.g., "Hexapod"). |
| `Variant` | `variant` | string | | Variant name: "Arctic", "Common", "Tropical", or "Custom". |
| `Tier` | `tier` | int | | 0 = Tier 1 (prey), 1 = Tier 2 (predator). |
| `Pop` | `pop` | int | | Initial population. |
| `Eating` | `eating` | float | | Prey consumed per creature per step. |
| `ReproMult` | `repro_mult` | float | | Reproduction multiplier. |
| `DeathThresh` | `death_thresh` | float | | Condition death threshold. |
| `DeathRate` | `death_rate` | float | | Condition death rate. |
| `ReproThresh` | `repro_thresh` | float | | Reproduction threshold (Condition inflection). |
| `NaturalDeathRate` | `natural_death_rate` | float | | Base natural death rate. |
| `NaturalDeathVar` | `natural_death_var` | float | | Natural death variance. |
| `HuntEff` | `hunt_eff` | float | | Base hunting efficiency. |
| `HuntVar` | `hunt_var` | float | | Hunting variance. |
| `OptTempC` | `opt_temp_c` | float | | Optimal temperature (Celsius). Converted to Kelvin (+273.15) internally. |
| `ArrhenBreadth` | `arrhen_breadth` | float | | Arrhenius breadth parameter. |
| `ArrhenLower` | `arrhen_lower` | float | | Arrhenius lower parameter. |
| `ArrhenUpper` | `arrhen_upper` | float | | Arrhenius upper parameter. |
| `LowerBoundC` | `lower_bound_c` | float | | Lower thermal bound (Celsius). Converted to Kelvin internally. |
| `UpperBoundC` | `upper_bound_c` | float | | Upper thermal bound (Celsius). Converted to Kelvin internally. |
| `Pmax` | `pmax` | float | 1.0 | Peak performance height. |
| `CTminC` | `ctmin_c` | float | -5.0 | Critical thermal minimum (Celsius). |
| `CTmaxC` | `ctmax_c` | float | 50.0 | Critical thermal maximum (Celsius). |
| `TempOffset` | `temp_offset` | float | 0 | Per-species temperature offset. |

---

## Output: Scenario CSV

**Source:** `Assets/scripts/Simulation/SimulationRunner.cs` (`StepRecord` class)

Each scenario produces a CSV with `#config:` comment lines, a `#species:` table, then daily step data.

### Step Record Columns

| Column | Type | Description |
|--------|------|-------------|
| `Day` | int | Simulation day (1-based). |
| `Year` | int | Year number (1-based, year = day/365 + 1). |
| `Temperature` | float | Temperature for this day (Celsius, F2 format). |
| `BiologyCycle` | int | Biology cycle counter. Increments only on days where biology runs. |
| `StartPop` | long | Total population (T1+T2) at start of step. |
| `EndPop` | long | Total population (T1+T2) at end of step. |
| `Tier1Pop` | long | Tier 1 population at end of step. |
| `Tier2Pop` | long | Tier 2 population at end of step. |
| `Tier1Arctic` | long | Tier 1 Arctic variant population. |
| `Tier1Common` | long | Tier 1 Common variant population. |
| `Tier1Tropical` | long | Tier 1 Tropical variant population. |
| `Tier1Custom` | long | Tier 1 Custom variant(s) population (sum of all custom T1 species). |
| `Tier2Arctic` | long | Tier 2 Arctic variant population. |
| `Tier2Common` | long | Tier 2 Common variant population. |
| `Tier2Tropical` | long | Tier 2 Tropical variant population. |
| `Tier2Custom` | long | Tier 2 Custom variant(s) population (sum of all custom T2 species). |
| `EatenT1` | long | Prey eaten this step (T1 deaths from predation). |
| `TempDeathsT1` | long | Thermal deaths T1 (instant kill at lethal limits). |
| `TempDeathsT2` | long | Thermal deaths T2. |
| `ConditionDeathsT1` | long | Condition deaths T1 (chronic stress below threshold). |
| `ConditionDeathsT2` | long | Condition deaths T2. |
| `NaturalDeathsT1` | long | Natural deaths T1 (flat rate). |
| `NaturalDeathsT2` | long | Natural deaths T2. |
| `TotalDeaths` | long | All deaths combined (predation + thermal + condition + natural, both tiers). |
| `BirthsT1` | long | New Tier 1 offspring this step. |
| `BirthsT2` | long | New Tier 2 offspring this step. |
| `FedRateT2` | float | Tier 2 feeding satisfaction (F3 format, 0-1). |
| `AvgHuntingEff` | float | Average hunting success rate (F3 format). |
| `AvgConditionT1` | float | Population-weighted average Condition for T1 (F3 format). |
| `AvgConditionT2` | float | Population-weighted average Condition for T2 (F3 format). |
| `BirthAccumT1` | float | Birth accumulator residual T1 (F3 format). |
| `BirthAccumT2` | float | Birth accumulator residual T2 (F3 format). |
| `NaturalDeathAccumT1` | float | Natural death accumulator residual T1 (F3 format). |
| `NaturalDeathAccumT2` | float | Natural death accumulator residual T2 (F3 format). |
| `ConditionDeathAccumT1` | float | Condition death accumulator residual T1 (F3 format). |
| `ConditionDeathAccumT2` | float | Condition death accumulator residual T2 (F3 format). |
| `PredationAccumT1` | float | Predation accumulator residual T1 (F3 format). |
| `ReproScaleT1` | float | Condition-based reproduction scale T1 [0-1] (F3 format). |
| `ReproScaleT2` | float | Condition-based reproduction scale T2 [0-1] (F3 format). |

### Config Comment Lines

Embedded as `#config:key,value` at the top of each scenario CSV. R's `read.csv()` ignores `#` lines.

| Key | Description |
|-----|-------------|
| `days_per_scenario` | Days simulated |
| `number_of_scenarios` | Total scenarios in this run |
| `scenario_index` | This scenario's 1-based index |
| `random_seed` | Seed used for this scenario |
| `biology_step` | Biology step interval |
| `use_carrying_capacity` | Carrying capacity enabled |
| `carrying_capacity_tier1` | T1 capacity limit |
| `condition_drain_rate` | Condition drain rate |
| `condition_recovery_rate` | Condition recovery rate |
| `base_temperature` | Base temp (Celsius) |
| `seasonal_amplitude` | Seasonal swing |
| `climate_trend` | Warming per year |
| `interannual_variation` | Year-to-year variation enabled |
| `variability_magnitude` | Interannual variation range |
| `warming_bias` | Warm year bias |
| `autocorrelated` | Smooth daily variation enabled |
| `daily_variation_range` | Daily variation range |
| `randomness_growth_rate` | Randomness growth per year |
| `temperature_bounds_min` | Temp floor |
| `temperature_bounds_max` | Temp ceiling |

---

## Output: ScenarioResult

**Source:** `Assets/scripts/Simulation/DataStructure/ScenarioResult.cs`

One per scenario. Contains summary stats and the full CSV string.

### Identity & Outcome

| Field | Type | Description |
|-------|------|-------------|
| `ScenarioIndex` | int | 1-based index within the run. |
| `RandomSeed` | int | Seed used for this scenario. |
| `TotalDays` | int | Days simulated. |
| `BiologyCycles` | int | Number of biology steps executed. |
| `Crashed` | bool | Did all populations go to zero? (Note: single-tier extinction is NOT a crash.) |
| `CrashDay` | int | Day of total extinction (-1 if no crash). |
| `CrashTier` | int | Which tier triggered the crash check (-1 if no crash). |

### Final Populations

| Field | Type | Description |
|-------|------|-------------|
| `FinalTier1Pop` | long | Final Tier 1 total population. |
| `FinalTier2Pop` | long | Final Tier 2 total population. |
| `FinalTier1Arctic` | long | Final T1 Arctic population. |
| `FinalTier1Common` | long | Final T1 Common population. |
| `FinalTier1Tropical` | long | Final T1 Tropical population. |
| `FinalTier1Custom` | long | Final T1 Custom population (sum of all custom T1). |
| `FinalTier2Arctic` | long | Final T2 Arctic population. |
| `FinalTier2Common` | long | Final T2 Common population. |
| `FinalTier2Tropical` | long | Final T2 Tropical population. |
| `FinalTier2Custom` | long | Final T2 Custom population (sum of all custom T2). |
| `FinalSpeciesPopulations` | Dict<string, long> | Per-species final populations keyed by FullName (e.g., "Hexapod_Arctic", "Coral_Custom"). |

### Population Extremes (Across All Days)

| Field | Type | Description |
|-------|------|-------------|
| `MaxTier1Pop` | long | Maximum T1 population observed during this scenario. |
| `MinTier1Pop` | long | Minimum T1 population observed. |
| `MaxTier2Pop` | long | Maximum T2 population observed. |
| `MinTier2Pop` | long | Minimum T2 population observed. |

### Temperature Stats

| Field | Type | Description |
|-------|------|-------------|
| `AvgTemperature` | float | Average temperature across all days. |
| `MinTemperature` | float | Coldest day. |
| `MaxTemperature` | float | Warmest day. |

### Condition Stats

| Field | Type | Description |
|-------|------|-------------|
| `AvgConditionT1` | float | Average Condition for T1 across all days. |
| `AvgConditionT2` | float | Average Condition for T2 across all days. |
| `FinalConditionT1` | float | Condition T1 on the last day. |
| `FinalConditionT2` | float | Condition T2 on the last day. |

### Per-Column Population Statistics

Keyed by column name (e.g., "Tier1Pop", "Tier1Arctic", etc.). See `PopColumns` and `VariantColumns` arrays.

| Field | Type | Description |
|-------|------|-------------|
| `PopMean` | Dict<string, double> | Mean population per column across all days. |
| `PopMax` | Dict<string, long> | Maximum population per column. |
| `PopMin` | Dict<string, long> | Minimum population per column. |
| `PopStdDev` | Dict<string, double> | Standard deviation per column. |
| `ExtinctionDay` | Dict<string, int> | Day each variant first reached 0 (-1 if it survived). |

### Static Column Arrays

| Array | Contents |
|-------|----------|
| `PopColumns` | "Tier1Pop", "Tier2Pop", "Tier1Arctic", "Tier1Common", "Tier1Tropical", "Tier1Custom", "Tier2Arctic", "Tier2Common", "Tier2Tropical", "Tier2Custom" |
| `VariantColumns` | "Tier1Arctic", "Tier1Common", "Tier1Tropical", "Tier1Custom", "Tier2Arctic", "Tier2Common", "Tier2Tropical", "Tier2Custom" |

---

## Output: AggregateResults

**Source:** `Assets/scripts/Simulation/DataStructure/ScenarioResult.cs`

Aggregates across all scenarios in one run. Computed by `CalculateAggregates()`.

### Run Info

| Field | Type | Description |
|-------|------|-------------|
| `TotalScenarios` | int | Total scenarios run. |
| `CompletedScenarios` | int | Scenarios completed (currently always = Total). |
| `SurvivedScenarios` | int | Scenarios that did NOT crash. |
| `CrashedScenarios` | int | Scenarios that crashed (total extinction). |
| `CompletedAt` | DateTime | Timestamp when aggregation completed. |

### Configuration (Stored for Export)

All `SimulationConfig` fields are copied here for config export:
`DaysPerScenario`, `BiologyStep`, `RandomSeed`, `UseCarryingCapacity`, `CarryingCapacity`, `ConditionDrainRate`, `ConditionRecoveryRate`, `BaseTemperature`, `SeasonalAmplitude`, `ClimateTrend`, `InterannualVariation`, `VariabilityMagnitude`, `WarmingBias`, `Autocorrelated`, `DailyVariationRange`, `RandomnessGrowthRate`, `TemperatureBoundsMin`, `TemperatureBoundsMax`, `RunSpecies`.

### Population Averages (Survived Scenarios Only)

| Field | Type | Description |
|-------|------|-------------|
| `AvgFinalTier1Pop` | float | Average final T1 population (survived scenarios only). |
| `AvgFinalTier2Pop` | float | Average final T2 population (survived scenarios only). |
| `MinFinalTier1Pop` | float | Minimum final T1 population across survived scenarios. |
| `MaxFinalTier1Pop` | float | Maximum final T1 population across survived scenarios. |
| `MinFinalTier2Pop` | float | Minimum final T2 population across survived scenarios. |
| `MaxFinalTier2Pop` | float | Maximum final T2 population across survived scenarios. |

### Crash Stats

| Field | Type | Description |
|-------|------|-------------|
| `CrashRate` | float | Fraction of scenarios that crashed (0-1). |
| `AvgCrashDay` | float | Average day of crash (crashed scenarios only). |

### Condition Stats

| Field | Type | Description |
|-------|------|-------------|
| `AvgConditionT1` | float | Grand mean of per-scenario average Condition T1 (ALL scenarios, crashed + survived). |
| `AvgConditionT2` | float | Grand mean of per-scenario average Condition T2 (ALL scenarios). |
| `AvgFinalConditionT1` | float | Mean final Condition T1 (survived scenarios only). |
| `AvgFinalConditionT2` | float | Mean final Condition T2 (survived scenarios only). |

### Per-Species Population Stats

Keyed by species `FullName` (e.g., "Hexapod_Arctic", "Coral_Custom"). Computed across ALL scenarios (including crashed).

| Field | Type | Description |
|-------|------|-------------|
| `PerSpeciesAvg` | Dict<string, float> | Average final population per species (all scenarios). |
| `PerSpeciesMin` | Dict<string, float> | Minimum final population per species. |
| `PerSpeciesMax` | Dict<string, float> | Maximum final population per species. |
| `PerSpeciesExtinct` | Dict<string, int> | Number of scenarios where species went extinct (final pop = 0). |
| `PerSpeciesSurvived` | Dict<string, int> | Number of scenarios where species survived (final pop > 0). |
| `PerSpeciesSurvivedAvg` | Dict<string, float> | Average population only across scenarios where the species survived. |

---

## Output: Bulk Summary

**Source:** `Assets/scripts/Simulation/BulkSimulationController.cs` (nested struct `BulkRunSummary`)

One per run (CSV row) in a bulk upload. Collected into `bulk_summary.csv` at the ZIP root.

| Field | Type | Description |
|-------|------|-------------|
| `BatchName` | string | Name/label from the CSV row. |
| `NumScenarios` | int | Total scenarios in this run. |
| `Survived` | int | Scenarios that survived. |
| `Crashed` | int | Scenarios that crashed. |
| `BaseTemp` | float | Base temperature used. |
| `ClimateTrend` | float | Climate trend used. |
| `AvgSpeciesPop` | Dict<string, float> | Average final population per species across all scenarios. |
| `SurvivedSpeciesPop` | Dict<string, float> | Average final population per species only from survived scenarios. |

### Bulk Summary Aggregation Notes

- **GrandMean** = mean of per-run averages across all runs in the bulk
- **SurvivedMean** = mean of per-run survived averages across runs where the species had any presence
- **Extinction Rate** = per-species, computed from per-run data
- **Min/Max are NOT valid** at the bulk summary level (they would be min/max of averages, not real population values)

---

## Output: SimulationSummary

**Source:** `Assets/scripts/Simulation/SimulationRunner.cs`

Lightweight summary returned by `SimulationRunner.GetSummary()` after a single scenario completes.

| Field | Type | Description |
|-------|------|-------------|
| `TotalDays` | int | Days simulated. |
| `TotalBiologyCycles` | int | Biology cycles executed. |
| `Crashed` | bool | Did total extinction occur? |
| `CrashDay` | int | Day of crash. |
| `CrashTier` | int | Tier that triggered crash. |
| `FinalTier1Pop` | long | Final T1 population. |
| `FinalTier2Pop` | long | Final T2 population. |
| `FinalTier1Arctic` | long | Final T1 Arctic. |
| `FinalTier1Common` | long | Final T1 Common. |
| `FinalTier1Tropical` | long | Final T1 Tropical. |
| `FinalTier1Custom` | long | Final T1 Custom. |
| `FinalTier2Arctic` | long | Final T2 Arctic. |
| `FinalTier2Common` | long | Final T2 Common. |
| `FinalTier2Tropical` | long | Final T2 Tropical. |
| `FinalTier2Custom` | long | Final T2 Custom. |
| `MaxTier1Pop` | long | Max T1 population during scenario. |
| `MinTier1Pop` | long | Min T1 population during scenario. |
| `MaxTier2Pop` | long | Max T2 population during scenario. |
| `MinTier2Pop` | long | Min T2 population during scenario. |
| `AvgTemperature` | float | Average temperature. |
| `MinTemperature` | float | Minimum temperature. |
| `MaxTemperature` | float | Maximum temperature. |

---

## Enums

### ThermalVariant

**Source:** `Assets/scripts/Simulation/SimSpecies.cs`

Used by the simulation runtime.

| Value | Description |
|-------|-------------|
| `Arctic` | Cold-adapted species. |
| `Common` | Temperate/generalist species. |
| `Tropical` | Warm-adapted species. |
| `Custom` | User-defined species from bulk CSV import. Tracked individually by name. |

### SpeciesName

**Source:** `Assets/scripts/Simulation/DataStructure/SpeciesDatabase.cs`

Used by the game/inspector system.

| Value | Tier | Description |
|-------|------|-------------|
| `Hexapod` | 1 (prey) | Primary Tier 1 species. |
| `Gelgi` | 1 | Tier 1 species (game only). |
| `Yelloa` | 1 | Tier 1 species (game only). |
| `Sheplik` | 2 (predator) | Primary Tier 2 species. |
| `Grabbler` | 2 | Tier 2 species (game only). |
| `Cyplo` | 2 | Tier 2 species (game only). |
| `Rooda` | 3 | Tier 3 species (game only, not in simulation). |
| `Sploof` | 3 | Tier 3 species (game only). |
| `Silu` | 3 | Tier 3 species (game only). |
| `Custom` | any | User-defined species. |

### SpeciesVariant

**Source:** `Assets/scripts/Simulation/DataStructure/SpeciesDatabase.cs`

Used by the game/inspector system. Note different ordering from `ThermalVariant`.

| Value | Description |
|-------|-------------|
| `Common` | Temperate/generalist. |
| `Tropical` | Warm-adapted. |
| `Arctic` | Cold-adapted. |
| `Custom` | User-defined. |

---

## Constants

All hardcoded constants across the simulation system.

### EcosystemSimulator Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `MIN_ALIVE_POP` | 1.0 | Minimum population to be considered alive. |
| `DRAIN_ACCEL_THRESHOLD` | 0.2 | Performance below this accelerates Condition drain. |
| `DRAIN_ACCEL_MAX` | 4.0 | Maximum drain acceleration multiplier (total 5x at perf=0, because it's `1 + severity * 4`). |
| `NEWBORN_CONDITION` | 0.5 | Condition value for newborn individuals. Dilutes group average. |
| `NORMAL_PREY_RATIO` | 20.0 | Prey:predator ratio where base hunting efficiency applies (Holling Type II half-saturation reference). |
| `MIN_HUNTING_SUCCESS` | 0.0 | Floor for hunting success. Zero prey = zero hunting. |
| `MAX_HUNTING_SUCCESS` | 1.0 | Ceiling for hunting success. |
| `MIN_POPULATION_FOR_REPRODUCTION` | 2.0 | Minimum population required to reproduce (need at least 2). |
| `STRUGGLING_REPRO_RATE` | 0.10 | Maximum reproScale when Condition equals ReproThreshold. The two piecewise regions of the reproduction curve meet at this value. |

### SimSpecies Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `NO_PREDATOR_PENALTY` | 0.85 | T1 birth multiplier when no predators exist (15% reduction to prevent unchecked growth). |
| `MIN_FINAL_PERF_FOR_NATURAL_DEATH` | 0.1 | Floor for FinalPerformance in natural death calculation to prevent division by zero. |
| `LETHAL_TRANSITION_WIDTH` | 2.0 | Width in Celsius of the smooth cosine fade at CTmin/CTmax boundaries. |

### TemperatureCalculator Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `DAYS_PER_YEAR` | 365 | Days per year for seasonal calculations. |

### SpeciesDatabase Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `DEFAULT_T1_COUNT` | 20 | Default initial Tier 1 population. |
| `DEFAULT_T2_COUNT` | 4 | Default initial Tier 2 population. |
| `DEFAULT_T3_COUNT` | 2 | Default initial Tier 3 population (game only). |

### Default Species Values (Quick Reference)

| Parameter | Hexapod (T1) | Sheplik (T2) |
|-----------|-------------|-------------|
| EatingAmount | 0 | 1.5 |
| ReproductionMultiplier | 0.45 | 0.1 |
| DeathThreshold | 0.3 | 0.3 |
| DeathRate | 0.6 | 0.3 |
| ReproThreshold | 0.25 | 0.25 |
| NaturalDeathRate | 2% | 1% |
| NaturalDeathVariance | +-1% | +-0.5% |
| HuntingEfficiency | N/A | 75% |
| HuntingVariance | N/A | +-15% |

### Default Thermal Variants (from SpeciesDatabase)

| Variant | OptimalTemp (C) | Arrhen Breadth | Arrhen Lower | Arrhen Upper | Lower Bound (C) | Upper Bound (C) | Pmax | CTmin (C) | CTmax (C) |
|---------|----------------|---------------|-------------|-------------|-----------------|-----------------|------|-----------|-----------|
| Common | 24 | 8000 | 3000 | 35000 | 23 | 25 | 0.65 | 0 | 40 |
| Tropical | 30 | 4000 | 15827 | 35000 | 29.75 | 29.95 | 0.85 | 0 | 40 |
| Arctic | 18 | 4000 | 13974 | 35000 | 17.75 | 17.95 | 0.85 | 0 | 40 |
