# Simulation Specification

Authoritative description of the headless ecosystem simulator, generated from source. This is the contract; the C# is the implementation.

Source files: `EcosystemSimulator.cs`, `SimSpecies.cs`, `TemperatureCalculator.cs`, `SimulationRunner.cs`.

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

### Step 2 — Feeding / Predation (Holling Type II)

Only runs if prey (Tier 1) and predators (Tier 2) are both present.

1. For each predator: `rawDemand = Pop · EatingAmount · ThermalPerformance · BiologyStep`.
2. Hunting success per predator: `holling = ratio / (ratio + halfSat)` where `halfSat = NORMAL_PREY_RATIO · (1 − baseEff) / baseEff` and `baseEff = HuntingEfficiency`. Add `variance = uniform(−HuntingVariance, +HuntingVariance)`. Clamp to `[MIN_HUNTING_SUCCESS, MAX_HUNTING_SUCCESS] = [0, 1]`.
3. `actualDemand = rawDemand · huntingSuccess`.
4. `totalEaten = min(availablePrey, sum(actualDemand))`.
5. `fedRate = totalEaten / totalRawDemand` (or 1 if demand is 0). Assigned to every predator's `FedRate`.
6. Distribute removals across prey variants proportional to their population. Track fractional deaths via `_predationAccumulators[prey.FullName]`; whole-integer deaths are subtracted from prey populations.

Prey always have `FedRate = 1.0` (they feed from the environment, not from each other).

### Step 3 — Raw final performance

For each species: `RawFinalPerformance = RawThermalPerformance · FedRate`. This is the target for Condition drift. **No Pmax.**

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

**Births** (v9):

```
births = Pop · reproScale · ReproductionMultiplier · Pmax · BiologyStep
```

Modifiers applied to `births` (in order):

1. **Tier 1 no-predator penalty**: if `Tier == 1` and `GetTierPopulation(2) < MIN_ALIVE_POP`, `births *= NO_PREDATOR_PENALTY = 0.85`.
2. **Tier 1 carrying capacity (soft cap)**: if `Tier == 1` and `UseCarryingCapacity`, `births *= max(0, 1 − tierPop / CarryingCapacityPerTier)`.
   - Note: `tierPop` is evaluated live mid-step, so species processed earlier in the list see a slightly lower `tierPop` than species processed later. This is a known first-mover artefact, not corrected in v9.

**Birth accumulator**: `_birthAccumulators[sp.FullName] += births`. Emit `floor(accumulated)` as whole births, keep the residual for the next day.

**Condition dilution**: after whole births are added, `Condition = (oldPop · oldCondition + wholeBirths · NEWBORN_CONDITION) / newPop`, with `NEWBORN_CONDITION = 0.5`.

### Step 9 — Natural death (flat rate)

For each species with `Pop > 0`:

- `rate = NaturalDeathRate + uniform(−NaturalDeathVariance, +NaturalDeathVariance)`.
- Clamp `rate ≥ 0`.
- `rawDeaths = Pop · rate · BiologyStep`.
- Accumulate via `_naturalDeathAccumulators[sp.FullName]`. Apply floor to population.

Independent of performance, condition, temperature, and predation — models background mortality (old age, accidents, disease).

### Step 10 — Population rounding

Each species: `Pop = Math.Round(Pop, MidpointRounding.AwayFromZero)`. Populations are integers from this point until the next step's biology runs.

### Bookkeeping after all 10 steps

- `ComputeAverageCondition()` — population-weighted per-tier `AvgConditionT1` / `AvgConditionT2`.
- `EndPopT1` / `EndPopT2` recorded.
- `UpdateAccumulatorTotals()` snapshots accumulator values for the CSV.

## 5. Crash detection (`EcosystemSimulator.HasCrashed`)

The simulator is considered crashed if the total population (Tier 1 + Tier 2) is below `MIN_ALIVE_POP` **and** at least one tier was populated at initialization (to avoid false crashes from scenarios that start with no predators).

`GetCrashedTier()` returns `1`, `2`, or `0` depending on which tier is empty.

## 6. Accumulators

Four `Dictionary<string, float>` keyed by `SimSpecies.FullName` (= `"{Name}_{Variant}"`):

| Accumulator | Step | Purpose |
|-------------|------|---------|
| `_birthAccumulators` | 8 | Fractional births carry across days. |
| `_predationAccumulators` | 2 | Fractional prey removals by variant. |
| `_naturalDeathAccumulators` | 9 | Fractional natural deaths. |
| `_conditionDeathAccumulators` | 7 | Fractional condition deaths. |

`_thermalDeathAccumulators` exists as a field but is not used by the current thermal-death logic (which is binary/instant).

Per-day snapshots of accumulator totals are written to the CSV via `BirthAccumT1/T2`, `NaturalDeathAccumT1/T2`, `ConditionDeathAccumT1/T2`, `PredationAccumT1`.

## 7. Design invariants

- **Condition is species-agnostic on [0, 1].** The drain target omits Pmax so thresholds (`ReproThreshold`, `DeathThreshold`) mean the same thing for every species.
- **Pmax enters exactly four pathways** (all post-Condition):
  1. `ThermalPerformance = Raw · Pmax` → predator hunting demand in Step 2.
  2. Condition drain rate divisor in Step 4.
  3. Condition recovery rate multiplier in Step 4.
  4. Birth multiplier in Step 8.
- **FinalPerformance is dead code path in biology.** Still computed for CSV output; not consumed by any downstream step. Pre-v8 history preserved for continuity.
- **All random draws use the seeded `_rng`** in `EcosystemSimulator` (for hunting/natural-death variance) or `TemperatureCalculator._rng` (for temperature noise). Reproducibility depends on `BaseSeed + scenarioIndex`.
- **First-mover bias** in Step 8 carrying capacity — known, untouched in v9.
- **Thermal death is terminal.** Suboptimal-but-survivable temperatures channel through Condition; the lethal cliff is binary.

## 8. Version history recorded in source comments

| Version | Change |
|---------|--------|
| v6 | Added Condition (health) system with drain/recovery. Split thermal death into instant (lethal) + condition (chronic). Decoupled natural death from performance (flat rate). Crash detection uses total population. Scenario length is days, not years. |
| v7 | Replaced dual hunting system with single Holling Type II. FedRate has no floor (zero prey = zero efficiency). |
| v8 | Reproduction moved off `FinalPerformance` onto `Condition`. Two-region continuous formula around `ReproThreshold`. Side effect: Pmax dropped out of reproduction and Condition pathways. |
| v9 | Re-wired Pmax into: reproduction birth multiplier, Condition drain divisor, Condition recovery multiplier. Condition target and thresholds unchanged. |

## 9. Fallback defaults (used only if no `RunSpeciesList` is provided)

`SimSpecies.CreateHexapod(variant, pop)` and `CreateSheplik(variant, pop)` produce hardcoded species for testing:

| Variant | Topt (°C) | Pmax | CTmin (°C) | CTmax (°C) | LowerBound (°C) | UpperBound (°C) |
|---------|-----------|------|------------|------------|-----------------|-----------------|
| Arctic | 5 | 1.0 | −30 | 20 | −3 | 7 |
| Common | 20 | 0.9 | −5 | 40 | 12 | 22 |
| Tropical | 35.5 | 1.0 | 0 | 80 | 27 | 37 |

Both species share `ArrhenBreadth = 5273.15`, `ArrhenLower = 10273.15`, `ArrhenUpper = 21273.15`. `Hexapod` is Tier 1 with `ReproMult = 0.45`, `DeathRate = 0.6`; `Sheplik` is Tier 2 with `ReproMult = 0.1`, `DeathRate = 0.3`, `EatingAmount = 1.5`, `HuntingEfficiency = 0.75`. Production runs always pass a `RunSpeciesList` and never hit these defaults.
