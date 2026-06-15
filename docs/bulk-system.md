# Bulk Batch System

The bulk batch system lets a researcher upload one CSV file, where each data row defines a
complete simulation configuration ("run"), and receive back a ZIP (or S3-backed download)
containing one output folder per run plus a single cross-run summary. This document covers the
upload path end to end: the input CSV schema with exact header names, the tier encoding
conversion, the free-text variant label, parsing and validation rules, run orchestration, and
ZIP packaging.

Scope note: the current simulator is Tier 1 (prey) only. The bulk parser hard-rejects any row
that declares a Tier 2 (predator) species (`CsvBatchParser.cs:318-319`). Tier 2 logic survives
in the biology engine from the original two-tier design but is dormant; this document marks it
where it appears and otherwise documents Tier 1 fully.

For topics owned by sibling documents, this document cross references rather than duplicates:

- Per-day biology and the temperature model: `biology-and-formulas.md`, `temperature-model.md`.
- The scenario loop, seeding, and what one scenario does: `run-scenario-batch.md`.
- The non-bulk standard run path and `SimulationConfig`: `configuration-reference.md`.
- Layouts of the per-scenario and per-run output files (`scenario_N.csv`, `aggregate.csv`,
  `config.csv`): `csv-output-formats.md`. The `bulk_summary.csv` layout is owned by this document
  (section 11).
- `SpeciesData`, `BulkBatchConfig`, `BulkSpeciesConfig`: `data-structures.md`. The
  `PerSpeciesAggregate` / `AggStat` / `ExtinctionStat` types and the cross-scenario aggregation math
  in `AggregateResults.CalculateAggregates` are documented in this document (section 11.3) because
  the bulk summary consumes them directly.
- The upload overlay, drag-drop wiring, and download UI: `ui-and-io.md`.

## 1. Terminology

| Term | Code class | Output unit |
|------|-----------|-------------|
| Scenario | `SimulationRunner`, `ScenarioResult` | one `scenario_N.csv` |
| Run / Batch | `BulkBatchConfig` (one CSV row), `AggregateResults` | one folder, `aggregate.csv` + `config.csv` |
| Bulk | `BulkSimulationController`, the uploaded CSV | one ZIP + `bulk_summary.csv` |

"Batch" and "run" are the same concept in this code: one CSV data row. `BulkBatchConfig` holds
one row's parsed config. A run expands into `num_scenarios` scenarios, each with its own random
seed (`BulkSimulationController.cs:138-141`, `:208-209`).

## 2. End-to-end flow

```
CSV file (drag-drop / file picker / paste)
   -> CsvUploadHandler.OnCsvFileReceived(string)               (CsvUploadHandler.cs:120)
   -> CsvBatchParser.TryParse(content, out batches, out errors) (CsvUploadHandler.cs:163)
        success -> ShowSuccess, enable Run button
        failure -> ShowError (first 10 errors)                  (CsvUploadHandler.cs:171-177)
   -> user clicks Run -> OnRunBulkSimulation event fires        (CsvUploadHandler.cs:211)
   -> BulkSimulationController.RunAllBatches(List<BulkBatchConfig>) coroutine
        for each batch:
            build temp RunSpeciesList from row species          (BulkSimulationController.cs:172-175)
            for each scenario: RunSingleScenarioFromBatch       (call sites: BulkSimulationController.cs:223 WebGL / :266 Editor;
                                                                 method def: SimulationController.cs:287)
                stream scenario_N.csv (S3 or progressive ZIP)   (BulkSimulationController.cs:226-234)
            CalculateAggregates, stream aggregate.csv + config.csv
            record BulkRunSummary
        stream bulk_summary.csv
   -> ResultsScreenUI.DisplayBulkResults -> "Download All (ZIP)" button
        progressive ZIP: FinalizeProgressiveZip on click
        server upload:    ServerUpload.TriggerDownload on click
```

The handler validates synchronously the moment a file lands, so the user sees pass/fail before
deciding to run. `BulkSimulationController` re-parses nothing; it consumes the already-validated
`List<BulkBatchConfig>` delivered by the event (`CsvUploadHandler.cs:34,211`).

The "for each scenario" step in the diagram is drawn as a single linear loop, which matches the
WebGL path exactly. The Editor/standalone path instead launches scenarios as parallel `Task.Run`
workers in chunks; section 9.3 documents the parallel-vs-sequential split. The per-scenario seed is
a pure function of the scenario index in both paths, so the two produce the same files (section
6.3, section 9.3).

## 3. Input CSV schema

### 3.1 Layout

The file is a header row plus one or more data rows. Each data row is one run. Columns are
split into a fixed global block followed by N repeated per-species blocks, each species block
prefixed `sp1_`, `sp2_`, `sp3_`, and so on (`CsvBatchParser.cs:7-11`).

```
<global columns> , sp1_<species columns> , sp2_<species columns> , ... , spN_<species columns>
```

Species count N is detected dynamically from the header, not hard-coded. The parser scans for
sequential prefixes `sp1_`, `sp2_`, ... up to `sp100_`. Index `i` counts as present if at least
one header cell starts with `spi_` (the test is `kv.Key.StartsWith("sp{i}_")`, so even a single
`spi_name` cell marks index `i` present, regardless of whether the other 18 suffixes exist). The
scan stops at the first index with no matching header cell and sets N to the highest consecutive
index found (`CsvBatchParser.cs:97-112`). It does not require all 19 suffixes to be present for an
index to be detected; any suffixes missing for a detected index are reported afterward by the
missing-required-columns check (section 8.2), not by silently truncating N. So a header with
`sp1_name`..`sp1_upper_bound_c` plus a stray `sp2_name` detects N = 2 and then fails with the
remaining `sp2_` columns listed as missing. If no `spN_` column exists at all, parsing fails with
"No species columns found." (`CsvBatchParser.cs:114-118`).

Header matching is case-insensitive (`StringComparer.OrdinalIgnoreCase`,
`CsvBatchParser.cs:89`). Header cells are trimmed; empty header cells are skipped
(`CsvBatchParser.cs:90-95`). A leading UTF-8 BOM on the file is stripped
(`CsvBatchParser.cs:76-78`). Rows are split on `\n` and `\r` with empty entries removed, so
blank lines are ignored (`CsvBatchParser.cs:80`). The file must have at least a header plus one
data row, else parsing fails (`CsvBatchParser.cs:81-85`).

### 3.2 Field quoting

Each line is parsed with a small state machine that supports double-quoted fields and escaped
quotes (`""` inside a quoted field becomes one `"`). A comma inside quotes is literal; a comma
outside quotes is a field separator (`CsvBatchParser.cs:578-612`). This lets a species name or
variant label contain commas if wrapped in quotes.

Newlines inside quoted fields are not supported. Row splitting (section 3.1) runs first and splits
on every `\n`/`\r`, and only then is each resulting line handed to the per-line quoting state
machine. A `\n` or `\r` inside a quoted field would therefore split that field across two rows
before quoting ever runs. So a quoted field may contain commas but must not contain a newline; a
species name or variant label has to fit on a single physical line.

### 3.3 Required global columns

These 15 columns are mandatory on every file. Order in the file does not matter; the parser maps
by header name (`CsvBatchParser.cs:18-27`). Each maps to a field on `BulkBatchConfig`
(`CsvBatchParser.cs:173-187`).

| Column | Type | `BulkBatchConfig` field | Meaning |
|--------|------|-------------------------|---------|
| `batch_name` | string | `BatchName` | Run name. Becomes the output folder name and the run label in `bulk_summary.csv`. Must be non-empty and unique across rows. |
| `days` | int | `Days` | Days per scenario (`runner.TotalDays`). Valid 1..182500. |
| `num_scenarios` | int | `NumScenarios` | Number of seeded scenarios for this run. Valid 1..100. |
| `base_temp` | float | `BaseTemp` | Mean temperature in Celsius for the parametric model. |
| `seasonal_amp` | float | `SeasonalAmp` | Seasonal sine amplitude in Celsius. |
| `climate_trend` | float | `ClimateTrend` | Long-term warming in Celsius per year. |
| `variability_mag` | float | `VariabilityMag` | Interannual variation magnitude in Celsius. |
| `warming_bias` | float | `WarmingBias` | Dimensionless shape factor for the interannual distribution (`TempCalc.WarmingBias`). Skews the warm tail wider when greater than 1; it changes only the distribution shape, not its mean, so it does not by itself add a warming trend. |
| `daily_var_range` | float | `DailyVarRange` | Daily noise amplitude in Celsius (maps to `TempCalc.BaseRandomness`). The daily offset is `(u*2-1) * currentRandomness` where `u` is uniform in [0,1), so this is a symmetric amplitude: the daily offset spans roughly `±daily_var_range` before autocorrelation. |
| `randomness_growth` | float | `RandomnessGrowth` | Daily-noise amplitude growth in Celsius per year (`TempCalc.RandomnessGrowthRate`). Added per integer year: `currentRandomness = daily_var_range + randomness_growth * year`, where `year = floor(day / 365)`. Additive, not multiplicative, and stepped per whole year rather than per day. |
| `autocorrelated` | bool | `Autocorrelated` | Enable day-to-day temperature autocorrelation. When on, the day's variation is `0.7 * yesterday + 0.3 * new`. |
| `interannual_variation` | bool | `InterannualVariation` | Enable interannual variation. When off, the per-year offset is 0. |
| `temp_min` | float | `TempMin` | Lower clamp on temperature in Celsius (`TempCalc.MinTemp`), inclusive, applied after summing all components. |
| `temp_max` | float | `TempMax` | Upper clamp on temperature in Celsius (`TempCalc.MaxTemp`), inclusive, applied after summing all components. Must be greater than `temp_min`. |
| `carrying_cap_t1` | float | `CarryingCapT1` | Tier 1 carrying capacity (resource ceiling). Must be positive. |

The parser applies no range validation to any temperature column beyond the `temp_max > temp_min`
cross-field check (section 8.3); the values are copied verbatim onto `runner.TempCalc`. The
parametric model assembles each day as `T(day) = base_temp + seasonal + climate_trend_component +
interannual + daily`, then clamps with `Math.Max(MinTemp, Math.Min(MaxTemp, T))`
(`TemperatureCalculator.cs:77-84`). The clamp is inclusive on both ends and is applied last, after
the daily noise is added, so `temp_min`/`temp_max` bound the post-noise value. The same clamp is
applied to a loaded temperature timeseries value (`TemperatureCalculator.cs:74`). For the full
breakdown of each component see `temperature-model.md`. The carrying capacity drives Tier 1 food
density and is always on; see `biology-and-formulas.md`.

### 3.4 Optional global columns

Missing optional columns fall back to a default; they never cause a missing-column error
(`CsvBatchParser.cs:35-40`, `:189-193`).

| Column | Type | Default | `BulkBatchConfig` field | Meaning |
|--------|------|---------|-------------------------|---------|
| `condition_drain_rate` | float | `0.15` | `ConditionDrainRate` | Run-global condition drain rate. Per-species columns may override per species. |
| `condition_recovery_rate` | float | `0.10` | `ConditionRecoveryRate` | Run-global condition recovery rate. Per-species columns may override per species. |
| `autocorrelation_coefficient` | float | `0.7` | `AutocorrelationCoefficient` | AR(1) coefficient (phi) for daily temperature variation. Valid 0..1 (section 8.3). Used only when `autocorrelated` is on: `variation = previousDay * coeff + newRandom * (1 - coeff)` (`TemperatureCalculator.cs:188`). Replaces the previously hardcoded 0.7/0.3 blend; the default 0.7 reproduces it exactly. 0 is white noise; as the coefficient approaches 1 the daily amplitude shrinks toward zero. Copied onto `runner.TempCalc.AutocorrelationCoefficient` (section 6.2) and emitted in the per-scenario CSV config header as `#config:autocorrelation_coefficient`. |
| `temperature_timeseries_file` | string | `""` | `TemperatureTimeseriesFile` | Optional path to a `Day,Temperature_C` CSV. Empty means use the parametric model. See section 6.4. |
| `use_carrying_cap` | (ignored) | n/a | n/a | Deprecated as of v11.1. If the column is present, the parser emits one `Debug.LogWarning` per row and ignores the value; carrying capacity is always on. New files should omit it. (`CsvBatchParser.cs:30-40`, `:195-203`) |

The deprecation warning is logged at the `Debug.LogWarning` level with the exact text "Row N:
'use_carrying_cap' column is deprecated and will be ignored. Carrying capacity is always on as of
v11.1. Remove the column from new CSV files." (`CsvBatchParser.cs:200-202`). It is a pure side
effect: it is never added to the returned `errors` list, so it cannot fail parsing and is not
counted anywhere.

### 3.5 Required per-species columns

For each detected species index `s` (1..N), these 19 columns are required, each prefixed `sps_`
(`CsvBatchParser.cs:42-52`, `:244-265`). The `SPECIES_COLUMNS` array holds exactly these 19 names,
and the table below has 19 rows, one per array entry in source order. They populate
`BulkSpeciesConfig` (`BulkBatchConfig.cs:7-35`).

| Suffix (after `spN_`) | Type | `BulkSpeciesConfig` field | Meaning |
|-----------------------|------|---------------------------|---------|
| `name` | string | `Name` | Species display name. Empty name means "this species slot is unused" for the row (skipped, see section 4.2). |
| `variant` | string | `Variant` | Free-text variant label. See section 5. |
| `tier` | int | `Tier` | CSV tier code. Must be 0 (prey). See section 7. |
| `pop` | int | `Pop` | Initial population. Must be non-negative. |
| `eating` | float | `Eating` | Prey consumed per creature per step (maps to `eatingAmount`). |
| `repro_mult` | float | `ReproMult` | Birth-rate multiplier (`reproductionMultiplier`). Must be non-negative. |
| `death_thresh` | float | `DeathThresh` | Condition death threshold (`deathThreshold`). Valid 0..1. |
| `death_rate` | float | `DeathRate` | Fraction dying when condition death triggers. Valid 0..1. |
| `repro_thresh` | float | `ReproThresh` | Reproduction condition threshold (`reproThreshold`). Valid 0..1. |
| `natural_death_rate` | float | `NaturalDeathRate` | Base natural death rate per step. Must be non-negative. |
| `natural_death_var` | float | `NaturalDeathVar` | Natural death variance. Must be non-negative. |
| `hunt_eff` | float | `HuntEff` | Hunting efficiency (`huntingEfficiency`). Tier 1 uses this in its food-density formula. Valid 0..1. |
| `hunt_var` | float | `HuntVar` | Hunting variance. Must be non-negative. (Tier 2 legacy.) |
| `opt_temp_c` | float | `OptTempC` | Thermal optimum in Celsius. Converted to Kelvin via `+273.15` on mapping. |
| `arrhen_breadth` | float | `ArrhenBreadth` | Arrhenius breadth parameter (`arrhenBreadth`). |
| `arrhen_lower` | float | `ArrhenLower` | Arrhenius lower parameter (`arrhenLower`). |
| `arrhen_upper` | float | `ArrhenUpper` | Arrhenius upper parameter (`arrhenUpper`). |
| `lower_bound_c` | float | `LowerBoundC` | Lower thermal bound in Celsius. Converted to Kelvin via `+273.15`. |
| `upper_bound_c` | float | `UpperBoundC` | Upper thermal bound in Celsius. Must be greater than `lower_bound_c`. Converted to Kelvin via `+273.15`. |

### 3.6 Optional per-species columns

These 8 per-species columns are optional. Their defaults are variant-aware for the three thermal
columns and fixed for the rest (`CsvBatchParser.cs:56-61`, `:267-284`).

| Suffix (after `spN_`) | Type | Default | `BulkSpeciesConfig` field | Meaning |
|-----------------------|------|---------|---------------------------|---------|
| `pmax` | float | variant default | `Pmax` | Peak thermal performance height (0..1). Default from `GetVariantThermalDefaults`. |
| `ctmin` | float | variant default | `CTminC` | Critical thermal minimum in Celsius. Default from variant. |
| `ctmax` | float | variant default | `CTmaxC` | Critical thermal maximum in Celsius. Default from variant. |
| `temp_offset` | float | `0` | `TempOffset` | Temperature debuff added before the thermal curve (`TemperatureDebuff`). |
| `condition_drain_rate` | float | `-1` | `ConditionDrainRate` | Per-species condition drain rate. `-1` means inherit the row-global rate. |
| `condition_recovery_rate` | float | `-1` | `ConditionRecoveryRate` | Per-species condition recovery rate. `-1` means inherit the row-global rate. |
| `temp_multiplier` | float | `1.0` | `TempMultiplier` | Per-species multiplier on the experienced deviation from the run base temperature (`tempMultiplier`). Valid `>= 0` (section 8.3). `1` is no change, below 1 dampens the swing, above 1 amplifies. See `configuration-reference.md` for the Step 1 damping formula. |
| `initial_condition` | float | `1.0` | `InitialCondition` | Day-0 Condition seed copied into `Condition` at scenario start (`initialCondition`). Valid 0..1 (section 8.3). `1` is fully charged. |

The variant-aware defaults are resolved at parse time. `ParseSpecies` calls
`SpeciesData.ResolveVariantEnum(species.Variant)` to map the row's `variant` string to a
`SpeciesVariant` enum bucket, then `SpeciesData.GetVariantThermalDefaults(bucket, out defPmax,
out defCtMin, out defCtMax)`, and passes those three resolved values as the defaults to the
`GetFloatOptional` calls for `pmax`/`ctmin`/`ctmax` (`CsvBatchParser.cs:268-273`). The resolved
values are written straight into `BulkSpeciesConfig.Pmax`/`CTminC`/`CTmaxC`, so by the time
`ConvertSpecies` runs the substitution has already happened and `ConvertSpecies` copies those
fields 1:1 (`BulkSimulationController.cs:757-759`). The default substitution therefore lives in
one place (the parser); `ConvertSpecies` never re-applies it, so there is no double-apply. The
`BulkSpeciesConfig` field initializers `Pmax=1.0f`, `CTminC=-5.0f`, `CTmaxC=50.0f`
(`BulkBatchConfig.cs:28-30`) are always overwritten by `GetFloatOptional` for a parsed row and so
are never the effective default.

The seven buckets, the exact `variant` input strings that resolve to each (case-insensitive,
trimmed, `SpeciesDatabase.cs:187-208`), and their `(pmax, ctMinC, ctMaxC)` values
(`SpeciesDatabase.cs:115-135`):

| Resolved bucket | Variant inputs that resolve here | pmax | ctMinC | ctMaxC |
|-----------------|----------------------------------|------|--------|--------|
| ColdSpecialist | `cold`, `arctic`, `coldspecialist` | 0.9843 | 0 | 35 |
| WarmSpecialist | `warm`, `common`, `warmspecialist` | 0.972 | 2 | 37 |
| HotSpecialist | `hot`, `tropical`, `hotspecialist` | 0.96 | 4 | 39 |
| ColdGeneralist | `coldgeneralist` only | 0.6616 | 0 | 35 |
| WarmGeneralist | `warmgeneralist` only | 0.6547 | 2 | 37 |
| HotGeneralist | `hotgeneralist` only | 0.6481 | 4 | 39 |
| Custom / unknown | `custom`, or any string that is not a `SpeciesVariant` enum name | 0.65 | 0 | 40 |

The bare temperature aliases (`cold`/`warm`/`hot` and the legacy `arctic`/`common`/`tropical`)
resolve to the Specialist buckets only. The Generalist buckets are reachable only via their
literal enum-name strings `coldgeneralist`/`warmgeneralist`/`hotgeneralist`; no natural-language
alias lands on a Generalist row. Any other string that happens to be a valid `SpeciesVariant`
enum name parses to that enum via the fallback `Enum.TryParse`; everything else becomes Custom
(`SpeciesDatabase.cs:206-207`).

These defaults only apply when the corresponding optional column is absent or blank. When the
column is present with a value, that value wins. A blank optional cell also falls back to default
(`CsvBatchParser.cs:391-402`).

Backward compatibility and validation for optional columns. For every optional column (the
per-species `pmax`, `ctmin`, `ctmax`, `temp_offset`, `condition_drain_rate`,
`condition_recovery_rate`, `temp_multiplier`, `initial_condition`, and the global
`condition_drain_rate`, `condition_recovery_rate`, `autocorrelation_coefficient`), a missing
column or a blank cell falls back to the default, so older CSVs written before these columns
existed still parse unchanged (backward compatible). A value that is present but unparseable is
flagged as an error and the entire bulk run is rejected (`GetFloatOptional`,
`CsvBatchParser.cs:409-425`); previously a bad optional value fell through to the default
silently. Out-of-range values are rejected separately in the validation pass (section 8.3).
Required columns are unchanged: they are still rejected when missing (section 8.2).

### 3.7 Unknown columns

Any header column that is neither required nor optional, and not a recognized `spN_` optional, is
logged as `CsvBatchParser: Unknown column '<name>' will be ignored.` via `Debug.Log` and then
ignored. It does not fail parsing (`CsvBatchParser.cs:143-157`).

## 4. Value parsing

### 4.1 Typed extractors

Five cell extractors read each cell, all using `CultureInfo.InvariantCulture` so decimals are
locale-independent (`CsvBatchParser.cs:354-418`):

- `GetString`: trims; returns `""` if the column is missing or the row is short
  (`CsvBatchParser.cs:354-359`).
- `GetInt`: empty cell adds error "'<col>' cannot be empty"; non-integer adds
  "'<col>' value '<v>' is not a valid integer"; both return 0 (`CsvBatchParser.cs:361-374`).
- `GetFloat`: same pattern with "is not a valid number" (`CsvBatchParser.cs:376-389`).
- `GetFloatOptional`: missing/short/blank returns the supplied default; unparseable adds
  "'<col>' value '<v>' is not a valid number" and returns the default
  (`CsvBatchParser.cs:409-425`). The error means the bulk run is rejected. (This is a V1 change:
  a bad optional value used to fall through to the default silently.)
- `GetBool`: accepts `true`/`1`/`yes` and `false`/`0`/`no`, case-insensitive; an empty cell adds
  "'<col>' cannot be empty" and returns false; any other value adds
  "is not a valid boolean (use true/false, 1/0, or yes/no)" and returns false
  (`CsvBatchParser.cs:404-418`). There is no optional-bool extractor, so the two required bool
  columns (`autocorrelated`, `interannual_variation`) always error on a blank cell; they have no
  default fallback.

A row shorter than the header (fewer cells than columns) does not crash; the missing trailing
columns are treated as absent. Required columns that resolve to absent/empty produce
"cannot be empty" errors through the typed extractors.

### 4.2 Skipping unused species slots

Within a row, the parser walks `s` from 1 to N. If `sps_name` is blank or whitespace, that
species slot is skipped entirely and contributes nothing (`CsvBatchParser.cs:206-210`). This lets
one file mix runs with different species counts: a run that uses 3 of the file's 6 declared
species simply leaves `sp4_name`, `sp5_name`, `sp6_name` blank on its row.

If every species slot on a row is blank, the parser adds
"Row N: at least one species is required — all sp_name columns are blank."
(`CsvBatchParser.cs:217-219`).

## 5. The free-text variant label

`spN_variant` is free text. Any non-empty string is accepted as the variant label
(`CsvBatchParser.cs:309-314`). The label flows through two independent paths.

1. Display identity. `BulkSimulationController.ConvertSpecies` stores
   `variantLabel = SpeciesData.NormalizeVariantLabel(sp.Variant)` on the `SpeciesData`
   (`BulkSimulationController.cs:738`). `NormalizeVariantLabel` canonical-cases any string that
   matches a `SpeciesVariant` enum name
   (ColdSpecialist/WarmSpecialist/HotSpecialist/ColdGeneralist/WarmGeneralist/HotGeneralist/Custom,
   case-insensitive) and passes any other string through unchanged via
   `Enum.TryParse<SpeciesVariant>(raw, true, ...) ? v.ToString() : raw` (`SpeciesDatabase.cs:142-147`).
   The enum has no Common/Tropical/Arctic members, so those bare legacy strings are not
   recognized here and pass through verbatim; only `Custom` of the legacy names canonical-cases.
   This is distinct from `ResolveVariantEnum` (point 2), which additionally accepts the
   cold/warm/hot and arctic/common/tropical aliases. The label, combined with the species name, becomes
   the `FullName` used as the identity key in every per-species dictionary and CSV column. Empty
   label means `FullName` is just the name; otherwise it is `"{Name}_{VariantLabel}"`. The
   `ThermalVariant`/`SpeciesVariant` enum names are never written to output; all output identity
   flows through `variantLabel`/`FullName`.

2. Default-parameter resolution. The same string is also parsed to a `SpeciesVariant` enum bucket
   via `SpeciesData.ResolveVariantEnum` (`BulkSimulationController.cs:731`,
   `CsvBatchParser.cs:268`). This only selects defaults for blank optional thermal columns
   (`pmax`/`ctmin`/`ctmax`) and is never written out. `ResolveVariantEnum`
   (`SpeciesDatabase.cs:187-209`):
   - `cold`/`arctic` -> ColdSpecialist; `warm`/`common` -> WarmSpecialist;
     `hot`/`tropical` -> HotSpecialist.
   - `coldspecialist`, `warmspecialist`, `hotspecialist`, `coldgeneralist`, `warmgeneralist`,
     `hotgeneralist`, `custom` -> the matching enum value.
   - Anything else that is a valid enum name parses to that enum; otherwise Custom.

So a variant string like `v2_M2` is kept verbatim as the display label and resolves to the Custom
bucket for default purposes. If the row supplies explicit `pmax`/`ctmin`/`ctmax` values, the
bucket resolution has no effect at all.

## 6. From CSV row to a running scenario

### 6.1 ConvertSpecies (row species -> SpeciesData)

For each batch, `RunAllBatches` builds a throwaway `RunSpeciesList` ScriptableObject in memory and
fills it by calling `ConvertSpecies(batch.Species[i], i)` per species
(`BulkSimulationController.cs:172-175`). This never touches the shipped `RunSpeciesList`
asset. `ConvertSpecies` (`BulkSimulationController.cs:723-764`) maps `BulkSpeciesConfig` to
`SpeciesData`:

- `speciesName`: `Enum.TryParse<SpeciesName>(sp.Name, true, out ...)`; unknown names become
  `SpeciesName.Custom`. The raw CSV name is always preserved in `speciesLabel = sp.Name`
  (`BulkSimulationController.cs:725-727`, `:740`).
- `variant`: `SpeciesData.ResolveVariantEnum(sp.Variant)` (enum bucket, internal use only).
- `variantLabel`: `SpeciesData.NormalizeVariantLabel(sp.Variant)` (display identity).
- `tier`: `sp.Tier` carried straight through (CSV 0-based; see section 7).
- Biology fields copied 1:1: `count`, `eatingAmount`, `reproductionMultiplier`,
  `deathThreshold`, `deathRate`, `reproThreshold`, `naturalDeathRate`, `naturalDeathVariance`,
  `huntingEfficiency`, `huntingVariance`, `arrhenBreadth`, `arrhenLower`, `arrhenUpper`, `pmax`,
  `ctMinC`, `ctMaxC`, `TemperatureDebuff`, `tempMultiplier`, `conditionDrainRate`,
  `conditionRecoveryRate`, `initialCondition` (`BulkSimulationController.cs:785,788`). For
  `pmax`/`ctMinC`/`ctMaxC` the variant-aware default substitution already happened in the parser
  (section 3.6), so these fields hold resolved values and `ConvertSpecies` copies them verbatim.
- Celsius to Kelvin conversion on three temperatures (`BulkSimulationController.cs:751,755,756`):
  `optimalTempK = OptTempC + 273.15`, `lowerBoundK = LowerBoundC + 273.15`,
  `upperBoundK = UpperBoundC + 273.15`.

### 6.2 RunSingleScenarioFromBatch (one scenario)

For scenario index `scenarioIndex` (1-based) and seed `seed`,
`SimulationController.RunSingleScenarioFromBatch(batch, tempSpecies, scenarioIndex, seed)`
constructs a fresh `SimulationRunner(seed)` and applies the row's parameters
(`SimulationController.cs:287-338`):

- `runner.TotalDays = batch.Days`.
- `runner.BiologyStep = config.BiologyStep` (taken from the live `SimulationConfig`, not the CSV;
  the bulk CSV has no biology-step column). `BiologyStep` is the integer number of days between
  biology calculations: 1 means biology runs every day (the default and most accurate), 5 means it
  runs every fifth day (the original turn-based game cadence). It is `[Range(1, 5)]` with default
  `1` (`SimulationConfig.cs:27-29`). The runner copies it onto the ecosystem each run via
  `Ecosystem.BiologyStep = BiologyStep` (`SimulationRunner.cs:400`). Because the CSV cannot supply
  it, every bulk run uses whatever the in-session `SimulationConfig` holds, default 1.
- Temperature model fields copied from the batch onto `runner.TempCalc`: `BaseTemperature`,
  `SeasonalAmplitude`, `ClimateTrendPerYear`, `VariabilityMagnitude`, `WarmingBias`,
  `BaseRandomness` (from `DailyVarRange`), `RandomnessGrowthRate`, `UseAutocorrelation`,
  `UseInterannualVariation`, `MinTemp`, `MaxTemp` (`SimulationController.cs:295-305`), and
  `AutocorrelationCoefficient` (from `batch.AutocorrelationCoefficient`,
  `SimulationController.cs:311`).
- `runner.RunSpecies = tempSpecies` (the in-memory species list for this batch).
- `runner.Ecosystem.CarryingCapacityPerTier = batch.CarryingCapT1`.
- `runner.Ecosystem.ConditionDrainRate = batch.ConditionDrainRate`,
  `runner.Ecosystem.ConditionRecoveryRate = batch.ConditionRecoveryRate`.
- `runner.Ecosystem.Tier2Enabled = config.Tier2Enabled` (from the live config; default false,
  `SimulationConfig.cs:130`). With Tier 2 disabled, Tier 2 species would be dropped at load, but
  the bulk parser already rejects Tier 2 rows so none reach here.

The bullets above are in the literal source order: `TotalDays` (`:292`), `BiologyStep` (`:293`), the
11 `TempCalc` fields (`:295-305`), `RunSpecies` (`:307`), `CarryingCapacityPerTier` (`:309`),
`ConditionDrainRate` and `ConditionRecoveryRate` (`:310-311`), then `Tier2Enabled` last (`:312`),
all before the timeseries block. Every line is an independent field assignment with no
interdependency, so the order does not affect behavior; `Tier2Enabled` is simply set unconditionally
at the end.

After parameter application it calls `runner.Run()` and returns
`runner.ToScenarioResult(scenarioIndex, batch.NumScenarios)`. The scenario loop itself and what
`Run()` does per day are documented in `run-scenario-batch.md`.

Note: `batch.ConditionDrainRate`/`RecoveryRate` is the run-global value applied to the ecosystem.
A `SpeciesData` whose per-species `conditionDrainRate`/`conditionRecoveryRate` is `-1` inherits
that global value inside the biology step; a non-negative per-species value overrides it. See
`biology-and-formulas.md` for the inheritance rule.

### 6.3 Seeding

Within a batch the scenario loop variable `s` runs `0..NumScenarios-1`. The 1-based scenario index
used for filenames and `ScenarioResult.ScenarioIndex` is `scenarioIndex = s + 1`, and the seed is
computed as:

```
seed = config.RandomSeed < 0 ? -1 : config.RandomSeed + s     // s is 0-based
```

(`BulkSimulationController.cs:208-209` WebGL, `:258-259` Editor). The seed is a pure function of
the base seed and the 0-based offset `s`, independent of completion order, so the same input always
maps the same seed to the same scenario file. Because `scenarioIndex = s + 1`, `scenario_1.csv`
uses offset `s = 0` and therefore seed `RandomSeed + 0`. Concrete example: with `RandomSeed = 12345`
and 3 scenarios, `scenario_1.csv`, `scenario_2.csv`, and `scenario_3.csv` get seeds 12345, 12346,
and 12347 respectively.

The base seed comes from the live `SimulationConfig` (`RandomSeed = 12345` by default,
`SimulationConfig.cs:139`). The CSV has no per-row seed column, so every run in one upload shares
the same base seed, offset only by the 0-based scenario index.

Seed = -1 (random-seed mode). When `config.RandomSeed < 0`, the ternary passes the literal `-1` to
every scenario's `SimulationRunner(-1)`. That value is forwarded to `TemperatureCalculator(-1)` and
`EcosystemSimulator(-1)`, both of which do `seed < 0 ? new System.Random() : new System.Random(seed)`
(`TemperatureCalculator.cs:45`, `EcosystemSimulator.cs:302`). The parameterless `System.Random()`
seeds itself from the system clock, so each scenario constructs its own independently seeded RNG.
The scenarios are therefore distinct nondeterministic draws, not identical and not reproducible.
The shared `-1` is only a sentinel selecting clock-seeded mode; it does not make the scenarios equal.
The `+ s` offset is not applied in this mode, so the scenarios are not deterministically varied
either, they are each independently random.

### 6.4 Optional temperature timeseries override

If `temperature_timeseries_file` is non-empty, `RunSingleScenarioFromBatch` tries to read that
path as a `Day,Temperature_C` CSV and load it onto `runner.TempCalc`, overriding the parametric
model (`SimulationController.cs:317-334`). It is best-effort: a missing file, a WebGL build (no
filesystem), a parse failure, or no numeric rows logs a `Debug.LogWarning` and falls back to the
parametric model. The timeseries format and loop behavior are documented in
`temperature-model.md`.

## 7. Tier encoding conversion

There are two tier numbering conventions in the codebase, and they differ by one.

- CSV / `SpeciesData.tier`: 0-based. 0 = Tier 1 (prey), 1 = Tier 2 (predator)
  (`SpeciesDatabase.cs:51`).
- Internal runtime `SimSpecies.Tier`: 1-based. 1 = prey, 2 = predator. The runner computes
  `Tier = SpeciesData.tier + 1` when it materializes species.

The bulk CSV column `spN_tier` is the 0-based form. It is carried unchanged into
`SpeciesData.tier` by `ConvertSpecies` (`BulkSimulationController.cs:741`). The `+1` shift to the
internal 1-based tier happens later inside the runner, not in the bulk path.

Validation enforces Tier 1 only: `ValidateSpecies` requires `sp.Tier == 0`, else it adds
"Row N: spK_tier must be 0. This is a Tier-1 (prey-only) simulator — Tier 2 (predator) species
are not supported." (`CsvBatchParser.cs:316-319`). A file declaring any predator row fails
validation and never runs. Here `K` is the compacted non-blank species index (1-based position in
`batch.Species`), not the header slot; see the definition in section 8.3.

When the bulk controller later annotates per-species summary rows with an explicit tier column, it
re-derives the 1-based value as `sp.tier + 1` (`BulkSimulationController.cs:341-342`). So
`bulk_summary.csv` shows Tier `1` for the same species the input CSV declared as `tier=0`.

## 8. Validation and error handling

### 8.1 Collect-all strategy

`CsvBatchParser.TryParse` accumulates every error into a `List<string>` rather than stopping at
the first (`CsvBatchParser.cs:13`, `:65-69`). It returns `true` only when the error list is empty
(`CsvBatchParser.cs:239`). Some failures are fatal and return early (empty content, fewer than two
lines, no species columns, missing required columns); the rest accumulate across all rows and
return at the end.

### 8.2 Fatal, early-return checks

| Condition | Error message | Source |
|-----------|---------------|--------|
| Null/whitespace content | "CSV content is empty." | `CsvBatchParser.cs:70-74` |
| < 2 lines after split | "CSV must have a header row and at least one data row." | `CsvBatchParser.cs:81-85` |
| No `spN_` column found | "No species columns found. CSV must have at least sp1_name, sp1_variant, etc." | `CsvBatchParser.cs:114-118` |
| Any required column missing | "Missing required columns (K): <comma list>" | `CsvBatchParser.cs:129-141` |

Required columns are the 15 global plus, for each detected species `s`, the 19 `sps_` species
columns (`CsvBatchParser.cs:120-127`).

### 8.3 Per-row, accumulated checks

These do not early-return; they add to the error list and parsing continues
(`CsvBatchParser.cs:282-350`):

Batch-level (`ValidateBatch`):

| Rule | Error |
|------|-------|
| `batch_name` non-empty | "Row N: batch_name cannot be empty." |
| `1 <= days <= 182500` | "Row N: days must be between 1 and 182500." |
| `1 <= num_scenarios <= 100` | "Row N: num_scenarios must be between 1 and 100." |
| `temp_max > temp_min` | "Row N: temp_max (x) must be greater than temp_min (y)." |
| `carrying_cap_t1 > 0` | "Row N: carrying_cap_t1 must be positive (carrying capacity is always on)." |
| `0 <= autocorrelation_coefficient <= 1` | "Row N: autocorrelation_coefficient must be between 0 and 1." |
| at least one usable species | "Row N: at least one species is required — all sp_name columns are blank." |
| unique `batch_name` | "Row N: Duplicate batch_name 'X'." |

The unique-name check uses a case-insensitive `HashSet`; the first occurrence is recorded and any
later row with the same name (ignoring case) fails (`CsvBatchParser.cs:160`, `:224-231`).

Species-level (`ValidateSpecies`, per non-blank species, prefix shown as `spK`):

The `K` in every `spK_` message is the 1-based position of the species in the row's compacted
non-blank species list, not the original header slot number. `ValidateBatch` iterates the parsed
species with `for (int s = 0; s < batch.Species.Count; s++)` and passes the prefix `$"sp{s + 1}"`
to `ValidateSpecies` (`CsvBatchParser.cs:300-301`); `ValidateSpecies` then prints that prefix
verbatim (`CsvBatchParser.cs:304-349`). Blank-name slots were already dropped during parsing
(section 4.2, `CsvBatchParser.cs:206-215`), so they never enter `batch.Species` and never shift
`K`. Worked example: a row leaves `sp1_name` blank but fills `sp2_name`. The parser skips slot 1,
so the slot-2 species becomes `batch.Species[0]` and any validation error on it is reported with
prefix `sp1_`, not `sp2_`. `K` therefore re-numbers from 1 over the surviving species and can
differ from the header slot whenever an earlier slot on the row was left blank. The two coincide
only when no earlier slot was skipped.

| Rule | Error |
|------|-------|
| name non-empty | "Row N: spK_name cannot be empty." |
| variant non-empty | "Row N: spK_variant cannot be empty." |
| `tier == 0` | "Row N: spK_tier must be 0. This is a Tier-1 (prey-only) simulator — Tier 2 (predator) species are not supported." |
| `pop >= 0` | "Row N: spK_pop must be non-negative." |
| `0 <= death_thresh <= 1` | "Row N: spK_death_thresh must be between 0 and 1." |
| `0 <= death_rate <= 1` | "Row N: spK_death_rate must be between 0 and 1." |
| `0 <= repro_thresh <= 1` | "Row N: spK_repro_thresh must be between 0 and 1." |
| `repro_mult >= 0` | "Row N: spK_repro_mult must be non-negative." |
| `natural_death_rate >= 0` | "Row N: spK_natural_death_rate must be non-negative." |
| `natural_death_var >= 0` | "Row N: spK_natural_death_var must be non-negative." |
| `0 <= hunt_eff <= 1` | "Row N: spK_hunt_eff must be between 0 and 1." |
| `hunt_var >= 0` | "Row N: spK_hunt_var must be non-negative." |
| `upper_bound_c > lower_bound_c` | "Row N: spK_upper_bound_c (x) must be greater than spK_lower_bound_c (y)." |
| `temp_multiplier >= 0` | "Row N: spK_temp_multiplier must be non-negative (1 = no change, <1 dampens the signal)." |
| `0 <= initial_condition <= 1` | "Row N: spK_initial_condition must be between 0 and 1." |

Row numbers in messages are `rowNum = rowIdx + 1` (`CsvBatchParser.cs:168`), where `rowIdx` indexes
the line array produced by the split. Because the split uses `StringSplitOptions.RemoveEmptyEntries`
(section 3.1), blank lines have already been dropped, so `rowNum` is the 1-based position among
non-blank lines, not the physical spreadsheet line number. The two coincide only when the file has
no blank lines before a given row; if blank lines precede a row, the reported number will be lower
than the spreadsheet's physical line number.

If after all rows `batches.Count == 0`, the parser adds "No data rows found."
(`CsvBatchParser.cs:236-237`).

Variant strings are intentionally not range-checked beyond non-empty: any free-text value passes
(`CsvBatchParser.cs:309-314`). There is no per-row column-count check; a short row is tolerated and
surfaces as "cannot be empty" errors on whichever required columns it lacks.

### 8.4 UI presentation of results

`CsvUploadHandler.ValidateCsv` runs the parser the moment a file is received
(`CsvUploadHandler.cs:158-178`):

- Success: stores `parsedBatches`, shows "CSV is valid. Ready to run K batch(es)." and enables the
  Run Simulation button (`CsvUploadHandler.cs:163-167`, `:187-193`).
- Failure: clears `parsedBatches`, shows at most the first 10 errors joined by newlines, and if
  there are more appends "... and M more errors." The Run button stays hidden, only Go Back shows
  (`CsvUploadHandler.cs:169-177`, `:180-185`).

The drag-drop, file-picker, and paste plumbing that feeds `OnCsvFileReceived` is platform-specific
and is documented in `ui-and-io.md`.

## 9. Run orchestration

`BulkSimulationController.RunAllBatches` is a Unity coroutine that drives all runs
(`BulkSimulationController.cs:91-399`).

### 9.1 Setup

1. Sets `_isRunning = true`, clears `_cancelRequested`, allocates `_bulkSummaries`
   (`BulkSimulationController.cs:93-95`).
2. Hides the upload overlay and shows `ResultsScreenUI` in progress mode
   (`BulkSimulationController.cs:98-103`).
3. Reads `simulationController.Config`; if null, reports
   "Error: SimulationConfig not assigned!" and aborts (`BulkSimulationController.cs:107-114`).
4. Chooses an output sink at runtime. `useServerUpload = ServerUpload.IsAvailable`, which is true
   only in a WebGL non-editor build (`BulkSimulationController.cs:117`, `ServerUpload.cs:33-43`). If
   server upload is chosen it calls `ServerUpload.CreateSession`; if that session creation fails it
   logs a warning and clears `useServerUpload`, falling back to the progressive ZIP
   (`BulkSimulationController.cs:119-129`). When `useServerUpload` ends up false it calls
   `WebGLZipDownload.InitProgressiveZip("tinysea_bulk_<timestamp>.zip")`
   (`BulkSimulationController.cs:131-136`). This sink choice is orthogonal to the compile-time
   WebGL-vs-Editor scenario loop in section 9.3: the scenario loop is selected by `#if UNITY_WEBGL`
   at build time, while the sink (server upload versus progressive ZIP) is selected at run time from
   the `useServerUpload` flag. In a WebGL build the precedence is: attempt server upload first, and
   fall back to progressive ZIP only if `CreateSession` fails. In Editor/standalone `IsAvailable` is
   false, so the sink is always the progressive ZIP (which writes to disk).
5. Sums `num_scenarios` across all batches into `totalScenarios` for progress reporting
   (`BulkSimulationController.cs:138-141`).

### 9.2 Per-batch loop

For each batch `b` (`BulkSimulationController.cs:148-381`):

1. Honor cancel and pause at the batch boundary (`BulkSimulationController.cs:150-151`, `:166-169`).
   `WaitWhilePaused` spins yielding to the UI while `resultsScreen.IsPaused` and not cancelled, so
   Resume and Cancel stay responsive and the simulation does not advance
   (`BulkSimulationController.cs:403-407`). The bulk path polls `ResultsScreenUI.IsPaused` rather
   than using the standard run's `RunControl`.
2. Build the in-memory `RunSpeciesList` via `ConvertSpecies` (section 6.1).
3. Build an `AggregateResults` for the batch, stamping it with the batch's days, biology step,
   seed, carrying capacity, condition rates, all temperature parameters, and the temp species list
   (`BulkSimulationController.cs:178-200`).
4. Run the scenarios (platform-specific, below).
5. After scenarios, if any produced results: set `CompletedAt`, set `BatchName`, call
   `CalculateAggregates`, then stream `aggregate.csv` and `config.csv` for the batch and record a
   `BulkRunSummary` (`BulkSimulationController.cs:320-380`).

### 9.3 Scenario execution: WebGL vs Editor/standalone

The scenario inner loop is compiled two ways.

WebGL (`#if UNITY_WEBGL && !UNITY_EDITOR`, `BulkSimulationController.cs:202-236`): scenarios run
strictly sequentially because the WASM runtime is single-threaded and must yield to keep the UI
alive. For each scenario it updates progress, yields a frame, honors pause, runs
`RunSingleScenarioFromBatch`, appends the result, streams `scenario_N.csv`, nulls
`result.CsvData`, and increments the completed counter.

Editor/standalone (`#else`, `BulkSimulationController.cs:237-317`): scenarios run in parallel
chunks to bound memory. Parallelism is `max(1, Environment.ProcessorCount - 1)`. The batch is
processed in chunks of that size; each chunk launches `Task.Run` workers that each call
`RunSingleScenarioFromBatch`, catching per-task exceptions into a `taskErrors[]` array so one
failing scenario does not abort the batch (`BulkSimulationController.cs:262-272`).

Each worker writes its result into a fixed array slot `scenarioResults[s]` where `s` is the
0-based scenario index it was assigned, and it was given `scenarioIndex = s + 1` and
`seed = config.RandomSeed + s` before the task started (`BulkSimulationController.cs:255-266`).
Because the seed and the destination slot are both pure functions of `s`, computed before the
task runs, out-of-order completion does not affect which seed produced which result or where the
result lands. After a chunk completes, the controller walks the slots in index order and streams
`scenario_{result.ScenarioIndex}.csv` from each (`:296-313`), so a scenario's filename always
matches the `s`-derived seed regardless of which worker finished first. The per-scenario seed is
therefore deterministic and order-independent, and the Editor path reproduces the same
seed-to-`scenario_N` pairing as the strictly sequential WebGL path.

While a chunk runs, the coroutine yields frames and updates progress. The global progress bar
fraction uses `totalDone = completedScenarios + chunk + done` over `totalScenarios`, where
`completedScenarios` is scenarios finished in prior batches, `chunk` is scenarios finished in
prior chunks of this batch, and `done` is the count of tasks completed in the current chunk
(`BulkSimulationController.cs:285-287`). The status text shows `chunk + done` of `batchSize`,
which is the per-batch count (`:289`). After a chunk finishes it logs any task errors, appends
each non-null result, streams its CSV, nulls `CsvData`, and nulls the result slot to free memory
(`BulkSimulationController.cs:295-314`).

Both paths produce byte-identical output files for the same input and the same base seed, because
the seed is a pure function of (base seed, scenario index) in both. Only concurrency and memory
handling differ between the two paths.

### 9.4 Memory strategy

The reason CSVs are streamed and then nulled immediately is memory. In WebGL the WASM heap is
bounded (the comment cites 512 MB, `BulkSimulationController.cs:13-17`). Each scenario's CSV is
handed to the JS side (or to disk) right after it is generated, then the C# string reference is set
to null so GC can reclaim it. Peak C# memory is therefore about one scenario's CSV at a time rather
than all scenarios at once. After streaming each batch's per-scenario CSVs, the controller also
calls `batchResults.Scenarios.Clear()` to release the per-scenario objects, keeping only the
lightweight `BulkRunSummary` snapshot alive for the final cross-run summary
(`BulkSimulationController.cs:379`).

### 9.5 Cancel and close

`OnCancelRequested` sets `_cancelRequested = true` (`BulkSimulationController.cs:409-412`). The
loop checks this flag at the batch boundary, inside the WebGL scenario loop, and between Editor
chunks, breaking out when set. `OnBulkResultsClosed` also requests cancel if a run is still in
progress and resets the upload handler to idle (`BulkSimulationController.cs:414-423`). A cancelled
run still finalizes whatever was already streamed.

### 9.6 ETA

`GetETA(completed, total, startTime)` estimates remaining wall-clock time
(`BulkSimulationController.cs:769-789`):

```
if (completed <= 0) return "";                         // guard: nothing finished yet, no estimate
if (now - _lastEtaUpdateTime < 60s && _cachedEta != "")
    return _cachedEta;                                 // serve the cached string verbatim
elapsed     = now - startTime;                         // seconds, Time.realtimeSinceStartup
perScenario = elapsed / completed;                     // seconds per completed scenario
remaining   = perScenario * (total - completed);       // seconds left; "remaining" = total - completed
```

`completed` is the global completed-scenario count, `total` is `totalScenarios` (sum of every
batch's `num_scenarios`), and `now`/`startTime` are `Time.realtimeSinceStartup` values in seconds.
The `completed <= 0` branch returns "" so there is never a divide-by-zero. The remaining seconds
are formatted as `~Ns left` when under 60, `~Nm left` when under 3600, else `~Nh Nm left`
(`:780-787`). The value is recomputed at most once per 60 seconds: between recomputes the cached
string `_cachedEta` is returned unchanged, and `_lastEtaUpdateTime` is reset to `now` only when a
fresh value is computed (`:773-775`, fields at `:53-54`). The cache is not explicitly invalidated on
pause or cancel; it simply expires after 60 seconds.

## 10. Output packaging

### 10.1 ZIP folder layout

Files are written under one folder per run named by `batch_name`, with the bulk summary at the
root (`BulkSimulationController.cs:154`, `:228`, `:309`, `:370-376`, `:386-390`):

```
tinysea_bulk_<timestamp>.zip
  <batch_name_1>/
    scenario_1.csv
    scenario_2.csv
    ...
    scenario_<num_scenarios>.csv
    aggregate.csv
    config.csv
  <batch_name_2>/
    ...
  bulk_summary.csv
```

Per-scenario filenames use the 1-based `ScenarioResult.ScenarioIndex`
(`scenario_{result.ScenarioIndex}.csv`, `BulkSimulationController.cs:228,309`). Because
`batch_name` is the folder name and must be unique per row, runs never collide. The exact contents
of `scenario_N.csv`, `aggregate.csv`, and `config.csv` are described in `csv-output-formats.md`;
they are produced by `SimulationRunner.ToCsv`, `AggregateResults.ToAggregateCsv`, and
`AggregateResults.ToConfigCsv` respectively.

### 10.2 Two sinks

The same `csvPath`/content pairs go to one of two sinks depending on `useServerUpload`:

- Progressive ZIP (`WebGLZipDownload`): in WebGL the file string is transferred to the JS heap via
  `TinySea_AddFileToZip` and the archive is assembled by JSZip on finalize; in Editor/standalone
  each file is written immediately to disk under
  `SavePaths.ResultsFolder/<zip-name-without-extension>/<csvPath>`, creating subdirectories as
  needed (`WebGLZipDownload.cs:106-146`). The progressive folder is cleaned and recreated on
  `InitProgressiveZip` (`WebGLZipDownload.cs:118-121`).
- Server upload (`ServerUpload`, WebGL non-editor only): each file is gzip+base64 compressed and
  POSTed to `/api/upload.php` under a session created by `/api/session.php`; one retry on failure
  (`ServerUpload.cs:50-129`). The server assembles the ZIP and serves it from
  `/api/download.php?session=...`. This path keeps large CSVs off the browser heap entirely.

### 10.3 Finalization and download

After all batches and the bulk summary are streamed, the controller calls
`resultsScreen.DisplayBulkResults(batches.Count, completedScenarios, useServerUpload)` and clears
`_isRunning`/`_runCoroutine` (`BulkSimulationController.cs:393-398`). `DisplayBulkResults` switches
to results mode, sets `_bulkServerReady = serverUpload` and `_bulkProgressiveReady = !serverUpload`,
hides the per-config and per-aggregate download buttons, and shows a single "Download All (ZIP)"
button (`ResultsScreenUI.cs:302-340`).

The actual ZIP build/download happens on click, not at run end (`ResultsScreenUI.cs:560-583`):

- Server-upload runs: `ServerUpload.TriggerDownload()` opens the server's download URL
  (`ResultsScreenUI.cs:563-570`, `ServerUpload.cs:143-155`).
- Progressive-ZIP runs: `DownloadBulkAsZip` calls `WebGLZipDownload.FinalizeProgressiveZip()`,
  which in WebGL triggers JSZip to build and download the archive, and in Editor/standalone returns
  the on-disk folder path and optionally opens it (`ResultsScreenUI.cs:779-800`,
  `WebGLZipDownload.cs:154-169`).

## 11. The bulk summary (`bulk_summary.csv`)

`GenerateBulkSummary(List<BulkRunSummary>)` builds one cross-run summary string
(`BulkSimulationController.cs:428-715`). Each `BulkRunSummary` is the lightweight per-run snapshot
recorded during the loop (`BulkSimulationController.cs:34-50`, `:346-364`):

| Field | Source |
|-------|--------|
| `BatchName`, `NumScenarios` | from the batch |
| `Survived`, `Crashed` | `AggregateResults.SurvivedScenarios` / `CrashedScenarios` |
| `BaseTemp`, `ClimateTrend` | from the batch |
| `AvgSpeciesPop` | copy of `AggregateResults.PerSpeciesAvg` (per-`FullName` mean pop over all scenarios) |
| `SurvivedSpeciesPop` | copy of `AggregateResults.PerSpeciesSurvivedAvg` (mean over scenarios where the species survived) |
| `PerSpeciesMetrics` | `AggregateResults.PerSpeciesMetrics` (rich per-species aggregate; full field set in section 11.2) |
| `SpeciesInfo` | per-`FullName` `(Tier, Variant)` lookup built from the temp species list |

`SpeciesInfo` is built from `tempSpecies` so every per-species summary row can carry explicit
Variant and Tier columns. The key is built as `fullName = $"{name}_{vlabel}"`, where
`name = !empty(sp.speciesLabel) ? sp.speciesLabel : sp.speciesName.ToString()` and
`vlabel = !empty(sp.variantLabel) ? sp.variantLabel : sp.variant.ToString()`
(`BulkSimulationController.cs:334-342`). Tier is stored as the 1-based `sp.tier + 1` (`:342`).

Two fallback differences are worth stating precisely:

- This `SpeciesInfo` key always inserts the `_` separator and always emits a `vlabel`, falling back
  to the `SpeciesVariant` enum name (`sp.variant.ToString()`) when `variantLabel` is empty. The
  runtime `SimSpecies.FullName` instead omits the suffix entirely when its label is empty,
  returning just the name with no underscore (`SimSpecies.cs:89`). So for a hypothetical
  empty-label species, `SpeciesInfo` would emit `Name_Custom` while `SimSpecies.FullName` would
  emit `Name`.
- In the bulk path the two never diverge, because `spN_variant` is a required non-empty column
  (section 8.3) and `ConvertSpecies` sets `variantLabel = NormalizeVariantLabel(sp.Variant)` from a
  guaranteed-non-empty string, so `variantLabel` is never blank and the enum-name fallback never
  fires. The `_Custom`-style enum-name fallback in this construction is therefore dead in practice,
  which is why the "enum names are never written to output" guarantee in section 5 still holds: the
  only branch that would write an enum name is unreachable given a mandatory non-empty variant.

### 11.1 Number formatting

Every numeric cell in `bulk_summary.csv` is written with an explicit .NET composite-format
specifier through `StringBuilder.AppendLine($"...")`, all under the thread's current culture (the
code does not pass `CultureInfo.InvariantCulture` to these interpolations, unlike the parser in
section 4.1). The specifiers are fixed per column and are the same in every section that emits that
quantity. A reader reproducing the file byte-for-byte must apply exactly these:

| Quantity | Specifier | Meaning | Example | Source |
|----------|-----------|---------|---------|--------|
| Base temperature (`BaseTemp`) | `:F2` | fixed-point, 2 decimals | `20.00` | `BulkSimulationController.cs:472` |
| Climate trend (`ClimateTrend`) | `:F4` | fixed-point, 4 decimals | `0.0000` | `BulkSimulationController.cs:472` |
| Population values (avg / survived-avg / grand-mean / min / max / final) | `:F1` | fixed-point, 1 decimal | `4231.0` | `:472,476,494,539,571,652,708` |
| Condition means and std devs | `:F3` | fixed-point, 3 decimals | `0.872` | `:568,649` |
| Birth-rate means and std devs | `:F4` | fixed-point, 4 decimals | `0.0123` | `:569,650` |
| Population CV means | `:F3` | fixed-point, 3 decimals | `0.145` | `:570,651` |
| Death-count means (temp/condition/natural/predation) | `:F1` | fixed-point, 1 decimal | `12.5` | `:572-573` |
| Mean extinction day, mean crash day | `:F1` | fixed-point, 1 decimal | `184.0` | `:709-710` |
| Rates (`CrashRate`, `ExtinctionRate`, stability extinction/crash rate) | `:P1` | percent, 1 decimal, multiplies by 100 and appends ` %` | `12.0 %` | `:472,539,709-710` |
| Counts (`Scenarios`, `Survived`, `Crashed`, `Runs`, `RunsSurvived`, `RunsExtinct`, `N`, `NSurvived`) | none | integer, `int.ToString()` | `5` | throughout |
| Run name, species `FullName`, variant string | none | raw string, unquoted | `Hexapod_Cold` | throughout |
| Model Version | literal | the constant string `v12-per-species-tracking` | `v12-per-species-tracking` | `:446` |
| Generated timestamp | `:yyyy-MM-dd HH:mm:ss` | `DateTime.Now`, 24-hour, space separator | `2026-06-08 14:03:51` | `:448` |

The `:P1` format is the .NET percent format: it multiplies the stored fraction by 100, rounds to
one decimal, and appends a space and a percent sign (so a stored `0.12f` prints `12.0 %`). The
`:F1`/`:F2`/`:F3`/`:F4` formats are fixed-point with the stated decimal count and no thousands
separator. Because the interpolations are culture-sensitive, the decimal separator and the percent
glyph follow the runtime culture; under the invariant or any en-US culture they are `.` and `%` as
shown. Mean extinction day and mean crash day are `-1.0` when the species had no extinction or
crash event in any scenario of any run (the sentinel `-1f`, `:704-705`).

### 11.2 Section-by-section layout

The summary is one string built by `GenerateBulkSummary`, written with `=== TITLE ===` section
headers and `# Key,Value` metadata lines (`BulkSimulationController.cs:445-712`). Sections appear
in the fixed order below. Sections 1 through 4 are always present; sections 5 through 7 are emitted
only when at least one run carries a non-empty `PerSpeciesMetrics` (`:555`). A blank line (one
`AppendLine()` with no argument) separates sections.

The species ordering rule differs by section. Sections 2, 3, and 4 iterate `allSpecies`, a
`SortedSet<string>` built from the union of every run's `AvgSpeciesPop` keys (`:452-458`), so they
are ordered by `FullName` ascending and include only species that appear in the population-average
dicts. Sections 5, 6, and 7 iterate `allSpeciesRich`, a `SortedSet<string>` built from the union of
every run's `PerSpeciesMetrics` keys (`:548-553`), also `FullName` ascending. Runs are always
iterated in the order they were appended to `_bulkSummaries`, which is input CSV row order.

**Section 1: `=== TINYSEA BULK SUMMARY (Across All Runs) ===`** (`:445-449`). Three metadata lines
followed by a blank line:

```
=== TINYSEA BULK SUMMARY (Across All Runs) ===
# Model Version,v12-per-species-tracking
# Total Runs,{summaries.Count}
# Generated,{DateTime.Now:yyyy-MM-dd HH:mm:ss}
```

`Total Runs` is the number of runs (CSV rows) that produced at least one scenario, i.e.
`_bulkSummaries.Count`.

**Section 2: `=== PER-RUN RESULTS - TIER LEVEL ===`** (`:462-480`). Wide format, kept for backward
compatibility. Header is the seven fixed columns plus one column per species in `allSpecies`:

```
Run,Scenarios,Survived,Crashed,CrashRate,BaseTemp,ClimateTrend,{sp1},{sp2},...
```

One row per run. Column bindings and formats:

| Column | Source | Format |
|--------|--------|--------|
| `Run` | `run.BatchName` | raw string |
| `Scenarios` | `run.NumScenarios` | integer |
| `Survived` | `run.Survived` | integer |
| `Crashed` | `run.Crashed` | integer |
| `CrashRate` | `total = Survived + Crashed`; `total > 0 ? (float)Crashed / total : 0` | `:P1` |
| `BaseTemp` | `run.BaseTemp` | `:F2` |
| `ClimateTrend` | `run.ClimateTrend` | `:F4` |
| one per species | `run.AvgSpeciesPop[sp]` if the key is present, else `0` | `:F1` |

**Section 3: `=== PER-RUN RESULTS - PER SPECIES ===`** (`:485-497`). Long format, one row per
(run, species) over `allSpecies`. Runs whose `AvgSpeciesPop` is null are skipped (`:489`).

```
Run,Species,Variant,Tier,AvgPop,SurvivedAvgPop
```

| Column | Source | Format |
|--------|--------|--------|
| `Run` | `run.BatchName` | raw string |
| `Species` | the `FullName` key `sp` | raw string |
| `Variant` | `GetVariant(sp)` (merged `bulkSpeciesMeta`, see below) | raw string |
| `Tier` | `GetTier(sp)` | raw string |
| `AvgPop` | `run.AvgSpeciesPop[sp]` if present, else `0` | `:F1` |
| `SurvivedAvgPop` | `run.SurvivedSpeciesPop[sp]` if present, else `0` | `:F1` |

**Section 4: `=== PER-SPECIES AGGREGATE (Across All Runs) ===`** (`:503-540`). One row per species
in `allSpecies`. The loop iterates the runs that have the species in `AvgSpeciesPop`:

```
Species,Variant,Tier,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate
```

For each such run it reads `val = run.AvgSpeciesPop[sp]` and applies one predicate: a run counts as
extinct for the species when `val <= 0`, otherwise it counts as surviving. "Presence" and "not
extinct" are the same predicate here (`val > 0`); there is no separate presence test.

| Column | Source / formula | Format |
|--------|------------------|--------|
| `Species` | the `FullName` key `sp` | raw string |
| `Variant` | `GetVariant(sp)` | raw string |
| `Tier` | `GetTier(sp)` | raw string |
| `GrandMean` | `count > 0 ? sum(val) / count : 0`, summed over every run that has the species (extinct runs contribute their `val`, typically 0) | `:F1` |
| `SurvivedMean` | `survivedCount > 0 ? survivedSum / survivedCount : 0`; for each surviving run it adds `run.SurvivedSpeciesPop[sp]` if present, else `val`; denominator is `RunsSurvived`, not `count` | `:F1` |
| `RunsExtinct` | count of runs with `val <= 0` | integer |
| `RunsSurvived` | count of runs with `val > 0` (`RunsExtinct + RunsSurvived == count`) | integer |
| `ExtinctionRate` | `count > 0 ? (float)RunsExtinct / count : 0` | `:P1` |

Here `count` is the number of runs that have the species at all.

When any run carries `PerSpeciesMetrics`, three more sections follow, all keyed off `allSpeciesRich`
(`:548-712`). Each reads a `PerSpeciesAggregate a = run.PerSpeciesMetrics[sp]`; runs missing the
species key are skipped per row. The `PerSpeciesAggregate` / `AggStat` / `ExtinctionStat` fields it
reads are defined in section 11.3.

**Section 5: `=== PER-RUN PER-SPECIES FINAL YEAR ===`** (`:559-575`). Per (run, species) detail.
Runs whose `PerSpeciesMetrics` is null are skipped (`:563`).

```
Run,Species,Variant,Tier,N,NSurvived,MeanCondition,MeanBirthRate,PopCv,MeanPop,MeanFinalYear_TempDeaths,MeanFinalYear_ConditionDeaths,MeanFinalYear_NaturalDeaths,MeanFinalYear_PredationDeaths
```

| Column | Source field on `a` | Format |
|--------|---------------------|--------|
| `Run` | `run.BatchName` | raw string |
| `Species` | `sp` (`FullName`) | raw string |
| `Variant` | `GetVariant(sp)` | raw string |
| `Tier` | `GetTier(sp)` | raw string |
| `N` | `a.N` | integer |
| `NSurvived` | `a.NSurvived` | integer |
| `MeanCondition` | `a.MeanConditionFinalYear.Mean` | `:F3` |
| `MeanBirthRate` | `a.MeanBirthRateFinalYear.Mean` | `:F4` |
| `PopCv` | `a.PopCvFinalYear.Mean` | `:F3` |
| `MeanPop` | `a.MeanPopulationFinalYear.Mean` | `:F1` |
| `MeanFinalYear_TempDeaths` | `a.FinalYearTempDeaths.Mean` | `:F1` |
| `MeanFinalYear_ConditionDeaths` | `a.FinalYearConditionDeaths.Mean` | `:F1` |
| `MeanFinalYear_NaturalDeaths` | `a.FinalYearNaturalDeaths.Mean` | `:F1` |
| `MeanFinalYear_PredationDeaths` | `a.FinalYearPredationDeaths.Mean` | `:F1` |

`MeanCondition`, `MeanBirthRate`, `PopCv`, and `MeanPop` here all read the `.Mean` field of their
`AggStat` (mean across the run's scenarios, including non-survivors), not `.SurvivedMean`. The four
death-pathway columns read the `.Mean` of the final-year death-count `AggStat`s.

**Section 6: `=== CROSS-RUN PER-SPECIES FINAL YEAR (Mean of per-run means) ===`** (`:592-653`).
Each species aggregated as the mean of per-run means with equal weight per run.

```
Species,Variant,Tier,Runs,RunsSurvived,GrandMeanCondition,GrandMeanCondition_StdDev,GrandMeanBirthRate,GrandMeanBirthRate_StdDev,GrandMeanPopCv,GrandMeanPop,GrandMeanPop_SurvivedMean
```

Two run-sample sets are used. The population grand-mean is over all runs that have the species (zero
is a real datum). Condition, birth rate, and population CV are averaged over surviving runs only,
defined as runs whose per-run `a.NSurvived > 0`, and they use each run's `.SurvivedMean` rather than
`.Mean` so partial-survival runs contribute their cleanest representative value. This avoids
non-surviving runs contributing real zeros or a sentinel condition of 1.0 that biology never
updated. The cross-run denominator for the three surviving-only metrics is `runsSurvivedCount`, the
number of runs with `a.NSurvived > 0` (`:616`, `:635-637`).

| Column | Source / formula | Format |
|--------|------------------|--------|
| `Species` | `sp` (`FullName`) | raw string |
| `Variant` | `GetVariant(sp)` | raw string |
| `Tier` | `GetTier(sp)` | raw string |
| `Runs` | `runs` = count of runs that have the species in `PerSpeciesMetrics` | integer |
| `RunsSurvived` | `runsSurvivedCount` = count of those runs with `a.NSurvived > 0` | integer |
| `GrandMeanCondition` | `runsSurvivedCount > 0 ? condSum / runsSurvivedCount : 0`; `condSum` sums `a.MeanConditionFinalYear.SurvivedMean` over surviving runs | `:F3` |
| `GrandMeanCondition_StdDev` | `sqrt(condSqSum / runsSurvivedCount - gmCond^2)` over the same sample, 0 if variance not positive or no surviving runs | `:F3` |
| `GrandMeanBirthRate` | `runsSurvivedCount > 0 ? brSum / runsSurvivedCount : 0`; `brSum` sums `a.MeanBirthRateFinalYear.SurvivedMean` | `:F4` |
| `GrandMeanBirthRate_StdDev` | `sqrt(brSqSum / runsSurvivedCount - gmBr^2)`, 0 if variance not positive or no surviving runs | `:F4` |
| `GrandMeanPopCv` | `runsSurvivedCount > 0 ? cvSum / runsSurvivedCount : 0`; `cvSum` sums `a.PopCvFinalYear.SurvivedMean` | `:F3` |
| `GrandMeanPop` | `popSum / runs`; `popSum` sums `a.MeanPopulationFinalYear.Mean` over all runs (zero-pop runs included) | `:F1` |
| `GrandMeanPop_SurvivedMean` | `popSurvivedCount > 0 ? popSurvivedSum / popSurvivedCount : 0`; `popSurvivedSum` sums `a.MeanPopulationFinalYear.SurvivedMean` over surviving runs, `popSurvivedCount` counts them | `:F1` |

The standard deviations are population-variance form (divide by the sample count, not `n-1`):
`Var = mean(x^2) - mean(x)^2`, `StdDev = sqrt(max(0, Var))`. A species with `runs == 0` is skipped
entirely (`:630`). Here `a.NSurvived` is the per-run, per-species count of scenarios in which the
species ended with final population greater than 0 (`PerSpeciesAggregate.NSurvived`,
`ScenarioResult.cs:178`); `a.N` is the count of scenarios contributing to that run-species
(`ScenarioResult.cs:177`).

**Section 7: `=== CROSS-RUN STABILITY ===`** (`:657-711`). Per species, population extremes averaged
across runs plus extinction and crash statistics pooled over all scenarios in all runs.

```
Species,Variant,Tier,Runs,RunsSurvived,MinPop_Mean,MaxPop_Mean,FinalPop_Mean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay
```

| Column | Source / formula | Format |
|--------|------------------|--------|
| `Species` | `sp` (`FullName`) | raw string |
| `Variant` | `GetVariant(sp)` | raw string |
| `Tier` | `GetTier(sp)` | raw string |
| `Runs` | `runs` = count of runs that have the species | integer |
| `RunsSurvived` | count of those runs with `a.NSurvived > 0` | integer |
| `MinPop_Mean` | `minSum / runs`; `minSum` sums `a.MinPopulation.Mean` over runs | `:F1` |
| `MaxPop_Mean` | `maxSum / runs`; `maxSum` sums `a.MaxPopulation.Mean` | `:F1` |
| `FinalPop_Mean` | `finalSum / runs`; `finalSum` sums `a.FinalPopulation.Mean` | `:F1` |
| `ExtinctionRate` | `totalScenarios > 0 ? (float)totalExtinctions / totalScenarios : 0`; `totalExtinctions` sums `a.ExtinctionTiming.NEvents`, `totalScenarios` sums `a.N`, both pooled over all runs | `:P1` |
| `MeanExtinctionDay` | scenario-count-weighted mean day: `extDaySum / extDayCount` where `extDaySum += a.ExtinctionTiming.MeanDay * a.ExtinctionTiming.NEvents` and `extDayCount += a.ExtinctionTiming.NEvents` over runs with events; `-1` if no events | `:F1` |
| `CrashRate` | `totalScenarios > 0 ? (float)totalCrashes / totalScenarios : 0`; `totalCrashes` sums `a.CrashTiming.NEvents` | `:P1` |
| `MeanCrashDay` | weighted analog using `a.CrashTiming`; `-1` if no events | `:F1` |

The extinction and crash rates here pool scenarios across runs (`totalExtinctions / totalScenarios`
over the whole bulk), unlike section 4's per-run `ExtinctionRate` which is a fraction of runs. The
mean day columns weight each run's `MeanDay` by that run's event count so the pooled mean equals the
simple mean over all individual events. A species with `runs == 0` is skipped (`:698`).

The `Variant` and `Tier` values in sections 3 through 7 come from the merged `bulkSpeciesMeta`
lookup, the union of every run's `SpeciesInfo` with last-write-wins (shared species carry identical
metadata). `GetVariant(fn)` returns the stored variant string or `"Unknown"` if the `FullName` is
absent; `GetTier(fn)` returns the stored 1-based tier as a string or `"?"`
(`BulkSimulationController.cs:432-443`).

### 11.3 Per-species aggregate data structures

The v12 sections read a `PerSpeciesAggregate` per (run, species), wrapping an `AggStat` per metric
and an `ExtinctionStat` per timing event. These types live in `ScenarioResult.cs` and are built by
`AggregateResults.CalculateAggregates` -> `BuildPerSpeciesAggregate` once per run, before the run
snapshot is taken (`BulkSimulationController.cs:324`). A run's `PerSpeciesMetrics` dictionary is
keyed by species `FullName`.

`AggStat` is a six-field struct holding the cross-scenario distribution of one scalar metric
(`ScenarioResult.cs:146-154`):

| Field | Meaning |
|-------|---------|
| `Mean` | mean of the metric over all scenarios of the run |
| `StdDev` | population standard deviation over all scenarios |
| `Min` | minimum value over all scenarios |
| `Max` | maximum value over all scenarios |
| `SurvivedMean` | mean over only the scenarios where the species ended with population > 0 |
| `SurvivedStdDev` | population standard deviation over only the surviving scenarios |

`ExtinctionStat` is a five-field struct summarizing the day an event first occurred across scenarios
(`ScenarioResult.cs:160-167`):

| Field | Meaning |
|-------|---------|
| `NEvents` | number of scenarios where the event occurred (recorded day was not `-1`) |
| `NNonEvents` | number of scenarios where it did not occur (recorded day was `-1`) |
| `MinDay` | earliest event day; `-1` if `NEvents == 0` |
| `MaxDay` | latest event day; `-1` if `NEvents == 0` |
| `MeanDay` | mean event day over the events; `-1` if `NEvents == 0` |

`PerSpeciesAggregate` carries `FullName`, the scenario counts `N` and `NSurvived`, one `AggStat`
per metric, and two `ExtinctionStat`s (`ScenarioResult.cs:174-197`):

| Field | Type | Metric aggregated |
|-------|------|-------------------|
| `FullName` | string | the species identity key |
| `N` | int | scenarios contributing to this run-species |
| `NSurvived` | int | scenarios where the species final pop > 0 |
| `MeanConditionFullRun` | AggStat | per-scenario mean Condition over the whole run |
| `MeanConditionFinalYear` | AggStat | per-scenario mean Condition over the last 365 days |
| `MeanBirthRateFullRun` | AggStat | per-scenario mean per-capita birth rate over the run |
| `MeanBirthRateFinalYear` | AggStat | same over the last 365 days |
| `PopCvFullRun` | AggStat | per-scenario population CV over the run |
| `PopCvFinalYear` | AggStat | per-scenario population CV over the last 365 days |
| `MeanPopulationFinalYear` | AggStat | per-scenario mean population over the last 365 days |
| `FinalYearTempDeaths` | AggStat | per-scenario total thermal deaths summed over the last 365 days |
| `FinalYearConditionDeaths` | AggStat | per-scenario total condition deaths over the last 365 days |
| `FinalYearNaturalDeaths` | AggStat | per-scenario total natural deaths over the last 365 days |
| `FinalYearPredationDeaths` | AggStat | per-scenario total predation deaths (Tier 1 `Eaten`; 0 for Tier 2) over the last 365 days |
| `MinPopulation` | AggStat | per-scenario minimum daily population over the whole run |
| `MaxPopulation` | AggStat | per-scenario maximum daily population over the whole run |
| `FinalPopulation` | AggStat | per-scenario final-day population |
| `ExtinctionTiming` | ExtinctionStat | first-extinction-day distribution across scenarios |
| `CrashTiming` | ExtinctionStat | first-crash-day distribution across scenarios |

Each `AggStat` field above is the cross-scenario aggregate of the matching scalar on
`PerSpeciesScenarioMetrics`, the per-scenario record computed at the end of each scenario run
(`ScenarioResult.cs:101-139`). The final-year window is the last 365 days of the scenario; if a
scenario is shorter than 365 days the slice covers all days, so final-year metrics equal full-run
metrics (`ScenarioResult.cs:98-99`).

`BuildPerSpeciesAggregate` (`ScenarioResult.cs:431-482`) computes the dictionary as follows:

1. Form the union of `FullName` keys across every scenario's `SpeciesMetrics` (`:437-442`).
2. For each key, collect the list of per-scenario `PerSpeciesScenarioMetrics` rows that contain it;
   skip the key if the list is empty (`:444-453`).
3. Set `N = rows.Count` and `NSurvived = count of rows where row.Survived` (final pop > 0)
   (`:458-461`).
4. Fill each `AggStat` field by calling `ComputeAggStat(rows, selector)` with the selector for that
   metric, and each `ExtinctionStat` by calling `ComputeExtinctionStat(rows, selector)` for
   `ExtinctionDay` and `CrashDay` (`:463-478`).

`ComputeAggStat(rows, sel)` (`ScenarioResult.cs:484-520`) iterates the rows once, accumulating the
sum, sum of squares, min, and max over all rows, and a second sum and sum of squares over only the
rows whose `Survived` is true. With `n` rows and `nSurvived` surviving rows:

```
Mean    = sum / n
Var     = sqSum / n - Mean*Mean
StdDev  = Var > 0 ? sqrt(Var) : 0
Min     = min over rows           (0 stays 0 if all rows are 0; min starts at float.MaxValue)
Max     = max over rows           (max starts at float.MinValue)

SurvivedMean   = nSurvived > 0 ? sSum / nSurvived : 0
SurvivedVar    = sSqSum / nSurvived - SurvivedMean*SurvivedMean
SurvivedStdDev = SurvivedVar > 0 ? sqrt(SurvivedVar) : 0
```

All variances are the population form (divide by count, not `n-1`). When `n == 0` every field stays
at its zero default; when `nSurvived == 0` the two survived fields stay 0.

`ComputeExtinctionStat(rows, selector)` (`ScenarioResult.cs:522-544`) reads an integer day per row.
A day `< 0` (the never-occurred sentinel) increments `NNonEvents`; a day `>= 0` increments `NEvents`
and feeds the day sum, min, and max. After the pass, if `NEvents > 0` it sets `MinDay`, `MaxDay`,
and `MeanDay = sum / NEvents`; otherwise all three days stay `-1`.

The two day fields `ExtinctionDay` and `CrashDay` on each per-scenario record are the first day the
event occurred, or `-1` if it never did. `ExtinctionDay` is the first day population reaches 0 after
the species was alive. `CrashDay` is the first day population drops below
`max(CRASH_FLOOR, CRASH_FRACTION * StartPop)` (`ScenarioResult.cs:135-136`).

Population CV (the `PopCv` columns and `PerSpeciesScenarioMetrics.PopCvFullRun`/`PopCvFinalYear`) is
the coefficient of variation of the species population, `StdDev(population) / Mean(population)`,
computed per scenario over the daily population series before aggregation. `PopCvFinalYear` uses the
last-365-day window and `PopCvFullRun` the whole run. It is computed from running sums as
`sqrt(sqSum/count - mean*mean) / mean` and returns 0 when the mean population is at or below 1e-4 or
when the variance is non-positive, since CV is undefined for a near-zero or constant series
(`SimulationRunner.cs:1171`, `:1192-1200`). The per-scenario CV values are then aggregated across
scenarios into the `AggStat` carried in `PerSpeciesAggregate.PopCvFinalYear` /
`PerSpeciesAggregate.PopCvFullRun` by the same `ComputeAggStat` shown above.

`PerSpeciesAvg` and `PerSpeciesSurvivedAvg` (the sources of `AvgSpeciesPop` and `SurvivedSpeciesPop`
in each `BulkRunSummary`) are computed separately in the first half of `CalculateAggregates` from
`ScenarioResult.FinalSpeciesPopulations`, not from `PerSpeciesScenarioMetrics`
(`ScenarioResult.cs:365-420`). For each `FullName` present in any scenario's final-population dict:
`PerSpeciesAvg[key] = sum(finalPop) / count` over the scenarios that have the key (extinct scenarios
contribute 0), and `PerSpeciesSurvivedAvg[key] = survivedSum / survivedCount` over only the
scenarios whose final pop for that key was `> 0`, or 0 when none survived. `SurvivedScenarios` and
`CrashedScenarios` on the run come from the same pass: a scenario counts as crashed when
`scenario.Crashed` is true, otherwise survived (`ScenarioResult.cs:309-336`).

## 12. Template generation

`CsvBatchParser.GenerateTemplate()` builds a downloadable example CSV whose header is generated
from the same column-name arrays used by `TryParse`, so the template can never drift out of sync
with the parser (`CsvBatchParser.cs:436-522`). The deprecated `use_carrying_cap` column is
omitted (`CsvBatchParser.cs:456-460`).

The template header includes the three V1 optional columns with their defaults: the global
`autocorrelation_coefficient` (default `0.7`, emitted from `OPTIONAL_GLOBAL_COLUMNS`), and the
per-species `sp{N}_temp_multiplier` and `sp{N}_initial_condition` (both default `1`, emitted from
`OPTIONAL_SPECIES_COLUMNS`, `CsvBatchParser.cs:477-491`). Older templates predating these columns
still re-upload fine because all three are optional (section 3.6).

The species rows mirror the live `RunSpeciesList` asset loaded from
`Resources.Load<RunSpeciesList>("RunSpeciesList")`, plus one fully custom example species appended
to demonstrate adding your own (`CsvBatchParser.cs:464-471`, `:555-562`). Every templated species
is Tier 0, consistent with the Tier-1-only validation. The global data row is hard-coded to the
canonical defaults
(`default_batch,365,5,20,5,0,0,0,5,0,true,false,0,40,5000,0.15,0.1,0.7,,`,
`CsvBatchParser.cs:504`), deliberately not read from the in-session config so the template always
reflects shipped defaults. The `0.7` is the `autocorrelation_coefficient` value; the trailing empty
field is `temperature_timeseries_file`. Each per-species block ends with the `temp_multiplier` and
`initial_condition` values, written `1:1` from the source species (`CsvBatchParser.cs:540-541`).

Because `ConvertSpecies` converts Celsius to Kelvin on the three temperature columns, the template
writer inverts it (Kelvin minus 273.15) when emitting `opt_temp_c`, `lower_bound_c`, and
`upper_bound_c`, so a freshly downloaded template re-uploads byte-consistently
(`CsvBatchParser.cs:483-510`). The template download button and platform-specific save paths are
covered in `ui-and-io.md`.
