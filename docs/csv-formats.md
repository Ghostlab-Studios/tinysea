# CSV Formats

All CSV formats the simulator reads or writes, column-by-column, generated from source.

## 1. Input: Bulk CSV (one row = one run)

Parsed by `CsvBatchParser.TryParse`. The header must include the 15 global columns below (unordered) and at least `sp1_*` for one species. The parser scans for sequential `sp1_`, `sp2_`, ... prefixes and detects species count dynamically (max 100, see [`CsvBatchParser.cs:97`](../Assets/scripts/Simulation/CsvBatchParser.cs)). Unknown columns log a warning and are ignored. (Note: the inline doc comment at [`CsvBatchParser.cs:10`](../Assets/scripts/Simulation/CsvBatchParser.cs) still says "16 global columns + 20 per species" — both counts are off-by-one stale; the canonical numbers are 15 global / 19 per-species, derived from the `GLOBAL_COLUMNS` and `SPECIES_COLUMNS` arrays at lines 18-27 and 41-51.)

### 1.1. Global columns (required)

Exact names from `GLOBAL_COLUMNS` in `CsvBatchParser.cs`:

```
batch_name, days, num_scenarios,
base_temp, seasonal_amp, climate_trend,
variability_mag, warming_bias,
daily_var_range, randomness_growth, autocorrelated,
interannual_variation,
temp_min, temp_max,
carrying_cap_t1
```

`use_carrying_cap` is **deprecated** as of v11.1 — carrying capacity is always on. If an old bulk CSV includes the column, the parser logs a warning and ignores the value. New CSVs from `GenerateTemplate()` no longer emit it.

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
| `carrying_cap_t1` | float | Must be `> 0`. Carrying capacity is always on as of v11.1. |

### 1.2. Global columns (optional)

From `OPTIONAL_GLOBAL_COLUMNS`:

```
condition_drain_rate, condition_recovery_rate, temperature_timeseries_file, use_carrying_cap (deprecated)
```

Missing column or empty value → defaults (0.15 and 0.10 respectively). `use_carrying_cap` is deprecated and ignored if present (v11.1). **Batch 3:** `temperature_timeseries_file` is an optional path to a `Day,Temperature_C` CSV; when set (and the file is readable in Editor/standalone), the run reads its daily temperature from the file instead of the parametric 5-component model — looping with a warning if the series is shorter than the run — while each species' `temp_offset` still applies. Empty / missing file / WebGL / parse failure → parametric model unchanged.

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
| `spK_name` | Non-empty. Free-text display label only — **not** matched against a species database. It becomes the species' `displayName` and the `<Name>` half of `FullName`; thermal/biology parameters always come from the row's own columns (so when `spK_variant=Custom` the name is purely cosmetic). Canonical spelling is lowercase DB style (`hexapod`, `gelgi`, `shelpik`); capitalized variants (`Hexapod`, `Golgi`, `Sheplik`) are accepted as-is. |
| `spK_variant` | Non-empty. **Free-text (Batch 1A)** — any label is accepted and carried through to output (`FullName`, per-species + bulk-summary variant columns, and the dynamic `Tier{n}_{variantLabel}` scenario rollup columns). The canonical default family is `Cold` / `Warm` / `Hot` (Batch 1B); the legacy names `Arctic` / `Common` / `Tropical` are still accepted as aliases (`Cold`=`Arctic`, `Warm`=`Common`, `Hot`=`Tropical`) **for default-parameter lookup only** (`ResolveVariantEnum`). The label itself — whatever its spelling — becomes the rollup column; the `ThermalVariant` enum it resolves to is internal and never appears in output. |
| `spK_tier` | 0 (prey) or 1 (predator). Converted to 1-based internally. |
| `spK_pop` | `≥ 0`. |
| `spK_death_thresh` | `[0, 1]`. |
| `spK_death_rate` | `[0, 1]`. |
| `spK_repro_thresh` | `[0, 1]`. |
| `spK_repro_mult` | `≥ 0`. |
| `spK_natural_death_rate` | `≥ 0`. |
| `spK_natural_death_var` | `≥ 0`. |
| `spK_hunt_eff` | `[0, 1]`. **Dual semantic by tier (v10):** Tier 2 = base hunting success at `NORMAL_PREY_RATIO` (Holling II); Tier 1 = resource-extraction efficiency from the shared food pool (default 1.0 = perfect plankton-style passive extraction). |
| `spK_hunt_var` | `≥ 0`. |
| `spK_upper_bound_c` | `> spK_lower_bound_c`. |

Species slots with an empty `spK_name` are skipped (allows ragged tables where not every row fills every slot).

### 1.4. Per-species columns (optional)

From `OPTIONAL_SPECIES_COLUMNS`:

```
pmax, ctmin, ctmax, temp_offset, condition_drain_rate, condition_recovery_rate
```

Defaults come from `SpeciesData.GetVariantThermalDefaults(variant)` per-variant (Pmax, CTmin, CTmax) and literal `0` for `temp_offset`. Missing column or empty value → variant default. The variant for this lookup is resolved via `SpeciesData.ResolveVariantEnum` (Batch 1B), so `Cold`/`Warm`/`Hot` and the legacy `Arctic`/`Common`/`Tropical` aliases all resolve correctly; unknown free-text labels fall back to `Custom`/`Common` defaults.

**Per-species condition timescale (Batch 2):** `spK_condition_drain_rate` and `spK_condition_recovery_rate` set that species' own condition integration rates (τ ≈ 1/rate). A missing column or blank/negative value inherits the row-global `condition_drain_rate` / `condition_recovery_rate`, so existing batches stay byte-identical. This lets two species share one TPC but differ in τ — the Paper 2 "same curve, different timescale" design.

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
#config:model_version,v12-per-species-tracking
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
#config:carrying_capacity_tier1,<value>
#config:condition_drain_rate,<value>
#config:condition_recovery_rate,<value>
#
#species:Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier,DeathThreshold,DeathRate,ReproThreshold,NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance,OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper,LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC,Pmax,CTminC,CTmaxC,TemperatureDebuff
#species:<name>,<variant>,<tier>,<count>,<eating>,<repro_mult>,<death_thresh>,<death_rate>,<repro_thresh>,<natural_death_rate>,<natural_death_var>,<hunt_eff>,<hunt_var>,<opt_K>,<opt_C>,<arrhen_breadth>,<arrhen_lower>,<arrhen_upper>,<lower_K>,<lower_C>,<upper_K>,<upper_C>,<pmax>,<ctmin>,<ctmax>,<temp_offset>
... (one #species: line per species)
#
```

`model_version` is the first line so downstream tooling can identify which simulator produced the file at a glance. `v12-per-species-tracking` indicates the per-species daily columns and rich aggregate sections introduced in this version (builds on v11.1's cap-always-on, which built on v10's food-pool reframe). See [`simulation-spec.md`](./simulation-spec.md) §v12 changes.

Both Kelvin and Celsius are emitted for temperature fields (`OptimalTempK` and `OptimalTempC`, etc.) for downstream analysis convenience.

### 2.2. Data section (real CSV rows)

Header row from `StepRecord.CsvHeader`:

```
Day,Year,Temperature,BiologyCycle,
StartPop,EndPop,
Tier1Pop,Tier2Pop,
Tier{n}_{variantLabel}…,   (dynamic — one column per distinct (tier, variant label); see below)
EatenT1,TempDeathsT1,TempDeathsT2,
ConditionDeathsT1,ConditionDeathsT2,
NaturalDeathsT1,NaturalDeathsT2,
TotalDeaths,
BirthsT1,BirthsT2,
FedRateT2,AvgHuntingEff,
FedRateT1,FoodDensityT1,
AvgConditionT1,AvgConditionT2,
BirthAccumT1,BirthAccumT2,
NaturalDeathAccumT1,NaturalDeathAccumT2,
ConditionDeathAccumT1,ConditionDeathAccumT2,
PredationAccumT1,
ReproScaleT1,ReproScaleT2
```

One data row per simulated day. Formatting: `Temperature` and all float metrics are `F2`–`F3` formatted; integer fields use plain integer formatting. Population fields are written as `long` to avoid overflow on large ecosystems.

**Dynamic tier-variant rollup columns** (`SimulationRunner.BuildVariantRollupColumns`): between `Tier1Pop`/`Tier2Pop` and `EatenT1`, the header emits one `Tier{n}_{variantLabel}` column per **distinct `(tier, variantLabel)` pair** present in the run — e.g. `Tier1_Hot_Specialist`, `Tier1_M2`, `Tier1_weird_name_`. The label is the species' `variantLabel` (or its `Name` when the label is empty), passed through `SanitizeColumnName` (ASCII-only; `_2`/`_3` suffix on collision, same rule as per-species columns). Columns are ordered by `(tier asc, label asc, Ordinal)`. Each column is the summed population of all species in that tier sharing that label. **This replaces the legacy fixed 4-bucket `Tier1Arctic, Tier1Common, Tier1Tropical, Tier1Custom` (+ `Tier2*`) columns** — the `ThermalVariant` enum names are no longer emitted anywhere in the CSV. (Breaking change for R scripts that read `Tier1Arctic` etc.) When Tier 2 is gated off, only `Tier1_*` rollup columns appear, matching the `Tier2Pop` omission.

> **Heads-up on `FedRateT2`** — column name unchanged since v10, but as of v11 this is a **population-weighted average across predators**, not a pooled scalar. See "v11 semantic change" below.

**v10 columns:**

- `FedRateT1` — population-weighted average FedRate across live Tier 1 species. With v10's food-pool model, this varies daily with population pressure on the shared resource pool. (Pre-v10 it was always 1.0, hence not previously logged.)
- `FoodDensityT1` — daily food density driving the Tier 1 FedRate calculation. Computed as `max(0, 1 − tier1Pop / CarryingCapacityPerTier)` when carrying capacity is enabled, else `1.0`. Useful for diagnosing logistic-overshoot dynamics around the cap.

Both columns are populated even on non-biology days (`BiologyStep > 1`) — they reflect the most recent computed values rather than zeroes, since food density itself doesn't change on skipped-biology days.

**v11 semantic change to existing column:**

- `FedRateT2` — was a pooled scalar shared across all predators (`totalEaten / totalRawDemand`); is now a **population-weighted average** of per-predator FedRates. In single-predator-species runs the value is identical to the v10 pooled formula. In mixed-HE multi-predator runs the average reflects each predator's individual hunting effort.

**v12 per-species daily columns (appended after `ReproScaleT2`):**

For each species in the simulation, 17 additional columns are appended to every daily row, in the order `OrderBy(Tier).ThenBy(FullName)`. Column names are `{SanitizedFullName}_{Field}` where `SanitizedFullName` is the species' `FullName` ("Hexapod_Common", "Coral_Custom", etc.) with any non-`[A-Za-z0-9_]` characters replaced by `_` (and a leading `_` prefix added if it would otherwise start with a digit). On collision, `_2`, `_3`, ... are appended.

Per-species columns:

```
{S}_Pop, {S}_Cond, {S}_ThermalPerf, {S}_FinalPerf,
{S}_FedRate, {S}_HuntingEff,
{S}_Births, {S}_TempDeaths, {S}_CondDeaths, {S}_NatDeaths, {S}_Eaten,
{S}_BirthRate, {S}_ReproScale,
{S}_BirthAccum, {S}_NatDeathAccum, {S}_CondDeathAccum, {S}_PredAccum
```

Semantics:
- `Pop` is rounded to integer (matches `Tier1Pop` etc.). Sums to the matching tier-level column.
- `Cond` is `[0,1]`, formatted `:F3`.
- `ThermalPerf` is `RawThermalPerformance` (Arrhenius output, no Pmax). `FinalPerf = ThermalPerformance × FedRate` (logging only, not a biology input).
- `HuntingEff` is `CurrentHuntingSuccess` for Tier 2 species; **always 0** for Tier 1.
- `Eaten` is predation deaths suffered by Tier 1; **always 0** for Tier 2.
- `BirthRate` is `Births / max(StartOfDayPop, 1)` — per-capita, formatted `:F4`.
- `PredAccum` is fractional-death residual for Tier 1 only; **always 0** for Tier 2.
- All event counters (`Births`, `TempDeaths`, `CondDeaths`, `NatDeaths`, `Eaten`) are **0 on non-biology days** when `BiologyStep > 1`. `Pop` and `Cond` continue to carry the most recent values across non-biology days.

Backward compatibility: existing tier-level columns (`Tier1Pop`...`ReproScaleT2`) appear in their original positions and order. Per-species columns are pure additions at the end. R's `read.csv(comment.char="#")` and pandas handle the wider rows transparently.

**Tier-rollup invariant** (verified at runtime by inspection): for any day, sum of `{S}_Pop` across Tier 1 species equals `Tier1Pop`; same for `BirthsT1 == sum({S}_Births)` etc.

### 2.3. Trailing summary section

After the main data, `ToCsvInternal` appends (still within the same CSV). Three header lines (`Statistic` / `Variant` / `Tier`) annotate each column with its species' tier-context, mirroring the aggregate CSV's wide-format convention.

```
#
#summary:Statistic,Tier1Pop,Tier2Pop,<species cols…>
#summary:Variant,All,All,<variant per species>
#summary:Tier,1,2,<tier per species>
#summary:Mean,<values…>
#summary:Max,<values…>
#summary:Min,<values…>
#summary:StdDev,<values…>
#
#extinction:Species,Variant,Tier,DayReachedZero
#extinction:<sanitized_fullname>,<variant>,<tier>,<day or -1>
... (one #extinction: line per species)
#
```

- Summary columns are `Tier1Pop`, `Tier2Pop`, then one column per species (sanitized FullName, sorted by `(Tier asc, FullName asc)`).
- The `#summary:Variant` and `#extinction:` rows now carry each species' free-text `variantLabel` (e.g. `Hot Specialist`, `M2`), not the legacy `ThermalVariant` enum name.
- The dynamic tier-variant rollup columns (`Tier{n}_{label}`) are intentionally omitted from this summary block — per-species columns subsume them, and the tier-rollup invariant (per-species sums to tier total) holds.
- `Min` and `Max` are integer (long) values; `Mean` and `StdDev` are formatted `:F1`.
- Extinction day `-1` means the species never reached zero during the scenario.

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
# Carrying Capacity,<value>
# Condition Drain Rate,<value>
# Condition Recovery Rate,<value>
```

The `Disabled` state was removed in v11.1. Carrying capacity is always on — Tier 1 species without a resource ceiling grow without bound, which is biologically meaningless and triggered integer-overflow accumulators. Source: [`ScenarioResult.cs:614`](../Assets/scripts/Simulation/DataStructure/ScenarioResult.cs) emits `{CarryingCapacity}` unconditionally; the underlying `ScenarioResult.UseCarryingCapacity` field was deleted ([`EcosystemSimulator.cs:121-135`](../Assets/scripts/Simulation/EcosystemSimulator.cs)).

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

Emitted if any per-species data exists. Species listed alphabetically by `FullName`, which includes custom species individually. `Variant` and `Tier` are emitted as separate columns (added in v12.2) so downstream tools can group / filter without re-parsing the species name.

```
=== PER-SPECIES POPULATION STATS (All Scenarios) ===
Species,Variant,Tier,Avg,SurvivedAvg,Min,Max,Extinct,Survived,ExtinctionRate
<sp>,<variant>,<tier>,<avg>,<survived_avg>,<min>,<max>,<extinct_count>,<survived_count>,<pct>
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

> **Known data-integrity bug**: all four values in this block are currently emitted as `0.000` regardless of actual scenario state. The fields they read (`ScenarioResult.AvgConditionT1/T2/FinalConditionT1/T2`, [`ScenarioResult.cs:57-60`](../Assets/scripts/Simulation/DataStructure/ScenarioResult.cs)) are summed in `AggregateResults.CalculateAggregates` ([lines 315-356](../Assets/scripts/Simulation/DataStructure/ScenarioResult.cs)) but never assigned in `SimulationRunner.ToScenarioResult` ([lines 895-936](../Assets/scripts/Simulation/SimulationRunner.cs)). Use the per-species [§3.5a](#35a-per-species-final-year-metrics-v12) `MeanCondition` / `MeanCondition_SurvivedMean` columns instead — those are populated correctly. Tracked in pending-list; see also `simulation-spec.md` §10.

### 3.5a. Per-species final-year metrics (v12)

Final year = last 365 days of the run. For runs shorter than 365 days, this equals full-run metrics. Each metric reports `Mean / StdDev / SurvivedMean` across the run's scenarios. `SurvivedMean` filters to scenarios where the species' final population > 0.

```
=== PER-SPECIES FINAL YEAR METRICS ===
Species,Variant,Tier,N,NSurvived,MeanCondition,MeanCondition_StdDev,MeanCondition_SurvivedMean,MeanBirthRate,MeanBirthRate_StdDev,MeanBirthRate_SurvivedMean,PopCv,PopCv_StdDev,MeanPop,MeanPop_StdDev,MeanPop_SurvivedMean
<sp>,<variant>,<tier>,<n>,<nSurvived>,<f3>,<f3>,<f3>,<f4>,<f4>,<f4>,<f3>,<f3>,<f1>,<f1>,<f1>
... (one row per species, sorted alphabetically by FullName)
```

- `MeanCondition` and `MeanBirthRate` (per-capita, `Births / max(StartPop,1)`) — averaged over the final 365 days of each scenario **only on days the species had `Population > 0`** (v12.3 fix), then averaged across scenarios. The alive-only filter avoids the per-scenario `MeanCondition` getting polluted by post-extinction days where `sp.Condition` is stuck at its initial 1.0 (or last pre-extinction value) because biology no longer updates it.
- `PopCv` — population coefficient of variation (StdDev / Mean) over the final year. Returns 0 when mean is ~0. Includes all days (zeros are biologically real for population stats).
- `MeanPop` — population averaged over the final year (different from `FinalPop` snapshot). Includes all days.
- `Variant` and `Tier` are emitted as separate columns (v12.2) so the species' tier-context is queryable without re-parsing the name.

### 3.5b. Per-species full-run metrics (v12)

Same metrics as 3.5a but averaged over the entire scenario (not just the final year). Useful for diagnosing whether final-year values are atypical or representative.

```
=== PER-SPECIES FULL-RUN METRICS ===
Species,Variant,Tier,N,NSurvived,MeanCondition,MeanCondition_StdDev,MeanBirthRate,MeanBirthRate_StdDev,PopCv,PopCv_StdDev
<sp>,<variant>,<tier>,<n>,<nSurvived>,<f3>,<f3>,<f4>,<f4>,<f3>,<f3>
...
```

### 3.5c. Per-species stability metrics (v12)

```
=== PER-SPECIES STABILITY METRICS ===
Species,Variant,Tier,N,NSurvived,MinPop_Mean,MinPop_Min,MaxPop_Mean,MaxPop_Max,FinalPop_Mean,FinalPop_SurvivedMean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay
<sp>,<variant>,<tier>,<n>,<nSurvived>,<f1>,<f0>,<f1>,<f0>,<f1>,<f1>,<pct>,<f1>,<pct>,<f1>
...
```

- `MinPop_Mean` / `MaxPop_Mean` — per-scenario population extremes during the entire sim, averaged across scenarios.
- `MinPop_Min` / `MaxPop_Max` — overall worst/best across all scenarios (the rare extreme).
- `ExtinctionRate` — fraction of scenarios where the species reached 0 population mid-run.
- `MeanExtinctionDay` / `MeanCrashDay` — mean day among scenarios that experienced the event; `-1` if no scenario did.
- A species is "crashed" on the first day its population drops below `max(10, 0.05 × StartPop)` (constants `CRASH_FLOOR` / `CRASH_FRACTION` in `SimulationRunner.cs`). Defaults are placeholders; tune as needed.

### 3.6. Individual scenarios (wide format)

Combined wide-format table — one row per scenario, all species reported as additional columns. Three header rows (column name / Variant / Tier) annotate each species column with its taxonomic context. The dynamic tier-variant rollup columns are intentionally omitted: the per-species columns sum to the tier totals, so the variant intermediate level is redundant. The `Variant` annotation row carries each species' free-text `variantLabel`.

```
=== INDIVIDUAL SCENARIOS ===
Scenario,Seed,Crashed,CrashDay,CrashTier,FinalT1,FinalT2,AvgTemp,MinTemp,MaxTemp,<species cols…>
Variant,,,,,All,All,,,,<variant per species>
Tier,,,,,1,2,,,,<tier per species>
<scenario_idx>,<seed>,<crashed>,<crash_day>,<crash_tier>,<finalT1>,<finalT2>,<avg_temp>,<min_temp>,<max_temp>,<finalPop per species…>
... (one data row per scenario)
```

- Header row 1 carries column names; rows 2 and 3 are annotation rows (R/pandas treat them as data rows with leading-string cells — filter them by `Scenario` not parsing as numeric).
- Empty cells under the scenario-meta columns (Seed, Crashed, CrashDay, CrashTier, AvgTemp, MinTemp, MaxTemp) in rows 2/3 indicate the column has no Variant/Tier annotation (it isn't a per-species or tier-total column).
- `FinalT1` / `FinalT2` carry `Variant=All` and tier numbers (`1` / `2`).
- Species columns are sanitized FullName (ASCII only — see [§2.2](#22-data-section-real-csv-rows)) sorted alphabetically. Species absent from a given scenario emit `FinalPop=0` so the table stays rectangular.
- Tier-rollup invariant holds: per-species `FinalPop` values sum to `FinalT1` (Tier 1) and `FinalT2` (Tier 2).

### 3.7. Summary statistics (Grand Mean Across All Scenarios)

Emitted only if scenarios have per-day pop stats. Wide format with the same three-header-row convention as §3.6.

```
=== SUMMARY STATISTICS (Grand Mean Across All Scenarios) ===
Statistic,Tier1Pop,Tier2Pop,<species cols…>
Variant,All,All,<variant per species>
Tier,1,2,<tier per species>
GrandMean_Mean,<values…>
GrandMean_Max,<values…>
GrandMean_Min,<values…>
GrandMean_StdDev,<values…>
```

- Header row 1's leading cell is the row-label column name (`Statistic`); rows 2 and 3 reuse the leading cell for their own labels (`Variant`, `Tier`). Subsequent data rows reuse the same leading-cell convention with `GrandMean_*` labels.
- `GrandMean_Mean` = average of per-scenario means. **Mean of means, not a raw population value.**
- `GrandMean_Max` / `GrandMean_Min` = mean of per-scenario maxes / mins. **Not real ecosystem extrema** — use the per-scenario rows in §3.6 for genuine extrema.
- The dynamic tier-variant rollup columns are dropped for the same reason as §3.6.

### 3.8. Extinction timing

Two sections — tier-variant rollup followed by per-species detail.

```
=== EXTINCTION TIMING - TIER VARIANTS (Across All Scenarios) ===
Variant,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived
<Tier{n}_{label}>,<min>,<max>,<avg>,<num_extinct>,<num_survived>
```

Variant rows are **dynamic**: the union of every scenario's `Tier{n}_{variantLabel}` rollup keys (e.g. `Tier1_Hot_Specialist`, `Tier1_M2`), sorted Ordinal — no longer the fixed `Tier1Arctic … Tier2Custom` set. Values are `-1` if no scenario recorded that tier-variant going extinct.

```
=== EXTINCTION TIMING - PER SPECIES (Across All Scenarios) ===
Species,Variant,Tier,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived
<sp>,<variant>,<tier>,<min>,<max>,<avg>,<num_extinct>,<num_survived>
```

Per-species rows are sourced from `PerSpeciesMetrics[key].ExtinctionTiming`. Values are `-1`/`-1`/`-1` with `NumExtinct=0` for species that never went extinct in any scenario.

## 4. Output: Config CSV (one per run)

Emitted by `ConfigExporter.BuildConfigCsv` via `ScenarioResult.ToConfigCsv`. Downloaded as `config.csv` per run and via the "Download Config" button in the UI.

Structure: same `=== SECTION ===` framing as the aggregate. Body is `Parameter,Value` rows for environment, then a species table with the same 26 columns as the `#species:` block in the scenario CSV (see §2.1).

## 5. Output: Bulk summary CSV (one per bulk upload)

Generated by `BulkSimulationController.GenerateBulkSummary`. File: `bulk_summary.csv` at the root of the downloaded ZIP.

```
=== TINYSEA BULK SUMMARY (Across All Runs) ===
# Model Version,v12-per-species-tracking
# Total Runs,<n>
# Generated,<yyyy-MM-dd HH:mm:ss>
```

The body emits six sections, summarised below. Per-species sections all carry separate `Species`, `Variant`, `Tier` columns (added in v12.2).

### 5.1. Per-run results — tier level (wide)

```
=== PER-RUN RESULTS - TIER LEVEL ===
Run,Scenarios,Survived,Crashed,CrashRate,BaseTemp,ClimateTrend,<species cols…>
<batch_name>,<scenarios>,<survived>,<crashed>,<pct>,<base_temp>,<trend>,<per-species averages…>
... (one row per run; species columns sorted alphabetically across all species seen)
```

Wide format. Each row = one run; per-species `AvgPop` columns are appended after the run-level columns. Convenient for at-a-glance comparison across runs in spreadsheets.

### 5.2. Per-run results — per species (long)

```
=== PER-RUN RESULTS - PER SPECIES ===
Run,Species,Variant,Tier,AvgPop,SurvivedAvgPop
<batch_name>,<sp>,<variant>,<tier>,<f1>,<f1>
... (one row per (run, species))
```

Long format of the same data as §5.1 plus `SurvivedAvgPop`. Database-friendly — joins cleanly with the long-format per-run-per-species sections below.

### 5.3. Per-species aggregate (across all runs)

```
=== PER-SPECIES AGGREGATE (Across All Runs) ===
Species,Variant,Tier,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate
<sp>,<variant>,<tier>,<grand_mean>,<survived_mean>,<extinct>,<survived>,<pct>
... (one row per species)
```

- `GrandMean` = mean of run-level averages (includes runs where the species was absent).
- `SurvivedMean` = mean of run-level survived averages (runs where the species had positive population).
- Min/Max are **not** present at this level — they would be min/max of averages, which is not a meaningful population value. Use the per-run table in §5.1 / §5.2 for range information.

### 5.4. Per-run per-species final year (v12)

Detailed per-run × per-species final-year breakdown. Each row is one (run, species) pair.

```
=== PER-RUN PER-SPECIES FINAL YEAR ===
Run,Species,Variant,Tier,N,NSurvived,MeanCondition,MeanBirthRate,PopCv,MeanPop
<batch_name>,<sp>,<variant>,<tier>,<n>,<nSurvived>,<f3>,<f4>,<f3>,<f1>
... (one row per (run, species) where the species had data)
```

Use this for fine-grained analysis: e.g. plot `MeanCondition` vs `BaseTemp` across runs to visualize the Jensen shift per species.

### 5.5. Cross-run per-species final year (v12)

Summary across runs for each species. "GrandMean" = mean of per-run means (each run weighted equally).

```
=== CROSS-RUN PER-SPECIES FINAL YEAR (Mean of per-run means) ===
Species,Variant,Tier,Runs,RunsSurvived,GrandMeanCondition,GrandMeanCondition_StdDev,GrandMeanBirthRate,GrandMeanBirthRate_StdDev,GrandMeanPopCv,GrandMeanPop,GrandMeanPop_SurvivedMean
<sp>,<variant>,<tier>,<runs>,<runsSurvived>,<f3>,<f3>,<f4>,<f4>,<f3>,<f1>,<f1>
...
```

- `Runs` — number of runs in which the species appeared at all.
- `RunsSurvived` — runs where the species had at least one surviving scenario (`NSurvived > 0` in that run).
- `GrandMeanCondition`, `GrandMeanBirthRate`, `GrandMeanPopCv` — averaged across **surviving runs only** (`RunsSurvived` denominator). Each run's contribution is its per-run `SurvivedMean` (not the unfiltered `Mean`). Pre-v12.3 these averaged across all runs and contained sentinel `Condition=1.0` values from non-surviving runs — that contamination is fixed.
- `GrandMeanPop` — averaged across **all runs** (`Runs` denominator). Zero-pop runs contribute a real 0; that's biologically meaningful for population statistics.
- `GrandMeanPop_SurvivedMean` — averaged across surviving runs only, using each run's per-run `SurvivedMean`.
- `StdDev` columns measure between-run variability of the per-run survived-means.

### 5.6. Cross-run stability (v12)

```
=== CROSS-RUN STABILITY ===
Species,Variant,Tier,Runs,RunsSurvived,MinPop_Mean,MaxPop_Mean,FinalPop_Mean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay
<sp>,<variant>,<tier>,<runs>,<runsSurvived>,<f1>,<f1>,<f1>,<pct>,<f1>,<pct>,<f1>
...
```

- `ExtinctionRate` here is computed across **all scenarios** in the bulk batch (sum of per-run `NEvents` / sum of per-run `N`), not a mean of per-run rates. Same for `CrashRate`.
- `MeanExtinctionDay` / `MeanCrashDay` — pooled mean across all scenarios that experienced the event, weighted by per-run event counts. `-1` if no event occurred anywhere.

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
