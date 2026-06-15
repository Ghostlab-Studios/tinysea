# CSV Output Formats

This document specifies every CSV file the TinySea headless simulation writes, column by column, with exact header strings, value formats, and the code that produces each. A developer should be able to reproduce a byte-equivalent file from this document.

Scope note. The shipping mode is Tier 1 (prey) only. `EcosystemSimulator.Tier2Enabled` defaults to `false` (EcosystemSimulator.cs:245) and `SimulationConfig.Tier2Enabled` defaults to `false` (SimulationConfig.cs:130). When the gate is off, every Tier 2 column is suppressed from the scenario CSV header and rows (SimulationRunner.cs:216-232, 270-286), and the aggregate writer drops the `Tier2Pop` column from the wide summary tables (ScenarioResult.cs:821-822). Tier 2 logic remains in the code from the original two tier design. Tier 2 columns are documented here, marked as legacy and suppressed in current runs, so you can recognize them if you flip the flag on. They do not reduce the depth of the Tier 1 description.

This document is self-contained for reconstruction. The production day loop is specified in Section 8, the temperature model in Section 9, the 10-step biology engine that computes every recorded value in Section 10, and the full parameter defaults in Section 11. The earlier sections (1 through 7) describe the CSV layout and reference Sections 8 through 11 for the values they serialize.

Related documents (cross reference only, not required for reconstruction):

- The bulk batch orchestration that produces the ZIP and `bulk_summary.csv`: `bulk-system.md`.
- Where each file is written (S3 vs progressive ZIP) and the download buttons: `ui-and-io.md`.

## 1. File inventory

| File | Produced by | One file per | Section 4 of this doc |
|------|-------------|--------------|-----------------------|
| `scenario_{N}.csv` | `SimulationRunner.ToCsvInternal` (SimulationRunner.cs:743) | scenario (one seeded run) | 3 |
| `aggregate.csv` | `AggregateResults.ToAggregateCsv` (ScenarioResult.cs:588) | run (one config, N scenarios) | 4 |
| `config.csv` | `AggregateResults.ToConfigCsv` -> `ConfigExporter.BuildConfigCsv` (ScenarioResult.cs:948, 1119) | run | 5 |
| `bulk_summary.csv` | `BulkSimulationController.GenerateBulkSummary` (BulkSimulationController.cs:428) | bulk upload (root of ZIP) | 6 |

In a bulk run the ZIP layout is one folder per run named by `BatchName`, each containing `scenario_1.csv` .. `scenario_K.csv`, `aggregate.csv`, and `config.csv`, plus a single `bulk_summary.csv` at the ZIP root (BulkSimulationController.cs:228, 309, 370-376, 388-390). `ConfigExporter` also exposes `BuildConfigJson` (ScenarioResult.cs:1011), used by the JSON download path. JSON is out of scope for this document except where it shares the species column list.

Number formatting throughout uses bare `float.ToString` unless a format specifier is shown. Specifiers seen in this document: `:F1`, `:F2`, `:F3`, `:F4` (fixed decimal places) and `:P1` (percent with one decimal). Booleans embedded in `#config:` lines are lowercased (`true`/`false`); booleans in row data use C# default `ToString` (`True`/`False`), noted where they occur.

Culture. No writer in `SimulationRunner`, `ScenarioResult`/`ConfigExporter`, or `BulkSimulationController` passes an `IFormatProvider`. Every numeric value, whether bare `ToString` or formatted with `:F*`/`:P1`, is rendered with the ambient `CultureInfo.CurrentCulture` of the process that runs the simulation (all formatting goes through C# string interpolation, for example `$"{x:F3}"` at SimulationRunner.cs:241-245, `$"{CrashRate:P1}"` at ScenarioResult.cs:627). These format strings are culture-sensitive: the decimal separator, the digit-group separator, and the percent sign placement all follow the current culture. The shipping build runs under invariant or en-US culture, so the document's examples assume that culture: the decimal point is `.`, there is no group separator, and `:P1` renders as `12.5%` with no space before the percent sign. Under a different `CurrentCulture` (for example `de-DE`) the identical code emits `12,5` and `12,5 %`, which corrupts numeric CSV columns. A reimplementer that needs byte-equivalent output must run under the same culture or force `CultureInfo.InvariantCulture` on every `ToString`/interpolation.

Float-to-string precision. Bare-float columns (every `#config:` value without a `:F*` specifier, the `# Base Temp`/`# Climate Trend` header values in 4.1, and the species-table Kelvin columns) use the runtime's default `float.ToString("G")`. Under Unity Mono and IL2CPP this produces the shortest round-trippable representation, so `20f` renders `20`, `0.15f` renders `0.15`, and `1f` renders `1` (not `1.0` or `0.10000000149`). A reimplementer on .NET Framework or a runtime with G7-style rounding can differ; for byte equality assume the Unity Mono/IL2CPP shortest round-trip behavior.

Non-finite values. Population `long` columns route every value through `SafePopToLong`, which maps `NaN`/`+Inf`/`-Inf` to `0L` (SimulationRunner.cs:594-598, see 3.3.2). Float columns formatted `:F*`/`:P1` have no such guard at the formatting site. `float.NaN` formats as the culture `NaNSymbol` (the string `NaN` under invariant/en-US) and `float.PositiveInfinity`/`NegativeInfinity` format as the culture infinity symbols, any of which would corrupt a numeric column. Several float columns are ratios that could divide by zero. The ones with an explicit guard are noted in their rows: `_BirthRate` is `0` when `startPop == 0` (SimulationRunner.cs:574), and `PopCv` is `0` when the mean is `<= 1e-4` or the computed variance is `<= 0` (SimulationRunner.cs:1196,1198). The denominators of the other ratios cannot be zero by construction: the aggregate `ExtinctionRate` divides by `Extinct + Survived`, which equals the scenario count (>= 1) for any species that appears (ScenarioResult.cs:413-418); `CrashRate`/`ExtinctionRate` in the stability sections divide by `a.N` (>= 1, the contributing scenario count). Mean and accumulator float columns are finite arithmetic over finite inputs. So in normal runs no float column emits a non-finite token, but the engine does not sanitize floats at the formatting site the way it sanitizes population longs, so a corrupted upstream value would print its culture symbol verbatim.

CSV quoting and escaping. There is none. Every value in every file, in all four formats, is written by C# string interpolation that concatenates the raw value with literal `,` separators (for example the `#species:` data row at SimulationRunner.cs:782-791, the aggregate per-species rows at ScenarioResult.cs:644-660, the bulk per-run rows at BulkSimulationController.cs:472,494). No writer wraps a field in double quotes, escapes an embedded quote by doubling it, or escapes embedded commas or newlines. This applies to every string-valued data cell: the `Name` and `Variant` cells in the `#species:` and `=== SPECIES ===` tables, the `Species`/`Variant`/`FullName` cells in every aggregate and bulk per-species section, and the `Run`/`BatchName` cells in the bulk summary. Column-name sanitization (Section 2) rewrites non-alphanumeric characters only on the header side for per-species column prefixes; it does not touch any data cell. So a `speciesLabel`, `variantLabel`, or `BatchName` that contains a comma, a double quote, or a newline is emitted verbatim and will break column alignment for that row. The shipping defaults (Cold/Warm/Hot labels, generated batch names) contain none of these characters, but a reimplementation that wants byte-equivalence must reproduce the verbatim, unquoted behavior rather than apply RFC 4180 quoting.

## 2. Species identity and column name sanitization

### 2.0 FullName, Name, and Variant

`FullName` is the primary species key and column-prefix source across every format: it orders the per-species daily columns, it is the argument to `SanitizeColumnName`, and it is the dictionary key for every per-species statistic, extinction block, and final-population map. Its construction must be reproduced exactly or no per-species column header or key will match.

There are two construction sites and they agree whenever the variant label is set, which is the case for every species that reaches output.

1. The in-engine value `SimSpecies.FullName` is a computed property (SimSpecies.cs:89):

   ```
   FullName = string.IsNullOrEmpty(VariantLabel) ? Name : $"{Name}_{VariantLabel}"
   ```

   `Name` is the species name string (the `speciesLabel`, or the `SpeciesName` enum name when the label is empty). `VariantLabel` is the free-text variant string. The separator is a single underscore `_`. When `VariantLabel` is empty, `FullName` is the bare `Name` with no separator and no variant suffix. The internal `ThermalVariant` enum name (`Arctic`/`Common`/`Tropical`/`Custom`) is never part of `FullName` and is never written to any CSV (SimSpecies.cs:82-89).

2. The aggregate and bulk writers do not have `SimSpecies` objects; they reconstruct the key from `SpeciesData` fields to build a `(Tier, Variant)` lookup. Both reconstructions are `{name}_{vlabel}` where `name = speciesLabel` (fallback `speciesName.ToString()`) and `vlabel = variantLabel` (fallback `variant.ToString()`, the enum name): ScenarioResult.cs:599-606 for `aggregate.csv` (`GetVariant`/`GetTier`), and BulkSimulationController.cs:334-342 for the bulk `SpeciesInfo` map.

The two agree exactly when `variantLabel` is non-empty: both yield `{Name}_{variantLabel}`. They diverge only when `variantLabel` is empty: site 1 yields bare `Name`, while site 2 yields `Name_{enumName}` (the reconstruction always appends `_{vlabel}` and falls back the empty label to the enum name). In that divergent case a lookup of the runtime `FullName` in the reconstructed map misses, and `GetVariant`/`GetTier` return their `Unknown`/`?` defaults (ScenarioResult.cs:609-610). The code keeps `variantLabel` populated on every path that reaches output (the default species set the label in `CreateHexapod`, for example `"Cold"`/`"Warm"`/`"Hot"` at SimSpecies.cs:169,180,189; bulk CSV rows carry a label per row), so in practice the labels are always present and the join succeeds. A reimplementation that leaves a variant label blank must replicate this exact fallback split to match both the column keys and the `Variant`/`Tier` annotations.

### 2.1 Column name sanitization

Per-species and per-rollup column prefixes are produced by `StepRecord.SanitizeColumnName(string fullName)` (SimulationRunner.cs:322-337). The exact rule:

```
SanitizeColumnName(fullName):
  if fullName is null or empty:        return "Unknown"
  for each char ch in fullName:
      keep ch if it is in [A-Za-z0-9_]   (ASCII letters, ASCII digits, underscore)
      otherwise replace ch with '_'
  if the first kept char is an ASCII digit:
      prepend '_'                       (result becomes "_" + sanitized)
  return result
```

Notes that matter for reproduction:

- The character test is ASCII-only by codepoint range. Non-ASCII letters (accented or non-Latin) are replaced with `_`, because downstream R and pandas pipelines do not handle them cleanly (SimulationRunner.cs:316-320).
- Underscores already present in the name are kept.
- The leading-digit guard prepends a single `_`. Example: a `FullName` of `3Hexapod` sanitizes to `_3Hexapod`.

Sanitization does not enforce uniqueness on its own. Two distinct names that sanitize to the same string are disambiguated by a `_2`, `_3`, ... numeric suffix at the point of header construction, applied in iteration order. The collision suffix logic appears in two header-building sites and is structurally identical, but each site maintains its own independent `HashSet<string> seen` scoped to that one call. There is no shared dedup set across the rollup columns and the per-species columns, and there is none across separate files. Collision detection runs on the sanitized PREFIX, before any field suffix (`_Pop`, `_Cond`, ...) is appended. So the suffix lands on the prefix, not on the full column name:

1. Per-species daily columns: `StepRecord.CsvHeader` keeps a `HashSet<string> seen` holding sanitized prefixes only. For each species it computes `baseName = SanitizeColumnName(sp.FullName)`, then `c = baseName`, and while `seen` already contains `c` it sets `c = $"{baseName}_{suffix}"` with `suffix` starting at 2 and incrementing until unique, adds `c` to `seen`, and only then appends the 17 field suffixes to `c` (SimulationRunner.cs:292-310). The first occurrence of prefix `Hexapod` yields `Hexapod_Pop`, `Hexapod_Cond`, ...; a second species that also sanitizes to `Hexapod` yields `Hexapod_2_Pop`, `Hexapod_2_Cond`, ... (suffix on the prefix, `Hexapod_2_Pop`, not `Hexapod_Pop_2`).
2. Tier-variant rollup columns: `StepRecord.BuildVariantRollupColumns` keeps a separate `HashSet<string> seenCols` and applies the same `_2`/`_3` suffix to the `Tier{tier}_{SanitizeColumnName(label)}` base name (SimulationRunner.cs:184-196). This set is independent of the per-species `seen` set even though both are built for the same header, so a rollup column and a per-species column never deduplicate against each other.

The daily rows do not recompute names: `ToCsvLine` walks the same `orderedSpecies` list in the same order and relies on positional alignment with the header (SimulationRunner.cs:235-247), and the rollup row values are keyed by the same `BuildVariantRollupColumns` output (SimulationRunner.cs:218-219). The collision suffix is therefore computed at header build time per call.

The aggregate and bulk summary blocks that emit a sanitized name without a suffix (for example the `INDIVIDUAL SCENARIOS` species columns, ScenarioResult.cs:737) call `SanitizeColumnName` directly with no collision handling, so two species whose `FullName` collides after sanitization would produce duplicate column headers there. In practice species `FullName`s are distinct identifiers, so a collision only arises with deliberately colliding custom names.

## 3. Scenario CSV (`scenario_{N}.csv`)

Built by `SimulationRunner.ToCsvInternal(scenarioIndex, numberOfScenarios, populationStats)` (SimulationRunner.cs:743-913). The file has four parts in this fixed order:

1. `#config:` comment lines (one parameter per line).
2. `#species:` table (one header line, then one line per species).
3. The daily data block (one header line, then one row per simulated day).
4. Trailing `#summary:` and `#extinction:` comment blocks.

Every line that begins with `#` is a comment for R's `read.csv()` (which treats `#` as the default comment character) and is skipped on import. The daily data block is the only part R reads as data. The variable `bool tier2` controls Tier 2 column emission and is `Ecosystem == null || Ecosystem.Tier2Enabled` (SimulationRunner.cs:802), so with the shipping default it is `false`.

The lines are emitted with `StringBuilder.AppendLine`, which on the target writes `\r\n`. The species order used by both the daily header and every daily row is computed once: `Ecosystem.Species.OrderBy(s => s.Tier).ThenBy(s => s.FullName)` (SimulationRunner.cs:798-800). That ordering is ascending tier, then ascending `FullName`, and it is the canonical per-species column order for the whole file.

A note on the comparer, because two different string comparisons are used in this codebase and they do not agree on case or punctuation. The `ThenBy(s => s.FullName)` here passes no comparer, so it uses LINQ's default, which is `Comparer<string>.Default`, that is `StringComparer.CurrentCulture`. This is culture-sensitive, not ordinal. The same culture-sensitive default applies to the aggregate per-species sorts (`OrderBy(k => k)`, ScenarioResult.cs:644,669,911), the `INDIVIDUAL SCENARIOS` species union (`new SortedSet<string>()`, ScenarioResult.cs:723), and the bulk species unions (`new SortedSet<string>()`, BulkSimulationController.cs:452,548). By contrast, the dynamic rollup column sort uses `string.CompareOrdinal` explicitly (SimulationRunner.cs:177-181), the rollup-column statistics list sorts with `StringComparer.Ordinal` (SimulationRunner.cs:645), and the `EXTINCTION TIMING - TIER VARIANTS` key set uses `new SortedSet<string>(StringComparer.Ordinal)` (ScenarioResult.cs:866). So the rollup and tier-variant column orders are ordinal, while the `FullName`-keyed orders (daily per-species columns, aggregate and bulk per-species sections) are current-culture. For ASCII identifiers of uniform case the two orders coincide; they diverge on mixed case, digits, and underscore relative to letters. A faithful reimplementation must use a current-culture compare for the `FullName` sorts and an ordinal compare for the rollup and tier-variant sorts, and must hold the culture fixed (see Section 1) for either to be deterministic across machines.

### 3.1 `#config:` block

Emitted at SimulationRunner.cs:749-767. Each line is literally `#config:` followed by `key,value`. The keys and value sources, in emission order:

| Line (key) | Value source | Notes |
|------------|--------------|-------|
| `model_version` | literal `v12-per-species-tracking` | Constant string (SimulationRunner.cs:749). |
| `days_per_scenario` | `TotalDays` | int. |
| `number_of_scenarios` | `numberOfScenarios` arg | int. |
| `scenario_index` | `scenarioIndex` arg | int, 1-based when called from the controllers. |
| `random_seed` | `UsedSeed` | int, the seed actually used; -1 means system-time seed. |
| `biology_step` | `BiologyStep` | int. |
| `base_temperature` | `TempCalc.BaseTemperature` | float, Celsius. |
| `seasonal_amplitude` | `TempCalc.SeasonalAmplitude` | float, Celsius. |
| `climate_trend_per_year` | `TempCalc.ClimateTrendPerYear` | float, Celsius per year. |
| `variability_magnitude` | `TempCalc.VariabilityMagnitude` | float. |
| `warming_bias` | `TempCalc.WarmingBias` | float. |
| `daily_variation_range` | `TempCalc.BaseRandomness` | float, default 5. One quantity carried under three names: the CSV key `daily_variation_range`, the calculator field `TempCalc.BaseRandomness` (default 5f, TemperatureCalculator.cs:23), and the config field that sets it at runtime, `SimulationConfig.DailyVariationRange` (default 5f, SimulationConfig.cs:105) or `BulkBatchConfig.DailyVarRange`. The config value is copied into `TempCalc.BaseRandomness` before the run (standard: SimulationController.cs:358; bulk: SimulationController.cs:300). |
| `randomness_growth_rate` | `TempCalc.RandomnessGrowthRate` | float, default 0.5 (SimulationConfig.cs:108, TemperatureCalculator.cs:24). |
| `autocorrelated` | `TempCalc.UseAutocorrelation.ToString().ToLower()` | `true` or `false`. |
| `autocorrelation_coefficient` | `TempCalc.AutocorrelationCoefficient` | float, default 0.7. Emitted immediately after `autocorrelated`. The AR(1) coefficient (phi) for daily temperature variation, in [0,1]. Set from `SimulationConfig.AutocorrelationCoefficient` (default 0.7f, SimulationConfig.cs:106) on the standard path (SimulationController.cs:370) or `BulkBatchConfig.AutocorrelationCoefficient` on the bulk path (SimulationController.cs:311). Emitted unconditionally, even when `autocorrelated` is false (it is then inert). |
| `temperature_bounds_min` | `TempCalc.MinTemp` | float, Celsius. Effective default -5. The runtime value comes from `SimulationConfig.TemperatureBoundsMin` (default -5f, SimulationConfig.cs:112), assigned into `TempCalc.MinTemp` at SimulationController.cs:362 (bulk: `batch.TempMin`, SimulationController.cs:304). The `TemperatureCalculator.MinTemp` field initializer (-5f, TemperatureCalculator.cs:27) is overwritten before any run, so the field default is not the effective default. |
| `temperature_bounds_max` | `TempCalc.MaxTemp` | float, Celsius. Effective default 50, not the 40 the calculator field shows in isolation. The runtime value comes from `SimulationConfig.TemperatureBoundsMax` (default 50f, SimulationConfig.cs:115), assigned into `TempCalc.MaxTemp` at SimulationController.cs:363 (bulk: `batch.TempMax`, SimulationController.cs:305). The `TemperatureCalculator.MaxTemp` field initializer is 40f (TemperatureCalculator.cs:28) but is overwritten before any run, so the shipped max-bound default is 50. |
| `carrying_capacity_tier1` | `Ecosystem.CarryingCapacityPerTier` | float, default 5000 (EcosystemSimulator.cs:237). |
| `condition_drain_rate` | `Ecosystem.ConditionDrainRate` | float, default 0.15 (EcosystemSimulator.cs:240). |
| `condition_recovery_rate` | `Ecosystem.ConditionRecoveryRate` | float, default 0.10 (EcosystemSimulator.cs:241). |

A bare `#` line follows the config block (SimulationRunner.cs:769). There is no `interannual_variation` key in the scenario `#config:` block; interannual settings are present only in `aggregate.csv` and `config.csv`.

### 3.2 `#species:` table

Emitted only when `RunSpecies` is non-null and has at least one species (SimulationRunner.cs:770-793). One header line then one data line per species, each prefixed with `#species:`. The bare `#` line that precedes the table (SimulationRunner.cs:769) and the bare `#` line that follows it (SimulationRunner.cs:794) are both outside the emission guard, so they are always written even when the `#species:` table itself is absent (`RunSpecies` null or empty). In that case the two consecutive bare `#` lines appear back to back with no table between them, and the four-part file structure still holds with an empty part 2.

Header (SimulationRunner.cs:772-777), exact string after the `#species:` prefix:

```
Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier,DeathThreshold,DeathRate,ReproThreshold,NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance,OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper,LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC,Pmax,CTminC,CTmaxC,TemperatureDebuff
```

The values iterate `RunSpecies.speciesList` (each element is a `SpeciesData`, SpeciesDatabase.cs:33). The column-by-column source (SimulationRunner.cs:778-792):

| Column | Source field on `SpeciesData` | Format | Notes |
|--------|-------------------------------|--------|-------|
| `Name` | `speciesLabel` if non-empty else `speciesName.ToString()` | string | The free-text label is preferred; the `SpeciesName` enum name is the fallback. |
| `Variant` | `variantLabel` if non-empty else the resolved `Name` value above | string | Falls back to the species name, not to the `SpeciesVariant` enum. This is the only variant string written; the `ThermalVariant`/`SpeciesVariant` enum names are never emitted. |
| `Tier` | `tier` | int | 0-based here. `SpeciesData.tier` is 0 for prey, 1 for predator (SpeciesDatabase.cs:51). This differs from the 1-based tier used in `aggregate.csv` per-species sections (see 4). |
| `InitialCount` | `count` | int | |
| `EatingAmount` | `eatingAmount` | float | |
| `ReproductionMultiplier` | `reproductionMultiplier` | float | |
| `DeathThreshold` | `deathThreshold` | float | |
| `DeathRate` | `deathRate` | float | |
| `ReproThreshold` | `reproThreshold` | float | |
| `NaturalDeathRate` | `naturalDeathRate` | float | |
| `NaturalDeathVariance` | `naturalDeathVariance` | float | |
| `HuntingEfficiency` | `huntingEfficiency` | float | Tier 1 convention value is 1.0; Tier 2 default 0.75. |
| `HuntingVariance` | `huntingVariance` | float | |
| `OptimalTempK` | `optimalTempK` | float | Kelvin. |
| `OptimalTempC` | `optimalTempK - 273.15f` | `:F2` | Celsius, two decimals. |
| `ArrhenBreadth` | `arrhenBreadth` | float | |
| `ArrhenLower` | `arrhenLower` | float | |
| `ArrhenUpper` | `arrhenUpper` | float | |
| `LowerBoundK` | `lowerBoundK` | float | Kelvin. |
| `LowerBoundC` | `lowerBoundK - 273.15f` | `:F2` | Celsius. |
| `UpperBoundK` | `upperBoundK` | float | Kelvin. |
| `UpperBoundC` | `upperBoundK - 273.15f` | `:F2` | Celsius. |
| `Pmax` | `pmax` | `:F2` | |
| `CTminC` | `ctMinC` | `:F2` | Celsius. |
| `CTmaxC` | `ctMaxC` | `:F2` | Celsius. |
| `TemperatureDebuff` | `TemperatureDebuff` | `:F2` | Per-species temperature offset, Celsius. |

This `#species:` table reflects the input species list (`RunSpecies`), not the in-engine `SimSpecies` objects. With Tier 2 disabled, the list passed to the runner has already had its Tier 2 entries removed at load (EcosystemSimulator.cs:336 drops them), but the `#species:` table iterates `RunSpecies.speciesList` directly, so whether a Tier 2 row appears here depends on what `RunSpecies` contains, independent of the `tier2` flag that gates the daily columns. In bulk runs `RunSpecies` is the per-batch `tempSpecies` built from the CSV rows (BulkSimulationController.cs:172-175), and the bulk parser rejects rows with non-zero tier, so only Tier 1 rows reach it.

### 3.3 Daily data block

Header from `StepRecord.CsvHeader(orderedSpecies, tier2)` (SimulationRunner.cs:264-313), then one row per record from `StepRecord.ToCsvLine(orderedSpecies, tier2)` (SimulationRunner.cs:209-249). There is one record per simulated day; `_records` is appended once per day in `RecordStep` (SimulationRunner.cs:583). A crash truncates the run, so the number of rows can be fewer than `TotalDays` (the loop breaks on crash, SimulationRunner.cs:440-447). The full day loop that drives every recorded value, including the exact ordering of temperature, biology, recording, and the crash break, is specified in Section 8; the biology engine that computes the values is specified in Section 10.

The header and the rows are built by the same code path with `tier2` controlling identical conditional segments, so the two always align. The columns, in order:

#### 3.3.1 Fixed leading columns (always present)

| # | Header | Row source (`StepRecord` field) | Format | Meaning |
|---|--------|----------------------------------|--------|---------|
| 1 | `Day` | `Day` | int | 1-based day index (`dayIndex + 1`, SimulationRunner.cs:425). |
| 2 | `Year` | `Year` | int | `(dayIndex / TemperatureCalculator.DAYS_PER_YEAR) + 1`, C# integer (truncating) division (SimulationRunner.cs:426). `DAYS_PER_YEAR == 365` (TemperatureCalculator.cs:30), a fixed constant with no leap-year handling, so day indices 0..364 are Year 1, 365..729 Year 2, and so on. The code uses the named constant, not a literal 365; the value is currently 365 but a reimplementation should treat the divisor as this shared constant so it stays in step if the constant is retuned. |
| 3 | `Temperature` | `Temperature` | `:F2` | Day temperature in Celsius from `TempCalc.GetTemperature` (SimulationRunner.cs:428). |
| 4 | `BiologyCycle` | `BiologyCycle` | int | The biology-cycle counter on biology days, 0 on skipped days (SimulationRunner.cs:485). The counter increments by 1 on each biology day starting at 1, and a day runs biology when `displayDay == 1` or `displayDay % BiologyStep == 0` (SimulationRunner.cs:425,430,434). See the biology-day predicate at the end of 3.3.3. With `BiologyStep = 1`, `BiologyCycle == Day`. |
| 5 | `StartPop` | `StartPop` | long | Total start-of-day population T1+T2, 0 on non-biology days (SimulationRunner.cs:488). |
| 6 | `EndPop` | `EndPop` | long | Total end-of-day population T1+T2 (SimulationRunner.cs:489). |
| 7 | `Tier1Pop` | `Tier1Pop` | long | Tier 1 total population (SimulationRunner.cs:494). |

Column 8 `Tier2Pop` (long, `Tier2Pop`) is emitted next only when `tier2` is true (SimulationRunner.cs:216, 270). Suppressed in shipping runs.

#### 3.3.2 Dynamic tier-variant rollup columns

After the tier totals, one column per distinct `(tier, variantLabel)` pair present in the run. The column set comes from `BuildVariantRollupColumns(orderedSpecies, tier2)` (SimulationRunner.cs:159-199) and is used identically by the header (SimulationRunner.cs:272-273) and rows (SimulationRunner.cs:218-219).

Construction of the column set:

1. For each species, compute its label as `VariantLabel` if non-empty, else `Name` (SimulationRunner.cs:172). This is the same label rule used everywhere for identity.
2. When `tier2` is false, species with `Tier != 1` are skipped (SimulationRunner.cs:171).
3. Collect distinct `(Tier, label)` pairs, then sort by tier ascending, then label ascending by ordinal string compare (SimulationRunner.cs:177-181).
4. The column name is `Tier{tier}_{SanitizeColumnName(label)}` with the `_2`/`_3` collision suffix from section 2 (SimulationRunner.cs:187-196).

Each row writes, for each such column, the summed population of all species in that `(tier, label)` group. The value is read from `StepRecord.TierVariantPop[col]`, defaulting to `0L` when absent (SimulationRunner.cs:218-219). `TierVariantPop` is filled in `RecordStep` by one pass over `Ecosystem.Species`, keying on the same `Tier{tier}_{SanitizeColumnName(label)}` string and adding `SafePopToLong(sp.Population)` (SimulationRunner.cs:548-551). Because both the rollup key and the per-species `_Pop` column use the same rounded `SafePopToLong(sp.Population)`, the tier-rollup invariant holds exactly: each `Tier{n}_{label}` column equals the sum of the per-species `_Pop` columns sharing that `(tier, label)`, and those rollup columns for a tier sum to that tier's `Tier{n}Pop` total.

Example: a run with Hexapod variants `Cold`, `Warm`, `Hot` (all Tier 1) yields rollup columns `Tier1_Cold`, `Tier1_Hot`, `Tier1_Warm` in that order (ordinal sort: Cold < Hot < Warm).

`SafePopToLong(float pop)` is `float.IsFinite(pop) ? (long)Math.Round(pop) : 0L` (SimulationRunner.cs:594-598). Every `long` population value in this file passes through it. Two rounding details a reimplementer must match exactly:

- The call is `Math.Round(pop)` with no `MidpointRounding` argument, so it uses the .NET default of round-half-to-even (banker's rounding): `2.5 -> 2`, `3.5 -> 4`, `0.5 -> 0`. This differs from the population rounding in biology Step 10, which is `Math.Round(sp.Population, MidpointRounding.AwayFromZero)` (EcosystemSimulator.cs:681) and rounds `2.5 -> 3`. The two sites disagree only on exact `.5` ties. In normal runs the disagreement rarely surfaces because populations reaching `SafePopToLong` were already rounded away-from-zero to whole numbers in Step 10, so the fractional part is 0 and both rules agree. It can still surface for derived sums such as `StartPop`/`EndPop`, which add two already-rounded tier totals and stay integral, and for any path that feeds a non-integral float in.
- Non-finite inputs (`NaN`, `+Inf`, `-Inf`) map to `0L` rather than the `long.MinValue` that a raw `(long)` cast of a non-finite float would produce.

#### 3.3.3 Death, birth, feeding, condition, accumulator, and reproduction columns

These follow the rollup columns. The table rows below are in exact top-to-bottom emission order, for both the `tier2 = true` and `tier2 = false` cases: dropping the gated rows from the list yields the literal Tier-1-only column order, and keeping them yields the full order. The header and the row writer append these segments in lockstep (header SimulationRunner.cs:274-286, rows SimulationRunner.cs:220-232), so the order is identical in both.

Every column except the last is emitted as `value` followed by a trailing comma, and every tier2-gated column is emitted in place by an `if (tier2) sb.Append($"{X},")` guard, so a gated column carries its own trailing comma and is simply omitted (with its comma) when `tier2` is false. There is no separate comma bookkeeping. The single exception is the final column: `ReproScaleT1` is written with no trailing comma (it is the last Tier-1 column), so when `tier2` is true `ReproScaleT2` is appended with a LEADING comma instead, `,{ReproScaleT2:F3}` (SimulationRunner.cs:232,286). Thus the row ends at `ReproScaleT1` when `tier2` is false and at `ReproScaleT2` when true, with exactly one comma between them in the latter case. The per-species block in 3.3.4 then follows, each of its fields likewise prefixed with a leading comma.

Sources (header SimulationRunner.cs:274-286, rows SimulationRunner.cs:220-232):

| Header | Row source (`StepRecord`) | Format | tier2-gated | Meaning |
|--------|---------------------------|--------|-------------|---------|
| `EatenT1` | `EatenT1` | long | no | Tier 1 deaths from predation that day. |
| `TempDeathsT1` | `TempDeathsT1` | long | no | Tier 1 thermal deaths (instant at lethal limits). |
| `TempDeathsT2` | `TempDeathsT2` | long | yes (legacy) | Tier 2 thermal deaths. |
| `ConditionDeathsT1` | `ConditionDeathsT1` | long | no | Tier 1 chronic-stress deaths. |
| `ConditionDeathsT2` | `ConditionDeathsT2` | long | yes (legacy) | Tier 2 chronic-stress deaths. |
| `NaturalDeathsT1` | `NaturalDeathsT1` | long | no | Tier 1 flat-rate natural mortality. |
| `NaturalDeathsT2` | `NaturalDeathsT2` | long | yes (legacy) | Tier 2 natural mortality. |
| `TotalDeaths` | `TotalDeaths` | long | no | Sum of all death sources T1+T2 that day (SimulationRunner.cs:476-478). |
| `BirthsT1` | `BirthsT1` | long | no | Tier 1 newborns that day. |
| `BirthsT2` | `BirthsT2` | long | yes (legacy) | Tier 2 newborns. |
| `FedRateT2` | `FedRateT2` | `:F3` | yes (legacy) | Tier 2 population-weighted feeding rate. |
| `AvgHuntingEff` | `AvgHuntingEff` | `:F3` | yes (legacy) | Tier 2 average hunting success. |
| `FedRateT1` | `FedRateT1` | `:F3` | no | Tier 1 population-weighted mean feeding rate. |
| `FoodDensityT1` | `FoodDensityT1` | `:F3` | no | Daily Tier 1 food density. See the formula and clamps below the table. |
| `AvgConditionT1` | `AvgConditionT1` | `:F3` | no | Tier 1 population-weighted mean Condition. |
| `AvgConditionT2` | `AvgConditionT2` | `:F3` | yes (legacy) | Tier 2 mean Condition. |
| `BirthAccumT1` | `BirthAccumT1` | `:F3` | no | Tier 1 fractional birth accumulator after this day. |
| `BirthAccumT2` | `BirthAccumT2` | `:F3` | yes (legacy) | Tier 2 birth accumulator. |
| `NaturalDeathAccumT1` | `NaturalDeathAccumT1` | `:F3` | no | Tier 1 natural-death accumulator. |
| `NaturalDeathAccumT2` | `NaturalDeathAccumT2` | `:F3` | yes (legacy) | Tier 2 natural-death accumulator. |
| `ConditionDeathAccumT1` | `ConditionDeathAccumT1` | `:F3` | no | Tier 1 condition-death accumulator. |
| `ConditionDeathAccumT2` | `ConditionDeathAccumT2` | `:F3` | yes (legacy) | Tier 2 condition-death accumulator. |
| `PredationAccumT1` | `PredationAccumT1` | `:F3` | no | Tier 1 predation accumulator (T2 has none). |
| `ReproScaleT1` | `ReproScaleT1` | `:F3` | no | Tier 1 condition-derived reproduction scale [0,1]. Last fixed column. |
| `ReproScaleT2` | `ReproScaleT2` | `:F3` | yes (legacy) | Tier 2 reproduction scale, written with a leading comma only when `tier2` (SimulationRunner.cs:232, 286). |

`FoodDensityT1` formula. The value stored in `LastFoodDensityT1` and emitted in this column is the Tier 1 supply cap computed in the feeding pass of biology Step 2 (EcosystemSimulator.cs:774-778):

```
capSafe          = max(CarryingCapacityPerTier, 1)  # cap floored at 1 (misconfig guard)
tier1Consumption = sum over live Tier 1 of sp.Population * max(1, sp.EatingAmount)  # appetite floored at 1
FoodDensityT1    = max(0, 1 - tier1Consumption / capSafe)  # float division, result clamped to >= 0
```

`CarryingCapacityPerTier` is the per-tier carrying capacity (the `carrying_capacity_tier1` `#config:` value, default 5000, EcosystemSimulator.cs:237). `tier1Consumption` sums `sp.Population * max(1, sp.EatingAmount)` over live Tier 1 species, taken at feeding time, before the current day's Step 10 rounding, so the populations are not pre-rounded to a `long`. It changed from a head count to a consumption sum: each individual draws its `EatingAmount` in resource points (floored at 1), so a higher appetite reaches the cap with fewer individuals and `FoodDensityT1` hits 0 sooner (EcosystemSimulator.cs:767-777). The division is float `/` float. The consumption is floored at 0 (guard against a corrupted negative population) and the denominator at 1 (guard against a non-positive `CarryingCapacityPerTier`); a byte-equivalent reimplementation must apply both floors. At `EatingAmount = 1` this equals the former `1 - tier1Pop/cap` head-count form.

On days that biology did not run (`biologyRan == false`), the event-count fields (`Eaten*`, `*Deaths*`, `Births*`, `TotalDeaths`, `ReproScale*`, `FedRateT2`, `AvgHuntingEff`) are recorded as 0 because `RecordStep` zeroes them (SimulationRunner.cs:467-473, 508-517). `FedRateT1`, `FoodDensityT1`, the `AvgCondition*` values, and all tier-level accumulator values are carried from the last computed values rather than zeroed, because density and condition do not change on a skipped-biology day (SimulationRunner.cs:518-535). The per-species physiology columns `_Cond`, `_ThermalPerf`, `_FinalPerf`, `_FedRate`, and `_HuntingEff` (Section 3.3.4) behave the same way: the per-species loop at SimulationRunner.cs:540-581 is not guarded by `biologyRan`, so those five columns are read from the current live `sp.*` values on a skipped day, not zeroed. Only the per-species event-count columns (`_Births`, `_TempDeaths`, `_CondDeaths`, `_NatDeaths`, `_Eaten`) and `_ReproScale` are zeroed on a skipped day, via the `biologyRan ?` guards at SimulationRunner.cs:559,570-573,575; the per-species accumulator columns (`_BirthAccum`, `_NatDeathAccum`, `_CondDeathAccum`, `_PredAccum`) are read live and so carry like the tier-level accumulators. Population columns (`Tier1Pop`, `EndPop`, the rollup columns, the per-species `_Pop`) always reflect the current rounded populations.

Initial and empty-pool defaults for the carried-forward fields. `LastFoodDensityT1` and `LastFedRateT1` both initialize to `1f` (EcosystemSimulator.cs:181-182), and `LastFedRateT2`/`LastAvgHuntingEfficiency` to `1f` (EcosystemSimulator.cs:176-177). So before the first biology cycle runs, and on any day where no Tier 1 species is alive (`LastFedRateT1` falls back to `1f` when the live Tier 1 population is 0, EcosystemSimulator.cs:780), the carried value is `1.000`, not `0`. The `AvgCondition*` carried default is likewise `1f` (EcosystemSimulator.cs:204-205) and the accumulator carried defaults are `0f` (EcosystemSimulator.cs:195-201). These initial values only become observable in the CSV if day 1 is a skipped-biology day, which requires `BiologyStep > 1`. With the shipping default `BiologyStep = 1` every day runs biology, so day 1 always computes fresh values and the skipped-day carry-over does not arise.

The skipped-day rules above depend on which days run biology. `SimulationRunner.Run` runs biology on day 1 and on every day whose 1-based index is a multiple of `BiologyStep`: `runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)`, where `displayDay = dayIndex + 1` (SimulationRunner.cs:425,430). On each biology day the cycle counter increments by 1 before the step runs, starting at 1 on day 1 (`_biologyCycleCounter++` then `ProcessBiologyStep`, SimulationRunner.cs:434-435), and the `BiologyCycle` column records that counter on biology days and `0` on skipped days (SimulationRunner.cs:485). With `BiologyStep = 1` every day is a biology day and `BiologyCycle == Day`.

#### 3.3.4 Appended per-species columns

After all tier-level columns, each species in `orderedSpecies` contributes exactly 17 columns. Header construction at SimulationRunner.cs:288-310, row construction at SimulationRunner.cs:235-247. The header prefix is `SanitizeColumnName(sp.FullName)` with the `_2`/`_3` collision suffix (section 2). For prefix `C`, the 17 columns in order are:

| Suffix | Header (`C` + suffix) | Row source (`PerSpeciesStepData`) | Format | Meaning |
|--------|------------------------|-----------------------------------|--------|---------|
| `_Pop` | `C_Pop` | `Population` | long | Rounded population (`SafePopToLong(sp.Population)`). |
| `_Cond` | `C_Cond` | `Condition` | `:F3` | Per-species Condition [0,1] (`sp.Condition`). |
| `_ThermalPerf` | `C_ThermalPerf` | `ThermalPerf` | `:F3` | `sp.RawThermalPerformance`, the Arrhenius performance without Pmax (SimSpecies.cs:74). |
| `_FinalPerf` | `C_FinalPerf` | `FinalPerf` | `:F3` | `sp.FinalPerformance`, assigned `sp.ThermalPerformance * sp.FedRate` (EcosystemSimulator.cs:628). Logging only, not a biology input. Note the `ThermalPerformance` factor here is the Pmax-scaled value (`RawThermalPerformance * Pmax`, SimSpecies.cs:74-75), so this column is a different quantity from the adjacent `_ThermalPerf` column above, which is `RawThermalPerformance` without Pmax. `_FinalPerf` therefore includes Pmax while `_ThermalPerf` does not. |
| `_FedRate` | `C_FedRate` | `FedRate` | `:F3` | Per-species feeding satisfaction. From `LastFedRateBySpecies[fn]`, fallback `sp.FedRate` (SimulationRunner.cs:567). |
| `_HuntingEff` | `C_HuntingEff` | `HuntingEff` | `:F3` | `sp.CurrentHuntingSuccess` for Tier 2 only, else 0 (SimulationRunner.cs:568). |
| `_Births` | `C_Births` | `Births` | long | Per-species births that day; 0 on non-biology days. From `LastBirthsBySpecies[fn]` (SimulationRunner.cs:559). |
| `_TempDeaths` | `C_TempDeaths` | `TempDeaths` | long | Per-species thermal deaths; 0 on non-biology days. |
| `_CondDeaths` | `C_CondDeaths` | `ConditionDeaths` | long | Per-species condition deaths; 0 on non-biology days. |
| `_NatDeaths` | `C_NatDeaths` | `NaturalDeaths` | long | Per-species natural deaths; 0 on non-biology days. |
| `_Eaten` | `C_Eaten` | `Eaten` | long | Per-species predation deaths (Tier 1 only; 0 for Tier 2); 0 on non-biology days. |
| `_BirthRate` | `C_BirthRate` | `BirthRate` | `:F4` | `Births / startPop` per day, 0 when startPop is 0 (SimulationRunner.cs:574). Four decimals. |
| `_ReproScale` | `C_ReproScale` | `ReproScale` | `:F3` | Per-species reproduction scale; from `LastReproScaleBySpecies[fn]`, fallback 0 (SimulationRunner.cs:575). |
| `_BirthAccum` | `C_BirthAccum` | `BirthAccum` | `:F3` | `Ecosystem.GetBirthAccum(fn)` residual after this day. |
| `_NatDeathAccum` | `C_NatDeathAccum` | `NaturalDeathAccum` | `:F3` | `Ecosystem.GetNaturalDeathAccum(fn)`. |
| `_CondDeathAccum` | `C_CondDeathAccum` | `ConditionDeathAccum` | `:F3` | `Ecosystem.GetConditionDeathAccum(fn)`. |
| `_PredAccum` | `C_PredAccum` | `PredationAccum` | `:F3` | `Ecosystem.GetPredationAccum(fn)` (Tier 1 only). |

The header field names (`Pop`, `Cond`, `ThermalPerf`, `FinalPerf`, `FedRate`, `HuntingEff`, `Births`, `TempDeaths`, `CondDeaths`, `NatDeaths`, `Eaten`, `BirthRate`, `ReproScale`, `BirthAccum`, `NatDeathAccum`, `CondDeathAccum`, `PredAccum`) are summarized in the doc comment at SimulationRunner.cs:254-258. When a species in `orderedSpecies` has no entry in a given day's `SpeciesData` dictionary, the row writer substitutes `default(PerSpeciesStepData)`, which is all zeros (SimulationRunner.cs:239-240).

When the dictionary-vs-fallback branch is taken for `_FedRate`, `_ReproScale`, and `_Births`. The three columns read from a `Last*BySpecies` dictionary keyed by `FullName` with a fallback for a missing key. The first action inside `ProcessBiologyStep` is to clear all seven per-species dictionaries and re-seed an entry for every current species (`LastBirthsBySpecies`, `LastReproScaleBySpecies`, `LastFedRateBySpecies`, etc. set to their zero default at EcosystemSimulator.cs:566-584), and `ProcessBiologyStep` runs only on biology days. Consequently:

- On a biology day, every species in `Ecosystem.Species` has an entry in all three dictionaries, so the dictionary branch is always taken and the fallback (`sp.FedRate` for `_FedRate`, `0f` for `_ReproScale`, `0L` for `_Births`) is never exercised for a current species. `_FedRate` reads the value written during that day's feeding pass (EcosystemSimulator.cs:778), `_Births` and `_ReproScale` read the values accumulated during reproduction.
- On a skipped-biology day, `_Births` and `_ReproScale` are forced to `0L`/`0f` by the `biologyRan ?` guard in `RecordStep` before the dictionary is consulted (SimulationRunner.cs:559,575), so their fallback path is unreachable on skipped days. `_FedRate` has no `biologyRan` guard, so it reads the dictionary value left from the most recent biology day; its `sp.FedRate` fallback is taken only when `fn` is absent from `LastFedRateBySpecies`, which happens only before the first biology cycle has ever run (day 1 skipped, requiring `BiologyStep > 1`). At that point `sp.FedRate` is the field's initial `1f` (SimSpecies.cs:76).

Because the dictionaries are keyed and re-seeded by the live `Ecosystem.Species` set each biology day, and the daily writer iterates that same set, the fallback never triggers in a `BiologyStep = 1` run.

Tier-rollup invariant for the per-species columns: for any `(tier, label)`, the sum of the `_Pop` columns of species in that group equals the matching `Tier{n}_{label}` rollup column (section 3.3.2). The per-species death and birth columns likewise sum to the tier-level `*T1`/`*T2` columns, because `RecordStep` builds both from the same per-species engine dictionaries and tier counters (the invariant is asserted in `EcosystemSimulator` per the shared design).

### 3.4 Trailing `#summary:` block

Emitted only when at least one record exists (SimulationRunner.cs:810). All lines are `#`-prefixed comments, so `read.csv` skips them. The block uses the per-day population statistics computed by `ComputePopulationStats` (SimulationRunner.cs:622) and per-species statistics computed inline (SimulationRunner.cs:826-860).

Column set for the summary block (`summaryCols`, SimulationRunner.cs:821-824): `Tier1Pop`, then `Tier2Pop` only when `tier2`, then one column per species in `orderedSpecies` using `SanitizeColumnName(sp.FullName)` (no collision suffix at this site). Layout:

```
#
#summary:Statistic,<summaryCols joined by comma>
#summary:Variant,All[,All if tier2]<,VariantLabel-or-Name per species>
#summary:Tier,1[,2 if tier2]<,sp.Tier per species>
#summary:Mean,<Tier means :F1><,per-species means :F1>
#summary:Max,<Tier max><,per-species max>
#summary:Min,<Tier min><,per-species min>
#summary:StdDev,<Tier stddev :F1><,per-species stddev :F1>
```

Row-by-row sources:

- `#summary:Statistic` header lists the column names (SimulationRunner.cs:866).
- `#summary:Variant` (SimulationRunner.cs:868-872): `All` for each tier-total column; per species, `VariantLabel` if non-empty else `Name`.
- `#summary:Tier` (SimulationRunner.cs:874-878): `1` for Tier1Pop, `2` for Tier2Pop (when present), and `sp.Tier` (1-based runtime tier) per species. Note the tier total annotation here uses 1 and 2 directly, and per-species uses `SimSpecies.Tier` which is 1-based, so this block is 1-based throughout, unlike the `#species:` table in 3.2 which is 0-based.
- `#summary:Mean` (SimulationRunner.cs:880-883): tier means from `stats.Mean["Tier1Pop"]`/`["Tier2Pop"]` formatted `:F1`; per-species means `spMean[fn]:F1`.
- `#summary:Max` (SimulationRunner.cs:885-888): `stats.Max[...]` (long) and `spMax[fn]` (long).
- `#summary:Min` (SimulationRunner.cs:890-893): `stats.Min[...]` and `spMin[fn]`.
- `#summary:StdDev` (SimulationRunner.cs:895-898): `stats.StdDev[...]:F1` and `spStdDev[fn]:F1`.

Per-species statistics are over all recorded days. `spMean` is the arithmetic mean of per-day population; `spMax`/`spMin` clamp the `long.MinValue`/`long.MaxValue` initial sentinels to 0 when no day was seen; `spStdDev` is the population standard deviation `sqrt(E[x^2] - E[x]^2)`, with negative variance from floating error clamped to 0 (SimulationRunner.cs:833-859).

The tier-total statistics come from `ComputePopulationStats`. That method computes Mean, Max, Min, StdDev for `Tier1Pop`, `Tier2Pop`, and every dynamic rollup column, and additionally an `ExtinctionDay` for each rollup column (SimulationRunner.cs:648-696). Only the two tier totals are surfaced in the `#summary:` block; the rollup-column statistics and per-species statistics from `ComputePopulationStats` flow instead into `ScenarioResult` for the aggregate file (section 4).

The exact algorithm of `ComputePopulationStats` (SimulationRunner.cs:622-735), which a reimplementer must reproduce because its outputs populate the scenario's `PopMean`/`PopMax`/`PopMin`/`PopStdDev`/`ExtinctionDay` dictionaries consumed by the aggregate file (Sections 4.8, 4.9):

1. Build the rollup column list `rollupCols` as the union of every record's `TierVariantPop` keys (a record only holds keys for tiers/labels alive that day), then sort `StringComparer.Ordinal` (SimulationRunner.cs:639-646).
2. For each column in `["Tier1Pop", "Tier2Pop"] + rollupCols`, walk all records tracking `max`, `min`, `sum` of `GetPopColumn(r, col)`, where `GetPopColumn` returns `r.Tier1Pop`/`r.Tier2Pop` for those two names and `r.TierVariantPop[col]` (or `0L`) otherwise (SimulationRunner.cs:611-619). Then `mean = sum / recordCount`, and a second pass computes `stddev = sqrt( sum((val - mean)^2) / recordCount )` (population standard deviation, not sample; SimulationRunner.cs:651-679). Store `Mean`/`Max`/`Min`/`StdDev` per column.
3. For each rollup column only, compute `ExtinctionDay`: walk records, set `wasAlive` once population > 0, and on the first record where `wasAlive && pop == 0` record `r.Day` and stop; otherwise -1 (SimulationRunner.cs:681-696).
4. For each species in `Ecosystem.Species`, compute per-`FullName` `Mean`/`Max`/`Min`/`StdDev` of `r.SpeciesData[fn].Population` (0 when absent) the same way, clamping the `long.MinValue`/`long.MaxValue` sentinels to 0 when no day was seen (SimulationRunner.cs:703-733). These per-species entries share the same `Mean`/`Max`/`Min`/`StdDev` dictionaries (keyed by `FullName` alongside the column names), which is why `PopMean` etc. carry both tier-total and per-species keys (Section 4.8).

Note the two standard-deviation formulas in the file differ in numerical form but both are population (divide by N) standard deviations: `ComputePopulationStats` uses the two-pass `sqrt(mean of squared deviations)` (SimulationRunner.cs:667-673), while the inline `#summary:` per-species block and `ComputePerSpeciesScenarioMetrics` use the one-pass `sqrt(E[x^2] - E[x]^2)` with a negative-variance clamp to 0 (SimulationRunner.cs:853-858). For the same data both yield the same value up to floating error.

### 3.5 Trailing `#extinction:` block

Emitted right after the summary block (SimulationRunner.cs:901-909). Header and rows are `#`-prefixed:

```
#
#extinction:Species,Variant,Tier,DayReachedZero
#extinction:<SanitizeColumnName(FullName)>,<VariantLabel-or-Name>,<sp.Tier>,<extinctionDay>
...
#
```

One row per species in `orderedSpecies`. `Species` is the sanitized `FullName` (no collision suffix). `Variant` is `VariantLabel` if non-empty else `Name`. `Tier` is `sp.Tier` (1-based runtime tier). `DayReachedZero` is the species' `spExtinctionDay[fn]`: the `Day` value of the first record where the population reached 0 after having been positive at some earlier record, or -1 if it never went extinct (SimulationRunner.cs:840-850, 903-908). A trailing bare `#` closes the file (SimulationRunner.cs:909).

## 4. Aggregate CSV (`aggregate.csv`)

Built by `AggregateResults.ToAggregateCsv()` (ScenarioResult.cs:588-926). This is a human-and-R-readable mixed-format file, not a single rectangular table. Section dividers use `=== TITLE ===`; metadata lines use `# Key,Value`; data sub-tables have their own header rows. `read.csv` cannot ingest the whole file at once; analysts read specific sections.

Two tier helpers resolve per-species metadata from `RunSpecies` once at the top (ScenarioResult.cs:594-610):

- `GetVariant(fullName)`: the `variantLabel` (fallback `variant.ToString()`) recorded for that `FullName`, or `Unknown` if not found.
- `GetTier(fullName)`: `sp.tier + 1` as a string, that is the 1-based tier, or `?` if not found (ScenarioResult.cs:605-606, 610). The lookup key is reconstructed as `{name}_{vlabel}` where `name` is `speciesLabel` (fallback `speciesName`) and `vlabel` is `variantLabel` (fallback `variant.ToString()`) (ScenarioResult.cs:599-604). This reconstruction is one of the two `FullName` construction sites discussed in 2.0. It matches the per-species dictionary keys (which come from the runtime `SimSpecies.FullName`) exactly when `variantLabel` is non-empty, which holds for every species that reaches output. If a species had an empty `variantLabel`, the runtime key would be the bare `Name` while this reconstruction would be `Name_{enumName}`, the lookup would miss, and these helpers would return `Unknown`/`?`. The per-species statistic values themselves still key correctly off the runtime `FullName`; only the `Variant` and `Tier` annotation columns depend on this reconstructed join.

Section order, as emitted (ScenarioResult.cs:612-925):

### 4.1 Header block

```
=== TINYSEA AGGREGATE RESULTS ===
# Run Name,<BatchName>
# Generated,<CompletedAt yyyy-MM-dd HH:mm:ss>
# Configuration,<DaysPerScenario> days x <TotalScenarios> scenarios
# Base Temp,<BaseTemperature>C
# Climate Trend,<ClimateTrend>C/year
# Carrying Capacity,<CarryingCapacity>
# Condition Drain Rate,<ConditionDrainRate>
# Condition Recovery Rate,<ConditionRecoveryRate>
<blank>
```

Sources: `BatchName` (set by the bulk controller from the CSV row, BulkSimulationController.cs:323; empty in standard runs), `CompletedAt`, `DaysPerScenario`, `TotalScenarios`, `BaseTemperature`, `ClimateTrend`, `CarryingCapacity`, `ConditionDrainRate`, `ConditionRecoveryRate`, all fields of `AggregateResults` (ScenarioResult.cs:612-621).

The `# Base Temp` and `# Climate Trend` lines append the unit string directly to a bare-float `ToString` with no format specifier and no space before the unit (`$"# Base Temp,{BaseTemperature}C"` and `$"# Climate Trend,{ClimateTrend}C/year"`, ScenarioResult.cs:616-617). With the default base temperature 20 and climate trend 1, and the shortest round-trip float rendering from Section 1, these lines read exactly:

```
# Base Temp,20C
# Climate Trend,1C/year
```

So the unit is `C` (or `C/year`) glued to the number with no separating space, and a value of 20 prints as `20C`, not `20.0C` or `20 C`. The other header values (`Carrying Capacity`, the two condition rates) are bare floats with no unit suffix.

### 4.2 `=== SUMMARY ===`

```
=== SUMMARY ===
Scenarios Run,<TotalScenarios>
Survived,<SurvivedScenarios>
Crashed,<CrashedScenarios>
Crash Rate,<CrashRate :P1>
Avg Crash Day,<AvgCrashDay :F1>      (only when CrashedScenarios > 0)
<blank>
```

`SurvivedScenarios`/`CrashedScenarios` are counted in `CalculateAggregates` (a scenario is crashed if `ScenarioResult.Crashed`, ScenarioResult.cs:315-336). `CrashRate = CrashedScenarios / TotalScenarios` (ScenarioResult.cs:355). `Avg Crash Day` is the mean `CrashDay` across crashed scenarios and is omitted entirely when none crashed (ScenarioResult.cs:628-631).

### 4.3 `=== POPULATION STATS (Survived Only) ===`

```
=== POPULATION STATS (Survived Only) ===
Avg Final,<AvgFinalTier1Pop :F1>
Min Final,<MinFinalTier1Pop>
Max Final,<MaxFinalTier1Pop>
<blank>
```

These are Tier 1 final-population statistics across surviving scenarios only (ScenarioResult.cs:634-637). `AvgFinalTier1Pop` is the mean of `FinalTier1Pop` over survived scenarios; `MinFinalTier1Pop`/`MaxFinalTier1Pop` are min/max over the same set, and are forced to 0 when no scenario survived (ScenarioResult.cs:338-363). Tier 2 equivalents exist as fields but are not printed in this section.

### 4.4 `=== PER-SPECIES POPULATION STATS (All Scenarios) ===`

Emitted only when `PerSpeciesAvg` is non-empty (ScenarioResult.cs:640). Header and one row per species, species keys sorted ascending by `FullName` (ScenarioResult.cs:644):

```
=== PER-SPECIES POPULATION STATS (All Scenarios) ===
Species,Variant,Tier,Avg,SurvivedAvg,Min,Max,Extinct,Survived,ExtinctionRate
<FullName>,<Variant>,<Tier>,<Avg :F1>,<SurvivedAvg :F1>,<Min>,<Max>,<Extinct>,<Survived>,<ExtinctionRate :P1>
```

| Column | Source | Format |
|--------|--------|--------|
| `Species` | dictionary key = `FullName` (unsanitized) | string |
| `Variant` | `GetVariant(key)` | string |
| `Tier` | `GetTier(key)` (1-based) | string |
| `Avg` | `PerSpeciesAvg[key]` = mean final population over all scenarios incl. crashed | `:F1` |
| `SurvivedAvg` | `PerSpeciesSurvivedAvg[key]` = mean final population over scenarios where this species' final pop > 0 | `:F1` |
| `Min` | `PerSpeciesMin[key]` final population (0 if never seen) | float printed plain |
| `Max` | `PerSpeciesMax[key]` final population | float printed plain |
| `Extinct` | `PerSpeciesExtinct[key]` = count of scenarios where final pop <= 0 | int |
| `Survived` | `PerSpeciesSurvived[key]` = count where final pop > 0 | int |
| `ExtinctionRate` | `Extinct / (Extinct + Survived)` | `:P1` |

All per-species inputs are computed from each scenario's `FinalSpeciesPopulations` dictionary keyed by `FullName` (ScenarioResult.cs:365-420). `FinalSpeciesPopulations` is captured at end of run as the rounded final population per species (SimulationRunner.cs:1039-1045).

### 4.5 `=== CONDITION STATS ===`

```
=== CONDITION STATS ===
Avg Condition (All Scenarios),<AvgConditionT1 :F3>
Avg Final Condition (Survived),<AvgFinalConditionT1 :F3>
<blank>
```

`AvgConditionT1` is `sumCondT1 / TotalScenarios`, the grand mean of each scenario's full-run average Tier 1 Condition over ALL scenarios, including crashed ones (ScenarioResult.cs:352; the accumulator `sumCondT1` adds every scenario's `AvgConditionT1` unconditionally, ScenarioResult.cs:311-313). Each scenario's `AvgConditionT1` is the per-day mean of the daily `AvgConditionT1` over that scenario's whole record list (SimulationRunner.cs:992-1002). For a crashed scenario this per-day mean includes the post-extinction days, where biology no longer updates condition and the recorded value sticks at its last pre-crash value or the initial 1.0, so crashed scenarios contribute that potentially stuck condition into the divisor `TotalScenarios`. This is the aggregate-level counterpart, but NOT the same rule, as the bulk-level v12.3 cross-run condition aggregation, which averages over surviving runs only to avoid exactly that stuck-1.0 bias (see 6.5.2). In contrast, `AvgFinalConditionT1` is `sumFinalCondT1 / survivedCount`, the mean final-day Tier 1 Condition across survived scenarios only (the accumulator is updated solely in the non-crashed branch, ScenarioResult.cs:328,342). Tier 2 condition fields exist but are not printed here.

### 4.6 v12 per-species rich sections

The next three sections are emitted only when `PerSpeciesMetrics` is non-empty (ScenarioResult.cs:665). Keys are iterated sorted ascending by `FullName`. Each row carries explicit `Species,Variant,Tier` columns (`GetVariant`/`GetTier`). The values come from `PerSpeciesAggregate` objects, which aggregate per-scenario `PerSpeciesScenarioMetrics` across the run's scenarios via `ComputeAggStat` and `ComputeExtinctionStat` (ScenarioResult.cs:431-544). Each `AggStat` carries `Mean`, `StdDev`, `Min`, `Max`, `SurvivedMean`, `SurvivedStdDev` (ScenarioResult.cs:146-154); `SurvivedMean`/`SurvivedStdDev` are computed over only the scenarios where that species survived.

`final year` means the last 365 days of a scenario; for scenarios shorter than 365 days it covers all days, so final-year metrics equal full-run metrics (SimulationRunner.cs:1047-1052, 1067).

#### 4.6.0 Per-scenario metric construction and cross-scenario aggregation

The `PerSpeciesAggregate` objects these sections print are built in two stages. First, each scenario produces a `PerSpeciesScenarioMetrics` per species via `ComputePerSpeciesScenarioMetrics` (SimulationRunner.cs:1060-1186). Then `BuildPerSpeciesAggregate` aggregates those per-scenario structs across the run's scenarios (ScenarioResult.cs:431-544). Both stages must be reproduced to get the numeric values; the section layouts above only describe the columns.

Per-scenario construction (`ComputePerSpeciesScenarioMetrics`, SimulationRunner.cs:1060-1186). Constants: `FINAL_YEAR_DAYS = 365`, `CRASH_FRACTION = 0.05`, `CRASH_FLOOR = 10` (SimulationRunner.cs:1052-1058). With `totalDays = _records.Count` and `finalYearStart = max(0, totalDays - 365)`, for each species `fn = sp.FullName` it walks the records once, reading `d = rec.SpeciesData[fn]` (skipping records where the species is absent):

```
on record i:
  if i == 0: startPop = d.Population
  finalPop = d.Population
  minPop = min(minPop, d.Population);  maxPop = max(maxPop, d.Population)
  if d.Population > 0: wasAlive = true
  if extinctionDay < 0 and wasAlive and d.Population == 0: extinctionDay = rec.Day
  if crashDay < 0 and startPop > 0:
      threshold = max(CRASH_FLOOR, (long)(startPop * CRASH_FRACTION))   # max(10, 0.05*startPop)
      if d.Population < threshold: crashDay = rec.Day
  # full-run accumulators (condition / birth rate only on alive days):
  if d.Population > 0: condSumFull += d.Condition; condCountFull++; brSumFull += d.BirthRate; brCountFull++
  popSumFull += d.Population; popSqSumFull += d.Population^2; popCountFull++
  # final-year window (i >= finalYearStart): same alive-only filter for condition/birthrate,
  #   population includes all days, plus per-pathway death sums:
  if i >= finalYearStart:
      if d.Population > 0: condSumYear += d.Condition; condCountYear++; brSumYear += d.BirthRate; brCountYear++
      popSumYear += d.Population; popSqSumYear += d.Population^2; popCountYear++
      tempDeathsYear += d.TempDeaths; condDeathsYear += d.ConditionDeaths
      natDeathsYear  += d.NaturalDeaths; predDeathsYear += d.Eaten
```

The alive-only filter on Condition and BirthRate matters: once a species hits 0 its recorded Condition sticks (biology stops updating it), so dead-day samples would distort the mean. Population accumulators include zero days (zero is a real datum). The resulting struct fields (SimulationRunner.cs:1162-1183):

- `MeanConditionFullRun`/`FinalYear` = `condSum / condCount` over alive days (0 when no alive days; final-year falls back to the full-run mean when the final-year alive count is 0).
- `MeanBirthRateFullRun`/`FinalYear` = `brSum / brCount` (same fallback rule). `BirthRate` is the per-day `_BirthRate` column (`Births / startOfDayPop`, 0 when startPop is 0).
- `PopCvFullRun`/`FinalYear` = `ComputeCvFromSums(popSum, popSqSum, popCount)` = `sqrt(variance)/mean` with `variance = popSqSum/count - mean^2`, returning 0 when `count <= 0`, when `mean <= 1e-4`, or when `variance <= 0` (SimulationRunner.cs:1192-1200). Final-year CV falls back to the full-run CV when the final-year population count is 0.
- `MeanPopulationFinalYear` = `popSumYear / popCountYear` (falls back to the full-run mean population when the final-year count is 0).
- `FinalYearTempDeaths`/`ConditionDeaths`/`NaturalDeaths`/`PredationDeaths` = the four final-year death-count sums (floats). `PredationDeaths` is the final-year `Eaten` total, Tier 1 only.
- `MinPopulation`/`MaxPopulation` = the run extremes (sentinels clamped to 0 when never seen), `ExtinctionDay`/`CrashDay` as above, `FinalPopulation = finalPop`, and `Survived = FinalPopulation > 0`.

A species with no record at all is skipped (`popCountFull == 0`, SimulationRunner.cs:1157). The per-scenario structs are stored on `ScenarioResult.SpeciesMetrics` keyed by `FullName` (SimulationRunner.cs:1017).

Cross-scenario aggregation (`BuildPerSpeciesAggregate`, ScenarioResult.cs:431-482). For the union of `FullName`s across all scenarios' `SpeciesMetrics`, it collects the per-scenario `PerSpeciesScenarioMetrics` into a list `rows`, sets `N = rows.Count` and `NSurvived = count of rows where Survived`, and computes one `AggStat` per metric via `ComputeAggStat(rows, selector)` and the two timing stats via `ComputeExtinctionStat`:

- `ComputeAggStat` (ScenarioResult.cs:484-520) walks the rows accumulating `sum`, `sqSum`, `min`, `max` over all rows and `sSum`, `sSqSum` over survived rows only. It sets `Mean = sum/n`, `StdDev = sqrt(sqSum/n - Mean^2)` clamped to 0 when the variance is `<= 0`, `Min`, `Max`, and (when any survived) `SurvivedMean = sSum/nSurvived`, `SurvivedStdDev` likewise clamped. So every `AggStat` column in Sections 4.6.1 through 4.6.3 (`*_Mean`, `*_StdDev`, `*_SurvivedMean`, `*_Min`, `*_Max`) is a cross-scenario statistic of the corresponding per-scenario metric.
- `ComputeExtinctionStat` (ScenarioResult.cs:522-544) is the day-aggregation: it skips rows whose day is `< 0` (the no-event sentinel) into `NNonEvents`, counts the rest into `NEvents`, and reports `MinDay`/`MaxDay`/`MeanDay = sum/NEvents` over event days only, all -1 when `NEvents == 0`. `ExtinctionTiming` aggregates `r.ExtinctionDay`, `CrashTiming` aggregates `r.CrashDay`.

These `PerSpeciesAggregate` objects are stored on `AggregateResults.PerSpeciesMetrics` and are exactly what Sections 4.6.1 through 4.6.3, 4.10, and the bulk Sections 6.5.1 through 6.5.3 read.

#### 4.6.1 `=== PER-SPECIES FINAL YEAR METRICS ===`

Header (ScenarioResult.cs:668):

```
Species,Variant,Tier,N,NSurvived,MeanCondition,MeanCondition_StdDev,MeanCondition_SurvivedMean,MeanBirthRate,MeanBirthRate_StdDev,MeanBirthRate_SurvivedMean,PopCv,PopCv_StdDev,MeanPop,MeanPop_StdDev,MeanPop_SurvivedMean
```

| Column | Source (per `PerSpeciesAggregate a`) | Format |
|--------|--------------------------------------|--------|
| `N` | `a.N` = number of scenarios contributing | int |
| `NSurvived` | `a.NSurvived` = scenarios where species survived | int |
| `MeanCondition` | `a.MeanConditionFinalYear.Mean` | `:F3` |
| `MeanCondition_StdDev` | `a.MeanConditionFinalYear.StdDev` | `:F3` |
| `MeanCondition_SurvivedMean` | `a.MeanConditionFinalYear.SurvivedMean` | `:F3` |
| `MeanBirthRate` | `a.MeanBirthRateFinalYear.Mean` | `:F4` |
| `MeanBirthRate_StdDev` | `a.MeanBirthRateFinalYear.StdDev` | `:F4` |
| `MeanBirthRate_SurvivedMean` | `a.MeanBirthRateFinalYear.SurvivedMean` | `:F4` |
| `PopCv` | `a.PopCvFinalYear.Mean` | `:F3` |
| `PopCv_StdDev` | `a.PopCvFinalYear.StdDev` | `:F3` |
| `MeanPop` | `a.MeanPopulationFinalYear.Mean` | `:F1` |
| `MeanPop_StdDev` | `a.MeanPopulationFinalYear.StdDev` | `:F1` |
| `MeanPop_SurvivedMean` | `a.MeanPopulationFinalYear.SurvivedMean` | `:F1` |

(ScenarioResult.cs:669-677.) `MeanCondition` and `MeanBirthRate` per scenario are computed over alive-only days; `PopCv` is the population coefficient of variation (StdDev/Mean), which `ComputeCvFromSums` returns as `0` in two guarded cases: when the mean is `<= 1e-4` (CV undefined for a near-zero mean, SimulationRunner.cs:1196) and when the computed variance is `<= 0` (a numerical guard against tiny negative variance from the sum-of-squares formula, SimulationRunner.cs:1198). `MeanPop` is the mean final-year population including zero days (SimulationRunner.cs:1119-1199).

#### 4.6.2 `=== PER-SPECIES FULL-RUN METRICS ===`

Header (ScenarioResult.cs:681):

```
Species,Variant,Tier,N,NSurvived,MeanCondition,MeanCondition_StdDev,MeanBirthRate,MeanBirthRate_StdDev,PopCv,PopCv_StdDev
```

Same metrics as 4.6.1 but over the entire scenario, using the `*FullRun` `AggStat`s: `MeanConditionFullRun`, `MeanBirthRateFullRun`, `PopCvFullRun` (ScenarioResult.cs:685-688). `MeanCondition`/`MeanCondition_StdDev` `:F3`, `MeanBirthRate`/`_StdDev` `:F4`, `PopCv`/`_StdDev` `:F3`. There are no `MeanPop`/`SurvivedMean` columns in this section.

#### 4.6.3 `=== PER-SPECIES STABILITY METRICS ===`

Header (ScenarioResult.cs:693):

```
Species,Variant,Tier,N,NSurvived,MinPop_Mean,MinPop_Min,MaxPop_Mean,MaxPop_Max,FinalPop_Mean,FinalPop_SurvivedMean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay
```

| Column | Source | Format |
|--------|--------|--------|
| `MinPop_Mean` | `a.MinPopulation.Mean` | `:F1` |
| `MinPop_Min` | `a.MinPopulation.Min` | `:F0` |
| `MaxPop_Mean` | `a.MaxPopulation.Mean` | `:F1` |
| `MaxPop_Max` | `a.MaxPopulation.Max` | `:F0` |
| `FinalPop_Mean` | `a.FinalPopulation.Mean` | `:F1` |
| `FinalPop_SurvivedMean` | `a.FinalPopulation.SurvivedMean` | `:F1` |
| `ExtinctionRate` | `a.ExtinctionTiming.NEvents / a.N` | `:P1` |
| `MeanExtinctionDay` | `a.ExtinctionTiming.MeanDay` | `:F1` |
| `CrashRate` | `a.CrashTiming.NEvents / a.N` | `:P1` |
| `MeanCrashDay` | `a.CrashTiming.MeanDay` | `:F1` |

(ScenarioResult.cs:697-704.) `ExtinctionStat` carries `NEvents` (scenarios where the day was not -1), `NNonEvents`, `MinDay`, `MaxDay`, `MeanDay` (-1 when no events) (ScenarioResult.cs:160-167, 522-543). `ComputeExtinctionStat` skips every scenario whose day is `< 0` (the -1 no-event sentinel) via `continue`, counting it only into `NNonEvents`, so `MinDay`/`MaxDay`/`MeanDay` are computed strictly over event days and `MeanDay = sum / NEvents` never averages in a -1 (ScenarioResult.cs:528-541). When `NEvents == 0` all three stay at their -1 initial value. A per-scenario `ExtinctionDay` is the first day the species hit 0 after being alive; `CrashDay` is the first day population dropped below `max(CRASH_FLOOR=10, CRASH_FRACTION=0.05 * StartPop)` (SimulationRunner.cs:1054-1117, 1182).

### 4.7 `=== INDIVIDUAL SCENARIOS ===`

Wide table, one row per scenario, three header rows (ScenarioResult.cs:709-766). The per-species columns are the union of every scenario's `FinalSpeciesPopulations` keys, sorted alphabetically by `FullName` via `SortedSet` (ScenarioResult.cs:719-730), sanitized with `SanitizeColumnName` (no collision suffix).

Header row 1 (column names, ScenarioResult.cs:736-738):

```
Scenario,Seed,Crashed,CrashDay,CrashTier,FinalPop,AvgTemp,MinTemp,MaxTemp<,SanitizedFullName per species>
```

Header row 2 (Variant annotation, ScenarioResult.cs:742-744): `Variant` then four empty cells (under Seed, Crashed, CrashDay, CrashTier), then `All` under `FinalPop`, then three empty cells (under AvgTemp, MinTemp, MaxTemp), then `GetVariant(k)` per species. The literal prefix is `Variant,,,,,All,,,`.

Header row 3 (Tier annotation, ScenarioResult.cs:748-749): `Tier,,,,,1,,,` then `GetTier(k)` per species.

Data rows (ScenarioResult.cs:755-765), per scenario `s`:

| Column | Source | Format |
|--------|--------|--------|
| `Scenario` | `s.ScenarioIndex` | int |
| `Seed` | `s.RandomSeed` | int |
| `Crashed` | `s.Crashed` | `True`/`False` (C# bool default ToString) |
| `CrashDay` | `s.CrashDay` | int (-1 if no crash) |
| `CrashTier` | `s.CrashTier` | int (-1 if no crash) |
| `FinalPop` | `s.FinalTier1Pop` | long |
| `AvgTemp` | `s.AvgTemperature` | `:F2` |
| `MinTemp` | `s.MinTemperature` | `:F2` |
| `MaxTemp` | `s.MaxTemperature` | `:F2` |
| per species | `s.FinalSpeciesPopulations[k]` or 0L if absent | long |

The `FinalPop` column carries only Tier 1; the dynamic tier-variant rollup columns are omitted because per-species columns subsume them (ScenarioResult.cs:716-718).

### 4.8 `=== SUMMARY STATISTICS (Grand Mean Across All Scenarios) ===`

Emitted only when some scenario has non-null `PopMean` (ScenarioResult.cs:780-781). Wide grand-mean table preceded by a blank line. Per-species column order is `PerSpeciesMetrics` keys sorted ascending (ScenarioResult.cs:786-788). Three header rows then four data rows.

Header row 1 (ScenarioResult.cs:827-830): `Statistic,Tier1Pop<,SanitizedFullName per species>`. Only `Tier1Pop` is included as a tier total here (no `Tier2Pop`).

Header row 2 (ScenarioResult.cs:834-836): `Variant,All<,GetVariant(key) per species>`.

Header row 3 (ScenarioResult.cs:840-842): `Tier,1<,GetTier(key) per species>`.

Data rows, one per statistic name in `{Mean, Max, Min, StdDev}` (ScenarioResult.cs:845-853):

```
GrandMean_<statName>,<GrandMean(statName,"Tier1Pop") :F1><,GrandMean(statName,key) :F1 per species>
```

`GrandMean(statName, col)` averages, across scenarios, the per-scenario statistic for that column from the scenario's `PopMean`/`PopMax`/`PopMin`/`PopStdDev` dictionaries (ScenarioResult.cs:794-821). All four statistic rows are formatted `:F1`. The doc comment warns that `GrandMean_Min`/`GrandMean_Max` are means of per-scenario mins/maxes, not real population extrema (ScenarioResult.cs:777-779). The per-scenario `PopMean` etc. dictionaries are populated from `ComputePopulationStats`, keyed by both tier-total names and per-species `FullName` (SimulationRunner.cs:1030-1034).

### 4.9 `=== EXTINCTION TIMING - TIER VARIANTS (Across All Scenarios) ===`

Emitted only when some scenario has non-null `ExtinctionDay` (ScenarioResult.cs:859-860). Preceded by a blank line.

```
=== EXTINCTION TIMING - TIER VARIANTS (Across All Scenarios) ===
Variant,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived
<variantKey>,<min>,<max>,<avg :F1>,<numExtinct>,<numSurvived>
```

The variant keys are the union of every scenario's `ExtinctionDay` keys, sorted ordinal (ScenarioResult.cs:866-871). Each key is a dynamic rollup column name `Tier{n}_{variantLabel}`. For each, scenarios where the day is -1 count toward `NumSurvived`; other days are collected. When no extinction events occurred for a key, the row is `<variant>,-1,-1,-1,0,<numSurvived>`; otherwise `MinDays`/`MaxDays`/`AvgDays` are the min/max/mean over the event days, `AvgDays` formatted `:F1` (ScenarioResult.cs:873-900). Each scenario's `ExtinctionDay` dictionary is the per-rollup-column extinction day from `ComputePopulationStats` (SimulationRunner.cs:681-695, 1034).

### 4.10 `=== EXTINCTION TIMING - PER SPECIES (Across All Scenarios) ===`

Emitted only when `PerSpeciesMetrics` is non-empty (ScenarioResult.cs:905). Preceded by a blank line. Keys sorted ascending by `FullName`.

```
=== EXTINCTION TIMING - PER SPECIES (Across All Scenarios) ===
Species,Variant,Tier,MinDays,MaxDays,AvgDays,NumExtinct,NumSurvived
```

For each species, the values come from `PerSpeciesMetrics[key].ExtinctionTiming`. When `NEvents == 0`: `<key>,<Variant>,<Tier>,-1,-1,-1,0,<NNonEvents>`. Otherwise `MinDay:F0,MaxDay:F0,MeanDay:F1,NEvents,NNonEvents` (ScenarioResult.cs:911-922).

## 5. Config CSV (`config.csv`)

Built by `ConfigExporter.BuildConfigCsv(...)` (ScenarioResult.cs:1119-1193), called from `AggregateResults.ToConfigCsv()` (ScenarioResult.cs:948-960). It contains configuration and the species table only, no results. Also reachable via `ConfigExporter.ToCsv(SimulationConfig)` for the standalone Download Config button (ScenarioResult.cs:992-1006). Sections use `=== SECTION ===` and `Parameter,Value` rows. Full layout:

```
=== TINYSEA CONFIGURATION ===
# Exported,<now yyyy-MM-dd HH:mm:ss>
<blank>
=== SIMULATION ===
Days Per Scenario,<daysPerScenario>
Number Of Scenarios,<numberOfScenarios>
Biology Step,<biologyStep>
Random Seed,<randomSeed>
<blank>
=== CARRYING CAPACITY (always on as of v11.1) ===
Tier 1 Limit,<carryingCapacity>
<blank>
=== CONDITION SYSTEM ===
Condition Drain Rate,<conditionDrainRate>
Condition Recovery Rate,<conditionRecoveryRate>
<blank>
=== TEMPERATURE ===
Base Temperature,<baseTemperature>
Seasonal Amplitude,<seasonalAmplitude>
Climate Trend Per Year,<climateTrend>
Interannual Variation,<interannualVariation lowercased>
Variability Magnitude,<variabilityMagnitude>
Warming Bias,<warmingBias>
Autocorrelated,<autocorrelated lowercased>
Daily Variation Range,<dailyVariationRange>
Randomness Growth Rate,<randomnessGrowthRate>
Bounds Min,<temperatureBoundsMin>
Bounds Max,<temperatureBoundsMax>
<blank>
=== SPECIES ===
Name,Variant,Tier,InitialCount,EatingAmount,ReproductionMultiplier,DeathThreshold,DeathRate,ReproThreshold,NaturalDeathRate,NaturalDeathVariance,HuntingEfficiency,HuntingVariance,OptimalTempK,OptimalTempC,ArrhenBreadth,ArrhenLower,ArrhenUpper,LowerBoundK,LowerBoundC,UpperBoundK,UpperBoundC,Pmax,CTminC,CTmaxC,TemperatureDebuff
<one row per species>
```

Parameter sources, in order (ScenarioResult.cs:1131-1163): `daysPerScenario`, `numberOfScenarios`, `biologyStep`, `randomSeed`, `carryingCapacity`, `conditionDrainRate`, `conditionRecoveryRate`, `baseTemperature`, `seasonalAmplitude`, `climateTrend`, `interannualVariation` (lowercased bool), `variabilityMagnitude`, `warmingBias`, `autocorrelated` (lowercased bool), `dailyVariationRange`, `randomnessGrowthRate`, `temperatureBoundsMin`, `temperatureBoundsMax`. These are the parameters of `ConfigExporter.BuildConfigCsv` (ScenarioResult.cs:1119-1127). When called from `AggregateResults.ToConfigCsv`, they come from the `AggregateResults` fields (ScenarioResult.cs:950-959); when called from `ConfigExporter.ToCsv(SimulationConfig)`, from the `SimulationConfig` fields (ScenarioResult.cs:996-1005). The backing field for `interannualVariation` is `AggregateResults.InterannualVariation` (a bool, ScenarioResult.cs:230) in the first path and `SimulationConfig.InterannualVariation` (bool, default true, SimulationConfig.cs:91) in the second; it gates `TempCalc.UseInterannualVariation` at runtime (SimulationController.cs:361). This is the only place interannual variation appears in any CSV: the scenario `#config:` block omits it entirely (see 3.1).

The `=== SPECIES ===` table is emitted only when `runSpecies` has at least one species (ScenarioResult.cs:1166). Its header string is byte-identical to the scenario `#species:` header (without the `#species:` prefix). The per-row value sources are identical to the scenario `#species:` table in section 3.2 (same fields, same `:F2` specifiers, same `optimalTempK - 273.15f` derivations), including `Tier` from `species.tier`, which is 0-based here (ScenarioResult.cs:1175-1189). So the config species table and the scenario species table agree row for row, and both write 0-based tier, while the aggregate per-species sections write 1-based tier.

The JSON variant `BuildConfigJson` (ScenarioResult.cs:1011-1114) carries the same parameter and species set in a nested object form, with species temperatures in both Kelvin and Celsius (`:F2`), and is invoked by `ToConfigJson`/`ConfigExporter.ToJson`. JSON details are out of scope; the species field list matches the CSV.

## 6. Bulk summary CSV (`bulk_summary.csv`)

Built by `BulkSimulationController.GenerateBulkSummary(List<BulkRunSummary>)` (BulkSimulationController.cs:428-715), written once at the ZIP root after all runs finish (BulkSimulationController.cs:384-390). Each `BulkRunSummary` is one run (one input CSV row). Section dividers use `=== TITLE ===`; metadata lines use `# Key,Value`.

A unified `(Tier, Variant)` lookup is merged across all runs' `SpeciesInfo` dictionaries (last-write-wins), giving `GetVariant(fn)` and `GetTier(fn)` where `GetTier` returns the 1-based tier or `?` (BulkSimulationController.cs:435-443). `SpeciesInfo` is built per run from `tempSpecies` as `fullName -> (tier+1, variantLabel)` (BulkSimulationController.cs:329-343).

Section order:

### 6.1 Header

```
=== TINYSEA BULK SUMMARY (Across All Runs) ===
# Model Version,v12-per-species-tracking
# Total Runs,<summaries.Count>
# Generated,<now yyyy-MM-dd HH:mm:ss>
<blank>
```

(BulkSimulationController.cs:445-449.)

The set `allSpecies` used by the next two sections is the union of every run's `AvgSpeciesPop` keys (`FullName`), in a `new SortedSet<string>()` with no comparer, so it is ascending by the current-culture default comparer, not ordinal (BulkSimulationController.cs:452-458). The cross-run rich set `allSpeciesRich` is built the same way (BulkSimulationController.cs:548). See the comparer note in Section 3.

### 6.2 `=== PER-RUN RESULTS - TIER LEVEL ===`

Wide format, one row per run, with one column per species in `allSpecies` (BulkSimulationController.cs:462-480):

```
=== PER-RUN RESULTS - TIER LEVEL ===
Run,Scenarios,Survived,Crashed,CrashRate,BaseTemp,ClimateTrend<,FullName per species>
<BatchName>,<NumScenarios>,<Survived>,<Crashed>,<crashRate :P1>,<BaseTemp :F2>,<ClimateTrend :F4><,AvgSpeciesPop :F1 per species>
```

| Column | Source (per `BulkRunSummary run`) | Format |
|--------|------------------------------------|--------|
| `Run` | `run.BatchName` | string |
| `Scenarios` | `run.NumScenarios` | int |
| `Survived` | `run.Survived` | int |
| `Crashed` | `run.Crashed` | int |
| `CrashRate` | `Crashed / (Survived + Crashed)` | `:P1` |
| `BaseTemp` | `run.BaseTemp` | `:F2` |
| `ClimateTrend` | `run.ClimateTrend` | `:F4` |
| per species | `run.AvgSpeciesPop[sp]` or 0 | `:F1` |

The per-species header columns here are the raw `FullName` strings (not sanitized) (BulkSimulationController.cs:464-465). `AvgSpeciesPop` is `AggregateResults.PerSpeciesAvg` snapshotted per run (BulkSimulationController.cs:354-356).

### 6.3 `=== PER-RUN RESULTS - PER SPECIES ===`

Long format, one row per `(run, species)` (BulkSimulationController.cs:485-497):

```
=== PER-RUN RESULTS - PER SPECIES ===
Run,Species,Variant,Tier,AvgPop,SurvivedAvgPop
<BatchName>,<FullName>,<Variant>,<Tier>,<AvgPop :F1>,<SurvivedAvgPop :F1>
```

`AvgPop` is `run.AvgSpeciesPop[sp]` (or 0); `SurvivedAvgPop` is `run.SurvivedSpeciesPop[sp]` (or 0), the per-run survived-only average snapshotted from `PerSpeciesSurvivedAvg` (BulkSimulationController.cs:357-359, 490-494).

### 6.4 `=== PER-SPECIES AGGREGATE (Across All Runs) ===`

One row per species in `allSpecies`, aggregating across runs (BulkSimulationController.cs:503-540):

```
=== PER-SPECIES AGGREGATE (Across All Runs) ===
Species,Variant,Tier,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate
<FullName>,<Variant>,<Tier>,<grandMean :F1>,<survivedMean :F1>,<runsExtinct>,<runsSurvived>,<extinctionRate :P1>
```

| Column | Definition | Format |
|--------|-----------|--------|
| `GrandMean` | mean across runs of each run's `AvgSpeciesPop[sp]` (includes runs where the species was extinct, contributing their value) | `:F1` |
| `SurvivedMean` | mean across runs (where `AvgSpeciesPop[sp] > 0`) of that run's survived average, falling back to the run average when no survived value is present | `:F1` |
| `RunsExtinct` | count of runs where `AvgSpeciesPop[sp] <= 0` | int |
| `RunsSurvived` | count of runs where `AvgSpeciesPop[sp] > 0` | int |
| `ExtinctionRate` | `RunsExtinct / (count of runs that had this species)` | `:P1` |

(BulkSimulationController.cs:506-539.)

### 6.5 v12 cross-run rich sections

The remaining sections are emitted only when the cross-run union of `PerSpeciesMetrics` keys is non-empty (`allSpeciesRich`, a `SortedSet`, BulkSimulationController.cs:548-555). The per-run `PerSpeciesMetrics` is the `Dictionary<string, PerSpeciesAggregate>` snapshotted from each run's `AggregateResults.PerSpeciesMetrics` (BulkSimulationController.cs:360-362).

#### 6.5.1 `=== PER-RUN PER-SPECIES FINAL YEAR ===`

Preceded by a blank line. One row per `(run, species)` that has metrics for that species (BulkSimulationController.cs:558-575):

```
=== PER-RUN PER-SPECIES FINAL YEAR ===
Run,Species,Variant,Tier,N,NSurvived,MeanCondition,MeanBirthRate,PopCv,MeanPop,MeanFinalYear_TempDeaths,MeanFinalYear_ConditionDeaths,MeanFinalYear_NaturalDeaths,MeanFinalYear_PredationDeaths
```

| Column | Source (per `PerSpeciesAggregate a`) | Format |
|--------|--------------------------------------|--------|
| `N` | `a.N` | int |
| `NSurvived` | `a.NSurvived` | int |
| `MeanCondition` | `a.MeanConditionFinalYear.Mean` | `:F3` |
| `MeanBirthRate` | `a.MeanBirthRateFinalYear.Mean` | `:F4` |
| `PopCv` | `a.PopCvFinalYear.Mean` | `:F3` |
| `MeanPop` | `a.MeanPopulationFinalYear.Mean` | `:F1` |
| `MeanFinalYear_TempDeaths` | `a.FinalYearTempDeaths.Mean` | `:F1` |
| `MeanFinalYear_ConditionDeaths` | `a.FinalYearConditionDeaths.Mean` | `:F1` |
| `MeanFinalYear_NaturalDeaths` | `a.FinalYearNaturalDeaths.Mean` | `:F1` |
| `MeanFinalYear_PredationDeaths` | `a.FinalYearPredationDeaths.Mean` | `:F1` |

`FinalYearPredationDeaths` is the final-year `Eaten` total (Tier 1 only; 0 for Tier 2) (SimulationRunner.cs:1147-1151, ScenarioResult.cs:124-128).

#### 6.5.2 `=== CROSS-RUN PER-SPECIES FINAL YEAR (Mean of per-run means) ===`

One row per species in `allSpeciesRich` (BulkSimulationController.cs:592-653):

```
=== CROSS-RUN PER-SPECIES FINAL YEAR (Mean of per-run means) ===
Species,Variant,Tier,Runs,RunsSurvived,GrandMeanCondition,GrandMeanCondition_StdDev,GrandMeanBirthRate,GrandMeanBirthRate_StdDev,GrandMeanPopCv,GrandMeanPop,GrandMeanPop_SurvivedMean
```

Aggregation rule (BulkSimulationController.cs:594-652):

- `Runs` = number of runs that had this species (population is counted for every such run).
- `RunsSurvived` = number of runs where the run's `a.NSurvived > 0`.
- `GrandMeanCondition`, `GrandMeanBirthRate`, `GrandMeanPopCv` are averaged across surviving runs only (those with `NSurvived > 0`), and they use each run's per-species `SurvivedMean` (not `Mean`): `a.MeanConditionFinalYear.SurvivedMean`, `a.MeanBirthRateFinalYear.SurvivedMean`, `a.PopCvFinalYear.SurvivedMean`. If no run survived, these emit 0.
- `GrandMeanCondition_StdDev` and `GrandMeanBirthRate_StdDev` are the standard deviations over that same surviving-run sample.
- `GrandMeanPop` = mean of `a.MeanPopulationFinalYear.Mean` over all runs (zero-population runs contribute a real 0).
- `GrandMeanPop_SurvivedMean` = mean of `a.MeanPopulationFinalYear.SurvivedMean` over surviving runs.

Formats: condition columns `:F3`, birth-rate columns `:F4`, `GrandMeanPopCv` `:F3`, population columns `:F1` (BulkSimulationController.cs:648-652). This surviving-run-only rule for condition/birth-rate/PopCv exists because a non-surviving run's per-species condition can be stuck at the initial 1.0 (biology never updated it), which would bias a naive grand mean (BulkSimulationController.cs:578-591).

#### 6.5.3 `=== CROSS-RUN STABILITY ===`

One row per species in `allSpeciesRich` (BulkSimulationController.cs:657-711):

```
=== CROSS-RUN STABILITY ===
Species,Variant,Tier,Runs,RunsSurvived,MinPop_Mean,MaxPop_Mean,FinalPop_Mean,ExtinctionRate,MeanExtinctionDay,CrashRate,MeanCrashDay
```

| Column | Definition | Format |
|--------|-----------|--------|
| `Runs` | runs that had this species | int |
| `RunsSurvived` | runs where `a.NSurvived > 0` | int |
| `MinPop_Mean` | mean over runs of `a.MinPopulation.Mean` | `:F1` |
| `MaxPop_Mean` | mean over runs of `a.MaxPopulation.Mean` | `:F1` |
| `FinalPop_Mean` | mean over runs of `a.FinalPopulation.Mean` | `:F1` |
| `ExtinctionRate` | `totalExtinctions / totalScenarios` summed across runs | `:P1` |
| `MeanExtinctionDay` | scenario-count-weighted mean extinction day across runs, -1 if none | `:F1` |
| `CrashRate` | `totalCrashes / totalScenarios` summed across runs | `:P1` |
| `MeanCrashDay` | scenario-count-weighted mean crash day across runs, -1 if none | `:F1` |

(BulkSimulationController.cs:659-711.) `totalScenarios`, `totalExtinctions`, `totalCrashes` accumulate `a.N`, `a.ExtinctionTiming.NEvents`, `a.CrashTiming.NEvents` over runs. The mean-day columns weight each run's `MeanDay` by its `NEvents` before dividing by the total event count. A run with zero events for the species is excluded from the weighted sum entirely: the accumulation is guarded by `if (a.ExtinctionTiming.NEvents > 0)` and `if (a.CrashTiming.NEvents > 0)`, so a zero-event run adds neither a term nor weight, rather than contributing a `-1 * 0 = 0` term (BulkSimulationController.cs:686-695). Because each run's per-scenario `MeanDay` already excludes no-event scenarios (see 4.6.3), and zero-event runs are dropped here, no `-1` sentinel ever enters the cross-run mean. The result is `extDaySum / extDayCount` (respectively `crashDaySum / crashDayCount`), or `-1` when the total event count across runs is 0 (BulkSimulationController.cs:704-705).

## 7. Invariants and cross-format consistency

1. Species column order in the scenario CSV (header, daily rows, summary, extinction) is `(Tier asc, FullName asc)`, computed once (SimulationRunner.cs:798-800). Aggregate and bulk per-species sections instead sort by `FullName` alphabetically, independent of tier. The `FullName` sorts at all these sites use the current-culture default comparer (LINQ `OrderBy`/`ThenBy` and `new SortedSet<string>()` with no comparer), not ordinal. The only ordinal-sorted column groups are the dynamic rollup columns and the `EXTINCTION TIMING - TIER VARIANTS` keys, which pass `string.CompareOrdinal`/`StringComparer.Ordinal` explicitly. See the comparer note in Section 3 for the per-site breakdown; for ASCII identifiers of uniform case the two orders coincide.
2. Tier numbering differs by format. The scenario `#species:` table and the config `=== SPECIES ===` table both write 0-based `SpeciesData.tier`. The scenario `#summary:`/`#extinction:` blocks and every aggregate and bulk per-species section write 1-based tier (either the literal `1`/`2` for tier totals, `SimSpecies.Tier`, or `SpeciesData.tier + 1`). A reader joining these on tier must add 1 to the species-table tier or subtract 1 from the rich-section tier.
3. The only variant string ever written to any file is the free-text label: `VariantLabel` (fallback `Name`) on the simulation side, `variantLabel` (fallback `speciesName`/`variant.ToString()`) on the config and aggregate side. The `ThermalVariant`/`SpeciesVariant` enum names (`Arctic`/`Common`/`Tropical`/`Custom`, `ColdSpecialist`/... ) are never written to CSV output.
4. Tier-rollup invariant in the scenario CSV: per-species `_Pop` columns sum to the matching `Tier{n}_{label}` rollup column, which sum to `Tier{n}Pop`. Per-species birth/death columns sum to the corresponding `*T1`/`*T2` tier columns. All population longs pass through `SafePopToLong`, which uses default `Math.Round` (banker's rounding, round-half-to-even, SimulationRunner.cs:598) and maps non-finite values to 0. This midpoint rule differs from biology Step 10's `MidpointRounding.AwayFromZero` (EcosystemSimulator.cs:681); see 3.3.2 for why the two rarely disagree.
5. When `tier2` is false (the shipping default), every Tier 2 column is removed from the scenario daily header and rows, and `Tier2Pop` is dropped from the aggregate wide tables. The scenario `#species:` and config species tables, by contrast, reflect whatever `RunSpecies` holds, and the bulk parser rejects Tier 2 input rows, so in practice no Tier 2 species reach those tables either.
6. The species parameter columns (`Name`, `Variant`, all biology and thermal params, Kelvin and Celsius temperatures at `:F2`) are identical between the scenario `#species:` table, the config `=== SPECIES ===` table, and the JSON species objects.

## 8. The production day loop and crash detection

This section specifies the per-day loop that drives every recorded value. The CSV layout in Sections 3 through 7 describes the columns; this section describes how the rows are produced.

### 8.1 Entry, seeding, and species initialization

A scenario is one `SimulationRunner` run. The runner holds a `TemperatureCalculator TempCalc` and an `EcosystemSimulator Ecosystem`, both constructed with the scenario seed in the `SimulationRunner(int seed)` constructor: `UsedSeed = seed; TempCalc = new TemperatureCalculator(seed); Ecosystem = new EcosystemSimulator(seed)` (SimulationRunner.cs:382-387). A seed of `-1` makes each `System.Random` use system time (non-reproducible); any non-negative seed is reproducible (TemperatureCalculator.cs:45, EcosystemSimulator.cs:302).

The two controllers derive the per-scenario seed from the run-level seed and the scenario index. For scenario `i` (0-based loop index), `scenarioSeed = config.RandomSeed < 0 ? -1 : config.RandomSeed + i` (standard mode SimulationController.cs:180 and 209, the WebGL-sequential and Editor-parallel branches of the same run; bulk mode BulkSimulationController.cs:209 and 259). So with a run seed of 12345, scenario 1 uses 12345, scenario 2 uses 12346, and so on. After constructing the runner the controller copies every temperature parameter from the config or batch into `TempCalc` (SimulationController.cs:353-363 standard, 295-305 bulk), then calls `runner.Run()` and `runner.ToScenarioResult(...)` (SimulationController.cs:378,381 standard, 336,337 bulk). The runner's own `TotalDays`, `BiologyStep`, `RunSpecies`, and `Ecosystem.CarryingCapacityPerTier`/`ConditionDrainRate`/`ConditionRecoveryRate` are set by the controller before `Run()` (not shown here; see Section 11 for defaults).

`Run()` begins by resetting state: it clears `_records`, sets `_biologyCycleCounter = 0`, sets `HasCrashed = false`, `CrashDay = -1`, `CrashTier = -1`, and copies `BiologyStep` into `Ecosystem.BiologyStep` (SimulationRunner.cs:394-400). It then initializes the species: if `RunSpecies` is non-null with a non-empty `speciesList`, it calls `Ecosystem.InitializeFromRunSpeciesList(RunSpecies)`; otherwise it logs a warning and calls `Ecosystem.InitializeDefaultSpecies()` (SimulationRunner.cs:403-411). Species initialization is detailed in Section 10.1.

### 8.2 The loop

The loop runs `dayIndex` from `0` to `TotalDays - 1` inclusive (SimulationRunner.cs:415). The body, in exact order (SimulationRunner.cs:415-448):

1. Optional cooperative pause/stop. If `Control` is non-null, spin with `Thread.Sleep(10)` while `Control.Paused && !Control.Stopped`, then `break` the loop if `Control.Stopped` (SimulationRunner.cs:419-423). The spin consumes no RNG and advances no state, so a paused-then-resumed run is byte-identical. `Control` is null for normal runs.
2. Compute `displayDay = dayIndex + 1` (the 1-based day, SimulationRunner.cs:425).
3. Compute `year = (dayIndex / TemperatureCalculator.DAYS_PER_YEAR) + 1` using C# integer division (truncating); `DAYS_PER_YEAR == 365` (SimulationRunner.cs:426, TemperatureCalculator.cs:30).
4. Compute the day temperature `temp = TempCalc.GetTemperature(dayIndex)` (SimulationRunner.cs:428). Note the argument is the 0-based `dayIndex`, not `displayDay`. The temperature model is specified in Section 9.
5. Decide whether biology runs this day: `runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)` (SimulationRunner.cs:430).
6. If `runBiology`, increment the biology cycle counter then run biology: `_biologyCycleCounter++; Ecosystem.ProcessBiologyStep(temp);` (SimulationRunner.cs:432-436). The increment happens before the step, so the first biology day records cycle 1. The 10-step biology engine is specified in Section 10.
7. Record the day unconditionally: `RecordStep(displayDay, year, temp, runBiology)` (SimulationRunner.cs:438). `RecordStep` appends exactly one `StepRecord` to `_records` (SimulationRunner.cs:583), so there is one row per day whether or not biology ran. What `RecordStep` zeroes versus carries on a skipped-biology day is specified in Sections 3.3.3 and 3.3.4.
8. Crash check, only on biology days: if `runBiology && Ecosystem.HasCrashed()`, set `HasCrashed = true`, `CrashDay = displayDay`, `CrashTier = Ecosystem.GetCrashedTier()`, log a warning, and `break` out of the loop (SimulationRunner.cs:440-447). The record for the crash day was already appended in step 7, so the crash day is present as the final row.

Because the crash check is after recording and breaks the loop, a crashed run has rows for days `1..CrashDay` and then stops; an uncrashed run has exactly `TotalDays` rows. The number of recorded days is `_records.Count`, which `ToScenarioResult` stores as `TotalDays` on the `ScenarioResult` (the recorded count, not the configured `TotalDays`), via `GetSummary().TotalDays` (SimulationRunner.cs:940, 1009).

Pseudocode for the whole loop:

```
Run():
  _records.clear(); _biologyCycleCounter = 0
  HasCrashed = false; CrashDay = -1; CrashTier = -1
  Ecosystem.BiologyStep = BiologyStep
  if RunSpecies has >= 1 species: Ecosystem.InitializeFromRunSpeciesList(RunSpecies)
  else:                           Ecosystem.InitializeDefaultSpecies()

  for dayIndex in 0 .. TotalDays-1:
      displayDay = dayIndex + 1
      year       = (dayIndex / 365) + 1                 # integer division
      temp       = TempCalc.GetTemperature(dayIndex)    # 0-based day
      runBiology = (displayDay == 1) or (displayDay % BiologyStep == 0)
      if runBiology:
          _biologyCycleCounter += 1
          Ecosystem.ProcessBiologyStep(temp)
      RecordStep(displayDay, year, temp, runBiology)    # appends exactly one row
      if runBiology and Ecosystem.HasCrashed():
          HasCrashed = true; CrashDay = displayDay; CrashTier = Ecosystem.GetCrashedTier()
          break
```

### 8.3 Scenario-level crash detection

`Ecosystem.HasCrashed()` returns true when the total live population is exactly zero: `GetTier1Population() + GetTier2Population() == 0` (EcosystemSimulator.cs:1386-1390). `GetTier1Population()`/`GetTier2Population()` sum `sp.Population` over species of that tier as floats (EcosystemSimulator.cs:1382-1383). The comparison is `== 0` against the float sum, and by this point in the day all populations have passed through Step 10 rounding to whole numbers (Section 10.11), so the sum is integral and the test is an exact-zero test on a whole number.

`Ecosystem.GetCrashedTier()` assigns the crashed tier (EcosystemSimulator.cs:1392-1398), using the two flags `_tier1WasPopulated`/`_tier2WasPopulated` set at initialization (true when that tier started with population > 0, EcosystemSimulator.cs:391-392):

```
GetCrashedTier():
  if _tier1WasPopulated and Tier1Pop == 0 and Tier2Pop == 0:  return 0   # all dead
  if _tier1WasPopulated and Tier1Pop == 0:                    return 1   # Tier 1 gone
  if _tier2WasPopulated and Tier2Pop == 0:                    return 2   # Tier 2 gone
  return -1
```

In the shipping Tier-1-only configuration only Tier 1 is populated, so a crash (total population 0) always returns `CrashTier = 0`. The runner copies `HasCrashed`/`CrashDay`/`CrashTier` onto the `ScenarioResult` (SimulationRunner.cs:1011-1013). These three fields drive the `INDIVIDUAL SCENARIOS` columns (Section 4.7) and the `SUMMARY` counts: a scenario counts as crashed when `ScenarioResult.Crashed` is true, `CrashRate = CrashedScenarios / TotalScenarios`, and `AvgCrashDay` is the mean `CrashDay` over crashed scenarios (Section 4.2, ScenarioResult.cs:315-355). This scenario-level crash (whole-ecosystem extinction) is a different quantity from the per-species `CrashDay` threshold in Section 4.6.3, which fires when one species drops below `max(10, 0.05 * StartPop)` while the ecosystem may still be alive.

## 9. Temperature model (`TempCalc.GetTemperature`)

The `Temperature` column (Section 3.3.1) and every thermally driven biology value come from `TemperatureCalculator.GetTemperature(int day)`, where `day` is the 0-based `dayIndex` from the loop. The `#config:` temperature keys (Section 3.1) are the inputs to this model. This section specifies the model exactly.

### 9.1 Optional timeseries override

If a daily temperature timeseries has been loaded (via `LoadTimeseries`, used only when a batch supplies a `TemperatureTimeseriesFile`, SimulationController.cs:325), `GetTemperature` ignores the parametric model and returns the series value for the day, looping when the series is shorter than the run: `clamp(MinTemp, MaxTemp, _timeseries[day % _timeseries.Count])` (TemperatureCalculator.cs:66-75). The first time `day` exceeds the series length it logs one looping warning. The standard `RunSpeciesList`/`SimulationConfig` path does not load a timeseries, so the parametric model below applies.

### 9.2 The five-component parametric model

When no timeseries is loaded, the daily temperature is the sum of five components, then clamped (TemperatureCalculator.cs:77-84):

```
T(day) = BaseTemperature
       + Seasonal(day)
       + ClimateTrend(day)
       + InterannualVariation(day)
       + DailyVariation(day)
T(day) = clamp(MinTemp, MaxTemp, T(day))     # Math.Max(MinTemp, Math.Min(MaxTemp, T))
```

All temperatures are in degrees Celsius. The components:

1. Seasonal (TemperatureCalculator.cs:126-129). A sine wave over the 365-day year, zero-mean over a full year, coldest near day 0 and warmest near day 182:

   ```
   Seasonal(day) = sin(2*pi*day / 365) * SeasonalAmplitude
   ```

   `SeasonalAmplitude` is in degrees Celsius (the `seasonal_amplitude` config key). `day` is the 0-based day index; the sine uses `double` math (`Math.Sin`).

2. Climate trend (TemperatureCalculator.cs:134-138). A linear warming ramp, the only component with a non-zero long-term mean:

   ```
   years        = day / 365.0          # float division
   ClimateTrend(day) = ClimateTrendPerYear * years
   ```

   `ClimateTrendPerYear` is degrees Celsius per year (the `climate_trend_per_year` config key). At day 0 this is 0; at day 365 it is exactly `ClimateTrendPerYear`.

3. Interannual variation (TemperatureCalculator.cs:148-170). One zero-mean random offset per calendar year, the same value for all 365 days of that year, drawn the first time a day in that year is seen. Returns 0 when `UseInterannualVariation` is false. The year index is `year = day / 365` (integer division). The draw, when the year is first encountered:

   ```
   coldPart = rng.NextDouble() * (-VariabilityMagnitude)              # uniform(-mag, 0)
   warmPart = rng.NextDouble() * (VariabilityMagnitude * WarmingBias) # uniform(0, mag*bias)
   biasMean = VariabilityMagnitude * (WarmingBias - 1) / 4
   yearVariation[year] = (coldPart + warmPart) / 2 - biasMean
   ```

   `VariabilityMagnitude` (config `variability_magnitude`) and `WarmingBias` (config `warming_bias`) are unitless multipliers on the degrees-Celsius magnitude. `WarmingBias` skews the shape of the distribution only: subtracting `biasMean` removes the `mag*(bias-1)/4` expected value the raw `(cold+warm)/2` would otherwise leak, so the component stays zero-mean and the long-term trend is owned solely by `ClimateTrendPerYear` (TemperatureCalculator.cs:156-166). `rng` is the calculator's seeded `System.Random`. Each year draws two `NextDouble()` values, so the RNG draw count is deterministic given the seed and the days simulated.

4. Daily variation (TemperatureCalculator.cs:175-196). Zero-mean random noise whose range grows with the year, optionally autocorrelated to smooth day-to-day transitions:

   ```
   year             = day / 365                                  # integer division
   currentRandomness = BaseRandomness + RandomnessGrowthRate * year
   newRandom        = (rng.NextDouble() * 2 - 1) * currentRandomness   # uniform(-cR, +cR)
   if UseAutocorrelation:
       variation = previousDayVariation * 0.7 + newRandom * 0.3
   else:
       variation = newRandom
   previousDayVariation = variation
   return variation
   ```

   `BaseRandomness` is the `daily_variation_range` config value (degrees Celsius half-range), and `RandomnessGrowthRate` (config `randomness_growth_rate`) widens that half-range by a fixed amount each year. With `UseAutocorrelation` true (config `autocorrelated`), each day blends 70% of yesterday's variation with 30% of a fresh draw; the blended value is stored in `previousDayVariation` for the next day. `GetDailyVariation` draws exactly one `NextDouble()` per day regardless of the autocorrelation flag, so it advances the RNG once per day.

5. Clamp. The summed temperature is clamped to `[MinTemp, MaxTemp]` (config `temperature_bounds_min`/`temperature_bounds_max`), so the recorded `Temperature` never leaves that band even when the components would push past it.

The `Temperature` column is this clamped value formatted `:F2` (Section 3.3.1). Because interannual and daily variation both draw from the same seeded RNG, and the order of draws is fixed (interannual is summed before daily in `GetTemperature`, TemperatureCalculator.cs:80-81, but each is drawn lazily on first use of a day/year), a reimplementation must reproduce the same draw order to get byte-equivalent temperatures: for each day in increasing order, `GetTemperature` calls `GetSeasonalComponent` (no RNG), `GetClimateTrend` (no RNG), `GetInterannualVariation` (draws two `NextDouble` only on the first day of a new year), then `GetDailyVariation` (draws one `NextDouble` every day).

### 9.3 Year helper

`TemperatureCalculator.GetYear(day) = (day / 365) + 1` (integer division, TemperatureCalculator.cs:201-204), the same formula the loop uses for the `Year` column. Day indices 0..364 are Year 1, 365..729 Year 2, and so on, with no leap-year handling.

## 10. The biology engine (`EcosystemSimulator.ProcessBiologyStep`)

`ProcessBiologyStep(float temperature)` runs once per biology day and computes every population, condition, performance, feeding, death, birth, accumulator, and per-species value that the CSV writers read. It is a fixed 10-step sequence over the live `Species` list (EcosystemSimulator.cs:543-700). The temperature argument is the day's clamped Celsius value from Section 9. All populations are stored as `float` (`SimSpecies.Population`) and only rounded to integers in Step 10.

Tier scope: the shipping configuration runs Tier 1 (prey) only. Tier 2 (predator) code remains from the original two-tier design and is described where it occurs, marked legacy. Current runs exclude Tier 2 species at load (Section 10.1), so the Tier 2 feeding, hunting, and predation paths do not execute.

### 10.1 Species load and identity

`InitializeFromRunSpeciesList(runSpecies)` clears `Species`, clears all accumulators, then iterates `runSpecies.speciesList` (each a `SpeciesData`) building one `SimSpecies` per row (EcosystemSimulator.cs:317-393):

- When `Tier2Enabled` is false (the default), rows with `data.tier == 1` (0-based Tier 2) are skipped, so only Tier 1 (`tier == 0`) species load (EcosystemSimulator.cs:336).
- Rows whose normalized name plus variant match-keys collide are merged into the first-seen species by summing `Population`, so duplicate variant spellings do not double-count (EcosystemSimulator.cs:341-349). Match keys lowercase the strings and keep only `[a-z0-9]` (SpeciesDatabase.cs:216-226, 243-253).
- Each `SimSpecies` copies the `SpeciesData` fields one-to-one, with `Tier = data.tier + 1` (the engine is 1-based: prey is Tier 1, predator is Tier 2, EcosystemSimulator.cs:356), `Name = speciesLabel` (fallback `speciesName.ToString()`), `VariantLabel = variantLabel` (fallback `variant.ToString()`), `Condition = 1.0f`, and the per-species `ConditionDrainRate`/`ConditionRecoveryRate` copied through (a negative value means inherit the global rate, see Step 4).
- `_tier1WasPopulated`/`_tier2WasPopulated` are set from the post-load tier populations (EcosystemSimulator.cs:391-392), feeding `GetCrashedTier` (Section 8.3).

`SimSpecies.FullName` is the per-species key everywhere: `string.IsNullOrEmpty(VariantLabel) ? Name : $"{Name}_{VariantLabel}"` (SimSpecies.cs:89). This is the same `FullName` whose construction Section 2.0 specifies. The fallback `InitializeDefaultSpecies` (used only when `RunSpecies` is empty) loads three Hexapod and three Sheplik variants via the factory methods in Section 11.4 (EcosystemSimulator.cs:501-523).

At the top of every biology day, before Step 1, `ProcessBiologyStep` resets all tier-level `Last*` counters to 0 (and `LastFedRateT2`/`LastAvgHuntingEfficiency` to 1, EcosystemSimulator.cs:550-562), clears the seven per-species event dictionaries, and re-seeds an entry for every current species: `LastBirthsBySpecies`/`LastTempDeathsBySpecies`/`LastConditionDeathsBySpecies`/`LastNaturalDeathsBySpecies`/`LastEatenBySpecies` to `0L`, `LastReproScaleBySpecies`/`LastFedRateBySpecies` to `0f`, and `StartPopBySpecies[FullName] = SafePopToLong(sp.Population)` (EcosystemSimulator.cs:566-584). `StartPopBySpecies` is the per-species start-of-day population used for the per-species `_BirthRate` column (Section 3.3.4). It also records the tier-level `StartPopT1 = GetTier1Population()` and `StartPopT2 = GetTier2Population()` (EcosystemSimulator.cs:546-547) used by the `StartPop` column.

### 10.2 Step 1: thermal performance (Arrhenius)

For each species (EcosystemSimulator.cs:591-598):

```
sp.RawThermalPerformance = sp.CalculatePerformance(temperature)   # [0,1], no Pmax
sp.ThermalPerformance    = sp.RawThermalPerformance * sp.Pmax      # [0,1] scaled by Pmax
sp.FedRate               = 1                                       # reset; set in Step 2
sp.CurrentHuntingSuccess = 1                                       # reset; set in Step 2 (T2)
```

`RawThermalPerformance` is the `_ThermalPerf` per-species column; `ThermalPerformance` (Pmax-scaled) feeds predator demand and the `_FinalPerf` column (Section 3.3.4).

`SimSpecies.CalculatePerformance(temperatureCelsius)` (SimSpecies.cs:95-133) computes the Arrhenius thermal-performance curve with a smooth lethal fade, clamped to [0,1]:

1. Apply the per-species offset: `temperatureCelsius += TemperatureDebuff` (the `TemperatureDebuff` column; degrees Celsius).
2. Lethal fade over a cosine transition of width `tw = min(2.0, (CTmaxC - CTminC) / 2)` degrees (`LETHAL_TRANSITION_WIDTH = 2.0`, SimSpecies.cs:57):

   ```
   fadeFactor = 1
   if temp <= CTminC:                 fadeFactor = 0
   elif temp <  CTminC + tw:          fadeFactor = 0.5 * (1 + cos(pi * (CTminC + tw - temp) / tw))
   if temp >= CTmaxC:                 fadeFactor = 0
   elif temp >  CTmaxC - tw:          fadeFactor *= 0.5 * (1 + cos(pi * (temp - (CTmaxC - tw)) / tw))
   if fadeFactor <= 0:                return 0
   ```

   `CTminC`/`CTmaxC` are the critical thermal minimum/maximum in Celsius (the `CTminC`/`CTmaxC` columns). Below `CTminC` or above `CTmaxC` performance is 0 (instant-death region for Step 6).
3. Convert to Kelvin `T = temperatureCelsius + 273.15` and evaluate the Arrhenius ratio with `OT = OptimalTempK`, `B = ArrhenBreadth`, `L = ArrhenLower`, `U = ArrhenUpper`, `LB = LowerBoundK`, `UB = UpperBoundK` (all in Kelvin except the unitless breadth/lower/upper shape constants):

   ```
   numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
   denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)
   perf        = numerator / denominator
   return clamp(0, 1, perf) * fadeFactor
   ```

   The exponentials use `double` (`Math.Exp`); the final result is cast to `float`. Pmax is not applied here (it is applied in Step 1's `ThermalPerformance` line and in reproduction/condition).

### 10.3 Step 2: feeding and predation

`ProcessFeedingWithAccumulator()` (EcosystemSimulator.cs:728-897) computes Tier 1 feeding from the shared food pool first, then Tier 2 predation. Only the Tier 1 part runs in shipping configurations.

Tier 1 (prey). Shared Holling foraging on the resource pool, the same mechanism as Tier 2 via `ComputeForagingSuccess` (EcosystemSimulator.cs:765-803):

```
tier1Pop         = max(0, GetTierPopulation(1))      # live float sum of Tier 1 populations
capSafe          = max(CarryingCapacityPerTier, 1)   # denominator floored at 1
tier1Consumption = sum over alive T1 of sp.Population * max(1, sp.EatingAmount)
foodDensity      = max(0, 1 - tier1Consumption / capSafe)   # >= 0, this is LastFoodDensityT1
resourceRatio    = capSafe / max(tier1Pop, 1)        # Holling search ratio
for each Tier 1 species sp:
    if sp.Population >= 1:
        gatherSuccess = ComputeForagingSuccess(sp, resourceRatio)   # Holling II + variance
        sp.FedRate    = min(1, gatherSuccess * foodDensity)
    else:                   sp.FedRate = 0
    LastFedRateBySpecies[sp.FullName] = sp.FedRate
LastFedRateT1 = (sum over alive T1 of sp.FedRate * sp.Population) / (sum of those populations)
                or 1 when no Tier 1 is alive
```

`CarryingCapacityPerTier` is the `carrying_capacity_tier1` config value (default 5000). `foodDensity` is `FoodDensityT1` (Section 3.3.3); `LastFedRateT1` is the population-weighted `FedRateT1` column; each `sp.FedRate` is the per-species `_FedRate` column. `MIN_ALIVE_POP = 1.0` is the alive threshold (EcosystemSimulator.cs:248). `ComputeForagingSuccess` is `CalculateHollingEfficiency(HuntingEfficiency, resourceRatio)` (returns 1 when efficiency >= 1) plus a random `HuntingVariance` term when that variance is > 0 (EcosystemSimulator.cs:931-940). At `HuntingEfficiency = 1`, `HuntingVariance = 0`, `EatingAmount = 1` this reduces to `FedRate = foodDensity`, identical to the previous linear model, so existing CSVs are unchanged.

Tier 2 (predators, legacy). When both predators and prey are alive, the engine computes Holling Type II hunting efficiency, predator demand, total prey eaten, per-predator FedRate, and removes prey proportionally with a predation accumulator (EcosystemSimulator.cs:792-896). The Holling efficiency is `efficiency = ratio / (ratio + halfSat)` with `halfSat = NORMAL_PREY_RATIO * (1 - baseEff) / baseEff`, `NORMAL_PREY_RATIO = 20`, returning 0 at zero prey, the species base efficiency at the 20:1 ratio, and approaching 1 at high prey density (EcosystemSimulator.cs:924-932). Predator demand uses `ThermalPerformance` (with Pmax) and `EatingAmount`; a per-predator hunting variance draw `(_rng.NextDouble()*2-1) * HuntingVariance` is added and clamped to [0,1] (EcosystemSimulator.cs:805-825). Total eaten is capped at available prey; each predator's `FedRate = min(1, huntingSuccess_i * scarcityFactor)` where `scarcityFactor = min(1, totalEaten/totalActualDemand)` (EcosystemSimulator.cs:849-863). Prey removal accumulates fractional deaths per prey species in `_predationAccumulators`, flushing whole deaths with `floor`, capped at the prey population; whole deaths add to `LastEatenT1` and `LastEatenBySpecies` (EcosystemSimulator.cs:868-896). This entire Tier 2 block is skipped when no predators or no prey are alive, in which case predator `FedRate` is 0 and `LastFedRateT2` is set to 0 (predators present) or 1 (none) (EcosystemSimulator.cs:783-790). In shipping Tier-1-only runs there are no predators, so `EatenT1` and `PredationAccumT1` stay 0.

### 10.4 Step 3: raw final performance

For each species, `sp.RawFinalPerformance = sp.RawThermalPerformance * sp.FedRate` (EcosystemSimulator.cs:606-610). This is the Condition drain/recovery target in Step 4. It is not Pmax-scaled (Pmax enters via the rate multipliers in Step 4 and the birth multiplier in Step 8).

### 10.5 Step 4: update condition

`UpdateCondition(sp)` moves each alive species' `Condition` toward `RawFinalPerformance`, asymmetrically and with Pmax-scaled rates (EcosystemSimulator.cs:952-992). Skipped when `sp.Population < 1`.

```
target      = sp.RawFinalPerformance
pmaxSafe    = max(sp.Pmax, 1e-4)
drainRate   = sp.ConditionDrainRate    >= 0 ? sp.ConditionDrainRate    : ConditionDrainRate     # global fallback
recoveryRate= sp.ConditionRecoveryRate >= 0 ? sp.ConditionRecoveryRate : ConditionRecoveryRate

if sp.Condition > target:                              # draining
    severity        = (1 - target)^2                   # 0 at target=1, 1 at target=0
    effectiveDrain  = drainRate * (1 + severity) / pmaxSafe
    sp.Condition   -= (sp.Condition - target) * effectiveDrain
else:                                                  # recovering
    boost            = target^2                        # 0 at target=0, 1 at target=1
    effectiveRecovery= recoveryRate * (1 + boost) * pmaxSafe
    sp.Condition    += (target - sp.Condition) * effectiveRecovery

sp.Condition = clamp(0, 1, sp.Condition)
```

`ConditionDrainRate`/`ConditionRecoveryRate` are the global `condition_drain_rate`/`condition_recovery_rate` config values (defaults 0.15 and 0.10). The quadratic multiplier ranges from 1x at the optimum to 2x at the lethal edge. Pmax divides the drain and multiplies the recovery, so high-Pmax specialists drain slower and recover faster. Pmax does not enter `target`, keeping Condition on a species-agnostic [0,1] scale. The resulting `sp.Condition` is the `_Cond` per-species column and feeds the tier-weighted `AvgConditionT1` (Section 10.11).

### 10.6 Step 5: final performance (logging only)

For each species, `sp.FinalPerformance = sp.ThermalPerformance * sp.FedRate` (EcosystemSimulator.cs:626-630). This includes Pmax (via `ThermalPerformance`) and is written to the `_FinalPerf` per-species column. It is computed for logging only and is not consumed by any later step; reproduction uses Condition, not FinalPerformance.

### 10.7 Step 6: thermal death (instant at lethal limits)

`ApplyThermalDeath(sp)` (EcosystemSimulator.cs:1000-1025). For an alive species, if `RawThermalPerformance > 0` it survives unchanged. If `RawThermalPerformance == 0` (temperature at or beyond `CTminC`/`CTmaxC`, where Step 1's fade returned 0), the entire population dies: `deaths = sp.Population; sp.Population = 0; sp.Condition = 0`. Deaths add to `LastTempDeathsT1` (Tier 1) or `LastTempDeathsT2` (Tier 2) and to `LastTempDeathsBySpecies[FullName]` (cast to `long`). These feed the `TempDeathsT1`/`_TempDeaths` columns. There is no accumulator: a lethal-limit day is a whole-population kill.

### 10.8 Step 7: condition death (graduated chronic stress)

`ApplyConditionDeath(sp)` (EcosystemSimulator.cs:1056-1099). Skipped when `sp.Population < 1` or `sp.Condition >= sp.DeathThreshold`. Otherwise deaths scale with how far below the threshold the condition has fallen:

```
severity   = (sp.DeathThreshold - sp.Condition) / sp.DeathThreshold   # 0 at threshold, 1 at condition 0
rawDeaths  = sp.Population * severity * sp.DeathRate * BiologyStep
_conditionDeathAccumulators[FullName] += rawDeaths
wholeDeaths = floor(_conditionDeathAccumulators[FullName])
_conditionDeathAccumulators[FullName] -= wholeDeaths          # carry the fraction
wholeDeaths = min(wholeDeaths, sp.Population)
if wholeDeaths > 0:
    oldPop = sp.Population; oldCond = sp.Condition
    sp.Population -= wholeDeaths
    if sp.Population > 0:                                     # survivor fitness boost
        sp.Condition = min(1, oldCond * oldPop / sp.Population)
    add wholeDeaths to LastConditionDeathsT1/T2 and LastConditionDeathsBySpecies[FullName]
```

`DeathThreshold`/`DeathRate` are the `DeathThreshold`/`DeathRate` species columns. The accumulator carries fractional deaths between days; its residual is the `ConditionDeathAccumT1`/`_CondDeathAccum` column. The survivor fitness boost redistributes the dead members' (assumed near-zero) condition into the survivors, pushing condition back toward the threshold and preventing death spirals. `BiologyStep` is the step length in days (default 1).

### 10.9 Step 8: reproduction (condition-driven, Pmax-scaled)

`ApplyReproduction(sp)` (EcosystemSimulator.cs:1150-1264). Skipped when `sp.Population < MIN_POPULATION_FOR_REPRODUCTION = 2` (EcosystemSimulator.cs:274). The reproduction scale is a continuous piecewise function of Condition, joined at `STRUGGLING_REPRO_RATE = 0.10` (EcosystemSimulator.cs:282):

```
if sp.ReproThreshold >= 1:        reproScale = 0.10 * sp.Condition
elif sp.ReproThreshold <= 0:      reproScale = sp.Condition
elif sp.Condition >= sp.ReproThreshold:
    t = (sp.Condition - sp.ReproThreshold) / (1 - sp.ReproThreshold)
    reproScale = 0.10 + (1 - 0.10) * t                       # ramps 0.10 -> 1 above threshold
else:
    reproScale = 0.10 * (sp.Condition / sp.ReproThreshold)   # ramps 0 -> 0.10 below threshold
reproScale = clamp(0, 1, reproScale)
```

`reproScale` is recorded per tier (`LastReproScaleT1`/`T2`, the `ReproScaleT1` column) and per species (`LastReproScaleBySpecies[FullName]`, the `_ReproScale` column). Births:

```
births = sp.Population * reproScale * sp.ReproductionMultiplier * sp.Pmax * BiologyStep
_birthAccumulators[FullName] += births
wholeBirths = floor(_birthAccumulators[FullName])
_birthAccumulators[FullName] -= wholeBirths                  # carry the fraction
sp.Population += wholeBirths
add wholeBirths to LastBirthsT1/T2 and LastBirthsBySpecies[FullName]
```

`ReproductionMultiplier` is the `ReproductionMultiplier` column, `ReproThreshold` the `ReproThreshold` column, `Pmax` the `Pmax` column. Pmax multiplies births directly (specialists convert condition to offspring more efficiently). There is no soft cap on births; high population throttles reproduction indirectly through the food-density to Condition pathway (Steps 2, 4, and 8). The birth accumulator residual is the `BirthAccumT1`/`_BirthAccum` column. Newborns inherit the group's current Condition (no explicit update needed since the population-weighted average is unchanged).

### 10.10 Step 9: natural death (flat rate)

`ApplyNaturalDeathWithAccumulator(sp)` (EcosystemSimulator.cs:1270-1319). Skipped when `sp.Population <= 0`.

```
variance      = (_rng.NextDouble() * 2 - 1) * sp.NaturalDeathVariance
baseRate      = max(0, sp.NaturalDeathRate + variance)
deaths        = sp.Population * baseRate * BiologyStep
_naturalDeathAccumulators[FullName] += deaths
wholeDeaths   = floor(_naturalDeathAccumulators[FullName])
_naturalDeathAccumulators[FullName] -= wholeDeaths            # carry the fraction
wholeDeaths   = min(wholeDeaths, sp.Population)
if wholeDeaths > 0:
    sp.Population -= wholeDeaths
    add wholeDeaths to LastNaturalDeathsT1/T2 and LastNaturalDeathsBySpecies[FullName]
```

`NaturalDeathRate`/`NaturalDeathVariance` are the `NaturalDeathRate`/`NaturalDeathVariance` columns (defaults 0.02 and 0.01 for Tier 1). The rate is independent of performance (old age, disease, accidents). Each call draws one `NextDouble()` per species, advancing the ecosystem RNG. The accumulator residual is the `NaturalDeathAccumT1`/`_NatDeathAccum` column.

### 10.11 Step 10: population rounding, then end-of-day bookkeeping

For each species (EcosystemSimulator.cs:667-686):

```
popCap = 100 * CarryingCapacityPerTier                       # MAX_POP_MULTIPLE_OF_K = 100
if sp.Population > popCap: sp.Population = popCap             # overflow guard
sp.Population = Math.Round(sp.Population, MidpointRounding.AwayFromZero)
```

The 100x cap is a defensive guard against runaway overshoot producing `long.MinValue` sentinels; it sits far above biological overshoots (2-10x K) and far below the overflow point. The rounding is away-from-zero (so 2.5 rounds to 3), which differs from the banker's rounding `SafePopToLong` uses at the CSV formatting site (Section 3.3.2, Invariant 4); the two agree because populations reaching `SafePopToLong` are already whole numbers after this step.

After rounding, `ProcessBiologyStep` finishes the day (EcosystemSimulator.cs:688-699):

- `ComputeAverageCondition()` sets `AvgConditionT1`/`AvgConditionT2` to the population-weighted mean Condition over alive species of each tier, or 0 when that tier has no live population (EcosystemSimulator.cs:1324-1338). These are the `AvgConditionT1` column and feed the scenario-level condition statistics (Section 4.5).
- `EndPopT1 = GetTier1Population()`, `EndPopT2 = GetTier2Population()` (the `EndPop` column is `SafePopToLong(EndPopT1 + EndPopT2)`).
- `UpdateAccumulatorTotals()` sums the per-species accumulator residuals into the tier-level `BirthAccumT1`, `NaturalDeathAccumT1`, `PredationAccumT1`, `ConditionDeathAccumT1` (and Tier 2 equivalents) read by the daily columns (EcosystemSimulator.cs:1343-1378).

### 10.12 Tier-rollup invariant

Because the per-species event counts (`LastBirthsBySpecies` etc.) are incremented from the same `wholeBirths`/`wholeDeaths` values that increment the tier-level `Last*T1`/`Last*T2` counters inside each step, the per-species birth and death columns sum exactly to the tier-level `*T1` columns, and the per-species `_Pop` columns sum to the `Tier{n}_{label}` rollup and `Tier{n}Pop` totals (Invariant 4). The reset-and-reseed at the top of each biology day (Section 10.1) guarantees every live species has a dictionary entry, so the daily writer never falls back to a default for a current species (Section 3.3.4).

## 11. Parameter defaults and validation

This section gives the exact default values, units, ranges, and validation rules for the configuration and species types the CSV writers serialize. Two distinct default sources exist and a reimplementer must not confuse them: the C# field initializers on a freshly constructed object, and the canonical default-family values the shipping assets and editor populators write. The `#species:` and config species tables serialize whatever `RunSpecies` holds, so the shipping default run uses the asset values in Section 11.3.

### 11.1 `SimulationConfig` defaults

`SimulationConfig` is the standard-mode run config (SimulationConfig.cs:24-139). Field initializers:

| Field | Default | Unit / range | CSV surface |
|-------|---------|--------------|-------------|
| `BiologyStep` | 1 | days, `[1,5]` | `biology_step` |
| `DaysPerScenario` | 365 | days, `[1,182500]` | `days_per_scenario` |
| `NumberOfScenarios` | 5 | count, `[1,100]` | `number_of_scenarios` |
| `CarryingCapacityTier1` | 5000 | pool size, `[100,100000]` | `carrying_capacity_tier1` |
| `ConditionDrainRate` | 0.15 | per step, `[0.01,1.0]` | `condition_drain_rate` |
| `ConditionRecoveryRate` | 0.10 | per step, `[0.01,1.0]` | `condition_recovery_rate` |
| `BaseTemperature` | 20 | degrees C | `base_temperature` |
| `SeasonalAmplitude` | 5 | degrees C | `seasonal_amplitude` |
| `ClimateTrend` | 1 | degrees C per year | `climate_trend_per_year` |
| `InterannualVariation` | true | bool | config.csv only |
| `VariabilityMagnitude` | 2 | multiplier | `variability_magnitude` |
| `WarmingBias` | 1.5 | multiplier | `warming_bias` |
| `Autocorrelated` | true | bool | `autocorrelated` |
| `DailyVariationRange` | 5 | degrees C half-range | `daily_variation_range` |
| `RandomnessGrowthRate` | 0.5 | degrees C per year | `randomness_growth_rate` |
| `TemperatureBoundsMin` | -5 | degrees C | `temperature_bounds_min` |
| `TemperatureBoundsMax` | 50 | degrees C | `temperature_bounds_max` |
| `Tier2Enabled` | false | bool | gates Tier 2 columns |
| `RandomSeed` | 12345 | int, -1 = system time | `random_seed` (per scenario) |

`IsValid` (SimulationConfig.cs:146-195) rejects the config when `DaysPerScenario < 1`, `NumberOfScenarios < 1`, `RunSpecies` is null or empty, or `CarryingCapacityTier1 <= 0` (the cap is always on). It logs a non-fatal warning when the summed initial Tier 1 population exceeds `CarryingCapacityTier1`.

Note the bounds defaults: `TemperatureCalculator`'s own field initializers are `MinTemp = -5` and `MaxTemp = 40` (TemperatureCalculator.cs:27-28), but the controller overwrites them from the config before any run, so the effective shipping bounds are -5 and 50 (Sections 3.1, 8.1).

### 11.2 `SpeciesData` defaults

`SpeciesData` is the per-species input row serialized by the `#species:` and config species tables (Sections 3.2, 5). Its C# field initializers (SpeciesDatabase.cs:33-109) define the values a freshly constructed or partially specified row carries:

| Field | C# initializer | Unit | `#species:` column |
|-------|----------------|------|--------------------|
| `count` | 0 | count | `InitialCount` |
| `tier` | 0 | 0 = prey, 1 = predator | `Tier` (0-based) |
| `eatingAmount` | 0 | prey/creature/step | `EatingAmount` |
| `reproductionMultiplier` | 0 | multiplier | `ReproductionMultiplier` |
| `deathThreshold` | 0.3 | condition | `DeathThreshold` |
| `deathRate` | 0 | fraction/step | `DeathRate` |
| `TemperatureDebuff` | 0 | degrees C offset | `TemperatureDebuff` |
| `reproThreshold` | 0.25 | condition | `ReproThreshold` |
| `conditionDrainRate` | 0.15 | per step (-1 = inherit global) | not in `#species:` |
| `conditionRecoveryRate` | 0.10 | per step (-1 = inherit global) | not in `#species:` |
| `naturalDeathRate` | 0.02 | fraction/step | `NaturalDeathRate` |
| `naturalDeathVariance` | 0.01 | +/- fraction | `NaturalDeathVariance` |
| `huntingEfficiency` | 0.75 | fraction (Tier 1 uses 1.0 by convention) | `HuntingEfficiency` |
| `huntingVariance` | 0.15 | +/- fraction | `HuntingVariance` |
| `optimalTempK` | 297.0 | Kelvin | `OptimalTempK` / `OptimalTempC` |
| `arrhenBreadth` | 8000.0 | shape constant | `ArrhenBreadth` |
| `arrhenLower` | 3000.0 | shape constant | `ArrhenLower` |
| `arrhenUpper` | 35000.0 | shape constant | `ArrhenUpper` |
| `lowerBoundK` | 296.0 | Kelvin | `LowerBoundK` / `LowerBoundC` |
| `upperBoundK` | 298.0 | Kelvin | `UpperBoundK` / `UpperBoundC` |
| `pmax` | 0.65 | `[0,1]` | `Pmax` |
| `ctMinC` | 0.0 | degrees C | `CTminC` |
| `ctMaxC` | 40.0 | degrees C | `CTmaxC` |

`GetVariantThermalDefaults` (SpeciesDatabase.cs:115-135) gives per-variant `(pmax, ctMinC, ctMaxC)` presets for the seven `SpeciesVariant` values; the Custom/unknown default is `(0.65, 0, 40)`. These field initializers are not the values a default run serializes; the shipping default run uses the asset in Section 11.3.

### 11.3 Canonical default species (the shipping `RunSpeciesList`)

The standard run uses `Assets/Resources/RunSpeciesList.asset`, a six-row Tier-1-only family of three Hexapod and three Gelgi variants. Every row shares these non-thermal values: `count = 20`, `tier = 0`, `eatingAmount = 3`, `reproductionMultiplier = 0.45`, `deathThreshold = 0.3`, `deathRate = 0.6`, `reproThreshold = 0.25`, `conditionDrainRate = 0.15`, `conditionRecoveryRate = 0.10`, `naturalDeathRate = 0.02`, `naturalDeathVariance = 0.01`, `huntingEfficiency = 1`, `huntingVariance = 0`, `TemperatureDebuff = 0` (RunSpeciesList.asset:17-238). The per-row identity and thermal parameters:

| index | speciesLabel | variantLabel | optimalTempK | arrhenBreadth | arrhenLower | arrhenUpper | lowerBoundK | upperBoundK | pmax | ctMinC | ctMaxC |
|-------|--------------|--------------|--------------|---------------|-------------|-------------|-------------|-------------|------|--------|--------|
| 0 | Hexapod | Cold Specialist | 293.15 | 5000 | 15998 | 43798 | 292.4 | 293.9 | 0.9843 | 0 | 35 |
| 1 | Hexapod | Warm Specialist | 295.15 | 5000 | 16000 | 43800 | 294.4 | 295.9 | 0.972 | 2 | 37 |
| 2 | Hexapod | Hot Specialist | 297.15 | 5000 | 16002 | 43802 | 296.4 | 297.9 | 0.96 | 4 | 39 |
| 3 | Gelgi | Cold Generalist | 293.15 | 7000 | 4998 | 31098 | 292.4 | 293.9 | 0.6616 | 0 | 35 |
| 4 | Gelgi | Warm Generalist | 295.15 | 7000 | 5000 | 31100 | 294.4 | 295.9 | 0.6547 | 2 | 37 |
| 5 | Gelgi | Hot Generalist | 297.15 | 7000 | 5002 | 31102 | 296.4 | 297.9 | 0.6481 | 4 | 39 |

The `variantLabel` is the space-separated nicified enum name (`"Cold Specialist"` etc., from `DeriveVariantLabel`/`NicifyEnumName`, SpeciesDatabase.cs:156-178), so the default-run `FullName`s are `Hexapod_Cold Specialist`, `Hexapod_Warm Specialist`, `Hexapod_Hot Specialist`, `Gelgi_Cold Generalist`, `Gelgi_Warm Generalist`, `Gelgi_Hot Generalist` (the underscore is the `Name`/`VariantLabel` separator from Section 2.0; the embedded space inside the label is preserved). After sanitization for column prefixes (Section 2.1) the space becomes an underscore, for example `Hexapod_Cold_Specialist_Pop`. The dynamic rollup columns are one per `(tier, variantLabel)`: `Tier1_Cold_Specialist`, `Tier1_Cold_Generalist`, and so on, ordered by ordinal label compare.

### 11.4 Default factory species (`InitializeDefaultSpecies` fallback)

When `RunSpecies` is empty, `InitializeDefaultSpecies` loads three Hexapod (20 each) and three Sheplik (4 each) via `SimSpecies.CreateHexapod`/`CreateSheplik` (EcosystemSimulator.cs:501-523). These factory methods set `variantLabel` to the bare `"Cold"`/`"Warm"`/`"Hot"` (Hexapod, SimSpecies.cs:169,180,189), which differs from the asset's space-separated labels, so a fallback run and an asset run produce different `FullName`s. Hexapod shared values: `EatingAmount = 0`, `ReproductionMultiplier = 0.45`, `DeathThreshold = 0.3`, `DeathRate = 0.6`, `ReproThreshold = 0.25`, `NaturalDeathRate = 0.02`, `NaturalDeathVariance = 0.01`, `HuntingEfficiency = 1.0`, `HuntingVariance = 0`, `ArrhenBreadth = 5000` (SimSpecies.cs:140-163); per-variant Topt/bounds/Pmax/CT match the Hexapod rows in Section 11.3. Sheplik (Tier 2, legacy) shared values: `EatingAmount = 1.5`, `ReproductionMultiplier = 0.1`, `DeathThreshold = 0.3`, `DeathRate = 0.3`, `ReproThreshold = 0.25`, `NaturalDeathRate = 0.01`, `NaturalDeathVariance = 0.005`, `HuntingEfficiency = 0.75`, `HuntingVariance = 0.15`, `ArrhenBreadth = 5273.15`, `ArrhenLower = 10273.15`, `ArrhenUpper = 21273.15` (SimSpecies.cs:207-227). The `SimSpecies` runtime field initializers that are not set by the factory default to `NaturalDeathRate = 0.02`, `NaturalDeathVariance = 0.01`, `HuntingEfficiency = 0.75`, `HuntingVariance = 0.15`, `ConditionDrainRate = -1`, `ConditionRecoveryRate = -1` (inherit global), `Pmax = 1.0`, `CTminC = -5.0`, `CTmaxC = 40.0`, `Condition = 1.0`, `FedRate = 1` (SimSpecies.cs:36-80).

### 11.5 `BulkBatchConfig` and bulk species-row defaults

In bulk mode each input CSV row becomes a `BulkBatchConfig` (one run) holding a list of `BulkSpeciesRow`. The `BulkBatchConfig` numeric fields have no initializers except `ConditionDrainRate = 0.15` and `ConditionRecoveryRate = 0.10`, and `TemperatureTimeseriesFile = ""` (BulkBatchConfig.cs:43-62); the parser sets the rest from the row or from its own column defaults. Each `BulkSpeciesRow` initializes `Pmax = 1.0`, `CTminC = -5.0`, `CTmaxC = 50.0`, `TempOffset = 0`, `ConditionDrainRate = -1` (inherit), `ConditionRecoveryRate = -1` (inherit), with the remaining biology and thermal fields defaulting to 0 until the parser fills them (BulkBatchConfig.cs:9-34). The bulk parser rejects rows whose tier is non-zero, so only Tier 1 species reach a bulk run (Section 3.2). When the controller runs a batch it copies the `BulkBatchConfig` temperature fields into `TempCalc` the same way the standard path copies `SimulationConfig` (SimulationController.cs:295-305), so the temperature model and biology engine are identical across standard and bulk runs.
