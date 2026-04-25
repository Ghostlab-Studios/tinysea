# CSV Formats

All CSV formats the simulator reads or writes, column-by-column, generated from source.

## 1. Input: Bulk CSV (one row = one run)

Parsed by `CsvBatchParser.TryParse`. The header must include the 16 global columns below (unordered) and at least `sp1_*` for one species. The parser scans for sequential `sp1_`, `sp2_`, ... prefixes and detects species count dynamically (max 100). Unknown columns log a warning and are ignored.

### 1.1. Global columns (required)

Exact names from `GLOBAL_COLUMNS` in `CsvBatchParser.cs`:

```
batch_name, days, num_scenarios,
base_temp, seasonal_amp, climate_trend,
variability_mag, warming_bias,
daily_var_range, randomness_growth, autocorrelated,
interannual_variation,
temp_min, temp_max,
use_carrying_cap, carrying_cap_t1
```

| Column | Type | Validation |
|--------|------|------------|
| `batch_name` | string | Non-empty. Unique across the file. |
| `days` | int | `1 ≤ days ≤ 182500`. |
| `num_scenarios` | int | `1 ≤ n ≤ 100`. |
| `base_temp` | float | — |
| `seasonal_amp` | float | — |
| `climate_trend` | float | — |
| `variability_mag` | float | — |
| `warming_bias` | float | Skews the *shape* of the per-year interannual draw — warm tail wider than cold tail when `> 1`. Distribution is zero-mean by construction (post-fix); does **not** add a long-term warming trend. Use `climate_trend` for that. |
| `daily_var_range` | float | — |
| `randomness_growth` | float | — |
| `autocorrelated` | bool | Accepts `true`/`false`/`1`/`0`/`yes`/`no` (case-insensitive). |
| `interannual_variation` | bool | Same tokens. |
| `temp_min` | float | — |
| `temp_max` | float | Must be `> temp_min`. |
| `use_carrying_cap` | bool | If true, `carrying_cap_t1 > 0` is required. |
| `carrying_cap_t1` | float | See above. |

### 1.2. Global columns (optional)

From `OPTIONAL_GLOBAL_COLUMNS`:

```
condition_drain_rate, condition_recovery_rate
```

Missing column or empty value → defaults (0.15 and 0.10 respectively).

### 1.3. Per-species columns (required, prefix `sp{N}_`)

Exact names from `SPECIES_COLUMNS`:

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

Per-species validation (see `ValidateSpecies`):

| Column | Validation |
|--------|-----------|
| `spK_name` | Non-empty. |
| `spK_variant` | Non-empty. Parseable as `Common`, `Tropical`, `Arctic`, or `Custom` (case-insensitive). |
| `spK_tier` | 0 (prey) or 1 (predator). Converted to 1-based internally. |
| `spK_pop` | `≥ 0`. |
| `spK_death_thresh` | `[0, 1]`. |
| `spK_death_rate` | `[0, 1]`. |
| `spK_repro_thresh` | `[0, 1]`. |
| `spK_repro_mult` | `≥ 0`. |
| `spK_natural_death_rate` | `≥ 0`. |
| `spK_natural_death_var` | `≥ 0`. |
| `spK_hunt_eff` | `[0, 1]`. |
| `spK_hunt_var` | `≥ 0`. |
| `spK_upper_bound_c` | `> spK_lower_bound_c`. |

Species slots with an empty `spK_name` are skipped (allows ragged tables where not every row fills every slot).

### 1.4. Per-species columns (optional)

From `OPTIONAL_SPECIES_COLUMNS`:

```
pmax, ctmin, ctmax, temp_offset
```

Defaults come from `SpeciesData.GetVariantThermalDefaults(variant)` per-variant (Pmax, CTmin, CTmax) and literal `0` for `temp_offset`. Missing column or empty value → variant default.

### 1.5. Parsing rules

- BOM stripped if present.
- Header and data rows are parsed with quoted-field support (`"..."` with `""` for escaped quote inside).
- Numeric parsing uses `CultureInfo.InvariantCulture` (decimal point, not comma).
- All errors collected and returned together — parsing does **not** stop on the first error.

### 1.6. Template generator

`CsvBatchParser.GenerateTemplate()` emits a 6-species example row (3 Hexapod variants + 3 Sheplik variants). Used by the "Download Template" button in the UI.

## 2. Output: Scenario CSV (one per scenario)

Emitted by `SimulationRunner.ToCsvInternal`. One file per scenario: `scenario_{index}.csv` inside the run's folder in the downloaded ZIP.

### 2.1. Header section (ignored by R's default `read.csv`)

```
#config:days_per_scenario,<value>
#config:number_of_scenarios,<value>
#config:scenario_index,<value>
#config:random_seed,<value>
#config:biology_step,<value>
#config:base_temperature,<value>
#config:seasonal_amplitude,<value>
#config:climate_trend_per_year,<value>
#config:variability_magnitude,<value>
#config:warming_bias,<value>
#config:daily_variation_range,<value>
#config:randomness_growth_rate,<value>
#config:autocorrelated,<value>
#config:temperature_bounds_min,<value>
#config:temperature_bounds_max,<value>
#config:use_carrying_capacity,<value>
#config:carrying_capacity_tier1,<value>
#config:condition_drain_rate,<value>
#config:condition_recovery_rate,<value>
#
#species:Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier,DeathThreshold,DeathRate,ReproThreshold,NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance,OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper,LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC,Pmax,CTminC,CTmaxC,TemperatureDebuff
#species:<name>,<variant>,<tier>,<count>,<eating>,<repro_mult>,<death_thresh>,<death_rate>,<repro_thresh>,<natural_death_rate>,<natural_death_var>,<hunt_eff>,<hunt_var>,<opt_K>,<opt_C>,<arrhen_breadth>,<arrhen_lower>,<arrhen_upper>,<lower_K>,<lower_C>,<upper_K>,<upper_C>,<pmax>,<ctmin>,<ctmax>,<temp_offset>
... (one #species: line per species)
#
```

Both Kelvin and Celsius are emitted for temperature fields (`OptimalTempK` and `OptimalTempC`, etc.) for downstream analysis convenience.

### 2.2. Data section (real CSV rows)

Header row from `StepRecord.CsvHeader`:

```
Day,Year,Temperature,BiologyCycle,
StartPop,EndPop,
Tier1Pop,Tier2Pop,
Tier1Arctic,Tier1Common,Tier1Tropical,Tier1Custom,
Tier2Arctic,Tier2Common,Tier2Tropical,Tier2Custom,
EatenT1,TempDeathsT1,TempDeathsT2,
ConditionDeathsT1,ConditionDeathsT2,
NaturalDeathsT1,NaturalDeathsT2,
TotalDeaths,
BirthsT1,BirthsT2,
FedRateT2,AvgHuntingEff,
AvgConditionT1,AvgConditionT2,
BirthAccumT1,BirthAccumT2,
NaturalDeathAccumT1,NaturalDeathAccumT2,
ConditionDeathAccumT1,ConditionDeathAccumT2,
PredationAccumT1,
ReproScaleT1,ReproScaleT2
```

One data row per simulated day. Formatting: `Temperature` and all float metrics are `F2`–`F3` formatted; integer fields use plain integer formatting. Population fields are written as `long` to avoid overflow on large ecosystems.

### 2.3. Trailing summary section

After the main data, `ToCsvInternal` appends (still within the same CSV):

```
#
#summary:Statistic,<PopColumns...>
#summary:Mean,<values...>
#summary:Max,<values...>
#summary:Min,<values...>
#summary:StdDev,<values...>
#
#extinction:Variant,DayReachedZero
#extinction:<variant>,<day or -1>
... (one #extinction: line per VariantColumn)
#
```

- `PopColumns` = `Tier1Pop, Tier2Pop, Tier1Arctic, Tier1Common, Tier1Tropical, Tier1Custom, Tier2Arctic, Tier2Common, Tier2Tropical, Tier2Custom` (from `ScenarioResult.PopColumns`).
- Extinction day `-1` means the variant never reached zero during the scenario.

## 3. Output: Aggregate CSV (one per run)

Emitted by `ScenarioResult.ToAggregateCsv` (via `batchResults.ToAggregateCsv()` in `BulkSimulationController`). File: `aggregate.csv` inside the run's folder.

Structured in named sections separated by blank lines. Each section starts with `=== TITLE ===`. Metadata within a section uses `# Key,Value`; tabular data uses plain CSV.

### 3.1. Header block

```
=== TINYSEA AGGREGATE RESULTS ===
# Generated,<yyyy-MM-dd HH:mm:ss>
# Configuration,<days> days x <scenarios> scenarios
# Base Temp,<value>C
# Climate Trend,<value>C/year
# Carrying Capacity,<value|Disabled>
# Condition Drain Rate,<value>
# Condition Recovery Rate,<value>
```

### 3.2. Summary

```
=== SUMMARY ===
Scenarios Run,<n>
Survived,<n>
Crashed,<n>
Crash Rate,<pct>
Avg Crash Day,<f1>            ← only if any crashed
```

### 3.3. Population stats (survived only)

```
=== POPULATION STATS (Survived Only) ===
Avg Final T1,<f1>
Avg Final T2,<f1>
Min Final T1,<int>
Max Final T1,<int>
Min Final T2,<int>
Max Final T2,<int>
```

### 3.4. Per-species population stats

Emitted if any per-species data exists. Species listed alphabetically by `FullName`, which includes custom species individually.

```
=== PER-SPECIES POPULATION STATS (All Scenarios) ===
Species,Avg,SurvivedAvg,Min,Max,Extinct,Survived,ExtinctionRate
<sp>,<avg>,<survived_avg>,<min>,<max>,<extinct_count>,<survived_count>,<pct>
...
```

- `Avg` = mean population across all scenarios (including extinct).
- `SurvivedAvg` = mean across scenarios where the species' final population > 0.
- `Min`/`Max` = min/max of the real population values across scenarios.

### 3.5. Condition stats

```
=== CONDITION STATS ===
Avg Condition T1 (All Scenarios),<f3>
Avg Condition T2 (All Scenarios),<f3>
Avg Final Condition T1 (Survived),<f3>
Avg Final Condition T2 (Survived),<f3>
```

### 3.6. Individual scenarios

```
=== INDIVIDUAL SCENARIOS ===
Scenario,Seed,Crashed,CrashDay,CrashTier,FinalT1,FinalT2,T1Arctic,T1Common,T1Tropical,T1Custom,T2Arctic,T2Common,T2Tropical,T2Custom,AvgTemp,MinTemp,MaxTemp
... (one row per scenario)
```

### 3.7. Grand-mean summary statistics (optional)

Emitted only if scenarios have per-day pop stats (they do when `ToScenarioResult` was used).

```
=== SUMMARY STATISTICS (Grand Mean Across All Runs) ===
Statistic,<PopColumns...>
GrandMean_Mean,<values...>
GrandMean_Max,<values...>
GrandMean_Min,<values...>
GrandMean_StdDev,<values...>
```

- `GrandMean_Mean` = average of per-scenario means. Note: this is a mean of averages, not a raw population value.
- `GrandMean_Max`, etc. = same idea for each per-scenario statistic.

### 3.8. Extinction timing

```
=== EXTINCTION TIMING (Across All Runs) ===
Variant,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived
<variant>,<min>,<max>,<avg>,<num_extinct>,<num_survived>
```

Values are `-1` if no scenario recorded that variant going extinct.

## 4. Output: Config CSV (one per run)

Emitted by `ConfigExporter.BuildConfigCsv` via `ScenarioResult.ToConfigCsv`. Downloaded as `config.csv` per run and via the "Download Config" button in the UI.

Structure: same `=== SECTION ===` framing as the aggregate. Body is `Parameter,Value` rows for environment, then a species table with the same 26 columns as the `#species:` block in the scenario CSV (see §2.1).

## 5. Output: Bulk summary CSV (one per bulk upload)

Generated by `BulkSimulationController.GenerateBulkSummary`. File: `bulk_summary.csv` at the root of the downloaded ZIP.

```
=== TINYSEA BULK SUMMARY (Across All Runs) ===
# Total Runs,<n>
# Generated,<yyyy-MM-dd HH:mm:ss>

=== PER-RUN RESULTS ===
Run,Scenarios,Survived,Crashed,CrashRate,BaseTemp,ClimateTrend,<species columns...>
<batch_name>,<scenarios>,<survived>,<crashed>,<pct>,<base_temp>,<trend>,<per-species averages...>
... (one row per run; species columns are sorted alphabetically across all species seen)

=== PER-SPECIES AGGREGATE (Across All Runs) ===
Species,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate
<sp>,<grand_mean>,<survived_mean>,<extinct>,<survived>,<pct>
... (one row per species)
```

- `GrandMean` = mean of run-level averages (includes runs where the species was absent).
- `SurvivedMean` = mean of run-level survived averages (runs where the species had positive population).
- Min/Max are **not** present at this level — they would be min/max of averages, which is not a meaningful population value. Use the per-run table above for range information.

## 6. Line endings and encoding

Outputs use `StringBuilder.AppendLine`, which emits the platform-default line ending. On Windows that is `\r\n`. All tools in the pipeline (R `read.csv`, Python `pandas.read_csv`, Excel) handle this transparently.

Files are written as UTF-8 via `File.WriteAllText` (no explicit BOM from this codebase). The CSV parser strips a leading BOM on input (Excel sometimes adds one), so round-tripping is safe.

## 7. File layout of a downloaded ZIP

Bulk-mode ZIP structure:

```
bulk_summary.csv
<batch_name_1>/
    scenario_1.csv
    scenario_2.csv
    ...
    aggregate.csv
    config.csv
<batch_name_2>/
    ...
```

Single-run mode emits just the per-scenario files + `aggregate.csv` + `config.csv` at the ZIP root.
