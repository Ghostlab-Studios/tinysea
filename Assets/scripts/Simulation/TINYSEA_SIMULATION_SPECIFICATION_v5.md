# TinySea Simulation Specification v5

## Overview

TinySea is a marine ecosystem simulation modeling a 2-tier food web with thermal adaptation. This document details the complete biology system, including accumulator systems for accurate tracking of fractional deaths and births, and carrying capacity for realistic population dynamics.

**Key Features:**
- Arrhenius thermal performance model
- 3 accumulator systems (birth, predation, natural death)
- Performance-scaled natural death
- Carrying capacity (soft limit on reproduction)
- No density death (removed in v5)

---

## Species Structure

### Tiers
- **Tier 1 (Hexapod)** - Prey/Producers, do not eat other species
- **Tier 2 (Sheplik)** - Predators, eat Tier 1

### Thermal Variants
Each species has 3 thermal variants optimized for different temperatures:

| Variant | Optimal Temperature | Comfortable Range |
|---------|--------------------|--------------------|
| Arctic | 5°C | -3°C to 7°C |
| Common | 20°C | 12°C to 22°C |
| Tropical | 35.5°C | 27°C to 37°C |

### Species Parameters

| Parameter | Tier 1 (Hexapod) | Tier 2 (Sheplik) | Description |
|-----------|------------------|------------------|-------------|
| EatingAmount | 0 | 1.5 | Prey consumed per creature per step |
| ReproductionMultiplier | 0.45 | 0.1 | Birth rate multiplier |
| DeathThreshold | 0.3 | 0.3 | FinalPerf below this triggers thermal death |
| DeathRate | 0.6 | 0.3 | Fraction dying when thermal death triggers |
| MinimumDeaths | 1 | 1 | Minimum deaths when thermal death triggers |
| ReproThreshold | 0.25 | 0.25 | FinalPerf required to reproduce |
| NaturalDeathRate | 0.02 (2%) | 0.03 (3%) | Base natural mortality per step |
| NaturalDeathVariance | 0.01 (±1%) | 0.015 (±1.5%) | Random variance in natural death |
| HuntingEfficiency | 1.0 (N/A) | 0.75 (75%) | Base hunting success rate |
| HuntingVariance | 0 | 0.15 (±15%) | Random variance in hunting |
| NoPredatorPenalty | 0.85 | N/A | Birth multiplier when Tier 2 extinct |
| MinFinalPerfForNaturalDeath | 0.1 | 0.1 | Floor value to prevent division by zero |

---

## Temperature Calculation

Temperature is calculated daily using multiple components:

```
Temperature = Base + Seasonal + ClimateTrend + InterannualVariation + DailyVariation
```

Then clamped to bounds (default: -5°C to 50°C).

### Components

| Component | Formula | Description |
|-----------|---------|-------------|
| Base | Constant (default 20°C) | Starting temperature |
| Seasonal | sin(2π × day/365) × Amplitude | Summer/winter cycle |
| Climate Trend | TrendPerYear × (day/365) | Long-term warming |
| Interannual | Random per year with warming bias | Year-to-year variation |
| Daily | Autocorrelated random noise | Day-to-day weather |

---

## Biology Step Sequence (7 Steps)

Every biology step (default: daily), the following sequence runs:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ STEP 1: THERMAL PERFORMANCE                                             │
│   For each species: ThermalPerf = Arrhenius(temperature)                │
│   Range: 0.0 to 1.0                                                     │
├─────────────────────────────────────────────────────────────────────────┤
│ STEP 2: FEEDING (Predation)                                             │
│   - Tier 2 hunts Tier 1                                                 │
│   - Calculate hunting success with variance                             │
│   - Remove prey PROPORTIONALLY from Tier 1 variants                     │
│   - Calculate FedRate for predators                                     │
│   - Uses PREDATION ACCUMULATOR for fractional prey deaths               │
├─────────────────────────────────────────────────────────────────────────┤
│ STEP 3: FINAL PERFORMANCE                                               │
│   FinalPerf = ThermalPerf × FedRate                                     │
│   (Tier 1 always has FedRate = 1.0)                                     │
├─────────────────────────────────────────────────────────────────────────┤
│ STEP 4: THERMAL DEATH                                                   │
│   IF FinalPerf < DeathThreshold (0.3):                                  │
│     deaths = max(MinimumDeaths, Population × DeathRate)                 │
│   NO ACCUMULATOR - MinimumDeaths ensures at least 1 dies                │
├─────────────────────────────────────────────────────────────────────────┤
│ STEP 5: REPRODUCTION (Birth)                                            │
│   IF Population >= 2 AND FinalPerf >= ReproThreshold (0.25):            │
│     births = Pop × FinalPerf × ReproMultiplier                          │
│   Uses BIRTH ACCUMULATOR for fractional births                          │
├─────────────────────────────────────────────────────────────────────────┤
│ STEP 6: NATURAL DEATH                                                   │
│   Applies to ALL species regardless of conditions                       │
│   Death rate SCALED by performance (weaker = more deaths)               │
│   Uses NATURAL DEATH ACCUMULATOR for fractional deaths                  │
├─────────────────────────────────────────────────────────────────────────┤
│ STEP 7: POPULATION ROUNDING                                             │
│   All populations rounded to integers for display                       │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Detailed Mechanics

### Step 1: Thermal Performance (Arrhenius Formula)

```
ThermalPerf = [exp(B/OT - B/T) × (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))]
              ÷ [1 + exp(L/T - L/LB) + exp(U/UB - U/T)]
```

Where:
- T = Current temperature in Kelvin
- OT = Optimal temperature in Kelvin
- B = Arrhenius breadth
- L = Arrhenius lower
- U = Arrhenius upper
- LB = Lower bound in Kelvin
- UB = Upper bound in Kelvin

Result is clamped to [0, 1].

**Example:**
- Arctic Hexapod at 5°C → ThermalPerf ≈ 1.0 (optimal)
- Arctic Hexapod at 20°C → ThermalPerf ≈ 0.35 (struggling but surviving)
- Arctic Hexapod at 30°C → ThermalPerf ≈ 0.15 (dying)

---

### Step 2: Feeding (Predation) with Accumulator

**Hunting Success Calculation:**

Hunting efficiency is affected by:
1. Base hunting efficiency (from species parameters)
2. Random variance (±15% for Tier 2)
3. Prey-ratio bonus/penalty (based on prey abundance)

```
preyRatio = TotalTier1Population / TotalTier2Population
huntingBonus = CalculateHuntingBonus(preyRatio)
huntingSuccess = BaseHuntingEfficiency + random(-Variance, +Variance) + huntingBonus
huntingSuccess = clamp(huntingSuccess, 0.1, 1.0)
```

**Prey-Ratio Hunting Bonus:**

When prey is abundant, hunting is easier. When prey is scarce, hunting becomes MUCH harder. This creates strong negative feedback to prevent predator overpopulation.

| Prey:Predator Ratio | Hunting Bonus | Description |
|---------------------|---------------|-------------|
| >= 200:1 | +15% | Extremely abundant prey |
| >= 100:1 | +10% | Very abundant prey |
| >= 50:1 | +5% | Abundant prey |
| >= 20:1 | 0% | Baseline (balanced ecosystem) |
| >= 10:1 | -15% | Getting crowded |
| >= 5:1 | -30% | Competitive hunting |
| >= 2:1 | -45% | Very competitive |
| < 2:1 | -55% | Desperate hunting |

**Example with 75% base hunting efficiency:**
- At ratio 100:1 → 85% hunting success
- At ratio 20:1 → 75% hunting success (baseline)
- At ratio 10:1 → 60% hunting success
- At ratio 5:1 → 45% hunting success
- At ratio 2:1 → 30% hunting success
- At ratio 1:1 → 20% hunting success

Hunting success is clamped to [5%, 100%].

**Demand Calculation:**
```
rawDemand = Population × EatingAmount × ThermalPerf × BiologyStep
actualDemand = rawDemand × huntingSuccess
```

**Proportional Prey Removal:**

Prey is removed proportionally based on each variant's share of total Tier 1 population.

```
For each Tier 1 variant:
  share = variantPopulation / totalTier1Population
  preyLost = totalEaten × share
```

**Predation Accumulator:**

When `preyLost` is fractional (e.g., 0.4), it accumulates:

```
predationAccumulator[variant] += preyLost
if predationAccumulator[variant] >= 1.0:
    actualDeaths = floor(predationAccumulator[variant])
    predationAccumulator[variant] -= actualDeaths
    population -= actualDeaths
```

**FedRate Calculation (IMPORTANT):**

FedRate measures whether predators caught what they **attempted** to catch, then applies a **scarcity penalty**.

```
baseFedRate = min(1.0, totalActuallyEaten / totalActualDemand)
preyPerPredator = availablePrey / totalPredators
scarcityMultiplier = CalculateScarcityMultiplier(preyPerPredator)
FedRate = baseFedRate × scarcityMultiplier
```

**Prey Scarcity Multiplier:**

Even if predators catch what they attempt, searching for scarce prey costs energy. This creates strong negative feedback when predators overpopulate.

| Prey per Predator | Scarcity Multiplier | Effect |
|-------------------|---------------------|--------|
| >= 50 | 1.0 | Plenty of prey, no penalty |
| >= 20 | 0.85 | Adequate prey |
| >= 10 | 0.70 | Getting scarce |
| >= 5 | 0.50 | Significant hunger |
| >= 2 | 0.35 | Severe hunger |
| < 2 | 0.20 | Critical scarcity |

**Example:**
- 2000 predators, 1000 prey → 0.5 prey per predator → scarcity = 0.20
- Even if predators catch everything: FedRate = 1.0 × 0.20 = **0.20**
- FinalPerf = ThermalPerf × 0.20 = very low
- Natural death rate = 3% × (1/0.14) = **21%** → predators die off rapidly

This creates the negative feedback loop needed to prevent predator population explosions.

**Note:** Hunting efficiency already limits how much predators can attempt. FedRate only drops further if prey is so scarce they can't even catch that reduced amount, OR if prey per predator is very low (scarcity penalty).

---

### Step 3: Final Performance

Simple multiplication:
```
FinalPerformance = ThermalPerformance × FedRate
```

- Tier 1 always has FedRate = 1.0 (they don't hunt)
- Tier 2 FedRate depends on hunting success and prey availability

---

### Step 4: Thermal Death

**Trigger Condition:** `FinalPerf < DeathThreshold (0.3)`

**When Triggered:**
```
deaths = max(MinimumDeaths, Population × DeathRate × BiologyStep)
deaths = min(deaths, Population)  // Can't kill more than exist
```

**NO ACCUMULATOR NEEDED** because `MinimumDeaths = 1` ensures at least 1 creature dies when the condition triggers.

**Key Point:** If FinalPerf >= 0.3, thermal death does NOT trigger at all. The creature survives the temperature check but may still die from natural causes (Step 6).

---

### Step 5: Reproduction (Birth) with Accumulator

**Requirements:**
- Population >= 2 (need at least 2 to reproduce)
- FinalPerf >= ReproThreshold (0.25)

**Birth Calculation:**
```
births = Population × FinalPerformance × ReproductionMultiplier × BiologyStep
```

**Tier 1 Penalty (No Predator Penalty):**

When Tier 2 population is 0 (no predators exist), Tier 1 births are reduced by 15%:
```
If Tier == 1 AND TotalTier2Population == 0:
    births = births × 0.85
```

This simulates intraspecific competition and resource limitations that would naturally occur without predator pressure.

**Carrying Capacity (Soft Limit):**

Carrying capacity reduces birth rate as population approaches the environmental limit. This is a SOFT limit - it does NOT kill creatures, only slows reproduction.

```
growthFactor = max(0, 1 - (tierPopulation / CarryingCapacityPerTier))
births = births × growthFactor
```

**Effects:**
| Tier Population | Growth Factor | Effect |
|-----------------|---------------|--------|
| 0 (empty) | 1.0 | 100% birth rate |
| 2,500 (50% of 5000) | 0.5 | 50% birth rate |
| 4,000 (80% of 5000) | 0.2 | 20% birth rate |
| 5,000 (100% of 5000) | 0.0 | 0% birth rate (no new births) |

**Why Carrying Capacity Creates Oscillations:**
1. When prey population is low, they grow fast (high growth factor)
2. As prey approaches capacity, growth slows
3. Predators eat prey, reducing population
4. Prey drops below capacity, grows again
5. Classic predator-prey oscillation pattern emerges

**Birth Accumulator:**
```
birthAccumulator[species] += births
if birthAccumulator[species] >= 1.0:
    actualBirths = floor(birthAccumulator[species])
    birthAccumulator[species] -= actualBirths
    population += actualBirths
```

**Example:**
- Tier 2 Common: 2 creatures, FinalPerf = 0.7, ReproMult = 0.1
- births = 2 × 0.7 × 0.1 = 0.14
- Step 1: accumulator = 0.14
- Step 2: accumulator = 0.28
- ...
- Step 8: accumulator = 1.12 → **1 birth**, accumulator = 0.12

---

### Step 6: Natural Death with Performance Scaling and Accumulator

**Performance-Scaled Death Rate:**

Creatures with poor performance are more vulnerable to natural causes (disease, accidents, weakness).

**Division by Zero Safeguard:**

FinalPerformance can be 0 when:
- ThermalPerf = 0 (completely outside thermal range)
- FedRate = 0 (Tier 2 with no prey available)

To prevent division by zero, use a floor value:
```
safeFinalPerf = max(0.1, FinalPerformance)
```

**Rate Calculation:**
```
baseRate = NaturalDeathRate + random(-Variance, +Variance)
effectiveRate = baseRate × (1 / safeFinalPerf)
effectiveRate = max(0, effectiveRate)  // Can't be negative
```

**Examples:**

| FinalPerf | safeFinalPerf | Effective Rate (Tier 1, 2% base) | Effective Rate (Tier 2, 3% base) |
|-----------|---------------|----------------------------------|----------------------------------|
| 0.0 | 0.1 | 2% × 10 = **20%** | 3% × 10 = **30%** |
| 0.1 | 0.1 | 2% × 10 = **20%** | 3% × 10 = **30%** |
| 0.35 | 0.35 | 2% × 2.86 = **5.7%** | 3% × 2.86 = **8.6%** |
| 0.5 | 0.5 | 2% × 2 = **4%** | 3% × 2 = **6%** |
| 0.7 | 0.7 | 2% × 1.43 = **2.9%** | 3% × 1.43 = **4.3%** |
| 1.0 | 1.0 | 2% × 1 = **2%** | 3% × 1 = **3%** |

The 0.1 floor caps maximum natural death rate at 10× base rate - aggressive but not instant extinction.

**Death Calculation:**
```
deaths = Population × effectiveRate × BiologyStep
```

**Natural Death Accumulator:**
```
naturalDeathAccumulator[species] += deaths
if naturalDeathAccumulator[species] >= 1.0:
    actualDeaths = floor(naturalDeathAccumulator[species])
    naturalDeathAccumulator[species] -= actualDeaths
    population -= actualDeaths
```

**Example - Solving the "Immortal Creature" Problem:**

Arctic Hexapod, Population = 1, Temperature = 20°C:
- ThermalPerf = 0.35 (above 0.3, so no thermal death)
- FinalPerf = 0.35
- Base natural death = 2%
- Effective rate = 2% × (1/0.35) = 5.7%
- Deaths per step = 1 × 5.7% = 0.057

With accumulator:
- Step 1: accumulator = 0.057
- Step 10: accumulator = 0.57
- Step 18: accumulator = 1.03 → **creature dies!**

The Arctic creature dies after ~18 steps because the harsh temperature weakened it, making it vulnerable to natural causes. No creature is immortal.

---

### Step 7: Population Rounding

All populations are rounded to integers for display and the next cycle.

```
population = round(population)
```

---

## Accumulator Summary

| Mechanic | Has Accumulator? | Why? |
|----------|------------------|------|
| **Birth** | ✅ YES | Slow reproducers (Tier 2) need fractional births to accumulate |
| **Predation** | ✅ YES | Rare variants need fractional predation to accumulate |
| **Natural Death** | ✅ YES | Small populations need fractional deaths to accumulate |
| **Thermal Death** | ❌ NO | MinimumDeaths=1 ensures at least 1 dies when triggered |

---

## CSV Output Columns

### Core Columns
| Column | Type | Description |
|--------|------|-------------|
| Day | int | Simulation day (1-365+) |
| Year | int | Simulation year |
| Temperature | float | Current temperature in °C |
| BiologyCycle | int | Biology step number |
| StartPop | int | Total population at start of step |
| Tier1Pop, Tier2Pop | int | Population by tier |
| Tier1Arctic, Tier1Common, Tier1Tropical | int | Tier 1 variants |
| Tier2Arctic, Tier2Common, Tier2Tropical | int | Tier 2 variants |
| EatenT1 | int | Prey eaten (= Tier 1 deaths from predation) |
| TempDeathsT1, TempDeathsT2 | int | Thermal deaths |
| NaturalDeathsT1, NaturalDeathsT2 | int | Natural mortality deaths |
| TotalDeaths | int | Sum of all deaths |
| BirthsT1, BirthsT2 | int | New offspring |
| FedRateT2 | float | Tier 2 feeding satisfaction |
| AvgHuntingEff | float | Average hunting efficiency |
| EndPop | int | Total population at end of step |

### Accumulator Columns
| Column | Type | Description |
|--------|------|-------------|
| BirthAccumT1 | float | Birth accumulator total for Tier 1 |
| BirthAccumT2 | float | Birth accumulator total for Tier 2 |
| NaturalDeathAccumT1 | float | Natural death accumulator total for Tier 1 |
| NaturalDeathAccumT2 | float | Natural death accumulator total for Tier 2 |
| PredationAccumT1 | float | Predation accumulator total for Tier 1 |

---

## How Problems Are Solved

### Problem 1: Tier 2 Never Reproduces
**Cause:** 0.14 births per step rounds to 0
**Solution:** Birth accumulator carries over fractional births

### Problem 2: Rare Variants Never Eaten
**Cause:** When 1 Arctic exists among 99 Common, 0.1 share rounds to 0
**Solution:** Predation accumulator carries over fractional predation

### Problem 3: Last Creature is Immortal
**Cause:** 2% of 1 = 0.02 deaths rounds to 0
**Solution:** Natural death accumulator + performance scaling:
- Accumulator carries over fractional deaths
- Poor performance increases death rate (5.7% instead of 2%)
- Creature dies in ~18 steps instead of never

### Problem 4: Creatures Survive Harsh Temperatures
**Cause:** FinalPerf = 0.35 is above 0.3 threshold, so no thermal death
**Solution:** Performance-scaled natural death means harsh temperatures indirectly cause faster death through increased natural mortality

---

## Expected Behavior

1. **Early simulation:** All variants start, Arctic/Tropical struggle at 20°C
2. **Arctic/Tropical decline:** Higher natural death rate due to poor performance
3. **Stragglers die:** Last 1-2 Arctic/Tropical creatures eventually die via accumulator
4. **Common dominates:** Tier 1 and Tier 2 Common thrive at optimal temperature
5. **Predator-prey cycles:** Tier 2 grows, eats Tier 1, Tier 1 declines, Tier 2 starves, etc.
6. **Seasonal shifts:** As temperature changes seasonally, different variants may become dominant
