# Condition (Health) System

## Overview

The Condition system adds a per-species health/energy buffer (0.0 to 1.0) that prevents species from dying immediately when temperatures become suboptimal. Instead of sudden death at poor performance, species now experience gradual health decline, giving them time to survive short temperature fluctuations.

This was implemented to address **species fragility** — previously, a few bad temperature days could instantly wipe out populations due to performance-scaled death rates.

## How It Works

### Condition Value

Every species group has a `Condition` value representing collective health:
- Starts at **1.0** (full health) when placed in the ecosystem
- Moves toward `RawFinalPerformance` (thermal performance x fed rate) each day
- Drains faster than it recovers (asymmetric by design)
- Newborns enter at **0.5** health, diluting the group average

### Daily Update Formula

Each day, for each species:

```
target = RawFinalPerformance  (thermal_perf x fed_rate, no Pmax)

If Condition > target (DRAINING):
    effectiveDrain = ConditionDrainRate  (default 0.15)

    // Acceleration near lethal temperatures (quadratic — Buckley et al. 2025)
    If target < 0.2:
        severity = 1 - target / 0.2    // 0 at perf=0.2, 1.0 at perf=0
        severity = severity²           // quadratic: concentrated near perf=0
        effectiveDrain *= 1 + severity * 4   // up to 5x at perf=0

    Condition -= (Condition - target) * effectiveDrain

If Condition < target (RECOVERING):
    effectiveRecovery = ConditionRecoveryRate  (default 0.10)

    // Acceleration at high performance (quadratic — Buckley et al. 2025)
    If target > 0.7:
        boost = (target - 0.7) / (1 - 0.7)    // 0 at perf=0.7, 1.0 at perf=1
        boost = boost²                         // quadratic: concentrated near perf=1
        effectiveRecovery *= 1 + boost * 4     // up to 5x at perf=1.0

    Condition += (target - Condition) * effectiveRecovery
```

### Drain Acceleration

When thermal performance drops below 0.2 (near lethal limits), drain rate accelerates using a quadratic curve (Buckley et al. 2025):

| Performance | Drain Multiplier | Example |
|---|---|---|
| 0.20 | 1.0x (normal) | Mildly stressed |
| 0.15 | 1.3x | Moderately stressed |
| 0.10 | 1.6x | Stressed |
| 0.05 | 2.3x | Near lethal |
| 0.00 | 5.0x | At lethal limit |

The quadratic curve concentrates acceleration near the extreme — most of the 5x multiplier only kicks in very close to lethal limits, matching the biology where damage spikes near CTmax.

### Recovery Acceleration

When thermal performance exceeds 0.7 (near optimal), recovery rate accelerates using a quadratic curve (Buckley et al. 2025):

| Performance | Recovery Multiplier | Example |
|---|---|---|
| 0.70 | 1.0x (normal) | At threshold |
| 0.80 | 1.4x | Good performance |
| 0.85 | 1.7x | Strong performance |
| 0.90 | 2.6x | Near optimal |
| 1.00 | 5.0x | At optimal |

The quadratic curve concentrates recovery boost near optimal temperature — the big recovery multiplier only kicks in when the species is very close to its thermal optimum, matching the Gaussian repair function from Buckley et al. (2025). Combined with drain acceleration, this produces biologically realistic "boom and bust" dynamics.

### Birth Dilution

When new individuals are born, they enter with `NEWBORN_CONDITION = 0.5` (a constant in `EcosystemSimulator`). This dilutes the group's average Condition:

```
newCondition = (oldPop * oldCondition + births * 0.5) / newPop
```

Example: 100 individuals at Condition 1.0, 10 born:
- New Condition = (100 * 1.0 + 10 * 0.5) / 110 = **0.955**

At high growth rates, this creates a natural cap where Condition stabilizes slightly below Pmax (observed ~0.643 at optimal temperature with Pmax 0.65).

## Four Death Types

Previously the simulation had 2 death types. Now there are 4, each with distinct triggers:

### 1. Thermal Death (Instant)
- **Trigger:** `RawThermalPerformance == 0` (temperature at or beyond CTmin/CTmax)
- **Effect:** Entire population dies immediately. No Condition buffer.
- **Example:** Arctic species (CTmax=20C) at 35C = instant wipeout day 1

### 2. Condition Death (Chronic)
- **Trigger:** `Condition < DeathThreshold` (default 0.3)
- **Effect:** Graduated severity: `severity = (DeathThreshold - Condition) / DeathThreshold`, then `Deaths = Population * severity * DeathRate * BiologyStep`. The further below the threshold, the more die. At exactly the threshold, severity is 0 (no deaths). At Condition = 0, severity is 1.0 (maximum death rate).
- **Uses fractional accumulator** for gradual decline (no sudden jumps)
- **Survivor fitness boost:** After deaths, surviving Condition is recalculated: `new_condition = old_condition * old_pop / new_pop`, preventing death spirals.
- **Example:** Common species at 38C: Condition drains to 0.217 by day 2, condition deaths begin, extinct by day 8

### 3. Natural Death (Flat Rate)
- **Trigger:** Always (every day, for all living species)
- **Effect:** Flat rate per day (`NaturalDeathRate`): 2% for T1 (+/-1% variance), 1% for T2 (+/-0.5% variance). Independent of performance.
- **Change from before:** Previously scaled by `1/performance`, which double-punished struggling species. Now decoupled.

### 4. Predation (Holling Type II)
- Higher-tier species eat lower-tier species via a **Holling Type II functional response**. Hunting success scales with the prey:predator ratio (half-saturation reference at 20:1 ratio). Key parameters: `HuntingEfficiency` (default 0.75), `HuntingVariance` (+/-0.15). Predation uses a fractional accumulator, same as the other death types.

## Biology Step Sequence (10 Steps)

Previously 7 steps, now expanded to 10:

1. **Thermal Performance** - Arrhenius formula with CTmin/CTmax cosine fade
2. **Feeding/Predation** - Holling Type II functional response + predation accumulator
3. **Raw Final Performance** - `RawThermalPerf x FedRate` (Condition drain target, without Pmax)
4. **Update Condition** - Drain or recover toward RawFinalPerformance
5. **Final Performance** - `ThermalPerf x FedRate` (includes Pmax; currently unused by later steps)
6. **Thermal Death** - Instant kill when `RawThermalPerformance == 0`
7. **Condition Death** - Graduated severity when `Condition < DeathThreshold`, with survivor fitness boost
8. **Reproduction** - Condition-based graduated scale + birth accumulator + carrying capacity
9. **Natural Death** - Flat rate + natural death accumulator
10. **Population Rounding** - Round fractional populations to nearest integer (`Math.Round`, midpoint rounds away from zero)

## HasCrashed() Fix

**Before:** Simulation stopped when ANY single tier went extinct.
**After:** Simulation only stops when ALL populations = 0 (total extinction).

This allows predators to starve after prey extinction as a valid outcome, rather than an error condition.

## Configurable Parameters

### In SimulationConfig (Unity Inspector)

| Parameter | Default | Description |
|---|---|---|
| `ConditionDrainRate` | 0.15 | How fast Condition drains toward poor performance. Higher = less resilient. |
| `ConditionRecoveryRate` | 0.10 | How fast Condition recovers toward good performance. Lower than drain (asymmetric). |

### Constants in EcosystemSimulator

| Constant | Value | Description |
|---|---|---|
| `NEWBORN_CONDITION` | 0.5 | Condition value for newborn individuals |
| `DRAIN_ACCEL_THRESHOLD` | 0.2 | Performance below this triggers drain acceleration (quadratic) |
| `DRAIN_ACCEL_MAX` | 4.0 | Max drain acceleration multiplier (5x total at perf=0) |
| `RECOVERY_BOOST_THRESHOLD` | 0.7 | Performance above this triggers recovery acceleration (quadratic) |
| `RECOVERY_BOOST_MAX` | 4.0 | Max recovery acceleration multiplier (5x total at perf=1.0) |

### Species-Level (unchanged)

| Parameter | Description |
|---|---|
| `DeathThreshold` | Condition below which condition deaths begin (default 0.3) |
| `DeathRate` | Rate of condition deaths when below threshold |
| `CTminC` / `CTmaxC` | Lethal temperature limits for instant thermal death |

## CSV Output

### Scenario CSV (per-day columns)

New columns added to daily step data:

| Column | Description |
|---|---|
| `ConditionDeathsT1` | Condition deaths for Tier 1 this day |
| `ConditionDeathsT2` | Condition deaths for Tier 2 this day |
| `AvgConditionT1` | Population-weighted average Condition for Tier 1 |
| `AvgConditionT2` | Population-weighted average Condition for Tier 2 |
| `ConditionDeathAccumT1` | Fractional accumulator residual for Tier 1 condition deaths |
| `ConditionDeathAccumT2` | Fractional accumulator residual for Tier 2 condition deaths |

Config lines added:
```
#config:condition_drain_rate,0.15
#config:condition_recovery_rate,0.10
```

### Aggregate CSV

New section:
```
=== CONDITION STATS ===
Avg Condition T1 (All Scenarios),0.643
Avg Condition T2 (All Scenarios),0.520
Avg Final Condition T1 (Survived),0.639
Avg Final Condition T2 (Survived),0.510
```

### Config CSV / JSON

New section:
```
=== CONDITION SYSTEM ===
Condition Drain Rate,0.15
Condition Recovery Rate,0.10
```

## Bulk CSV Support

Optional columns in bulk batch CSVs (backward compatible - defaults used if missing):

| Column | Default | Description |
|---|---|---|
| `condition_drain_rate` | 0.15 | Override drain rate per batch |
| `condition_recovery_rate` | 0.10 | Override recovery rate per batch |

## UI Support

Two TMP_InputField slots available in `SimulationInputUI`:
- `ConditionDrainRate` - With validation and null-safe handling
- `ConditionRecoveryRate` - With validation and null-safe handling

If not assigned in the Unity Inspector, the ScriptableObject defaults are used silently.

## Validation Results

Tested with targeted single-species bulk tests (10 batches, flat temperatures, 60 days):

| Temperature | Outcome | Notes |
|---|---|---|
| Beyond CTmin/CTmax | Instant death day 1 | Thermal death fires correctly |
| 38C (near CTmax=40) | Dead day 8 | Drain acceleration works (condition drops 0.6 on day 1) |
| 30C (past optimal) | Survived, pop 20K | Condition stabilizes at 0.471 (above 0.3 threshold) |
| 25C (slightly warm) | Survived, pop 2.1M | Condition at 0.605, growing steadily |
| 20C (optimal) | Thriving, pop 19M | Condition at 0.643 (near Pmax, diluted by births) |

**All validation checks passed:** death type separation clean, no anomalies, birth dilution visible, drain acceleration confirmed (6x faster at 38C vs optimal).
