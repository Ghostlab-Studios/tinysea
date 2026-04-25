# Simulation — Logical Review

Line-by-line review of the headless simulation code, flagging places where the implementation's behavior may diverge from biological or design intent. Not a code-quality review (no style nits). The intent is the same kind of scrutiny that surfaced the pre-v9 Pmax-missing-from-reproduction issue.

Severity tags used throughout:
- **INFO** — noting a design choice, no action implied.
- **MINOR** — documentation mismatch or cosmetic inconsistency.
- **CONCERN** — design choice with a plausible biological argument either way; worth confirming intent.
- **ISSUE** — likely unintended behaviour; recommend fixing or re-justifying.

Source scanned: `EcosystemSimulator.cs` (v9), `SimSpecies.cs`, `TemperatureCalculator.cs`, `SimulationRunner.cs`, `SimulationConfig.cs`, `BulkBatchConfig.cs`, `CsvBatchParser.cs`, `BulkSimulationController.cs`, `ScenarioResult.cs`.

---

## 1. Scope and method

I walked through every function the simulator calls during one day (`ProcessBiologyStep` and everything it invokes), plus the temperature model, the scenario runner, and the config/CSV surfaces. Each subsection below summarises what the code does and then flags any logical concerns.

This is a **logical** review — not checking for C# bugs or performance, but for cases where "what the code does" and "what a marine ecologist expects the model to do" might diverge.

---

## 2. Biology Step 1 — Thermal Performance (`SimSpecies.CalculatePerformance`)

### What the code does

1. Apply per-species temperature offset: `T_eff = temperatureCelsius + TemperatureDebuff`.
2. Apply a cosine fade over `LETHAL_TRANSITION_WIDTH = 2°C` near `CTminC` and `CTmaxC`. Performance goes to exactly 0 at the lethal limits and smoothly to 1 × Arrhenius a full `tw` degrees inside them. `tw` is capped at `(CTmaxC - CTminC)/2` so fade halves cannot overlap for narrow lethal ranges.
3. Convert to Kelvin: `T = T_eff + 273.15`.
4. Evaluate the Schoolfield–Sharpe style Arrhenius:
   ```
   numerator   = exp(B/OT − B/T) · (1 + exp(L/OT − L/LB) + exp(U/UB − U/OT))
   denominator = 1 + exp(L/T − L/LB) + exp(U/UB − U/T)
   perf        = numerator / denominator  (clamp to [0, 1])
   ```
5. `RawThermalPerformance = clamp(perf, 0, 1) * fadeFactor`.
6. `ThermalPerformance = RawThermalPerformance × Pmax` (applied externally in `EcosystemSimulator` Step 1).

### Notes and concerns

- **INFO** — The Arrhenius form evaluates to exactly 1.0 at `T = OT` by construction (numerator = denominator). This is the correct normalisation for a "shape only" TPC before Pmax scaling.
- **INFO** — `ArrhenLower` and `ArrhenUpper` are dimensionless scale factors; Brian has been tuning these to control asymmetry. The left-skew constraint `B ≤ 8000` that Brian adopted is not enforced in code — configurations with `B > 8000` can still produce right-skewed curves.
- **CONCERN** — `LETHAL_TRANSITION_WIDTH = 2°C` is hard-coded in `SimSpecies.cs` and not tunable per species. For species with very tight lethal windows (e.g. `CTmax − CTmin < 4°C`) the fade automatically narrows, but for ordinary species the 2°C is a globally fixed smoothing — worth confirming that 2°C is biologically representative across the species you expect to model.
- **INFO** — `TemperatureDebuff` shifts the *experienced* temperature, not the TPC shape. Good for modelling body-temperature offset (Harley et al.). Default is 0.
- **MINOR** — The cosine fade can compound when both CTmin and CTmax sides are active simultaneously (narrow species whose current temperature is inside both fade regions). Line 90: `fadeFactor *= 0.5 * (1 + cos(...))`. Rare but possible if `CTmax - CTmin < 2 * LETHAL_TRANSITION_WIDTH`.

---

## 3. Biology Step 2 — Feeding / Predation (`ProcessFeedingWithAccumulator`)

### What the code does

1. Filter live predators (Tier 2, pop ≥ `MIN_ALIVE_POP`) and live prey (Tier 1, pop ≥ `MIN_ALIVE_POP`).
2. If either side is empty: all predators get `FedRate = 0`, return early. (Prey `FedRate` is never set here and stays at its default 1.0.)
3. Compute global `preyRatio = totalPrey / totalPredators`.
4. For each predator:
   - `hollingEff = CalculateHollingEfficiency(HuntingEfficiency, preyRatio)` — Holling II curve parameterised so it passes through `(NORMAL_PREY_RATIO = 20, HuntingEfficiency)`.
   - Add `variance = uniform(−HuntingVariance, +HuntingVariance)`.
   - `huntingSuccess = clamp(hollingEff + variance, 0, 1)`.
   - `rawDemand = Pop × EatingAmount × ThermalPerformance × BiologyStep` (uses Pmax via `ThermalPerformance`).
   - `actualDemand = rawDemand × huntingSuccess`.
5. `totalEaten = min(availablePrey, sum(actualDemand))`.
6. `fedRate = totalEaten / totalRawDemand` (or 1 if totalRawDemand ≤ 0). **All predators get the same `FedRate`**, regardless of their individual `huntingSuccess`.
7. Prey removals distributed across prey variants by population share: `preyLost[variant] = totalEaten × Pop_variant / availablePrey`, accumulated fractionally in `_predationAccumulators`.

### Concerns

- **ISSUE** — **Pooled FedRate ignores per-predator hunting efficiency.** Every predator species gets the same `fedRate = totalEaten / totalRawDemand`, but that quantity averages across predators with very different `huntingSuccess`. A specialist predator with `huntingSuccess = 0.9` sharing a prey pool with a predator at `huntingSuccess = 0.3` ends up with the same FedRate as its weaker competitor. In nature, good hunters eat more than bad hunters in the same environment — this pooling destroys the competitive signal that `HuntingEfficiency` is meant to encode. Fix idea: `fedRate_i = min(1, actualDemand_i / rawDemand_i) = min(1, huntingSuccess_i × (totalEaten / totalActualDemand))` so each predator keeps its own proportional share.

- **CONCERN** — **No prey preference across variants.** Line 603–606: predators take from each prey variant strictly proportional to that variant's population share. In real ecology, predators often show variant/species preference (thermal, size, or behavioural). At present, a cold-adapted predator at 25°C eats Arctic prey and Tropical prey with identical preference. Depending on the research question, this may or may not matter.

- **CONCERN** — **Prey `FedRate` is always 1.0.** Prey never have food limitation in the model — only reproductive limitation via the soft carrying cap. Resource depletion (e.g. algae limitation on prey) is not represented. For a study of pure predator–prey dynamics this is clean; for studies of bottom-up vs top-down control it's a known omission.

- **INFO** — Holling Type II parameterisation is textbook and passes cleanly through `(NORMAL_PREY_RATIO, HuntingEfficiency)`. Good.

- **INFO** — `NORMAL_PREY_RATIO = 20` is the assumed ecological prey:predator ratio. Hard-coded, not per-species. Might be worth exposing if you model systems with very different trophic structures.

- **MINOR** — Predator variance clamp at `[0, 1]` (Line 556). If `HuntingVariance` is large relative to `hollingEff`, variance can push outside [0,1] and be clamped asymmetrically, biasing the mean. Only relevant for extreme variance settings.

---

## 4. Biology Step 3 — Raw Final Performance

### What the code does

```
RawFinalPerformance = RawThermalPerformance × FedRate
```

One line. Feeds Step 4 as the Condition drain target. **No Pmax** (deliberate, v9).

### Notes

- **INFO** — Correct and simple. Target in [0, 1]. No concerns.

---

## 5. Biology Step 4 — Update Condition (v9)

### What the code does

```
target    = RawFinalPerformance
pmaxSafe  = max(Pmax, 1e-4)

if Condition > target:
    severity      = (1 - target)²
    effectiveDrain = ConditionDrainRate × (1 + severity) / pmaxSafe
    Condition    -= (Condition - target) × effectiveDrain

elif Condition < target:
    boost            = target²
    effectiveRecovery = ConditionRecoveryRate × (1 + boost) × pmaxSafe
    Condition       += (target - Condition) × effectiveRecovery

Condition = clamp(Condition, 0, 1)
```

Base rates: drain 0.15/day, recovery 0.10/day. Asymmetric (drain > recovery) implements Buckley, Huey & Ma (2025). Quadratic acceleration increases drain toward lethal, recovery toward optimal (both max 2×).

### Notes and concerns

- **INFO** — v9 Pmax rate scaling is the intended fix and matches Brian's direction. Generalist (Pmax=0.72) drains 1/0.72 ≈ 1.39× baseline; specialist (Pmax=0.9) drains 1/0.9 ≈ 1.11× baseline. Recovery is the inverse.

- **CONCERN** — **Recovery at very low target.** The recovery branch runs even when `target` is close to 0. At `target = 0.01`, `boost = 0.0001`, so `effectiveRecovery ≈ RecoveryRate × 1.0001 × Pmax`. A species sitting just below lethal can still "recover" if the temperature briefly nudges upward. Thermal death (Step 6) only fires at `RawThermalPerf == 0` exactly, so a species at `Raw = 0.01` stays in the condition system. Whether this is biologically plausible depends on the "point of no return" concept — Gosselin et al. (2021) argue there's a threshold beyond which recovery is physically impossible even in benign conditions. The current model doesn't have that.

- **CONCERN** — **Euler integration at `BiologyStep > 1`.** The drain and recovery formulas apply `rate × BiologyStep` as a single multiplicative step, which is a first-order explicit Euler approximation. For small rates × step, fine. For `BiologyStep = 5` and `ConditionDrainRate = 0.15`, the per-step factor is `0.75 + severity·0.75/Pmax`. With `severity ≈ 1` and `Pmax = 0.72`, drain factor = 2.08. The formula `Condition -= (Cond − target) × 2.08` then **overshoots** — it can drive Condition below target by more than the gap. This isn't currently a problem because `BiologyStep = 1` is the default, but any user who sets `BiologyStep > 1` will get numerically different (and biologically suspect) Condition dynamics. Worth either clamping the per-step change or switching to an exact exponential update (`Cond = target + (Cond − target) × exp(−rate × step)`).

- **INFO** — `pmaxSafe` clamp at `1e-4` safely avoids division blow-up; no species configurations use Pmax=0 currently but the guard is sensible.

- **CONCERN** — The recovery multiplier `(1 + target²) × Pmax` can be less than 1 when `Pmax < 1 / (1 + target²)`. At `target = 1` and `Pmax = 0.5`, recovery multiplier is `2 × 0.5 = 1`, so recovery is slower for a low-Pmax species than it would be for `Pmax = 1`. This is the intended v9 behaviour. But note: at `target = 0.5` and `Pmax = 0.72`, the multiplier is `1.25 × 0.72 = 0.9`, meaning the "accelerated" recovery is actually *slower* than the base rate. The quadratic boost no longer guarantees "at least base rate" when Pmax is less than 1. Consider clamping the recovery multiplier to `≥ 1`, or explicitly documenting that low-Pmax species recover sub-linearly.

---

## 6. Biology Step 5 — Final Performance

### What the code does

```
FinalPerformance = ThermalPerformance × FedRate
```

### Notes

- **INFO** — Computed and logged only. No biology step reads this field in v9. Kept for CSV output and because callers may still reference it.

---

## 7. Biology Step 6 — Thermal Death (Instant)

### What the code does

```
if sp.RawThermalPerformance > 0:  return    // survives
// else: wipeout
deaths = sp.Population
sp.Population = 0
sp.Condition = 0
LastTempDeathsT1/T2 += deaths
```

### Notes and concerns

- **CONCERN** — **Binary cliff vs smooth fade.** The cosine fade in Step 1 already brings `RawThermalPerformance` to exactly 0 only at or past `CTminC`/`CTmaxC`. Inside the fade region (e.g. `CTmaxC - 1°C`), Raw might be 0.3 × fadeFactor ≈ 0.05 — still non-zero, so thermal death doesn't fire. In practice, condition death (Step 7) picks up survival at these borderline temperatures. OK, but worth knowing: thermal death is a true cliff at the lethal limit, not a gradient.

- **MINOR** — If a species hits `RawThermalPerf == 0` and the population wipeout fires, `Condition` is also zeroed. Fine.

- **INFO** — No Pmax involvement. Intentional.

---

## 8. Biology Step 7 — Condition Death (Graduated)

### What the code does

```
if sp.Condition >= sp.DeathThreshold:  return

severity  = (DeathThreshold - Condition) / DeathThreshold
rawDeaths = sp.Population × severity × DeathRate × BiologyStep

_conditionDeathAccumulators[sp.FullName] += rawDeaths
wholeDeaths = floor(accumulator), capped at Pop
residual stays in accumulator

if wholeDeaths > 0:
    Population -= wholeDeaths
    Condition = min(1, oldCond × oldPop / newPop)   // survivor fitness boost
```

### Notes and concerns

- **INFO** — Graduated severity replaces the old binary-cliff kill. Backed by Casini et al. (2016), Dutil & Lambert (2000), Booth & Hixon (1999). Good.

- **CONCERN** — **Survivor fitness boost math.** Assumes the dead individuals had Condition ≈ 0, so they "leave behind" the old population's total health to be redistributed: `newCond = oldCond × (oldPop / newPop)`. This can push Condition upward sharply when many die. Example: `oldPop = 100, oldCond = 0.25, wholeDeaths = 50`. New Condition = `0.25 × 100/50 = 0.5`. A single catastrophic day doubles the average Condition. Mathematically consistent under the "dead were weakest" assumption, but if mortality is *random with respect to condition* (e.g. random culling by a storm), this boost is unjustified and over-credits survivors. Fine for thermally-driven death; questionable for natural death and possibly predation-driven death (which currently doesn't apply this boost, so the inconsistency matters).

- **CONCERN** — **Pmax not in condition death.** In v9 we scale drain/recovery by Pmax but not the death rate itself. A specialist and a generalist with the same Condition below threshold get the same `severity × DeathRate` mortality. The indirect specialist advantage comes only from drain-rate scaling in Step 4. Whether this is enough to match your biological intent (specialists should be more resilient overall) is a design question, raised in the email to Brian.

- **CONCERN** — **`ReproThreshold = 0.25 < DeathThreshold = 0.3` in factory defaults.** This creates a band `(0.25, 0.3)` where the species is actively dying of low Condition *and* producing "healthy"-branch reproduction (reproScale ramps from 0.1 up). Brian's CSVs use `ReproThreshold = 0.6 > DeathThreshold = 0.3`, which is clearer. The default ordering feels wrong; either reverse the defaults or add a validator warning if `ReproThreshold < DeathThreshold`.

- **INFO** — Accumulator pattern is consistent with other step accumulators.

---

## 9. Biology Step 8 — Reproduction (v9)

### What the code does

1. Short-circuit if `Pop < MIN_POPULATION_FOR_REPRODUCTION = 2`.
2. Compute `reproScale` piecewise at `ReproThreshold`:
   - `Cond ≥ ReproThreshold`: `reproScale = 0.10 + 0.90 × (Cond - thresh) / (1 - thresh)` — ramps 0.10 to 1.0.
   - `Cond < ReproThreshold`: `reproScale = 0.10 × (Cond / thresh)` — ramps 0 to 0.10.
   - Edge cases handled for `thresh >= 1` and `thresh <= 0`.
3. `births = Pop × reproScale × ReproMult × Pmax × BiologyStep`.
4. If Tier 1 and no predators: `births *= NO_PREDATOR_PENALTY = 0.85`.
5. If Tier 1 and carrying cap on: `births *= max(0, 1 − tierPop / CarryingCapacityPerTier)` where `tierPop` is evaluated live (known first-mover artefact).
6. Birth accumulator, then integer births applied to Population.
7. Condition dilution by newborns: `newCond = (oldPop × oldCond + wholeBirths × NEWBORN_CONDITION) / newPop`, `NEWBORN_CONDITION = 0.5`.

### Concerns

- **ISSUE** — **First-mover bias in carrying-capacity cap.** Already flagged (Brian Mail 8). `tierPop = GetTierPopulation(1)` is evaluated live after prior species' births have been added. Species processed first see a smaller `tierPop`, get a higher `growthFactor`, and reproduce more. Compounded over years, this produces competitive exclusion purely from list ordering. Fix: either snapshot `tierPop` once at step start, or shuffle species order per step.

- **CONCERN** — **`NO_PREDATOR_PENALTY = 0.85` contradicts ecological intuition.** In real systems, removing predators typically causes prey release (mesopredator release, trophic cascade). The simulator instead *penalises* prey reproduction by 15%. If the justification is "unbalanced ecosystem slows reproduction" that overlaps with the carrying-capacity soft cap, so the penalty is double-counting. If the justification is "predators remove weak individuals thereby improving the mean fitness of the prey pool", that isn't modelled by the simulator's proportional predation. Recommend: either delete this penalty, or explicitly document and cite a biological mechanism the model represents.

- **CONCERN** — **Reproduction threshold slope asymmetry.** The piecewise reproScale is continuous in value at `ReproThreshold` but has a slope jump. For `thresh = 0.6`, below-slope is `0.10/0.60 = 0.167` and above-slope is `0.90/0.40 = 2.25` — a 13.5× kink. Biologically this is an "if healthy then produce lots, if stressed then almost none" step function with a small soft zone. Probably fine as an intentional design choice, but worth knowing the regime flips sharply.

- **CONCERN** — **`NEWBORN_CONDITION = 0.5` is arbitrary.** Why 0.5 rather than, say, 0.7 (offspring of a healthy-enough parent inherit partial health) or a function of parent Condition? Currently, a species at `Cond = 1.0` that produces 50% of its population as newborns drops to `Cond = 0.75` in one day. If that drop pushes below `ReproThreshold`, subsequent days' reproScale collapses. The dilution mechanic creates feedback that depends on how fast the species reproduces. Document or tune.

- **INFO** — `MIN_POPULATION_FOR_REPRODUCTION = 2` matches the biological need for at least two individuals to reproduce sexually. Fine for sexually-reproducing species; might under-represent asexual/parthenogenetic reproduction.

- **CONCERN** — **Reproduction rates are very fast.** With `ReproMult = 0.45`, `Pmax = 0.9`, `reproScale = 1`, `BiologyStep = 1`, per-capita births = 0.405/day. A prey population doubles in under two days absent predation or carrying cap. This is fine for modelling fast-reproducing invertebrates, but when Brian's group interprets outputs against real-world marine taxa (fish, crustaceans), the timescales are effectively compressed. Worth documenting the "per simulated day" semantics alongside any empirical comparison.

---

## 10. Biology Step 9 — Natural Death (Flat Rate)

### What the code does

```
variance       = uniform(-1, +1) × NaturalDeathVariance
baseRate       = max(0, NaturalDeathRate + variance)
deaths         = Pop × baseRate × BiologyStep
// accumulator → whole deaths subtracted
```

Independent of temperature, feeding, condition, and predation.

### Notes and concerns

- **INFO** — Models background mortality (old age, accidents, disease). Decoupling from performance is intentional and justified.

- **CONCERN** — **No survivor fitness boost here.** Condition death in Step 7 reshuffles Condition to reflect "dead were weakest". Natural death doesn't — it just removes population. That asymmetry is OK *only if* natural death is truly random with respect to Condition (plausible for old-age / accident mortality). If instead natural death is partly correlated with Condition (e.g. disease strikes weaker individuals harder), the model under-boosts survivors. Known simplification.

- **INFO** — `NaturalDeathVariance` is symmetric around `NaturalDeathRate`, clamped at 0 on the low side. Can introduce a tiny upward bias if variance exceeds rate, but only for configurations where that's true.

---

## 11. Biology Step 10 — Population Rounding

### What the code does

```
Population = Math.Round(Population, MidpointRounding.AwayFromZero)
```

### Notes

- **INFO** — Populations are float during the step for accumulator precision, rounded to int at the end. `AwayFromZero` rounding avoids banker's rounding biases. Fine.

- **MINOR** — Rounding happens *after* all accumulators have been applied, so the accumulators correctly preserve fractional events across day boundaries. No double-counting.

---

## 12. Temperature Model (`TemperatureCalculator`)

### What the code does

```
T(day) = BaseTemperature
       + sin(2π · day / 365) × SeasonalAmplitude
       + ClimateTrendPerYear × (day / 365)
       + InterannualVariation(year)    // same value all year
       + DailyVariation(day)            // red noise if autocorrelated
       → clamp to [MinTemp, MaxTemp]
```

Interannual variation per year: `cold = uniform(−mag, 0)`, `warm = uniform(0, mag × WarmingBias)`, `variation = (cold + warm) / 2`. Cached in `_yearVariations`.

Daily variation: `R = BaseRandomness + RandomnessGrowthRate × year`; raw draw `uniform(−R, +R)`. If autocorrelated: `v_today = 0.7 × v_yesterday + 0.3 × v_new`.

### Concerns

- **ISSUE** — **`WarmingBias > 1` introduces a hidden warming trend.** With `mag = 2`, `bias = 1.5`: `E[cold] = −1`, `E[warm] = 1.5`, `E[variation] = (−1 + 1.5)/2 = 0.25°C/year`. This is *on top of* the explicit `ClimateTrendPerYear`. Over 50 years of simulation, an unsuspecting user gets `50 × 0.25 = 12.5°C` of warming from interannual variation alone, on top of whatever climate trend they set. Either this was intended (and needs documenting so users know), or it needs a correction term so `WarmingBias` skews the *distribution* without changing the *mean*.
  - Easy fix: subtract `mag × (bias − 1) / 4` from each year's variation to re-center the distribution on 0.

- **ISSUE** — **Seasonal phase comment is wrong.** Line 63 of `TemperatureCalculator.cs` says "Seasonal: sin wave, coldest at day 0, warmest at day 182". But the formula `sin(2π · day / 365)`:
  - day 0 → sin 0 = 0 (neutral, *not* coldest).
  - day 91 → sin(π/2) = +1 (warmest).
  - day 182 → sin(π) = 0 (neutral, *not* warmest).
  - day 273 → sin(3π/2) = −1 (coldest).
  Correct the comment; the formula itself is fine, just phase-shifted from what the comment claims.

- **CONCERN** — **Daily variation scaling.** `currentRandomness = BaseRandomness + RandomnessGrowthRate × year` means daily noise amplitude grows linearly over time. IPCC scenarios do predict increased variability under warming (plausible), but the simulator doesn't link this to anything — it's exogenous and additive regardless of what's happening in the ecosystem. Fine for a "climate change" scenario axis, but worth naming honestly.

- **CONCERN** — **Autocorrelation fixed at 0.7/0.3.** The red-noise mixing ratio is hard-coded. Typical environmental autocorrelation values range 0.3–0.9 depending on timescale and system. Users have no knob to tune this. Consider exposing.

- **INFO** — Temperature clamp at `[MinTemp, MaxTemp]`. Can create flat plateaus during long heat-wave years. Expected behaviour for a hard physical bound.

- **INFO** — `DAYS_PER_YEAR = 365` — no leap-year handling. Accumulates a ~1-day offset per 4 years but doesn't affect long-term trends.

---

## 13. Cross-cutting concerns

### 13.1 BiologyStep > 1 semantics

Most rate × BiologyStep scaling is accurate to first order when rates are small. The two places it hurts:

- **ISSUE** — **Condition drain/recovery with `BiologyStep = 5`** can overshoot the target (drain factor ≈ 2 means `Cond -= 2 × gap` drives Condition past `target`). Either switch to exact exponential update, clamp per-step change to `≤ gap`, or document that `BiologyStep = 1` is the only validated value.

### 13.2 Species processing order

- **ISSUE** — Already covered in §9: first-mover bias in Step 8 carrying-capacity. Also applies *implicitly* to the order RNG is consumed, but that's a deterministic artefact rather than a bias.

### 13.3 RNG usage

- **INFO** — `EcosystemSimulator._rng` draws for hunting variance (Step 2), natural death variance (Step 9). `TemperatureCalculator._rng` draws for interannual + daily variation. Separate streams — sensible.

- **CONCERN** — No seed isolation between species within a step. Two species' variance draws in Step 2 come from the same RNG stream in species order, so re-ordering species changes realised variance even if everything else is identical. Worth noting for reproducibility when users reorder species.

### 13.4 Crash detection

- **INFO** — `HasCrashed()` returns true only when total population (T1 + T2) is exactly 0. Simulator breaks out of the day loop. Partial crashes (only T1 or only T2 extinct) don't stop the sim; T2 naturally starves without prey.

### 13.5 Per-scenario seeding

- **INFO** — `SimulationRunner(seed)` is called per scenario with `BaseSeed + scenarioIndex`. Deterministic if `BaseSeed` is fixed. Fine.

---

## 14. Summary of concerns, ranked by severity

| # | Severity | Area | Concern | Suggested action |
|---|----------|------|---------|------------------|
| 1 | **ISSUE** | Predation (§3) | Pooled FedRate ignores per-predator hunting efficiency. | Use per-predator FedRate = `huntingSuccess × totalEaten/totalActualDemand`. |
| 2 | **ISSUE** | Temperature (§12) | `WarmingBias > 1` adds hidden warming trend to interannual variation. | Re-center the distribution; subtract `mag × (bias−1)/4` from each year. |
| 3 | **ISSUE** | Reproduction (§9) | First-mover bias in carrying-capacity cap (Brian Mail 8). | Snapshot `tierPop` at step start, or shuffle species each step. |
| 4 | **ISSUE** | Condition dynamics (§5) | Euler integration overshoots at `BiologyStep > 1`. | Use exact exponential update or clamp per-step change. |
| 5 | **ISSUE** | Temperature (§12) | Seasonal phase comment documents wrong extremes. | Fix the comment; formula is fine. |
| 6 | **CONCERN** | Reproduction (§9) | `NO_PREDATOR_PENALTY = 0.85` contradicts ecological intuition; may double-count with carrying cap. | Delete, or justify with a cited mechanism. |
| 7 | **CONCERN** | Condition death (§8) | Default `ReproThreshold = 0.25 < DeathThreshold = 0.3` allows simultaneous healthy reproduction and death. | Reverse defaults or validate `ReproThreshold > DeathThreshold`. |
| 8 | **CONCERN** | Condition dynamics (§5) | Recovery multiplier `(1+target²) × Pmax` can be < 1; the quadratic boost no longer guarantees at-least-base rate for low Pmax. | Clamp recovery multiplier ≥ 1, or document. |
| 9 | **CONCERN** | Condition dynamics (§5) | Recovery fires even at very low `target`; no "point of no return" below lethal. | Add a minimum target below which recovery is disabled; cite Gosselin 2021. |
| 10 | **CONCERN** | Condition death (§8) | Survivor fitness boost assumes dead were weakest; not applied to natural or predation death, so semantics are inconsistent across death types. | Apply consistently, or document why only condition death gets the boost. |
| 11 | **CONCERN** | Predation (§3) | No prey preference across variants; predation is proportional-to-population only. | If relevant, add a per-variant preference weight. |
| 12 | **CONCERN** | Predation (§3) | Prey `FedRate` always 1.0; prey have no resource-limitation axis beyond carrying cap on reproduction. | If studying bottom-up control, add a prey-food axis; else document as a scope choice. |
| 13 | **CONCERN** | Reproduction (§9) | `NEWBORN_CONDITION = 0.5` is arbitrary; not a function of parent Condition. | Make it a parameter, possibly defaulting to parent Condition × 0.5. |
| 14 | **CONCERN** | Reproduction (§9) | High per-capita reproduction rates compress timescales vs real marine taxa. | Document "simulated days" semantics; provide conversion in methods text. |
| 15 | **CONCERN** | Temperature (§12) | Autocorrelation fixed at 0.7/0.3; not configurable. | Expose as a parameter. |
| 16 | **MINOR** | Thermal performance (§2) | `LETHAL_TRANSITION_WIDTH = 2°C` is global, not per-species. | Make per-species if biology demands it. |
| 17 | **MINOR** | Temperature (§12) | `DAYS_PER_YEAR = 365`, no leap year. | Usually fine; document. |
| 18 | **INFO** | Many | No tests in the repo for biology invariants. | Consider adding property-based tests: e.g. Condition ∈ [0,1], Pop ≥ 0, fractional accumulators bounded. |

## 15. Things I checked and cleared

- Arrhenius normalisation at Topt → `perf == 1.0`. Correct.
- Pmax pathways after v9 — all four entry points (ThermalPerf for predator demand, drain rate, recovery rate, birth multiplier) present and consistent with the design doc.
- Accumulators — initialise, add fractional, floor to integer, keep residual. Correct pattern for all four.
- Crash detection — total-population check is appropriate given the extinction semantics.
- Deterministic replay — seeds propagate correctly; same seed + same config gives same result.
- CSV output columns match what `StepRecord` fields and the `#config:` / `#species:` sections emit.

---

## 16. Biggest asks

If you want a short list of *must-address* items before the MEE manuscript, I'd nominate:

1. **Pooled FedRate** (§3) — changes quantitative outcomes for any multi-predator run.
2. **Hidden warming trend from WarmingBias** (§12) — invalidates long climate-scenario runs if not corrected or documented.
3. **Processing-order bug** (§9) — already on your Monday-meeting list; re-flagging for completeness.
4. **Defaults `ReproThreshold < DeathThreshold`** (§8) — a validator check is cheap and avoids future confusion.

Everything else is either a scope decision (Brian + Tarik to arbitrate) or a documentation fix.
