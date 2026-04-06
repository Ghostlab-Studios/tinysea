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
| `condition_drain_rate` | number | 0.15 | *(Optional)* How fast health condition drains when thermal performance is poor. Higher = faster drain |
| `condition_recovery_rate` | number | 0.10 | *(Optional)* How fast health condition recovers when thermal performance is good. Higher = faster recovery |

---

## Species Columns

Each species uses a numbered prefix: `sp1_` for species 1, `sp2_` for species 2, etc. The simulation detects how many species you have by scanning for sequential prefixes. A typical setup has 2 species (sp1 = prey, sp2 = predator).

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

### Feeding (Predators Only)

These values are ignored for Tier 1 (prey) species since they don't hunt.

| Column Suffix | Type | Default (T2) | Description |
|---------------|------|-------------|-------------|
| `eating` | number | 1.5 | Food demand per individual per day (in units of prey). Raw demand = `population x eating x ThermalPerformance` |
| `hunt_eff` | number | 0.75 | Base hunting efficiency (0-1). Modified by Holling Type II functional response based on prey-to-predator ratio. At normal ratio (~20:1), efficiency equals this base value. At lower ratios, efficiency drops toward 0. At higher ratios, efficiency approaches 1.0 |
| `hunt_var` | number | 0.15 | Random daily variance on hunting success. Actual efficiency = `HollingEfficiency +/- random(hunt_var)` |

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

## Default Species Values (Common Variant)

For quick reference, these are the default values used when not specified:

| Parameter | Hexapod (Prey, Tier 1) | Sheplik (Predator, Tier 2) |
|-----------|----------------------|--------------------------|
| Population | 20 | 4 |
| Eating | 0 (doesn't hunt) | 1.5 |
| Repro Mult | 0.45 | 0.1 |
| Death Threshold | 0.3 | 0.3 |
| Death Rate | 0.6 | 0.3 |
| Repro Threshold | 0.25 | 0.25 |
| Natural Death Rate | 0.02 (2%/day) | 0.01 (1%/day) |
| Natural Death Var | 0.01 | 0.005 |
| Hunting Efficiency | 1.0 (ignored) | 0.75 |
| Hunting Variance | 0 (ignored) | 0.15 |
| Optimal Temp | 20 C | 20 C |
| Lower/Upper Bound | 12 C / 22 C | 12 C / 22 C |
| Pmax | 0.9 | 0.9 |
| CTmin / CTmax | -5 C / 40 C | -5 C / 40 C |

---

## Tips

- **A/B testing**: Duplicate a row, change the `batch_name` and one parameter to compare results side by side
- **Keep runs short for iteration**: Use 365 days (1 year) with 3-5 scenarios for quick checks. Use 3650 days (10 years) only for long-term stability tests
- **Large batch warning**: Many batches with high scenario counts and long durations will take significant time. Start small
- **Climate trend = 0**: Set `climate_trend` to 0 to test species in a stable climate (no warming). This isolates seasonal effects from long-term trends
- **Optional columns**: Columns marked optional can be omitted entirely. The simulation will use default values. Old CSVs with removed columns (like `min_deaths`) will still work — unknown columns are ignored with a warning
