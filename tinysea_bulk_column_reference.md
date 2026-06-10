# TinySea Bulk Simulation — CSV Column Reference

## How to Use

1. Open the template CSV (`bulk_test_template.csv`) in Excel or Google Sheets
2. Each row = one **batch** configuration. Each batch runs `num_scenarios` independent simulations with the same settings but different random seeds
3. Copy a row and change only the values you want to test
4. `batch_name` must be unique per row (it becomes the folder name in the output ZIP)
5. Drag and drop the CSV file into the simulation screen to run all batches automatically
6. Results are downloaded as a ZIP containing per-scenario CSVs and an aggregate summary for each batch

---

## Simulation Settings

| Column | Type | Default | Description |
|--------|------|---------|-------------|
| `batch_name` | text | *(required)* | Label for this batch. Becomes folder name in output ZIP |
| `days` | integer | *(required)* | Number of simulated days per scenario (e.g. 365 = 1 year, 3650 = 10 years) |
| `num_scenarios` | integer | *(required)* | How many times to repeat this configuration with different random seeds |

---

## Temperature Settings

All temperatures are in **Celsius**. The simulation converts to Kelvin internally where needed.

| Column | Type | Default | Description |
|--------|------|---------|-------------|
| `base_temp` | number | 20 | Mean annual temperature (C). The center of the seasonal cycle |
| `seasonal_amp` | number | 10 | Seasonal temperature swing (C). Temperature oscillates base_temp +/- this value over a year |
| `climate_trend` | number | 0 | Long-term warming rate (C/year). Set to 0 for stable climate, 1 for +1C per year |
| `variability_mag` | number | 2 | Magnitude of random year-to-year temperature variation |
| `warming_bias` | number | 1.5 | Bias factor that makes warm anomalies more likely than cold ones |
| `daily_var_range` | number | 5 | Daily random temperature fluctuation range (C) |
| `randomness_growth` | number | 0.5 | Rate at which temperature randomness increases over time |
| `autocorrelated` | true/false | true | If true, each day's temperature is influenced by the previous day (more realistic). If false, each day is independent |
| `interannual_variation` | true/false | true | If true, adds random year-to-year shifts to the base temperature |
| `temp_min` | number | -5 | Absolute minimum temperature floor (C). Temperature never goes below this |
| `temp_max` | number | 50 | Absolute maximum temperature ceiling (C). Temperature never goes above this |

---

## Ecosystem Settings

| Column | Type | Default | Description |
|--------|------|---------|-------------|
| `use_carrying_cap` | true/false | true | Enable carrying capacity limit for Tier 1 (prey). When enabled, Tier 1 reproduction slows as population approaches the limit |
| `carrying_cap_t1` | integer | 5000 | Maximum Tier 1 population. Reproduction gradually decreases as population approaches this value (soft cap, not a hard wall) |
| `condition_drain_rate` | number | 0.20 | *(Optional)* How fast health condition drains when thermal performance is poor. Higher = faster drain |
| `condition_recovery_rate` | number | 0.10 | *(Optional)* How fast health condition recovers when thermal performance is good. Higher = faster recovery |

---

## Species Columns

Each species uses a numbered prefix: `sp1_` for species 1, `sp2_` for species 2, etc. The simulation detects how many species you have by scanning for sequential prefixes (up to 100). Prefixes must be sequential with no gaps (sp1, sp2, sp3...).

**You can have as many species as you want in each tier.** The `tier` field on each species determines whether it is prey (0) or predator (1), not its position in the spreadsheet. For example, you could have 4 prey species and 2 predator species, or 1 prey and 5 predators. The standard 6-species setup is:

| Prefix | Species | Variant | Tier |
|--------|---------|---------|------|
| sp1_ | Hexapod | Common | 0 (prey) |
| sp2_ | Hexapod | Arctic | 0 (prey) |
| sp3_ | Hexapod | Tropical | 0 (prey) |
| sp4_ | Sheplik | Common | 1 (predator) |
| sp5_ | Sheplik | Arctic | 1 (predator) |
| sp6_ | Sheplik | Tropical | 1 (predator) |

The included template (`bulk_test_template.csv`) ships with all 6 species pre-filled. You can add more by adding `sp7_`, `sp8_`, etc. columns.

To skip a species for a particular batch row, set its `pop` to `0`. The simulation ignores any species with population below 1. This lets you define all species in the header once and toggle them on/off per row by changing just the population, which is useful for running matched vs mismatched experiments in the same spreadsheet.

### Identity

| Column Suffix | Type | Description |
|---------------|------|-------------|
| `name` | text | Species name (e.g. Hexapod, Sheplik). Used in output labels |
| `variant` | text | Thermal variant: `Common`, `Tropical`, `Arctic`, or `Custom`. Determines default thermal curve if Arrhenius values aren't specified |
| `tier` | integer | Food chain position: `0` = prey (Tier 1), `1` = predator (Tier 2). Tier 2 eats Tier 1 |
| `pop` | integer | Starting population count |

### Reproduction

Reproduction is driven by **Condition** (species health), not instantaneous thermal performance. Condition integrates temperature, feeding, and history — a species with stored health reserves can reproduce even in poor conditions, just at a reduced rate. There is no hard cliff: reproduction scales smoothly from zero (at Condition = 0) to full rate (at Condition = 1.0).

| Column Suffix | Type | Default (T1/T2) | Description |
|---------------|------|-----------------|-------------|
| `repro_mult` | number | 0.45 / 0.1 | Reproduction rate multiplier. Higher = faster population growth. Birth formula: `births = population x reproScale x repro_mult`. The `reproScale` is condition-based: above threshold it ramps from 0.10 to 1.0, below threshold it ramps from 0 to 0.10. No hard cliff anywhere |
| `repro_thresh` | number | 0.25 / 0.25 | Condition inflection point for reproduction. Above this Condition value, reproduction ramps strongly toward full rate. Below it, reproduction is diminished but non-zero (up to 10% of full rate). Zero reproduction only occurs when Condition = 0 (species is effectively dead). This is NOT a hard cutoff |

### Death — Condition System

When thermal performance is poor, a species' health **condition** (0-1) drains over time. When condition drops below `death_thresh`, individuals start dying at a rate proportional to how far below the threshold they are.

| Column Suffix | Type | Default (T1/T2) | Description |
|---------------|------|-----------------|-------------|
| `death_thresh` | number | 0.3 / 0.3 | Condition threshold for condition-death. When condition drops below this value, deaths begin. Formula: `severity = (death_thresh - condition) / death_thresh` |
| `death_rate` | number | 0.6 / 0.3 | Maximum fraction of population killed per day by condition-death (at severity = 1, i.e. condition = 0). Formula: `deaths = population x severity x death_rate`. T1 has higher death rate because prey populations are larger and recover faster |

### Death — Natural (Background Mortality)

A small constant daily death rate representing old age, disease, and accidents. Applied every day regardless of temperature or condition.

| Column Suffix | Type | Default (T1/T2) | Description |
|---------------|------|-----------------|-------------|
| `natural_death_rate` | number | 0.02 / 0.01 | Daily probability of natural death per individual. T2 (predators) use 0.01 based on allometric scaling (larger animals have lower background mortality) |
| `natural_death_var` | number | 0.01 / 0.005 | Random variance applied to natural death rate each day. Actual rate = `natural_death_rate +/- random(natural_death_var)` |

### Death — Thermal (Instant Kill)

If temperature goes outside a species' absolute survival range, the species dies instantly. This represents lethal temperature extremes.

| Column Suffix | Type | Description |
|---------------|------|-------------|
| `ctmin` | number | *(Optional)* Critical thermal minimum (C). Below this temperature, species dies instantly. Default: -5 |
| `ctmax` | number | *(Optional)* Critical thermal maximum (C). Above this temperature, species dies instantly. Default: 40 |

### Feeding / Foraging (all tiers)

These now apply to all tiers. Tier 1 (prey) forages the shared resource pool with the same Holling Type II mechanism Tier 2 predators use to hunt, so `eating`, `hunt_eff`, and `hunt_var` all affect Tier 1.

| Column Suffix | Type | Default (T2) | Description |
|---------------|------|-------------|-------------|
| `eating` | number | 1.5 | Tier 2: prey eaten per predator per day (raw demand = `population x eating x ThermalPerformance`). Tier 1: resource units each individual draws from the shared pool, floored at 1, so a higher value feeds fewer individuals |
| `hunt_eff` | number | 0.75 | Base foraging/hunting efficiency (0-1), driving a Holling Type II response on availability (prey-to-predator ratio for Tier 2, resource-per-forager for Tier 1). At the normal ratio (~20:1) efficiency equals this base value; at lower ratios it drops toward 0, at higher ratios it approaches 1.0 |
| `hunt_var` | number | 0.15 | Random daily variance on foraging/hunting success (all tiers). Actual efficiency = `HollingEfficiency +/- random(hunt_var)`; 0 = deterministic |

### Thermal Performance Curve (Arrhenius Parameters)

These parameters define how well a species performs at different temperatures. The thermal performance curve uses the Arrhenius equation and outputs a value between 0 and `pmax`.

**These values are provided by the marine biology team and should not be changed without consultation.**

| Column Suffix | Type | Description |
|---------------|------|-------------|
| `opt_temp_c` | number | Optimal temperature (C) where species performs best |
| `arrhen_breadth` | number | Arrhenius breadth parameter. Controls width of the thermal performance curve. Higher = wider tolerance range |
| `arrhen_lower` | number | Arrhenius lower deactivation energy. Controls how steeply performance drops below optimal temp |
| `arrhen_upper` | number | Arrhenius upper deactivation energy. Controls how steeply performance drops above optimal temp |
| `lower_bound_c` | number | Lower thermal performance bound (C). Below this, the Arrhenius curve transitions to decline |
| `upper_bound_c` | number | Upper thermal performance bound (C). Above this, the Arrhenius curve transitions to decline |
| `pmax` | number | *(Optional)* Maximum thermal performance (0-1). Caps the Arrhenius curve output. Default varies by variant |
| `temp_offset` | number | *(Optional)* Temperature offset (C) applied to the species' perception of temperature. Default: 0 |

---

## Default Species Values

Biology parameters (reproduction, death, feeding) are the same across all variants of a species. Only the thermal curve differs between Common, Arctic, and Tropical variants.

### Biology Defaults (Same for All Variants)

| Parameter | Hexapod (Prey, Tier 0) | Sheplik (Predator, Tier 1) |
|-----------|----------------------|--------------------------|
| Population | 20 | 4 |
| Eating | 3 (resource units drawn from the pool) | 1.5 |
| Repro Mult | 0.45 | 0.1 |
| Death Threshold | 0.3 | 0.3 |
| Death Rate | 0.6 | 0.3 |
| Repro Threshold | 0.25 | 0.25 |
| Natural Death Rate | 0.02 (2%/day) | 0.01 (1%/day) |
| Natural Death Var | 0.01 | 0.005 |
| Hunting Efficiency | 1.0 (foraging the pool) | 0.75 |
| Hunting Variance | 0 (no foraging variance) | 0.15 |

### Thermal Curve Defaults (Vary by Variant)

All values below are in Celsius for the CSV. The simulation converts to Kelvin internally by adding 273.15. Arrhenius energy parameters are dimensionless and used as-is (do NOT add 273.15 to them).

| Parameter | Common | Arctic | Tropical |
|-----------|--------|--------|----------|
| Optimal Temp (C) | 23.85 (297 K) | 17.85 (291 K) | 29.85 (303 K) |
| Arrhenius Breadth | 8000 | 4000 | 4000 |
| Arrhenius Lower | 3000 | 13974 | 15827 |
| Arrhenius Upper | 35000 | 35000 | 35000 |
| Lower Bound (C) | 22.85 (296 K) | 17.75 (290.9 K) | 29.75 (302.9 K) |
| Upper Bound (C) | 24.85 (298 K) | 17.95 (291.1 K) | 29.95 (303.1 K) |
| Pmax | 0.65 | 0.85 | 0.85 |
| CTmin (C) | 0 | 0 | 0 |
| CTmax (C) | 40 | 40 | 40 |

**Note:** Common has a wider thermal breadth (8000 vs 4000) but lower Pmax (0.65 vs 0.85). Arctic and Tropical variants are specialists with higher peak performance but a narrower tolerance range. These values come from the SpeciesDatabase and should not be changed without consultation with the marine biology team.

---

## Tips

- **A/B testing**: Duplicate a row, change the `batch_name` and one parameter to compare results side by side
- **Keep runs short for iteration**: Use 365 days (1 year) with 3-5 scenarios for quick checks. Use 3650 days (10 years) only for long-term stability tests
- **Large batch warning**: Many batches with high scenario counts and long durations will take significant time. Start small
- **Climate trend = 0**: Set `climate_trend` to 0 to test species in a stable climate (no warming). This isolates seasonal effects from long-term trends
- **Optional columns**: Columns marked optional can be omitted entirely. The simulation will use default values. Old CSVs with removed columns (like `min_deaths`) will still work — unknown columns are ignored with a warning
- **`use_carrying_cap` is deprecated (v11.1)**: Carrying capacity is always on. If an old bulk CSV includes the column, the parser logs a warning and ignores the value.

---

## Output: What's in the ZIP

Each bulk run produces a ZIP with this structure:

```
bulk_summary.csv                          # Cross-batch summary
<batch_name_1>/
    scenario_1.csv ... scenario_N.csv     # One per scenario
    aggregate.csv                         # Per-batch summary
    config.csv                            # Per-batch config echo
<batch_name_2>/
    ...
```

### v12 bulk_summary.csv sections

`bulk_summary.csv` at the root of the ZIP is a single multi-section CSV. Sections (in order):

1. **`=== TINYSEA BULK SUMMARY (Across All Runs) ===`** — header metadata (Model Version, Total Runs, Generated timestamp).
2. **`=== PER-RUN RESULTS ===`** — one row per batch with `Run, Scenarios, Survived, Crashed, CrashRate, BaseTemp, ClimateTrend` plus per-species average final populations.
3. **`=== PER-SPECIES AGGREGATE (Across All Runs) ===`** — `Species, GrandMean, SurvivedMean, RunsExtinct, RunsSurvived, ExtinctionRate`.
4. **`=== PER-RUN PER-SPECIES FINAL YEAR ===`** *(v12)* — `(Run, Species, N, NSurvived, MeanCondition, MeanBirthRate, PopCv, MeanPop)` rows. Final year = last 365 days of each scenario, averaged.
5. **`=== CROSS-RUN PER-SPECIES FINAL YEAR ===`** *(v12)* — per-species mean of per-run means: `Species, Runs, RunsSurvived, GrandMeanCondition (+StdDev), GrandMeanBirthRate (+StdDev), GrandMeanPopCv, GrandMeanPop, GrandMeanPop_SurvivedMean`.
6. **`=== CROSS-RUN STABILITY ===`** *(v12)* — `Species, Runs, RunsSurvived, MinPop_Mean, MaxPop_Mean, FinalPop_Mean, ExtinctionRate, MeanExtinctionDay, CrashRate, MeanCrashDay`. Pooled across all scenarios in the bulk.

**Reading in R:**

```R
# Read just one section by skipping prior lines, or split file at "=== " markers.
# Example: read PER-RUN RESULTS table
lines <- readLines("bulk_summary.csv")
start <- grep("^=== PER-RUN RESULTS ===", lines) + 1   # row after section header
end   <- grep("^$", lines[start:length(lines)])[1] - 1  # blank line before next section
df <- read.csv(text = paste(lines[start:(start+end)], collapse="\n"))
```

### v12 scenario CSV per-species columns

Each `scenario_N.csv` has 17 additional columns per species, ordered by `(Tier asc, FullName asc)`:
`{S}_Pop, {S}_Cond, {S}_ThermalPerf, {S}_FinalPerf, {S}_FedRate, {S}_HuntingEff, {S}_Births, {S}_TempDeaths, {S}_CondDeaths, {S}_NatDeaths, {S}_Eaten, {S}_BirthRate, {S}_ReproScale, {S}_BirthAccum, {S}_NatDeathAccum, {S}_CondDeathAccum, {S}_PredAccum`

`{S}` = sanitized `FullName` (e.g. `Hexapod_Common`, `Coral_Custom`). Existing tier-level columns are preserved verbatim — all per-species columns are appended at the end.

For full output schema details see [`docs/csv-formats.md`](docs/csv-formats.md).
