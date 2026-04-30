# Simulation Specification

Authoritative description of the headless ecosystem simulator, generated from source. This is the contract; the C# is the implementation.

Source files: `EcosystemSimulator.cs`, `SimSpecies.cs`, `TemperatureCalculator.cs`, `SimulationRunner.cs`.

## v12.2 changes (CSV format consolidation)

Pure CSV-format changes; simulation logic and `model_version` unchanged. See [`csv-formats.md`](./csv-formats.md) for the full layout.

- **All per-species sections gain explicit `Variant` and `Tier` columns** (in `aggregate.csv` and `bulk_summary.csv`). Downstream tooling can group/filter by tier-context without re-parsing the FullName string.
- **Aggregate `=== INDIVIDUAL SCENARIOS ===` is now a single wide-format table.** Was previously split into `… - TIER ROLLUPS` (one row per scenario, variant-rollup columns) and `… - PER SPECIES` (long format, one row per scenario × species). The merged table has one row per scenario with scenario metadata + tier totals + temperatures + one `FinalPop` column per species, plus two extra header rows (`Variant`, `Tier`) annotating each species column. Variant-rollup columns (`T1Arctic, …, T2Custom`) dropped — the per-species columns subsume them and the tier totals (`FinalT1`, `FinalT2`) preserve the rollup-invariant.
- **Aggregate `=== SUMMARY STATISTICS (Grand Mean Across All Scenarios) ===` is now a single wide-format table** with the same three-header-row pattern (`Statistic` / `Variant` / `Tier`). Replaces the old `… - TIER ROLLUPS` + `… - PER SPECIES` split. Variant-rollup columns dropped here for the same reason.
- **`bulk_summary.csv` `=== PER-RUN RESULTS ===` is split into `… - TIER LEVEL`** (wide; one row per run with per-species `AvgPop` columns appended) **and `… - PER SPECIES`** (long; one row per run × species with `AvgPop` and `SurvivedAvgPop`). Both retain the v12 metadata.
- **Backward compatibility**: scenario CSV `#summary:` and `#extinction:` blocks were already updated to the wide / per-species format in v12.1. The aggregate updates here align with that. RNG sequence and biology unchanged.

## v12 changes (current model version)

- **Per-species daily tracking added.** Every `StepRecord` now carries a `Dictionary<string, PerSpeciesStepData>` keyed by `SimSpecies.FullName`, holding population, condition, thermal performance, FedRate, hunting efficiency, daily event counts (births, eaten, all death types), per-capita birth rate, repro scale, and accumulator residuals — all per individual species.
  - The scenario CSV header is now species-list-parameterized: existing tier columns appear first in their original order, and per-species columns (`{SanitizedFullName}_{Field}`) are appended at the end. ASCII-only sanitization (any non-`[A-Za-z0-9_]` becomes `_`); duplicate sanitized names get `_2`, `_3` suffixes.
  - Tier-rollup invariant: per-species values sum to existing tier-level values (e.g. `Tier1Pop == sum({S}_Pop for S in T1)`).
- **`EcosystemSimulator` exposes per-species event counters**: `LastBirthsBySpecies`, `LastTempDeathsBySpecies`, `LastConditionDeathsBySpecies`, `LastNaturalDeathsBySpecies`, `LastEatenBySpecies`, `LastReproScaleBySpecies`, `LastFedRateBySpecies`, `StartPopBySpecies`. All cleared and re-initialized at the top of `ProcessBiologyStep`. Plus public accessors `GetBirthAccum`, `GetNaturalDeathAccum`, `GetConditionDeathAccum`, `GetPredationAccum` for accumulator residuals.
- **Final-year (last 365 days) and full-run rich metrics added** at the scenario level via `PerSpeciesScenarioMetrics`: mean Condition, mean per-capita birth rate, population CV, min/max population, mean population, extinction day, crash day. Computed by `SimulationRunner.ComputePerSpeciesScenarioMetrics()` in a single linear pass over `_records`.
- **Aggregate CSV (per run) gains 3 sections**: `=== PER-SPECIES FINAL YEAR METRICS ===`, `=== PER-SPECIES FULL-RUN METRICS ===`, `=== PER-SPECIES STABILITY METRICS ===`. Each metric reports Mean / StdDev / SurvivedMean across scenarios.
- **Bulk summary CSV gains 3 sections**: `=== PER-RUN PER-SPECIES FINAL YEAR ===`, `=== CROSS-RUN PER-SPECIES FINAL YEAR ===`, `=== CROSS-RUN STABILITY ===`. Cross-run grand means use mean-of-means (per-run weighted equally), consistent with existing legacy semantics.
- **Crash threshold (placeholder defaults)**: a species "crashes" on the first day its population drops below `max(CRASH_FLOOR=10, CRASH_FRACTION=0.05 × StartPop)`. Constants live at the top of `ComputePerSpeciesScenarioMetrics`. Tune as needed.
- **`model_version` bumped** to `v12-per-species-tracking` in scenario CSV `#config:` header and in `bulk_summary.csv`.
- **Backward compatibility**: all existing tier-level / variant-level columns and aggregate sections preserved verbatim. New data is purely additive. RNG sequence unchanged, so byte-identical regression on tier columns is achievable for the same seed.

## v11.1 changes

- **Removed `UseCarryingCapacity` toggle.** Carrying capacity is always on. Tier 1 species without a resource ceiling grow without bound, which is biologically meaningless and triggered integer-overflow accumulators in long runs.
  - `SimulationConfig.UseCarryingCapacity`, `BulkBatchConfig.UseCarryingCap`, `EcosystemSimulator.UseCarryingCapacity`, `ScenarioResult.UseCarryingCapacity` all deleted.
  - `food_density = max(0, 1 − tier1Pop / max(cap, 1))` — no toggle, no fallback.
  - `IsValid()` now hard-rejects `CarryingCapacityTier1 ≤ 0`.
  - `CsvBatchParser` keeps `use_carrying_cap` as an optional/deprecated column: if present in an old bulk CSV the value is read but ignored, and a `Debug.LogWarning` is emitted. New CSVs from `GenerateTemplate()` omit the column.
  - `#config:use_carrying_capacity` line removed from scenario CSV header.
  - `bulk_summary.csv` config-section line `# Carrying Capacity,...` no longer reports a "Disabled" state.
- **`GenerateTemplate()` defaults updated**: `seasonal_amp = 5` (was 10), `condition_drain_rate = 0.15` (was 0.20, now matches `SimulationConfig` default), `use_carrying_cap` column omitted entirely.
- **`model_version` bumped** to `v11.1-cap-always-on` in scenario CSV `#config:` header and in `bulk_summary.csv`.

## v11 changes

- **Per-predator FedRate (review item A1).** Tier 2 predators previously all received the same pooled `fedRate = totalEaten / totalRawDemand`, which erased the competitive signal between specialist and generalist hunters. Now each predator's FedRate is its own hunting success scaled by an overall scarcity factor:
  - `scarcityFactor = totalEaten / totalActualDemand` (1.0 when prey abundant; <1.0 when demand exceeds supply).
  - `fedRate_i = min(1, huntingSuccess_i × scarcityFactor)`.
- Reduces to the v10 pooled formula exactly when there is only one predator species. Equal-HE multi-predator runs unchanged by symmetry. Mixed-HE multi-predator runs now show competitive exclusion between predator species for the first time.
- `LastFedRateT2` is now a population-weighted average across predators (was a pooled scalar pre-v11). CSV column name unchanged.
- `#config:model_version` and `bulk_summary.csv` `# Model Version` lines bumped to `v11-per-predator-fedrate`.

## v10 changes

- **Carrying capacity reframed as a shared food/resource pool.** The same parameter (`CarryingCapacityPerTier`) now drives Tier 1's `FedRate` directly via a linear food-density curve in Step 2, instead of multiplying births in Step 8. Tier 1 reproduction now throttles indirectly through the Condition pathway (high pop → low food density → low FedRate → Condition drains → fewer births and condition deaths fire). Logistic-overshoot dynamics emerge naturally — populations oscillate around cap rather than approaching it smoothly. Cite: Lotka 1925, Volterra 1926, Krebs 1996 *Population Cycles*.
- **Soft-cap-on-births block deleted from `ApplyReproduction`.** The previous live-`tierPop` read was the source of the processing-order bug (first-listed Tier 1 species reproducing against a smaller pool than later-listed species). Eliminated as a side effect of the reframe.
- **`HuntingEfficiency` for Tier 1 is now meaningful** — semantically "resource extraction efficiency". Default `1.0` = perfect plankton-style passive extraction.
- **`NEWBORN_CONDITION = 0.5` constant removed.** Newborns inherit the species' current group Condition; the parent's Condition already encodes recent provisioning capacity via lagged drain dynamics, so multiplying by today's FedRate would double-count. Same logic for Tier 1 and Tier 2.
- **CSV columns added**: `FedRateT1`, `FoodDensityT1`. **`#config:` line added**: `model_version` (initially `v10-food-pool`, bumped to `v11-per-predator-fedrate`).

## 1. Execution model

The simulator is pure C# (not a MonoBehaviour). Unity-side entry points are:

- `SimulationController` (MonoBehaviour) — runs N scenarios of a single `SimulationConfig` (ScriptableObject).
- `BulkSimulationController` (MonoBehaviour) — reads a CSV where each row is a `BulkBatchConfig` (one run) and runs N scenarios per row.

Both drive an `EcosystemSimulator` + `TemperatureCalculator` pair per scenario via `SimulationRunner`. The same core logic runs regardless of entry point.

### Terminology

| Term | Definition | Code representation |
|------|-----------|---------------------|
| Scenario | One deterministic simulation with one seed, `DaysPerScenario` days long. | `SimulationRunner.Run()` |
| Run | One configuration (species + env); runs N scenarios with seeds `BaseSeed + i`. | `SimulationConfig` or one `BulkBatchConfig` row |
| Bulk | A CSV file of runs. Each row produces one `aggregate.csv`. | `BulkSimulationController` |

## 2. Scenario loop (`SimulationRunner.Run`)

For each scenario:

1. Create a fresh `EcosystemSimulator` and `TemperatureCalculator`, seeded from `BaseSeed + scenarioIndex`.
2. Build `SimSpecies` instances from `RunSpeciesList` (or fall back to `InitializeDefaultSpecies()` if none).
3. For `dayIndex = 0..TotalDays-1`:
   1. Compute today's temperature via `TemperatureCalculator.GetTemperature(dayIndex)`.
   2. If `dayIndex == 0` **or** `(dayIndex+1) % BiologyStep == 0`, call `EcosystemSimulator.ProcessBiologyStep(temp)` (§4).
   3. Record a `StepRecord` with populations, condition, and event counters for this day.
   4. If `Ecosystem.HasCrashed()` (all populations extinct), mark `HasCrashed`, set `CrashDay`, and break.
4. Emit CSV via `ToCsvInternal`. Aggregate stats via `ToScenarioResult`.

`BiologyStep` defaults to 1 (biology runs daily). Values 2–5 skip biology on non-multiple days but still advance temperature and record the row.

## 3. Temperature model (`TemperatureCalculator`)

Per day:

```
T(day) = BaseTemperature
       + Seasonal(day)
       + ClimateTrend(day)
       + InterannualVariation(day)
       + DailyVariation(day)
```

then clamped to `[MinTemp, MaxTemp]`.

| Component | Formula | Notes |
|-----------|---------|-------|
| Seasonal | `sin(2π · day / 365) · SeasonalAmplitude` | Coldest at `day = 0`, warmest at `day ≈ 182`. `DAYS_PER_YEAR` is a constant 365. |
| Climate trend | `ClimateTrendPerYear · (day / 365)` | Linear in simulated years. |
| Interannual variation | Per-year random offset, cached in `_yearVariations`. Same value for all days of that year. Formula: `(cold + warm) / 2 − biasMean`, where `cold = uniform(−VariabilityMagnitude, 0)`, `warm = uniform(0, VariabilityMagnitude · WarmingBias)`, and `biasMean = VariabilityMagnitude · (WarmingBias − 1) / 4`. The `biasMean` subtraction zero-centers the distribution so `WarmingBias` only controls the *shape* (warm tail wider when `bias > 1`); long-term warming/cooling trend is owned solely by `ClimateTrendPerYear`. Disabled if `UseInterannualVariation = false`. |
| Daily variation | `R = BaseRandomness + RandomnessGrowthRate · year`; raw draw `uniform(−R, +R)`. If `UseAutocorrelation` (default true): `v_today = 0.7 · v_yesterday + 0.3 · v_new`; stored in `_previousDayVariation`. |

### Year numbering

`TemperatureCalculator.GetYear(day)` returns `(day / 365) + 1`, so year 1 = days 0–364, year 2 = 365–729, etc.

## 4. Biology sequence (`ProcessBiologyStep`)

Ten ordered sub-steps. Every sub-step iterates species in the order they appear in `Species` (the order in which they were added to `RunSpeciesList`).

### Step 1 — Thermal performance (`SimSpecies.CalculatePerformance`)

For each species:

1. Apply the per-species temperature offset: `T_eff = T_ambient + TemperatureDebuff`.
2. Compute the lethal fade factor using a cosine taper of width `LETHAL_TRANSITION_WIDTH = 2.0 °C`:
   - Below `CTminC`: `fade = 0`.
   - `CTminC ≤ T_eff < CTminC + tw`: `fade = 0.5 · (1 + cos(π · (CTminC + tw − T_eff) / tw))`.
   - Above `CTmaxC`: `fade = 0`.
   - `CTmaxC − tw < T_eff < CTmaxC`: multiply existing fade by `0.5 · (1 + cos(π · (T_eff − (CTmaxC − tw)) / tw))`.
   - Otherwise: `fade = 1`.
3. Convert to Kelvin: `T = T_eff + 273.15`.
4. Evaluate the Arrhenius formula:
   ```
   numerator   = exp(B/OT − B/T)
               · (1 + exp(L/OT − L/LB) + exp(U/UB − U/OT))
   denominator = 1 + exp(L/T − L/LB) + exp(U/UB − U/T)
   perf        = numerator / denominator
   ```
   where `OT = OptimalTempK`, `B = ArrhenBreadth`, `L = ArrhenLower`, `U = ArrhenUpper`, `LB = LowerBoundK`, `UB = UpperBoundK`.
5. `RawThermalPerformance = clamp(perf, 0, 1) · fade`. **Pmax is not applied here.**

Then `ThermalPerformance = RawThermalPerformance · Pmax`.

### Step 2 — Feeding / Predation

Two distinct sub-steps in `ProcessFeedingWithAccumulator`, computed in this order:

#### 2a. Tier 1 FedRate from shared food-pool (v10, linear; cap always on as of v11.1)

Runs unconditionally — does not depend on the presence of predators.

```
food_density = max(0, 1 − tier1Pop / max(CarryingCapacityPerTier, 1))
FedRate_T1   = min(1, HuntingEfficiency · food_density)               # per Tier 1 species
```

- **Linear, not Holling II.** Tier 1 species are passive extractors (filter feeding, surface-area-driven nutrient uptake) — no search-time + handling-time structure that motivates Holling II. Holling II also collapses to `1` at HE=1 default (halfSat → 0), which would defeat the food-pool effect. Linear matches plankton-style biology directly.
- `HuntingEfficiency` for Tier 1 semantically = "resource extraction efficiency". Default `1.0` = perfect extraction.
- **Carrying capacity is always on (v11.1)**. `CarryingCapacityPerTier > 0` is enforced at validation time. The previous `UseCarryingCapacity = false` mode was removed because Tier 1 species without a resource ceiling grow exponentially.

#### 2b. Tier 2 feeding (Holling Type II + per-predator FedRate, v11)

Runs only if prey (Tier 1) and predators (Tier 2) are both present (otherwise predator `FedRate = 0` and the function returns early).

1. For each predator: `rawDemand = Pop · EatingAmount · ThermalPerformance · BiologyStep`.
2. Hunting success per predator: `holling = ratio / (ratio + halfSat)` where `halfSat = NORMAL_PREY_RATIO · (1 − baseEff) / baseEff` and `baseEff = HuntingEfficiency`. Add `variance = uniform(−HuntingVariance, +HuntingVariance)`. Clamp to `[MIN_HUNTING_SUCCESS, MAX_HUNTING_SUCCESS] = [0, 1]`. Stored on `pred.CurrentHuntingSuccess`.
3. `actualDemand = rawDemand · huntingSuccess`.
4. `totalEaten = min(availablePrey, sum(actualDemand))`.
5. **Per-predator FedRate (v11)**: each predator gets a share of the catch proportional to its own hunting effort.
   - `scarcityFactor = totalEaten / totalActualDemand` (capped at 1.0; defaults to 1.0 if `totalActualDemand = 0`).
   - `fedRate_i = min(1, huntingSuccess_i × scarcityFactor)`.
   - When prey is abundant, each predator's FedRate equals its hunting success. When prey is scarce, every predator is scaled down by the same factor — the relative gap between specialist and generalist persists.
   - Reduces exactly to the v10 pooled formula `fedRate = totalEaten / totalRawDemand` when there is only one predator species (no behaviour change for single-species runs).
   - `LastFedRateT2` (CSV column) is the population-weighted average across predator species.
6. Distribute removals across prey variants proportional to their population. Track fractional deaths via `_predationAccumulators[prey.FullName]`; whole-integer deaths are subtracted from prey populations. (No predator-side preference for prey variants — separate concern, item B7 in pending list.)

### Step 3 — Raw final performance

For each species: `RawFinalPerformance = RawThermalPerformance · FedRate`. This is the target for Condition drift. **No Pmax.**

Note: in v10 Tier 1 `FedRate` varies with food density (no longer hardcoded to 1.0), so Tier 1 `RawFinalPerformance` — and therefore the Condition drain target — now depends on both temperature and density. Tier 2 already had density-dependent `FedRate` via Holling II.

### Step 4 — Update Condition

Condition ∈ [0, 1] per species, initialised to 1.0 at scenario start.

```
target    = RawFinalPerformance
pmaxSafe  = max(Pmax, 1e-4)

if Condition > target:
    severity      = (1 − target)²
    effectiveDrain = ConditionDrainRate · (1 + severity) / pmaxSafe
    Condition    -= (Condition − target) · effectiveDrain

else if Condition < target:
    boost            = target²
    effectiveRecovery = ConditionRecoveryRate · (1 + boost) · pmaxSafe
    Condition       += (target − Condition) · effectiveRecovery

Condition = clamp(Condition, 0, 1)
```

Properties:

- **Asymmetric**: base drain 0.15/day > base recovery 0.10/day (defaults from `SimulationConfig`).
- **Quadratic acceleration**: drain reaches 2× at `target → 0`; recovery reaches 2× at `target → 1`.
- **Pmax rate scaling (v9)**: specialists (high `Pmax`) drain slower and recover faster. Target is untouched, so Condition still peaks at 1.0 for every species.
- **pmaxSafe clamp**: `Pmax = 0` would divide-by-zero; we treat it as `1e-4`.

### Step 5 — Final performance

`FinalPerformance = ThermalPerformance · FedRate`. Computed for logging and CSV output. **Not read by any downstream biology step.** (Pre-v8 this drove reproduction; v8 moved reproduction onto Condition.)

### Step 6 — Thermal death (instant, terminal)

For each species:
- If `RawThermalPerformance > 0`, nothing happens.
- If `RawThermalPerformance == 0` (i.e. past CTmin or CTmax lethal limit), `Population → 0` and `Condition → 0`. The count goes into `LastTempDeathsT1` / `LastTempDeathsT2`.

Temperatures that reduce performance but don't hit the lethal limit do **not** trigger thermal death — they drive Condition down in Step 4, and then Step 7 handles mortality.

### Step 7 — Condition death (graduated, with survivor fitness boost)

For each species:

- Skip if `Condition ≥ DeathThreshold`.
- `severity = (DeathThreshold − Condition) / DeathThreshold`, range [0, 1].
- `rawDeaths = Pop · severity · DeathRate · BiologyStep`.
- Add to `_conditionDeathAccumulators[sp.FullName]`. Floor gives `wholeDeaths`; residual stays in accumulator. Cap `wholeDeaths` at current population.
- If `wholeDeaths > 0`:
  - `Pop -= wholeDeaths`.
  - **Survivor fitness boost**: `Condition = min(1, oldCondition · oldPop / newPop)`. This assumes the dead were the weakest (Condition ≈ 0) and redistributes the surviving group's health pool. Prevents death spirals.

### Step 8 — Reproduction

Skip if `Pop < MIN_POPULATION_FOR_REPRODUCTION = 2`.

**ReproScale** is piecewise-continuous in Condition, joined at `ReproThreshold`:

```
STRUGGLING_REPRO_RATE = 0.10

if ReproThreshold >= 1.0:
    reproScale = STRUGGLING_REPRO_RATE · Condition            # degenerate edge case
elif ReproThreshold <= 0:
    reproScale = Condition                                     # degenerate edge case
elif Condition >= ReproThreshold:
    t = (Condition - ReproThreshold) / (1 - ReproThreshold)
    reproScale = STRUGGLING_REPRO_RATE + (1 - STRUGGLING_REPRO_RATE) · t
else:
    reproScale = STRUGGLING_REPRO_RATE · (Condition / ReproThreshold)

reproScale = clamp(reproScale, 0, 1)
```

- `Condition = ReproThreshold` → `reproScale = 0.10` (STRUGGLING rate, no discontinuity).
- `Condition = 1.0` → `reproScale = 1.0` (full reproduction).
- `Condition = 0` → `reproScale = 0`.

**Births** (v9 + v10):

```
births = Pop · reproScale · ReproductionMultiplier · Pmax · BiologyStep
```

Modifiers applied to `births`:

1. **Tier 1 no-predator penalty**: if `Tier == 1` and `GetTierPopulation(2) < MIN_ALIVE_POP`, `births *= NO_PREDATOR_PENALTY = 0.85`.
2. **(removed in v10)** ~~Tier 1 carrying-capacity soft cap on births.~~ The previous `births *= max(0, 1 − tierPop/cap)` block was deleted. Reproduction is now throttled indirectly through the Condition pathway: high pop → low food density (Step 2a) → low Tier 1 `FedRate` → low `RawFinalPerformance` target (Step 3) → Condition drains (Step 4) → reproScale shrinks AND condition deaths fire (Step 7). The processing-order bug that came from the live `tierPop` read is gone with this code path.

**Birth accumulator**: `_birthAccumulators[sp.FullName] += births`. Emit `floor(accumulated)` as whole births, keep the residual for the next day.

**Newborn Condition (v10)**: newborns inherit the species' current group Condition. No fixed constant, no food-density multiplier. Rationale: parent's current Condition is already a lagged integral of recent food density (Tier 1) or hunting success (Tier 2), so it encodes recent provisioning capacity; multiplying by today's `FedRate` would double-count the same signal. The egg/larva's reserves were determined by past conditions, not by the moment of birth. Newborn vulnerability emerges from same-drain-no-head-start dynamics, not from a starting-Condition penalty. The population-weighted average is mathematically unchanged when newborns match the group, so no explicit Condition update is needed in this step. Same logic applies symmetrically for Tier 1 and Tier 2.

### Step 9 — Natural death (flat rate)

For each species with `Pop > 0`:

- `rate = NaturalDeathRate + uniform(−NaturalDeathVariance, +NaturalDeathVariance)`.
- Clamp `rate ≥ 0`.
- `rawDeaths = Pop · rate · BiologyStep`.
- Accumulate via `_naturalDeathAccumulators[sp.FullName]`. Apply floor to population.

Independent of performance, condition, temperature, and predation — models background mortality (old age, accidents, disease).

**Default rate per tier** ([SimSpecies.cs:195](../Assets/scripts/Simulation/SimSpecies.cs)):
- Tier 1 (prey): `NaturalDeathRate = 0.02` (2% / day).
- Tier 2 (predator): `NaturalDeathRate = 0.01` (1% / day) — allometric:
  larger, longer-lived predators have lower background mortality.
Both are user-overridable per species.

### Step 10 — Population rounding

Each species: `Pop = Math.Round(Pop, MidpointRounding.AwayFromZero)`. Populations are integers from this point until the next step's biology runs.

### Bookkeeping after all 10 steps

- `ComputeAverageCondition()` — population-weighted per-tier `AvgConditionT1` / `AvgConditionT2`.
- `EndPopT1` / `EndPopT2` recorded.
- `UpdateAccumulatorTotals()` snapshots accumulator values for the CSV.

## 5. Crash detection (`EcosystemSimulator.HasCrashed`)

**Source of truth**: a scenario is crashed if and only if the **total population
(Tier 1 + Tier 2) is exactly 0** ([EcosystemSimulator.cs:1252-1256](../Assets/scripts/Simulation/EcosystemSimulator.cs)):

```csharp
public bool HasCrashed()
{
    float totalPop = GetTier1Population() + GetTier2Population();
    return totalPop == 0;
}
```

There is no `MIN_ALIVE_POP` threshold check and no "tier-was-populated-at-init"
guard in the crash predicate itself. A scenario that starts with only one tier
populated will report a crash as soon as that tier reaches zero — this is
intentional and considered correct.

`GetCrashedTier()` returns:
- `1` — Tier 1 is empty (and Tier 1 was populated at init).
- `2` — Tier 2 is empty (and Tier 2 was populated at init).
- `0` — both tiers empty (or other ambiguous state).
- `-1` — neither tier was populated at initialization (degenerate config).

The fields `_tier1WasPopulated` / `_tier2WasPopulated` are tracked at init solely
to drive `GetCrashedTier`'s tier attribution; they do not gate `HasCrashed`.

`MIN_ALIVE_POP = 1.0f` ([EcosystemSimulator.cs:218](../Assets/scripts/Simulation/EcosystemSimulator.cs)) is used
in Step 8 as the no-predator-penalty threshold (when computing Tier 1 births),
**not** in crash detection.

## 6. Accumulators

Four `Dictionary<string, float>` keyed by `SimSpecies.FullName` (= `"{Name}_{Variant}"`):

| Accumulator | Step | Purpose |
|-------------|------|---------|
| `_birthAccumulators` | 8 | Fractional births carry across days. |
| `_predationAccumulators` | 2 | Fractional prey removals by variant. |
| `_naturalDeathAccumulators` | 9 | Fractional natural deaths. |
| `_conditionDeathAccumulators` | 7 | Fractional condition deaths. |

`_thermalDeathAccumulators` exists as a field but is not used by the current thermal-death logic (which is binary/instant).

Per-day snapshots of accumulator totals are written to the CSV via tier-level columns `BirthAccumT1/T2`, `NaturalDeathAccumT1/T2`, `ConditionDeathAccumT1/T2`, `PredationAccumT1`.

**v12 accessors (per-species residuals):** `GetBirthAccum(fullName)`, `GetNaturalDeathAccum(fullName)`, `GetConditionDeathAccum(fullName)`, `GetPredationAccum(fullName)` expose the accumulator state for individual species. These are surfaced as per-day per-species CSV columns `{S}_BirthAccum`, `{S}_NatDeathAccum`, `{S}_CondDeathAccum`, `{S}_PredAccum`.

### Per-species event counters (v12)

In addition to the tier-level `Last*` properties below, `EcosystemSimulator` keeps per-species event counters as `Dictionary<string, long>` (or `<float>`) keyed by `FullName`:

| Counter | Updated in | CSV column suffix |
|---|---|---|
| `LastBirthsBySpecies` | Step 8 (`ApplyReproduction`) | `{S}_Births` |
| `LastTempDeathsBySpecies` | Step 6 (`ApplyThermalDeath`) | `{S}_TempDeaths` |
| `LastConditionDeathsBySpecies` | Step 7 (`ApplyConditionDeath`) | `{S}_CondDeaths` |
| `LastNaturalDeathsBySpecies` | Step 9 (`ApplyNaturalDeathWithAccumulator`) | `{S}_NatDeaths` |
| `LastEatenBySpecies` | Step 2 predation block | `{S}_Eaten` |
| `LastReproScaleBySpecies` | Step 8 | `{S}_ReproScale` |
| `LastFedRateBySpecies` | Step 2a (T1) and Step 2b (T2) | `{S}_FedRate` |
| `StartPopBySpecies` | Top of `ProcessBiologyStep` | (used to compute per-capita `BirthRate`) |

All cleared and re-initialized to zero per species at the top of `ProcessBiologyStep`. The increments live alongside the tier-level `LastX += deaths` lines in the same conditional branch — invariant: sum of per-species values equals the tier-level value.

### Per-day diagnostic fields (set by biology, read by CSV writer)

`EcosystemSimulator` exposes the following `LastX` properties, all updated
during `ProcessBiologyStep` and snapshotted into `StepRecord` for the CSV:

| Field | Updated in | CSV column | Meaning |
|-------|------------|------------|---------|
| `LastFedRateT1` | Step 2a | `FedRateT1` | Tier-1 food-pool feeding rate (single value, all prey share food pool). |
| `LastFoodDensityT1` | Step 2a | `FoodDensityT1` | `max(0, 1 − tier1Pop / cap)`. |
| `LastFedRateT2` | Step 2b | `FedRateT2` | **Population-weighted average** of per-predator FedRate across Tier 2 (v11 semantic — was a pooled scalar pre-v11). |
| `LastAvgHuntingEfficiency` | Step 2b | `AvgHuntingEff` | Arithmetic mean of `huntingSuccess_i` across all predators. |
| `LastReproScaleT1` | Step 8 | `ReproScaleT1` | Reproduction throttle for Tier 1 (driven by Condition). Useful for diagnosing why births stalled. |
| `LastReproScaleT2` | Step 8 | `ReproScaleT2` | Same for Tier 2. |

These are not used by any biology step — purely for human/CSV inspection.

### Predation: how prey are removed across variants

When Tier 2 predators eat from a Tier 1 species that has multiple variants
populated (Arctic / Common / Tropical / Custom), the per-day kill total is
distributed **proportional to each variant's current population fraction**
([EcosystemSimulator.cs:749-775](../Assets/scripts/Simulation/EcosystemSimulator.cs)).
There is no per-variant prey preference yet (tracked as item B7 in pending list).

### Carrying-capacity validation

`SimulationConfig.IsValid()` hard-rejects `CarryingCapacityTier1 ≤ 0`
([SimulationConfig.cs:160](../Assets/scripts/Simulation/DataStructure/SimulationConfig.cs)).
Since v11.1 there is no fallback / "disabled" state — a config that fails
this check will not run.

### Backward compatibility for legacy bulk CSVs

`CsvBatchParser` keeps `use_carrying_cap` as an optional, deprecated column.
If an older bulk CSV still includes it the value is read but ignored, and a
`Debug.LogWarning` is emitted. New CSVs from `GenerateTemplate()` omit the
column entirely.

## 7. Design invariants

- **Condition is species-agnostic on [0, 1].** The drain target omits Pmax so thresholds (`ReproThreshold`, `DeathThreshold`) mean the same thing for every species.
- **Pmax enters exactly four pathways** (all post-Condition):
  1. `ThermalPerformance = Raw · Pmax` → predator hunting demand in Step 2.
  2. Condition drain rate divisor in Step 4.
  3. Condition recovery rate multiplier in Step 4.
  4. Birth multiplier in Step 8.
- **FinalPerformance is dead code path in biology.** Still computed for CSV output; not consumed by any downstream step. Pre-v8 history preserved for continuity.
- **All random draws use the seeded `_rng`** in `EcosystemSimulator` (for hunting/natural-death variance) or `TemperatureCalculator._rng` (for temperature noise). Reproducibility depends on `BaseSeed + scenarioIndex`.
- **First-mover bias in Step 8 carrying capacity** — eliminated in v10 by deleting the soft-cap-on-births block (the live `tierPop` read no longer exists).
- **Per-predator Tier 2 FedRate** (v11): each predator's FedRate reflects its own hunting effort (scaled by overall scarcity), not a pooled group average. Specialist hunters get higher FedRate than generalists in mixed-HE runs. Reduces to v10 pooled formula in single-predator-species runs.
- **Thermal death is terminal.** Suboptimal-but-survivable temperatures channel through Condition; the lethal cliff is binary.

## 8. Version history recorded in source comments

| Version | Change |
|---------|--------|
| v6 | Added Condition (health) system with drain/recovery. Split thermal death into instant (lethal) + condition (chronic). Decoupled natural death from performance (flat rate). Crash detection uses total population. Scenario length is days, not years. |
| v7 | Replaced dual hunting system with single Holling Type II. FedRate has no floor (zero prey = zero efficiency). |
| v8 | Reproduction moved off `FinalPerformance` onto `Condition`. Two-region continuous formula around `ReproThreshold`. Side effect: Pmax dropped out of reproduction and Condition pathways. |
| v9 | Re-wired Pmax into: reproduction birth multiplier, Condition drain divisor, Condition recovery multiplier. Condition target and thresholds unchanged. |
| v10 | Carrying capacity reframed as a shared food/resource pool driving Tier 1 FedRate (linear: `min(1, HE × food_density)`). Soft-cap-on-births block deleted from `ApplyReproduction` (processing-order bug eliminated as side effect). `HuntingEfficiency` for Tier 1 now meaningful as resource-extraction efficiency. `NEWBORN_CONDITION` constant removed — newborns inherit parent group Condition. CSV adds `FedRateT1`, `FoodDensityT1`, `model_version`. Pooled Tier 2 FedRate intentionally untouched (v11). |
| v11 | Per-predator Tier 2 FedRate (review item A1). `fedRate_i = min(1, huntingSuccess_i × scarcityFactor)` where `scarcityFactor = totalEaten / totalActualDemand`. Replaces the pooled `fedRate = totalEaten / totalRawDemand` that erased per-species competitive signal. `LastFedRateT2` is now a population-weighted average across predators. Reduces to v10 formula in single-predator-species runs. `model_version` bumped to `v11-per-predator-fedrate`. |
| v11.1 | Removed `UseCarryingCapacity` toggle from `SimulationConfig`, `BulkBatchConfig`, `EcosystemSimulator`, `ScenarioResult`. Carrying capacity is always on. Old bulk CSVs that include `use_carrying_cap` parse with a deprecation warning and the value is ignored. `GenerateTemplate()` omits the column and uses `seasonal_amp = 5`, `condition_drain_rate = 0.15`. `IsValid()` hard-rejects non-positive cap. `model_version` bumped to `v11.1-cap-always-on`. |
| v12 | Per-species daily tracking added. `StepRecord.SpeciesData : Dictionary<string, PerSpeciesStepData>` keyed by `FullName`. Scenario CSV gains 17 per-species columns (Pop, Cond, ThermalPerf, FinalPerf, FedRate, HuntingEff, Births, TempDeaths, CondDeaths, NatDeaths, Eaten, BirthRate, ReproScale, BirthAccum, NatDeathAccum, CondDeathAccum, PredAccum). `EcosystemSimulator` exposes `LastXBySpecies` event counters and `GetXAccum(fullName)` accessors. `ScenarioResult.SpeciesMetrics` (PerSpeciesScenarioMetrics) holds final-365-day means, full-run means, population CV, min/max, extinction day, crash day. `AggregateResults.PerSpeciesMetrics` and `BulkRunSummary.PerSpeciesMetrics` aggregate across scenarios and runs. Aggregate CSV gains 3 sections (`PER-SPECIES FINAL YEAR METRICS`, `PER-SPECIES FULL-RUN METRICS`, `PER-SPECIES STABILITY METRICS`); bulk_summary CSV gains 3 sections (`PER-RUN PER-SPECIES FINAL YEAR`, `CROSS-RUN PER-SPECIES FINAL YEAR`, `CROSS-RUN STABILITY`). All existing tier columns and sections preserved verbatim. `model_version` bumped to `v12-per-species-tracking`. |

## 9. Fallback defaults (used only if no `RunSpeciesList` is provided)

`SimSpecies.CreateHexapod(variant, pop)` and `CreateSheplik(variant, pop)` produce hardcoded species for testing:

| Variant | Topt (°C) | Pmax | CTmin (°C) | CTmax (°C) | LowerBound (°C) | UpperBound (°C) |
|---------|-----------|------|------------|------------|-----------------|-----------------|
| Arctic | 5 | 1.0 | −30 | 20 | −3 | 7 |
| Common | 20 | 0.9 | −5 | 40 | 12 | 22 |
| Tropical | 35.5 | 1.0 | 0 | 80 | 27 | 37 |

Both species share `ArrhenBreadth = 5273.15`, `ArrhenLower = 10273.15`, `ArrhenUpper = 21273.15`. `Hexapod` is Tier 1 with `ReproMult = 0.45`, `DeathRate = 0.6`; `Sheplik` is Tier 2 with `ReproMult = 0.1`, `DeathRate = 0.3`, `EatingAmount = 1.5`, `HuntingEfficiency = 0.75`. Production runs always pass a `RunSpeciesList` and never hit these defaults.
