# Biology and Formulas

This document specifies every biological formula in the TinySea headless ecosystem simulation, with each input named, its unit stated, and its source line cited. It walks one simulated day in the exact order `EcosystemSimulator.ProcessBiologyStep(float temperature)` executes it.

Scope: the current simulation runs Tier 1 (prey) only. `EcosystemSimulator.Tier2Enabled` defaults to `false` (EcosystemSimulator.cs:245) and Tier 2 (predator) species are dropped at load when the gate is off (EcosystemSimulator.cs:336). Tier 1 is documented in full. Tier 2 predation logic still exists from the original two tier design. Where this document meets Tier 2 it marks it as secondary legacy and states plainly that current runs consider Tier 1 only.

Related documents (do not duplicate, cross reference by filename):
- `temperature-model.md` for how the daily Celsius temperature `temperature` is produced.
- `simulation-spec.md` for the authoritative step contract and design invariants.
- `run-scenario-batch.md` for the day loop that calls `ProcessBiologyStep` and for `BiologyStep` gating.
- `data-structures.md` for `SimSpecies`, `StepRecord`, and the per-species dictionaries.
- `csv-output-formats.md` for how the values below are written to CSV.
- `configuration-reference.md` for `SimulationConfig` and the `.asset` parameter sources.

## 1. Units and shared conventions

All performance, condition, and fed-rate quantities are dimensionless in `[0, 1]`. Populations are stored as `float` for fractional precision during a day and rounded to integers at the end of the day (EcosystemSimulator.cs:660-686). Temperature is Celsius on input and converted to Kelvin (`+273.15`) only inside the thermal curve (SimSpecies.cs:116).

`BiologyStep` is a dimensionless integer multiplier applied to per-day rate terms (predator demand, condition deaths, births, natural deaths). It defaults to `1` (EcosystemSimulator.cs:140, SimulationConfig.cs:29) which means one biology evaluation equals one day. The day loop runs biology on display day 1 and on every day where `displayDay % BiologyStep == 0` (SimulationRunner.cs:430), so with `BiologyStep = N` one biology evaluation represents `N` elapsed days and each rate term is multiplied by `N` to scale the per-day rate to that interval. The valid inspector range is 1 to 5 (SimulationConfig.cs:28). The rest of this document assumes `BiologyStep = 1` unless stated otherwise; multiply the cited rate terms by `BiologyStep` for larger steps.

For `BiologyStep = N > 1` the first interval is not uniform: day 1 is forced, then biology runs on day N, 2N, and so on, so the gap from day 1 to day N is `N - 1` days while every later gap is `N` days. The code applies the same `BiologyStep = N` multiplier on day 1 as on every other biology day; it does not shrink the multiplier for the first, shorter interval. This first-interval mismatch is out of scope for this document, which specifies `BiologyStep = 1` where day 1 and the multiplier are both 1.

`MIN_ALIVE_POP = 1.0` (EcosystemSimulator.cs:248) is the floor below which a species is treated as not present for feeding, condition, thermal death, and condition death. Reproduction uses a separate floor `MIN_POPULATION_FOR_REPRODUCTION = 2.0` (EcosystemSimulator.cs:274).

### 1.1 Per-species parameters (`SimSpecies`)

These fields are copied from `SpeciesData` at load, field by field (EcosystemSimulator.cs:351-380), and read by the biology steps. Two default columns are given because they differ. The `SimSpecies` initializer is the value a `SimSpecies` object has if nothing overrides it (the C# field initializer); a field declared with no initializer defaults to `0`. The `SpeciesData` load default is the value actually copied in at load when the data source does not set the field, taken from the `SpeciesData` field initializers in `SpeciesDatabase.cs`. The canonical Tier 1 `.asset` family overrides several of these per variant (see `configuration-reference.md`); where it does, the per-variant value is noted in the Meaning column.

| Field | Unit | SimSpecies initializer | SpeciesData load default | Meaning and source |
|---|---|---|---|---|
| `Population` | individuals | 0 | set from `count` | Current population, float during a day (SimSpecies.cs:22; loaded at EcosystemSimulator.cs:357) |
| `Tier` | enum 1/2 | 0 | `tier + 1` | 1 = prey, 2 = predator (1-based; database `tier` is 0-based, converted `+1` at EcosystemSimulator.cs:356) (SimSpecies.cs:19) |
| `Pmax` | dimensionless [0,1] | 1.0 (SimSpecies.cs:68) | 0.65 (SpeciesDatabase.cs:105) | Peak thermal height; scales thermal performance, condition rates, and births. Canonical Tier 1 family sets it per variant: Cold 0.9843, Warm 0.972, Hot 0.96 (SimSpecies.cs:175, 184, 195). The 1.0 initializer is never the value used at load |
| `EatingAmount` | resource points per individual per day | 0 | 0 (SpeciesDatabase.cs:52, no initializer) | Per-individual consumption, used by both tiers. Tier 2: prey eaten per predator per step. Tier 1: resource points each individual draws from the shared food pool, floored at 1, so a higher appetite feeds fewer individuals (EcosystemSimulator.cs:776). At appetite 1 it equals the head count and existing runs are unchanged (SimSpecies.cs:25) |
| `ReproductionMultiplier` | dimensionless | 0 | 0 (SpeciesDatabase.cs:53, no initializer) | Birth-rate multiplier; canonical Tier 1 family sets 0.45 (SimSpecies.cs:149) (SimSpecies.cs:26) |
| `DeathThreshold` | dimensionless [0,1] | 0 | 0.3 (SpeciesDatabase.cs:54) | Condition below this triggers condition death. The 0.3 is the `SpeciesData` initializer, applied at load, not the `SimSpecies` field initializer (SimSpecies.cs:27) |
| `DeathRate` | fraction per day | 0 | 0 (SpeciesDatabase.cs:55, no initializer) | Max fraction killed per day at Condition 0; canonical Tier 1 family sets 0.6 (SimSpecies.cs:151) (SimSpecies.cs:28) |
| `ReproThreshold` | dimensionless [0,1] | 0 | 0.25 (SpeciesDatabase.cs:57) | Condition inflection between healthy and struggling reproduction. The 0.25 is the `SpeciesData` initializer, applied at load, not the `SimSpecies` field initializer (SimSpecies.cs:29) |
| `ConditionDrainRate` | per day | -1 (SimSpecies.cs:36) | 0.15 (SpeciesDatabase.cs:63) | Condition drain rate; negative means inherit the global rate. The `SimSpecies` -1 sentinel inherits the global; the `SpeciesData` source defaults to an explicit 0.15 so the inspector never shows -1 (SpeciesDatabase.cs:60-63) |
| `ConditionRecoveryRate` | per day | -1 (SimSpecies.cs:37) | 0.10 (SpeciesDatabase.cs:64) | Condition recovery rate; negative means inherit the global rate |
| `NaturalDeathRate` | fraction per day | 0.02 (SimSpecies.cs:40) | 0.02 (SpeciesDatabase.cs:71) | Flat background mortality |
| `NaturalDeathVariance` | fraction | 0.01 (SimSpecies.cs:41) | 0.01 (SpeciesDatabase.cs:73) | +/- range added to the natural death rate |
| `HuntingEfficiency` | dimensionless [0,1] | 0.75 (SimSpecies.cs:52) | 0.75 (SpeciesDatabase.cs:78) | Base foraging success at the normal availability ratio; drives the shared Holling II curve (`ComputeForagingSuccess`) for every tier. Tier 1 searches the resource pool, Tier 2 hunts prey. At efficiency >= 1 the curve returns 1; the canonical Tier 1 family sets 1.0 (SimSpecies.cs:155, EcosystemSimulator.cs:971) |
| `HuntingVariance` | dimensionless | 0.15 (SimSpecies.cs:53) | 0.15 (SpeciesDatabase.cs:80) | +/- range on foraging success; applies to all tiers. 0 is deterministic and draws no RNG (EcosystemSimulator.cs:934) |
| `OptimalTempK` | Kelvin | 0 | 297.0 (SpeciesDatabase.cs:95) | Thermal optimum; canonical Tier 1 family sets it per variant: Cold 293.15, Warm 295.15, Hot 297.15 (SimSpecies.cs:170, 181, 190) (SimSpecies.cs:60) |
| `ArrhenBreadth` | Kelvin | 0 | 8000.0 (SpeciesDatabase.cs:96) | Arrhenius breadth `B`; canonical Tier 1 family sets 5000 (SimSpecies.cs:160) (SimSpecies.cs:61) |
| `ArrhenLower` | Kelvin | 0 | 3000.0 (SpeciesDatabase.cs:97) | Low-side deactivation constant `L` (SimSpecies.cs:62) |
| `ArrhenUpper` | Kelvin | 0 | 35000.0 (SpeciesDatabase.cs:98) | High-side deactivation constant `U` (SimSpecies.cs:63) |
| `LowerBoundK` | Kelvin | 0 | 296.0 (SpeciesDatabase.cs:99) | Low-side reference temperature `LB` (SimSpecies.cs:64) |
| `UpperBoundK` | Kelvin | 0 | 298.0 (SpeciesDatabase.cs:100) | High-side reference temperature `UB` (SimSpecies.cs:65) |
| `CTminC` | Celsius | -5.0 (SimSpecies.cs:69) | 0.0 (SpeciesDatabase.cs:107) | Critical thermal minimum; performance is 0 at or below |
| `CTmaxC` | Celsius | 40.0 (SimSpecies.cs:70) | 40.0 (SpeciesDatabase.cs:109) | Critical thermal maximum; performance is 0 at or above |
| `TemperatureDebuff` | Celsius | 0 (SimSpecies.cs:71) | 0.0 (SpeciesDatabase.cs:56) | Per-species offset added to the experienced temperature |
| `TempMultiplier` | dimensionless | 1.0 (SimSpecies.cs:72) | 1.0 (SpeciesDatabase.cs:58) | Per-species scale on the deviation from the run base temperature, applied in Step 1 before the thermal curve (Section 2, Step 1). 1.0 = no change (bit-identical), below 1 dampens the swing (thermal inertia), above 1 amplifies it |
| `InitialCondition` | dimensionless [0,1] | 1.0 (SimSpecies.cs:82) | 1.0 (SpeciesDatabase.cs:69) | Day-0 Condition seed, copied into `Condition` at scenario start (EcosystemSimulator.cs:384-385). 1.0 = fully charged. Range 0..1 |

### 1.2 Per-species runtime values (recomputed each day)

| Field | Unit | Meaning and source |
|---|---|---|
| `RawThermalPerformance` | [0,1] | Arrhenius output with the lethal fade, without Pmax (SimSpecies.cs:74) |
| `ThermalPerformance` | [0,1] | `RawThermalPerformance * Pmax` (SimSpecies.cs:75) |
| `FedRate` | [0,1] | Feeding satisfaction; both tiers use Holling II foraging (`ComputeForagingSuccess`) capped by a per-tier supply term, Tier 1 by `foodDensity` and Tier 2 by the scarcity factor (SimSpecies.cs:77) |
| `RawFinalPerformance` | [0,1] | `RawThermalPerformance * FedRate`; the condition drain target (SimSpecies.cs:77) |
| `FinalPerformance` | [0,1] | `ThermalPerformance * FedRate`; logging/CSV only, no later step reads it (SimSpecies.cs:78) |
| `CurrentHuntingSuccess` | [0,1] | This day's hunting success; 1.0 for Tier 1 (SimSpecies.cs:79) |
| `Condition` | [0,1] | Per-species health; seeded at scenario start from `InitialCondition` (default 1.0), then persists across days (SimSpecies.cs:80-82, EcosystemSimulator.cs:384-385) |

`FullName` is the identity key for all per-species dictionaries and CSV columns. It is `Name` when `VariantLabel` is empty, otherwise `"{Name}_{VariantLabel}"` (SimSpecies.cs:89).

### 1.3 Global condition rates and carrying capacity

| Symbol | Unit | Default | Source |
|---|---|---|---|
| `ConditionDrainRate` (global) | per day | 0.15 | EcosystemSimulator.cs:240 |
| `ConditionRecoveryRate` (global) | per day | 0.10 | EcosystemSimulator.cs:241 |
| `CarryingCapacityPerTier` | individuals | 5000 | EcosystemSimulator.cs:237 |

`CarryingCapacityPerTier` is the Tier 1 shared resource pool ceiling. It is always on as of v11.1. The `UseCarryingCapacity` toggle was removed; the in-code rationale is that Tier 1 species without a resource ceiling grow without bound, which is biologically meaningless and triggered integer overflow in birth accumulators around day 50 of any cap-off scenario (EcosystemSimulator.cs:121-129, 233-237). There is no off-mode. The value floors at 1 inside the food-density calculation to guard against misconfiguration (EcosystemSimulator.cs:760).

## 2. Day step order

`ProcessBiologyStep(float temperature)` (EcosystemSimulator.cs:543) runs the following at the given Celsius temperature.

### 2.0 Top of step: snapshot and reset (EcosystemSimulator.cs:545-587)

1. Record start populations: `StartPopT1 = GetTier1Population()`, `StartPopT2 = GetTier2Population()` (EcosystemSimulator.cs:546-547). `GetTierPopulation(tier)` sums `Population` over species of that tier (EcosystemSimulator.cs:1382-1384).
2. Reset all tier-level `Last*` counters to 0 (eaten, thermal deaths, condition deaths, natural deaths, births, repro scales) and `LastFedRateT2 = 1`, `LastAvgHuntingEfficiency = 1` (EcosystemSimulator.cs:550-562).
3. Clear all per-species dictionaries (`LastBirthsBySpecies`, `LastTempDeathsBySpecies`, `LastConditionDeathsBySpecies`, `LastNaturalDeathsBySpecies`, `LastEatenBySpecies`, `LastReproScaleBySpecies`, `LastFedRateBySpecies`, `StartPopBySpecies`) then re-seed exactly one entry per species so later `+=` operations always find an existing key (EcosystemSimulator.cs:566-584). The seed values are: the five event dictionaries (`LastBirthsBySpecies`, `LastTempDeathsBySpecies`, `LastConditionDeathsBySpecies`, `LastNaturalDeathsBySpecies`, `LastEatenBySpecies`) seeded to `0L`; `LastReproScaleBySpecies` and `LastFedRateBySpecies` seeded to `0f`; `StartPopBySpecies[FullName] = SafePopToLong(Population)` (EcosystemSimulator.cs:576-583). `SafePopToLong` rounds the float and maps non-finite values to 0 (EcosystemSimulator.cs:296-297).

The accumulator residual dictionaries (`_birthAccumulators`, `_naturalDeathAccumulators`, `_predationAccumulators`, `_conditionDeathAccumulators`) are NOT cleared here. They persist across days and carry fractional residue forward (see Section 4).

The per-species dictionary value types are fixed: `LastBirthsBySpecies`, `LastTempDeathsBySpecies`, `LastConditionDeathsBySpecies`, `LastNaturalDeathsBySpecies`, `LastEatenBySpecies`, and `StartPopBySpecies` are `Dictionary<string, long>` (whole event counts and start population); `LastReproScaleBySpecies` and `LastFedRateBySpecies` are `Dictionary<string, float>` (rates in `[0, 1]`) (EcosystemSimulator.cs:213-220). The tier-rollup invariant in Section 11 holds only because the per-species event counts and their matching tier counters are both whole counts. See `data-structures.md` for the same types.

For a Tier-1-only run (no Tier 2 species, the default per Section 5.2) the Tier 2 fed-rate fields never get a normal-path value. `LastFedRateT2` and `LastAvgHuntingEfficiency` are both reset to 1 here, then the Tier 2 block in Step 2 takes its early-exit path because `predators.Count == 0`, which sets `LastAvgHuntingEfficiency = 1` and `LastFedRateT2 = 1` (the `predators.Count > 0 ? 0f : 1f` branch resolves to 1 with no predators) and returns (EcosystemSimulator.cs:783-790). So both fields end the day at 1, set by the early-exit path, not by any predation arithmetic.

The remaining steps iterate the `Species` list in list order.

### Step 1: Thermal performance (EcosystemSimulator.cs:589-598)

For each species:

```
dampedTemp            = temperature + (temperature - BaseTemperatureC) * (TempMultiplier - 1)
RawThermalPerformance = CalculatePerformance(dampedTemp)     // SimSpecies, [0,1], no Pmax
ThermalPerformance    = RawThermalPerformance * Pmax         // [0,1]
FedRate               = 1                                     // reset; set per tier in Step 2
CurrentHuntingSuccess = 1                                     // reset; set per predator in Step 2
```

`dampedTemp` applies the per-species temperature multiplier before the thermal curve (EcosystemSimulator.cs:607). `TempMultiplier` (default 1.0, SimSpecies.cs:72) scales the experienced deviation from the run base temperature `BaseTemperatureC`. The multiplier rewrites the deviation as `(temperature - BaseTemperatureC) * TempMultiplier`: at 1.0 it is bit-identical to passing `temperature` straight through (the added term is 0), below 1 it dampens the swing toward the base (thermal inertia), above 1 it amplifies it. `BaseTemperatureC` is the run base temperature, set once per run from `TempCalc.BaseTemperature` before the day loop (SimulationRunner.cs:421), default 20 (EcosystemSimulator.cs:245). The per-species `TemperatureDebuff` (Section 3.1) is still added separately inside `CalculatePerformance`, so the two offsets compose: `CalculatePerformance` receives `dampedTemp` and then internally adds `TemperatureDebuff`. Pmax is applied here, outside `CalculatePerformance`, not inside it (EcosystemSimulator.cs:609). `CalculatePerformance` returns the raw curve and the lethal fade only.

### Step 2: Feeding and predation (`ProcessFeedingWithAccumulator`, EcosystemSimulator.cs:728)

Tier 1 fed rate is computed first (EcosystemSimulator.cs:765-804), then the Tier 2 predator block (legacy). See Section 5 for the full derivation.

### Step 3: Raw final performance (EcosystemSimulator.cs:604-610)

For each species:

```
RawFinalPerformance = RawThermalPerformance * FedRate
```

This is the condition drain target in Step 4. It uses the raw (no-Pmax) thermal performance, so Pmax stays out of the condition target and enters only through the condition rates (Section 6).

### Step 4: Update condition (`UpdateCondition`, EcosystemSimulator.cs:952)

Condition drains toward or recovers toward `RawFinalPerformance`. See Section 6.

### Step 5: Final performance (EcosystemSimulator.cs:619-630)

For each species:

```
FinalPerformance = ThermalPerformance * FedRate
```

This value is written to CSV and the verbose log only. No later step reads it (EcosystemSimulator.cs:620-624). Reproduction uses Condition, not `FinalPerformance`.

### Step 6: Thermal death (`ApplyThermalDeath`, EcosystemSimulator.cs:1000)

Instant whole-population kill when at or beyond a lethal limit. See Section 7.1.

### Step 7: Condition death (`ApplyConditionDeath`, EcosystemSimulator.cs:1056)

Graduated mortality when `Condition < DeathThreshold`. See Section 7.2.

### Step 8: Reproduction (`ApplyReproduction`, EcosystemSimulator.cs:1150)

Condition-driven births scaled by Pmax, with a birth accumulator. See Section 8.

### Step 9: Natural death (`ApplyNaturalDeathWithAccumulator`, EcosystemSimulator.cs:1270)

Flat rate plus variance with a natural-death accumulator. See Section 7.3.

### Step 10: Population rounding and overflow guard (EcosystemSimulator.cs:660-686)

For each species:

```
popCap = 100 * CarryingCapacityPerTier                       // MAX_POP_MULTIPLE_OF_K = 100
if Population > popCap: Population = popCap                   // defensive overshoot guard
Population = Math.Round(Population, MidpointRounding.AwayFromZero)
```

The cap at `100 * CarryingCapacityPerTier` (EcosystemSimulator.cs:667-668) is applied per species inside the Step 10 loop (`for each species`, EcosystemSimulator.cs:671-679), so each species is individually capped at `100 * CarryingCapacityPerTier` regardless of the tier total. This is a numeric overflow bound, not the ecological resource pool: the shared per-tier food-density pool of Section 5.1 saturates at a tier-wide total near `CarryingCapacityPerTier`, while this Step 10 cap lets a single species reach up to `100 * CarryingCapacityPerTier` before being clamped. It is a defensive guard against runaway overshoot when condition feedback is too slow to throttle reproduction, which would otherwise let `Population` exceed `long.MaxValue` and produce sentinel values in CSV (EcosystemSimulator.cs:661-666). `MidpointRounding.AwayFromZero` rounds 0.5 up to 1 (EcosystemSimulator.cs:681).

### 2.1 End of step (EcosystemSimulator.cs:688-699)

1. `ComputeAverageCondition()` computes population-weighted mean Condition per tier into `AvgConditionT1` and `AvgConditionT2`; tiers with no living population get 0 (EcosystemSimulator.cs:1324-1338).
2. `EndPopT1 = GetTier1Population()`, `EndPopT2 = GetTier2Population()` (EcosystemSimulator.cs:692-693).
3. `UpdateAccumulatorTotals()` sums the per-species accumulator residuals into tier-level `*AccumT1/T2` fields for CSV (EcosystemSimulator.cs:1343-1378).

## 3. Thermal performance (`SimSpecies.CalculatePerformance`, SimSpecies.cs:95-133)

Input: `temperatureCelsius` in Celsius. Output: `RawThermalPerformance` in `[0, 1]`, before Pmax.

The two halves of this method work in different units. The lethal-limit fade in 3.2 operates entirely in Celsius: it compares the debuff-adjusted `temperatureCelsius` against `CTminC` and `CTmaxC`, which are Celsius fields (SimSpecies.cs:69-70). The Arrhenius core in 3.3 operates entirely in Kelvin: it adds `+273.15` to that same Celsius value once (SimSpecies.cs:116) and uses the already-Kelvin curve fields (`OptimalTempK`, `LowerBoundK`, `UpperBoundK`) without further conversion. Only the Arrhenius input gets `+273.15`; the `CTminC`/`CTmaxC` comparisons and the Kelvin curve fields are never re-offset.

### 3.1 Temperature debuff

```
temperatureCelsius += TemperatureDebuff      // Celsius offset, default 0 (SimSpecies.cs:97)
```

### 3.2 Lethal limit cosine fade (SimSpecies.cs:99-114)

A smooth cosine taper multiplies the curve to 0 across a transition band near each lethal limit. The comparisons below all use the debuff-adjusted `temperatureCelsius` (after Section 3.1 adds `TemperatureDebuff`), not the raw input. The comparison operators are exactly as written: the lethal-edge tests are inclusive (`<=` on the low side, `>=` on the high side) and the cosine-band tests are strict (`<` and `>`). Equality matters: at `temperatureCelsius == CTminC` or `== CTmaxC` the inclusive branch sets `fadeFactor = 0`, the method returns 0, and Step 6 thermal death then kills the whole population (Section 7.1). The band width is the smaller of `LETHAL_TRANSITION_WIDTH = 2.0` Celsius (SimSpecies.cs:57) and half the lethal span:

```
halfRange = (CTmaxC - CTminC) / 2
tw        = min(LETHAL_TRANSITION_WIDTH, halfRange)     // transition width, Celsius
fadeFactor = 1
```

Low side (SimSpecies.cs:104-107):

```
if temperatureCelsius <= CTminC:
    fadeFactor = 0
else if temperatureCelsius < CTminC + tw:
    fadeFactor = 0.5 * (1 + cos(PI * (CTminC + tw - temperatureCelsius) / tw))
```

At `CTminC` the cosine argument is `PI`, so `fadeFactor = 0`. At `CTminC + tw` the argument is `0`, so `fadeFactor = 1`. Between, the factor rises smoothly from 0 to 1.

High side (SimSpecies.cs:109-112), multiplied onto the existing factor:

```
if temperatureCelsius >= CTmaxC:
    fadeFactor = 0
else if temperatureCelsius > CTmaxC - tw:
    fadeFactor *= 0.5 * (1 + cos(PI * (temperatureCelsius - (CTmaxC - tw)) / tw))
```

At `CTmaxC - tw` the argument is `0`, so the multiplier is `1`. At `CTmaxC` the argument is `PI`, so the multiplier is `0`.

```
if fadeFactor <= 0: return 0                  // skip the Arrhenius evaluation (SimSpecies.cs:114)
```

When `RawThermalPerformance` returns exactly 0 here, Step 6 thermal death triggers a whole-population kill (Section 7.1).

### 3.3 Arrhenius evaluation (SimSpecies.cs:116-132)

Convert to Kelvin and bind the curve parameters:

```
T  = temperatureCelsius + 273.15      // Kelvin
OT = OptimalTempK                     // Kelvin
B  = ArrhenBreadth                    // Kelvin
L  = ArrhenLower                      // Kelvin
U  = ArrhenUpper                      // Kelvin
LB = LowerBoundK                      // Kelvin
UB = UpperBoundK                      // Kelvin
```

`B`, `L`, and `U` are constants in units of Kelvin. They are deactivation-energy constants in the `E/k` form (energy divided by the Boltzmann constant), so each ratio such as `B/OT`, `L/T`, or `U/UB` is Kelvin divided by Kelvin and therefore dimensionless, which is what `exp` requires. They are used directly as written in the `exp` arguments. No `abs()`, no sign flip, and no other transform is applied to `B`, `L`, `U`, `T`, `OT`, `LB`, or `UB` (SimSpecies.cs:116-127); the only operators on them are binary `/` and binary `-`.

The performance is a ratio of an activation term over a temperature-dependent deactivation denominator, normalized so the numerator's deactivation factor is evaluated at the optimum (SimSpecies.cs:125-127):

```
numerator   = exp((B/OT) - (B/T)) * (1 + exp((L/OT) - (L/LB)) + exp((U/UB) - (U/OT)))
denominator = 1 + exp((L/T) - (L/LB)) + exp((U/UB) - (U/T))
perf        = numerator / denominator
```

Operator precedence is standard C#: `/` binds tighter than binary `-`, so `B / OT - B / T` evaluates as `(B/OT) - (B/T)`, and likewise for every other `X/Y - X/Z` group above. The parentheses in the block are explicit only to make that grouping unmistakable; they match the code's evaluation order exactly.

`exp` is `Math.Exp` over `double`, so the whole ratio is computed in `double` (SimSpecies.cs:125-129). There is no special handling for extreme arguments inside the formula. The only safeguard is the final clamp `Math.Max(0.0, Math.Min(1.0, perf))` (SimSpecies.cs:132). If an `exp` argument overflows, `Math.Exp` returns `+Infinity`, and a finite numerator over `+Infinity` denominator gives `perf = 0`, while a `+Infinity` numerator over a finite denominator gives `perf = +Infinity` which the clamp maps to 1. The code applies no guard against `T` near 0 K, `OT == LB`, division producing `Infinity / Infinity`, or other degenerate parameter sets. A `NaN` from such a case is not converted: .NET's `Math.Min` and `Math.Max` propagate `NaN`, so the clamp would return `NaN` and the method would return `NaN * fadeFactor`. These degenerate inputs are assumed to be ruled out by valid Kelvin parameters (`T` is always near 273 to 320 K in practice, and the canonical curve constants are far from the degenerate points). The `exp((B/OT) - (B/T))` factor is the Arrhenius rise with temperature. The `L` terms suppress performance below the optimum; the `U` terms suppress it above.

At the optimum `T == OT`, every `exp` difference in the numerator equals its counterpart so the numerator's bracket equals the denominator. Substituting `T = OT`: the rise factor `exp((B/OT) - (B/OT)) = exp(0) = 1`, the numerator bracket becomes `1 + exp((L/OT) - (L/LB)) + exp((U/UB) - (U/OT))`, and the denominator becomes the identical `1 + exp((L/OT) - (L/LB)) + exp((U/UB) - (U/OT))`, so `perf = 1.0` exactly. The unfaded peak height is therefore exactly 1.0 at `T == OT`, and the `clamp(perf, 0, 1)` is a no-op at the optimum. Away from `OT` the ratio is below 1.

Final clamp and fade (SimSpecies.cs:132):

```
return clamp(perf, 0, 1) * fadeFactor
```

Pmax is not applied in this method (SimSpecies.cs:131). Step 1 applies it to produce `ThermalPerformance`.

## 4. Fractional event accumulators

Predation, condition death, reproduction, and natural death each compute a fractional raw count per day and route it through a per-species accumulator keyed by `FullName`. The accumulators are `Dictionary<string, float>` declared at EcosystemSimulator.cs:150-154 and never cleared between days; they only clear on a fresh load (EcosystemSimulator.cs:480-487).

The pattern, identical in all four cases:

```
accumulator[FullName] += rawCount               // float, this day's fractional events
accumulated = accumulator[FullName]
wholeEvents = (long)Math.Floor(accumulated)     // long avoids int32 overflow at large pops
accumulator[FullName] = accumulated - wholeEvents   // keep the fractional residue
wholeEvents = min(wholeEvents, (long)Population) // never remove more than exist (deaths only)
```

Floor takes the integer part; the residue under 1.0 carries to the next day, so fractional births and deaths accumulate until they cross an integer boundary. The `long` cast prevents int32 overflow at large populations (EcosystemSimulator.cs:883, 1069, 1234, 1295). Births do not clamp to population (they add), but all three death pathways clamp the whole count to current `Population` before applying.

The residue is subtracted in the line above the clamp, so the clamp acts on the already-debited count. When `wholeEvents` is clamped down to `(long)Population` (the species would otherwise lose more than it has), the clamped-off excess is discarded permanently. It is NOT returned to the accumulator, because the fractional residue was already written back at the `accumulator[FullName] = accumulated - wholeEvents` step before the clamp. A reimplementation must drop the clamped excess, not re-credit it to the accumulator, or it will diverge near population limits.

`_thermalDeathAccumulators` (EcosystemSimulator.cs:153) is maintained but never read in v11.1. It is cleared on a fresh load (`ClearAccumulators`, EcosystemSimulator.cs:485) and zero-initialized per species (`InitializeAccumulators`, EcosystemSimulator.cs:494), but the thermal-death path (`ApplyThermalDeath`, Section 7.1) is a whole-population kill that uses no accumulator, so nothing ever reads it, and there is no public accessor (the accessor comment at EcosystemSimulator.cs:222-223 notes this).

Accessors expose the residuals for CSV: `GetBirthAccum`, `GetNaturalDeathAccum`, `GetConditionDeathAccum`, `GetPredationAccum` (EcosystemSimulator.cs:224-231).

How fractional counts become integer population: each step applies only `wholeEvents` (a `long`) to `Population`. Every within-day mutation of `Population` is therefore an integer amount: the three death pathways subtract whole counts (EcosystemSimulator.cs:889, 1077, 1304) and reproduction adds whole counts (EcosystemSimulator.cs:1238). Because `Population` starts the day integer-valued (Step 10 rounded it the previous day) and only integers are added or subtracted, it stays integer-valued all day. Step 10's `Math.Round(Population, AwayFromZero)` (EcosystemSimulator.cs:681) therefore runs unconditionally as a defensive guard and is a no-op in normal operation; it is not tied to the cap, which assigns `Population = 100 * CarryingCapacityPerTier`, an integer-valued float that introduces no residue either.

## 5. Feeding and predation (`ProcessFeedingWithAccumulator`, EcosystemSimulator.cs:728)

The method partitions living species into predators (Tier 2, `Population >= MIN_ALIVE_POP`) and prey (Tier 1, `Population >= MIN_ALIVE_POP`) (EcosystemSimulator.cs:730-731), then computes Tier 1 fed rate, then the Tier 2 predator block.

### 5.1 Tier 1 fed rate (shipping mode, EcosystemSimulator.cs:765-804)

Tier 1 uses the same foraging mechanism as Tier 2: a Holling Type II success on resource availability plus an optional random variance, via the shared helper `ComputeForagingSuccess(species, availabilityRatio)` (EcosystemSimulator.cs:931-940). It is not the old linear `HuntingEfficiency * foodDensity` model. The difference from Tier 2 is only the availability ratio passed in (the resource pool instead of a prey:predator ratio) and the supply cap the caller applies afterward. Two pool-level scalars are computed once, then per-species foraging runs.

Compute the supply cap and the search ratio once from the Tier 1 population and consumption:

```
tier1Pop         = max(0, GetTierPopulation(1))                 // individuals, clamped non-negative
capSafe          = max(CarryingCapacityPerTier, 1)              // floor 1 against misconfig
tier1Consumption = sum over living Tier 1 of Population * max(1, EatingAmount)   // appetite floored at 1
foodDensity      = max(0, 1 - tier1Consumption / capSafe)       // [0,1] supply cap; 1 when empty, 0 at/over capacity
LastFoodDensityT1 = foodDensity
resourceRatio    = capSafe / max(tier1Pop, 1)                   // land-pool analog of Tier 2's prey:predator ratio
```

Sources: EcosystemSimulator.cs:765-782. `tier1Pop` is clamped non-negative so a corrupted negative population cannot inflate `foodDensity` (EcosystemSimulator.cs:763-765). `foodDensity` is the supply cap, the Tier 1 analog of Tier 2's scarcity factor; it now falls with total consumption `Population * EatingAmount`, not with head count, so appetite tightens the pool: with `EatingAmount = 1` it equals the old `1 - tier1Pop / capSafe` and existing runs are unchanged, while at appetite 3 a 5000 pool supports about 1667 individuals instead of 5000 (EcosystemSimulator.cs:767-777). `EatingAmount` is floored at 1 per individual, so a 0 or unset appetite behaves like the old head-count model and the carrying-capacity limit can never switch off (EcosystemSimulator.cs:776). `resourceRatio` is a head count, the land-pool analog of Tier 2's prey:predator ratio; it drives the Holling search efficiency. Appetite does not enter `resourceRatio`, it enters only through `foodDensity`, mirroring how Tier 2 keeps `EatingAmount` out of its `preyRatio` (EcosystemSimulator.cs:779-782). The cap is always on (Section 1.3); there is no branch that forces `foodDensity = 1`. Both divisions are floating-point, so the ratios are exact fractions, not truncated.

Then per living Tier 1 species (`Species.Where(s => s.Tier == 1)` with `Population >= MIN_ALIVE_POP`, EcosystemSimulator.cs:785-802):

```
gatherSuccess = ComputeForagingSuccess(sp, resourceRatio)   // Holling II + variance, [0,1]
FedRate       = min(1, gatherSuccess * foodDensity)         // Holling success capped by supply
```

`ComputeForagingSuccess(sp, ratio)` is shared by Tier 1 and Tier 2 (EcosystemSimulator.cs:931-940): `eff = CalculateHollingEfficiency(HuntingEfficiency, ratio)`, which returns 1 when `HuntingEfficiency >= 1` (EcosystemSimulator.cs:967-975); then if `HuntingVariance > 0`, `eff = clamp(eff + (rng*2-1)*HuntingVariance, MIN_HUNTING_SUCCESS = 0, MAX_HUNTING_SUCCESS = 1)` (EcosystemSimulator.cs:934-938, constants at 270-271). `HuntingVariance == 0` is deterministic and draws no RNG, so it does not perturb the shared `_rng` draw order. At `HuntingEfficiency = 1`, `HuntingVariance = 0`, `EatingAmount = 1` the success is 1 and `FedRate = min(1, 1 * foodDensity) = foodDensity`, identical to the old linear model; the new behavior appears only when efficiency < 1, variance > 0, or appetite != 1.

Species below `MIN_ALIVE_POP` get `FedRate = 0` and are skipped by the weighted-mean sums. Each species' fed rate is recorded into `LastFedRateBySpecies[FullName]`, including dead species at 0 (EcosystemSimulator.cs:801). The population-weighted mean `LastFedRateT1` sums only over living Tier 1 species, that is species with `Population >= MIN_ALIVE_POP`, using their just-computed `FedRate` and their `Population`; dead Tier 1 species (and their zero `FedRate`) are excluded from both numerator and denominator (EcosystemSimulator.cs:787-794, 803). It defaults to 1 when no Tier 1 individuals are alive:

```
// i ranges over Tier 1 species with Population >= MIN_ALIVE_POP only
LastFedRateT1 = sum(FedRate_i * Population_i) / sum(Population_i)   // 1 if denominator is 0
```

The canonical Tier 1 family sets `HuntingEfficiency = 1.0` and `HuntingVariance = 0` (SimSpecies.cs:155-156), so `gatherSuccess = 1` and `FedRate` equals `foodDensity` for default prey. This fed rate flows into Step 3 `RawFinalPerformance` and therefore into the Step 4 condition target, which is how Tier 1 reproduction throttles indirectly: high population lowers food density, which lowers fed rate, which lowers the condition target, which shrinks reproduction and fires condition deaths (EcosystemSimulator.cs:1132-1138).

### 5.2 Tier 2 predation (secondary legacy)

Current runs are Tier 1 only, so this block does nothing when no predators are present. When predators and prey both exist it runs as follows.

The `predators` and `prey` sets used throughout this section are the living-species lists built at the top of the method: `predators = Species.Where(s => s.Tier == 2 && s.Population >= MIN_ALIVE_POP)` and `prey = Species.Where(s => s.Tier == 1 && s.Population >= MIN_ALIVE_POP)` (EcosystemSimulator.cs:730-731). Both apply the `MIN_ALIVE_POP` floor, so a Tier 1 species below 1.0 individual is not part of `prey` and contributes to none of the predation arithmetic below.

`availablePrey = prey.Sum(p => p.Population)` is computed once after the early exit (EcosystemSimulator.cs:792) and reused unchanged for the prey ratio, the eaten cap, and the per-prey share. It is the sum of `Population` over the filtered `prey` set (Tier 1, `Population >= MIN_ALIVE_POP`), not a fresh sum of all Tier 1 species, and it is never re-summed inside the removal loop.

Random variance (hunting variance below and natural-death variance in Section 7.3) is drawn from `_rng`, a single `System.Random` instance held by the simulator. It is created in the constructor and replaced by `SetSeed(int)`: `_rng = seed < 0 ? new System.Random() : new System.Random(seed)` (EcosystemSimulator.cs:299-303, 308-311). A non-negative seed makes a scenario reproducible; a negative seed uses the time-based default. `_rng.NextDouble()` returns a `double` in `[0, 1)`. The same `_rng` instance is shared by every consumer, so draws are sequential in the exact order the code calls them: within one biology day, the Tier 2 hunting-variance loop draws first, once per predator in `Species` list order (EcosystemSimulator.cs:809), then Step 9 natural death draws once per species in `Species` list order (EcosystemSimulator.cs:1278). A reimplementation must use one shared RNG and reproduce this `Species`-list draw order, or per-species draws will differ even with the same seed.

Early exit: if there are no predators or no prey, all living predator fed rates are set to 0 (the loop iterates the `predators` list, EcosystemSimulator.cs:785), `LastFedRateT2` is 0 when predators exist else 1, `LastAvgHuntingEfficiency = 1`, and the method returns (EcosystemSimulator.cs:783-790).

Prey-to-predator ratio (EcosystemSimulator.cs:792-796):

```
availablePrey  = sum of prey Population          // prey = living Tier 1 (Population >= MIN_ALIVE_POP), summed once
totalPredators = sum of predators Population     // predators = living Tier 2 (Population >= MIN_ALIVE_POP)
preyRatio      = totalPredators > 0 ? availablePrey / totalPredators : 0
```

Holling Type II hunting success per predator (`CalculateHollingEfficiency`, EcosystemSimulator.cs:924-932):

```
if preyRatio <= 0 or baseEfficiency <= 0: hollingEff = 0
else if baseEfficiency >= 1:              hollingEff = 1
else:
    halfSaturation = NORMAL_PREY_RATIO * (1 - baseEfficiency) / baseEfficiency
    hollingEff     = preyRatio / (preyRatio + halfSaturation)
```

`NORMAL_PREY_RATIO = 20` (EcosystemSimulator.cs:269) is the prey:predator ratio at which `hollingEff` equals the species' base `HuntingEfficiency`. The half-saturation is derived from the species' own base efficiency, so the curve passes through `(20, baseEfficiency)`.

Per predator, the success gets random variance and clamps to `[MIN_HUNTING_SUCCESS, MAX_HUNTING_SUCCESS] = [0, 1]` (EcosystemSimulator.cs:270-271, 805-811):

```
variance       = (_rng.NextDouble() * 2 - 1) * HuntingVariance     // +/- HuntingVariance
huntingSuccess = clamp(hollingEff + variance, 0, 1)
CurrentHuntingSuccess = huntingSuccess
```

Demand per predator, summed across predators (EcosystemSimulator.cs:813-819):

```
rawDemand    = Population * EatingAmount * ThermalPerformance * BiologyStep   // uses Pmax via ThermalPerformance
actualDemand = rawDemand * huntingSuccess
totalRawDemand    += rawDemand
totalActualDemand += actualDemand
```

`ThermalPerformance` in `rawDemand` is the Step 1 value `RawThermalPerformance * Pmax` (set in Section 2, Step 1). It is available here because Step 1 runs before this feeding step (Step 2). It is not `FinalPerformance` (computed later in Step 5) and not `RawThermalPerformance`; predation reads the Pmax-scaled thermal value before the condition and final-performance steps run (EcosystemSimulator.cs:594, 814).

`LastAvgHuntingEfficiency` is the unweighted mean of `huntingSuccess` across the `predators` list (EcosystemSimulator.cs:827).

Total prey eaten is capped by available prey (EcosystemSimulator.cs:830-831):

```
totalEaten = min(availablePrey, totalActualDemand)
```

Per-predator fed rate (v11, EcosystemSimulator.cs:849-863). Each predator's satisfaction is its own hunting success scaled by an overall scarcity factor, restoring the competitive signal between specialist and generalist hunters that a pooled average erased:

```
scarcityFactor = totalActualDemand > 0 ? min(1, totalEaten / totalActualDemand) : 1
FedRate_i      = min(1, CurrentHuntingSuccess_i * scarcityFactor)
LastFedRateBySpecies[FullName] = FedRate_i
// i ranges over the predators list (living Tier 2) only
LastFedRateT2  = sum(FedRate_i * Population_i) / sum(Population_i)   // population-weighted, 1 if no pop
```

The `FedRate_i` assignment and the weighted mean both loop over the `predators` list only, that is living Tier 2 species with `Population >= MIN_ALIVE_POP`; dead Tier 2 species are not in the list and contribute to neither sum (EcosystemSimulator.cs:855-863). `LastFedRateT2` is therefore a population-weighted average over living predators, defaulting to 1 when their total population is 0. This reduces to the older pooled formula when there is a single predator species.

When `totalActualDemand == 0` (for example every predator has `huntingSuccess == 0`, or `EatingAmount == 0`), `scarcityFactor = 1` by the ternary's else branch, even though `totalEaten` is also 0 in that case (it is `min(availablePrey, 0)`). This `scarcityFactor = 1` is intentional: zero demand means there is no scarcity to penalize, so a predator's `FedRate_i` falls back to its own `CurrentHuntingSuccess_i`. Do not collapse this branch to 0; that would wrongly starve predators that simply had no demand (EcosystemSimulator.cs:849-851).

Prey removal is proportional across prey species, with the predation accumulator. The entire removal loop is wrapped in a guard, so when `totalEaten` is 0 (predators present but zero actual demand) or `availablePrey` is 0, no prey are removed and no per-species predation counters are touched (EcosystemSimulator.cs:867-896):

```
if totalEaten > 0 and availablePrey > 0:                 // guard: skip the whole loop otherwise
  for each prey p:
    share    = p.Population / availablePrey
    preyLost = totalEaten * share                       // fractional this-day removal
    // predation accumulator (Section 4)
    _predationAccumulators[p.FullName] += preyLost
    wholeDeaths = floor(_predationAccumulators[p.FullName])
    _predationAccumulators[p.FullName] -= wholeDeaths
    wholeDeaths = min(wholeDeaths, (long)p.Population)
    p.Population -= wholeDeaths
    LastEatenT1 += wholeDeaths
    LastEatenBySpecies[p.FullName] += wholeDeaths
```

The `share = p.Population / availablePrey` division is floating-point (both are `float`), and the loop iterates the same filtered `prey` list whose populations summed to `availablePrey`, so the shares sum to 1 across living prey. Tier 2 fed rate flows into the same Step 3 and Step 4 condition pathway as Tier 1; a starving predator drains condition even at good temperatures.

## 6. Condition state update (`UpdateCondition`, EcosystemSimulator.cs:952)

Condition is per-species health in `[0, 1]`, seeded at scenario start from `InitialCondition` (default 1.0, Section 1.1) and persisting across days (SimSpecies.cs:80-82, EcosystemSimulator.cs:384-385). Each day it moves a fraction of the way toward the day's target with one explicit Euler-style step. Skipped when `Population < MIN_ALIVE_POP` (EcosystemSimulator.cs:954).

Target and rate selection (EcosystemSimulator.cs:956-964):

```
target  = RawFinalPerformance                  // RawThermalPerformance * FedRate, [0,1]
pmaxSafe = max(Pmax, 1e-4)                      // divide-by-zero guard
drainRate    = ConditionDrainRate    >= 0 ? ConditionDrainRate    : global ConditionDrainRate
recoveryRate = ConditionRecoveryRate >= 0 ? ConditionRecoveryRate : global ConditionRecoveryRate
```

A negative per-species rate means inherit the global rate (EcosystemSimulator.cs:962-964). Globals default to 0.15 drain and 0.10 recovery (Section 1.3).

Drain branch, when `Condition > target` (EcosystemSimulator.cs:1018-1029):

```
severity       = (1 - target)^2                              // 0 at target=1, 1 at target=0
effectiveDrain = (drainRate * (1 + severity)) / pmaxSafe     // up to 2x near lethal; divided by Pmax
Condition     -= (Condition - target) * effectiveDrain
Condition      = max(target, Condition)                       // overshoot guard: cannot cross below target this step
```

The grouping is `(drainRate * (1 + severity)) / pmaxSafe`: the rate is multiplied by `(1 + severity)` first, then the product is divided by `pmaxSafe` last (EcosystemSimulator.cs:1026). In code `severity` is computed as `severity = 1 - target; severity *= severity;`, which equals `(1 - target)^2`.

Recovery branch, when `Condition <= target` (EcosystemSimulator.cs:1030-1041):

```
boost             = target^2                                       // 0 at target=0, 1 at target=1
effectiveRecovery = (recoveryRate * (1 + boost)) * pmaxSafe        // up to 2x at optimum; multiplied by Pmax
Condition        += (target - Condition) * effectiveRecovery
Condition         = min(target, Condition)                         // overshoot guard: cannot cross above target this step
```

The grouping is `(recoveryRate * (1 + boost)) * pmaxSafe`: the rate is multiplied by `(1 + boost)` first, then by `pmaxSafe` last (EcosystemSimulator.cs:1038). `boost` is computed as `boost = target; boost *= boost;`, which equals `target^2`.

Overshoot guard (EcosystemSimulator.cs:1028, 1040): each branch clamps `Condition` against the target immediately after the Euler move and before the final `[0, 1]` clamp below. The drain branch applies `Condition = max(target, Condition)` and the recovery branch applies `Condition = min(target, Condition)`, so a single step can never cross the target. The drain and recovery rates are not hard-capped at 1.0; setting either above 1.0 (up to about 3.0) previously let the per-step move overshoot the target and oscillate around it. With the guard, high rates make Condition snap exactly to its instantaneous target each day, a memoryless organism, which is what the no-memory experiments need. At the default rates (drain 0.15, recovery 0.10) the per-step move never reaches the target, so the guard never fires and existing results are byte-identical.

Clamp (EcosystemSimulator.cs:1043):

```
Condition = clamp(Condition, 0, 1)
```

Properties of the update:
- It is a single explicit Euler step toward `target` with a per-day effective rate, followed by the per-branch overshoot guard. When the effective rate is in `[0, 1]` the move undershoots the target and the guard never fires. When the effective rate exceeds 1 the bare move would cross the target, but the guard (`max(target, Condition)` on drain, `min(target, Condition)` on recovery, EcosystemSimulator.cs:1028, 1040) snaps `Condition` exactly to the target instead, so it never overshoots or oscillates. The two branches still respond to Pmax in opposite directions. In the drain branch `effectiveDrain = (drainRate * (1 + severity)) / pmaxSafe` divides by Pmax, so a small Pmax amplifies the effective drain, while a large Pmax shrinks it. In the recovery branch `effectiveRecovery = (recoveryRate * (1 + boost)) * pmaxSafe` multiplies by Pmax, so a large Pmax amplifies the effective recovery, while a small Pmax shrinks it. Either amplification only moves Condition closer to the target faster; with the guard the practical effect of a large effective rate is that Condition reaches the target in one step (memoryless), not that it overshoots (EcosystemSimulator.cs:1026, 1038).
- Drain accelerates up to 2x as `target` approaches 0 (lethal cold or heat, or starvation); recovery accelerates up to 2x as `target` approaches 1 (EcosystemSimulator.cs:968-984).
- Pmax scales the rates only, never the target (EcosystemSimulator.cs:956). Specialists with high Pmax drain slower and recover faster; generalists with low Pmax do the opposite. Keeping Pmax out of the target preserves Condition's species-agnostic `[0, 1]` scale, so `ReproThreshold` and `DeathThreshold` need no per-species retuning.
- Default drain 0.15 is asymmetrically faster than default recovery 0.10. Because Tier 1 `target = RawThermalPerformance * FedRate` now varies with food density, Tier 1 Condition no longer plateaus at 1.0 even at perfect temperature when the resource pool is depleted (EcosystemSimulator.cs:947-950).

## 7. Death pathways

Three pathways run in order: thermal death (Step 6), condition death (Step 7), natural death (Step 9). Predation (Step 2) is a fourth removal mechanism, documented in Section 5.2 as secondary legacy.

### 7.1 Thermal death (`ApplyThermalDeath`, EcosystemSimulator.cs:1000)

Instant whole-population kill at lethal limits. Skipped when `Population < MIN_ALIVE_POP` (EcosystemSimulator.cs:1002-1006).

```
if RawThermalPerformance > 0: return            // survives, no death
// at or beyond CTmin/CTmax (RawThermalPerformance == 0 from the lethal fade)
deaths = Population
Population = 0
Condition  = 0
LastTempDeathsT1 += deaths (Tier 1) or LastTempDeathsT2 += deaths (Tier 2)
LastTempDeathsBySpecies[FullName] += (long)deaths
```

Sources: EcosystemSimulator.cs:1008-1024. The trigger is `RawThermalPerformance == 0`, which the lethal cosine fade produces at or beyond `CTminC`/`CTmaxC` (Section 3.2). No Condition buffer can prevent it. Suboptimal but non-lethal temperatures are handled by the condition system instead.

### 7.2 Condition death (`ApplyConditionDeath`, EcosystemSimulator.cs:1056)

Graduated mortality from chronic stress. Two guards run first, in order: it is skipped when `Population < MIN_ALIVE_POP` (line 1058 is `if (sp.Population < MIN_ALIVE_POP) return;`), and otherwise it fires only when `Condition < DeathThreshold` (line 1059 is `if (sp.Condition >= sp.DeathThreshold) return;`). So a species below 1.0 individual is skipped before the condition test is even reached (EcosystemSimulator.cs:1058-1059).

```
severity  = (DeathThreshold - Condition) / DeathThreshold     // 0 at threshold, 1 at Condition=0
rawDeaths = Population * severity * DeathRate * BiologyStep    // fractional this-day deaths
```

Routed through the condition death accumulator (Section 4), clamped to population (EcosystemSimulator.cs:1067-1071):

```
_conditionDeathAccumulators[FullName] += rawDeaths
wholeDeaths = floor(_conditionDeathAccumulators[FullName])
_conditionDeathAccumulators[FullName] -= wholeDeaths
wholeDeaths = min(wholeDeaths, (long)Population)
```

When `wholeDeaths > 0`, apply the deaths and the survivor fitness boost (EcosystemSimulator.cs:1073-1094):

```
oldPop = Population; oldCondition = Condition
Population -= wholeDeaths
if Population > 0:
    Condition = min(1, oldCondition * oldPop / Population)     // survivors absorb the health pool
LastConditionDeathsT1 += wholeDeaths (Tier 1) or LastConditionDeathsT2 += wholeDeaths (Tier 2)
LastConditionDeathsBySpecies[FullName] += wholeDeaths
```

In `Condition = min(1, oldCondition * oldPop / Population)`, `oldPop` is the `float` `Population` captured immediately before the subtraction, and the denominator `Population` is the same `float` field immediately after subtracting the integer `wholeDeaths` (`Population -= wholeDeaths`, an integer change to a float, EcosystemSimulator.cs:1075-1084). The expression `oldCondition * oldPop / Population` evaluates left to right as `((oldCondition * oldPop) / Population)`, so the health pool `oldCondition * oldPop` is divided by the reduced float count. `oldCondition` is the pre-death `Condition`.

The survivor boost assumes the dead were the weakest members (condition near 0), so the same total health pool spread over fewer survivors raises the average, capped at 1.0 (EcosystemSimulator.cs:1045-1052, 1082-1086). This self-correction pushes Condition back toward the threshold and prevents a death spiral. `severity` reaches 1 only at `Condition = 0`, where the full `DeathRate` applies.

### 7.3 Natural death (`ApplyNaturalDeathWithAccumulator`, EcosystemSimulator.cs:1270)

Flat background mortality independent of performance. Skipped when `Population <= 0` (EcosystemSimulator.cs:1272-1275).

```
variance      = (_rng.NextDouble() * 2 - 1) * NaturalDeathVariance   // +/- NaturalDeathVariance
baseRate      = max(0, NaturalDeathRate + variance)                  // fraction per day
effectiveRate = baseRate                                             // no performance scaling
deaths        = Population * effectiveRate * BiologyStep             // fractional this-day deaths
```

Routed through the natural death accumulator (Section 4), clamped to population (EcosystemSimulator.cs:1288-1299):

```
_naturalDeathAccumulators[FullName] += deaths
wholeDeaths = floor(_naturalDeathAccumulators[FullName])
_naturalDeathAccumulators[FullName] -= wholeDeaths
wholeDeaths = min(wholeDeaths, (long)Population)
if wholeDeaths > 0:
    Population -= wholeDeaths
    LastNaturalDeathsT1 += wholeDeaths (Tier 1) or LastNaturalDeathsT2 += wholeDeaths (Tier 2)
    LastNaturalDeathsBySpecies[FullName] += wholeDeaths
```

Sources: EcosystemSimulator.cs:1278-1313. Default `NaturalDeathRate = 0.02` and `NaturalDeathVariance = 0.01` give an effective daily rate in `[0.01, 0.03]` (SimSpecies.cs:40-41). Natural death does not touch Condition.

## 8. Reproduction (`ApplyReproduction`, EcosystemSimulator.cs:1150)

Condition-driven births, scaled by Pmax, with a birth accumulator. There is no soft cap on births; Tier 1 throttling is via the Condition pathway (Section 5.1).

Population floor (EcosystemSimulator.cs:1152-1156):

```
if Population < MIN_POPULATION_FOR_REPRODUCTION (2.0): return     // reproScale stays 0 from reset
```

Reproduction scale from Condition, continuous at `ReproThreshold` (EcosystemSimulator.cs:1168-1191). `STRUGGLING_REPRO_RATE = 0.10` (EcosystemSimulator.cs:282) is the value where the two regions meet:

```
if ReproThreshold >= 1:                       reproScale = STRUGGLING_REPRO_RATE * Condition
else if ReproThreshold <= 0:                  reproScale = Condition
else if Condition >= ReproThreshold:
    t          = (Condition - ReproThreshold) / (1 - ReproThreshold)        // [0,1]
    reproScale = STRUGGLING_REPRO_RATE + (1 - STRUGGLING_REPRO_RATE) * t    // 0.10 .. 1.0
else:
    reproScale = STRUGGLING_REPRO_RATE * (Condition / ReproThreshold)       // 0 .. 0.10
reproScale = clamp(reproScale, 0, 1)
```

The continuity claim applies only to the interior case `0 < ReproThreshold < 1`. There, at `Condition = ReproThreshold` both interior branches give exactly `STRUGGLING_REPRO_RATE = 0.10`, so the two regions meet with no discontinuity, and at `Condition = 0` the struggling branch gives `reproScale = 0`. The two degenerate branches are separate and not subject to that continuity claim: when `ReproThreshold >= 1` the formula is `STRUGGLING_REPRO_RATE * Condition` (line 1172), and when `ReproThreshold <= 0` it is `Condition` directly (line 1177). These do not join the interior ramps at any point and exist only to avoid the interior branches dividing by `(1 - ReproThreshold)` or `ReproThreshold` when either would be 0. When `ReproThreshold` is exactly 1, the first branch (`>= 1`) is taken; when it is exactly 0, the second branch (`<= 0`) is taken; the `Condition >= ReproThreshold` interior branch is reached only for strictly interior thresholds.

### 8.1 Recording `reproScale` per tier and per species

After `reproScale` is clamped, the same value is written to three fields, all `float`, before any births are computed (EcosystemSimulator.cs:1193-1198):

```
if Tier == 1: LastReproScaleT1 = reproScale          // plain assignment, line 1194
else if Tier == 2: LastReproScaleT2 = reproScale     // plain assignment, line 1195
LastReproScaleBySpecies[FullName] = reproScale        // plain assignment, line 1198
```

The per-tier fields use plain ASSIGNMENT (`=`), not the accumulating `+=` that the death and birth count counters use. This is the single most error-prone detail in this section, so it is spelled out exactly:

| Field | Type | Declared | Reset each day | Write op in step | Final value after the per-species loop |
|-------|------|----------|----------------|------------------|----------------------------------------|
| `LastReproScaleT1` | `float` | EcosystemSimulator.cs:185 | `= 0f` at EcosystemSimulator.cs:561 | `=` (EcosystemSimulator.cs:1194) | `reproScale` of the LAST Tier 1 species iterated that reproduced, NOT a sum |
| `LastReproScaleT2` | `float` | EcosystemSimulator.cs:186 | `= 0f` at EcosystemSimulator.cs:562 | `=` (EcosystemSimulator.cs:1195) | `reproScale` of the LAST Tier 2 species iterated that reproduced, NOT a sum |
| `LastReproScaleBySpecies[FullName]` | `float` in `Dictionary<string,float>` | EcosystemSimulator.cs:218 | `= 0f` per species at EcosystemSimulator.cs:581 | `=` (EcosystemSimulator.cs:1198) | this exact species' `reproScale` for the day (assignment is correct here because the key is unique per species) |

Reimplementation rules for the per-tier fields:

1. Reset `LastReproScaleT1` and `LastReproScaleT2` to `0f` at the start of every biology step, in the same reset block as the count counters (EcosystemSimulator.cs:561-562).
2. Inside the per-species reproduction loop, OVERWRITE the matching tier field with the current species' `reproScale` using `=`. Do NOT add to it. Each later species of the same tier clobbers the value the previous one wrote.
3. Because `ApplyReproduction` returns early when `Population < MIN_POPULATION_FOR_REPRODUCTION` (2.0) before reaching line 1194 (EcosystemSimulator.cs:1152-1156), a species that cannot reproduce does not write its tier field at all. With N Tier 1 species, the surviving `LastReproScaleT1` is the `reproScale` of whichever reproducing Tier 1 species the iterator visited last, or `0f` if no Tier 1 species reproduced that day.

Consequences for output. With three Tier 1 species each producing, for example, `reproScale = 0.5`, an accumulating `+=` implementation would record `1.5` and a per-species average would record `0.5`; the real code records `0.5` (the last species' value). A `+=` reimplementation would therefore inflate this field by roughly the count of reproducing Tier 1 species (about 3x in that example), so the assignment semantics must be matched exactly to reproduce the CSV and `SimLog` values.

The per-tier `reproScale` fields are NOT count counters and are deliberately excluded from the per-species-sum-equals-tier invariant in Section 11. That invariant covers births, thermal deaths, condition deaths, natural deaths, and eaten (all integer event counts incremented with `+=`); it does not apply to `reproScale`, which is a scalar rate snapshot, not a tally. The only consistency relation that holds for `reproScale` is that `LastReproScaleBySpecies[FullName]` equals the day's `reproScale` for that species, and the matching tier field equals the `reproScale` of the last-iterated reproducing species in that tier.

Secondary legacy note (Tier 2). `LastReproScaleT2` follows the identical assignment rule but is exercised only when Tier 2 predators are present and reproduce. Current runs are Tier 1 only, so `LastReproScaleT2` stays at its `0f` daily reset (no Tier 2 species reach line 1195). It is documented here for completeness; it does not affect Tier 1 output.

Birth count (EcosystemSimulator.cs:1204):

```
births = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep
```

Pmax multiplies here so a high-Pmax specialist converts the same Condition into more offspring than a generalist (EcosystemSimulator.cs:1200-1201).

Birth accumulator (Section 4). Births add to population and are not clamped to population (EcosystemSimulator.cs:1224-1238):

```
_birthAccumulators[FullName] += births
wholeBirths = floor(_birthAccumulators[FullName])
_birthAccumulators[FullName] -= wholeBirths
Population += wholeBirths
LastBirthsT1 += wholeBirths (Tier 1) or LastBirthsT2 += wholeBirths (Tier 2)
LastBirthsBySpecies[FullName] += wholeBirths
```

Newborn condition: `Condition` is a single per-species scalar, not a per-individual value, so reproduction does not modify it at all. Adding `wholeBirths` to `Population` while leaving the `Condition` field untouched automatically keeps the per-species health value the same, which is mathematically equivalent to newborns entering at the current group `Condition`. The "inherit the group Condition" framing (EcosystemSimulator.cs:1240-1253) is conceptual only and requires no code: there is no fixed newborn constant, no food-density multiplier, and no weighted-average blend. The in-code rationale is that the parent's Condition is already a lagged integral of recent food density or hunting success, so multiplying by today's fed rate would double-count. A reimplementation must not recompute or blend Condition during reproduction.

## 9. Carrying capacity summary

`CarryingCapacityPerTier` (default 5000, EcosystemSimulator.cs:237) is the Tier 1 shared resource pool ceiling, always on (Section 1.3). It enters the model in two places:

1. Step 2 food density (Section 5.1): `foodDensity = max(0, 1 - tier1Consumption / max(cap, 1))` where `tier1Consumption = sum over living Tier 1 of Population * max(1, EatingAmount)` (EcosystemSimulator.cs:774-777). This drives Tier 1 fed rate, the condition target, reproduction, and condition deaths. The equilibrium Tier 1 population is appetite-dependent: at appetite 1 it settles around 70 to 75 percent of the cap, and at the shipped appetite 3 around one third of the cap, since `foodDensity` reaches 0 when total consumption (not head count) hits the cap (SimulationConfig.cs:55-56).
2. Step 10 overflow guard (Section 2, Step 10): a hard ceiling at `100 * CarryingCapacityPerTier` prevents `float`/`long` overflow during transients (EcosystemSimulator.cs:667-668). This ceiling is applied per species: the Step 10 loop runs `if (sp.Population > popCap) sp.Population = popCap` for each species individually, where `popCap = 100 * CarryingCapacityPerTier` (EcosystemSimulator.cs:668, 671-679). So one species can reach up to `100 * CarryingCapacityPerTier` (500000 at the default 5000) on its own, even though the shared food-density pool in place 1 saturates `foodDensity` to 0 at a tier-wide total near `CarryingCapacityPerTier`. The two are different mechanisms: place 1 is the ecological resource pool summed across all Tier 1 species, place 2 is a per-species numeric overflow bound, not an ecological cap. The Step 10 guard is not divided among species and does not look at the tier total.

Both the food-density calculation and the Step 10 guard read `EcosystemSimulator.CarryingCapacityPerTier` directly (EcosystemSimulator.cs:766-777, 668); there is no separate per-run copy. The configuration value `SimulationConfig.CarryingCapacityTier1` (default 5000, range 100 to 100000, SimulationConfig.cs:58-59) does not feed the model directly. Instead it is written into `EcosystemSimulator.CarryingCapacityPerTier` once before the run starts: `runner.Ecosystem.CarryingCapacityPerTier = config.CarryingCapacityTier1` (SimulationController.cs:369), and the bulk path sets it from `batch.CarryingCapT1` (SimulationController.cs:309). If no config is applied (for example a unit test that constructs `EcosystemSimulator` directly and never assigns the field), the simulator's own field initializer governs, which is 5000 (EcosystemSimulator.cs:237). See `configuration-reference.md`.

## 10. End-to-end day pseudocode (BiologyStep = 1)

```
ProcessBiologyStep(temperature):                                  // EcosystemSimulator.cs:543
    StartPopT1/T2 = tier sums
    reset all Last* tier counters; clear per-species dicts; seed StartPopBySpecies

    for sp in Species:                                            // Step 1
        sp.RawThermalPerformance = sp.CalculatePerformance(temperature)
        sp.ThermalPerformance    = sp.RawThermalPerformance * sp.Pmax
        sp.FedRate = 1; sp.CurrentHuntingSuccess = 1

    ProcessFeedingWithAccumulator():                              // Step 2
        tier1Consumption = sum over alive Tier1 of Population * max(1, EatingAmount)
        foodDensity   = max(0, 1 - tier1Consumption/max(cap,1))   // supply cap
        resourceRatio = max(cap,1) / max(tier1Pop, 1)            // Holling search ratio
        for Tier1 sp alive:                                       // same foraging as Tier 2
            gatherSuccess = ComputeForagingSuccess(sp, resourceRatio)   // Holling II + variance
            sp.FedRate    = min(1, gatherSuccess * foodDensity)
        // Tier 2 Holling II block (legacy; no-op when no predators)

    for sp in Species: sp.RawFinalPerformance = sp.RawThermalPerformance * sp.FedRate   // Step 3
    for sp in Species: UpdateCondition(sp)                        // Step 4: Euler toward RawFinalPerformance
    for sp in Species: sp.FinalPerformance = sp.ThermalPerformance * sp.FedRate         // Step 5: logging only
    for sp in Species: ApplyThermalDeath(sp)                      // Step 6: kill all if RawThermalPerformance==0
    for sp in Species: ApplyConditionDeath(sp)                    // Step 7: graduated if Condition<DeathThreshold
    for sp in Species: ApplyReproduction(sp)                      // Step 8: Condition*Pmax births + accumulator
    for sp in Species: ApplyNaturalDeathWithAccumulator(sp)       // Step 9: flat rate + accumulator

    for sp in Species:                                            // Step 10
        if sp.Population > 100*cap: sp.Population = 100*cap
        sp.Population = round(sp.Population, AwayFromZero)

    ComputeAverageCondition(); EndPopT1/T2 = tier sums; UpdateAccumulatorTotals()
```

## 11. Invariants

- Per-species event dictionaries sum to the matching tier counter each day: `sum(LastBirthsBySpecies) == LastBirthsT1 + LastBirthsT2`, and likewise for thermal deaths, condition deaths, natural deaths, and eaten (EcosystemSimulator.cs:212; the per-species and tier counters are incremented together in each step at EcosystemSimulator.cs:890-892, 1021-1024, 1090-1093, 1258-1263, 1308-1313). This invariant covers only the integer event counts incremented with `+=`. It does NOT cover `reproScale`: the per-tier `LastReproScaleT1/T2` fields are written by plain assignment (`=`) and hold the last-iterated reproducing species' value, not a sum, so `sum(LastReproScaleBySpecies)` does not equal `LastReproScaleT1 + LastReproScaleT2` in general (Section 8.1, EcosystemSimulator.cs:1194-1198).
- Condition stays in `[0, 1]` after every update due to the clamp at EcosystemSimulator.cs:989, and is forced to 0 on thermal death (EcosystemSimulator.cs:1017).
- All performance and fed-rate quantities stay in `[0, 1]`: `RawThermalPerformance` and `RawFinalPerformance` from the curve clamp and the `[0,1]` fed rate; `ThermalPerformance` and `FinalPerformance` because Pmax is in `[0, 1]`.
- No death pathway removes more than the current `Population` (each clamps `wholeDeaths` to `(long)Population`). Births are not clamped; the Step 10 cap and rounding bound the final integer population.
