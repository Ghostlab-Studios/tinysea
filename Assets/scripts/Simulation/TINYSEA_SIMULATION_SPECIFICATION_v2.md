# TinySea Simulation Specification v2

## Overview

TinySea is a marine ecosystem simulation that models population dynamics of species across multiple trophic tiers under varying temperature conditions. This document specifies the complete simulation logic for the research version.

**Purpose**: Study climate change impacts on marine food web stability through batch simulations.

**Key Design Decisions:**
- **Population Type**: Float for precision (allows fractional accumulation like 2.19 → 2.41 → 2.65)
- **Prey Removal**: Proportional to population share
- **Biology Step**: Configurable (default: 1 = daily for research accuracy)
- **Extinction Check**: Population < 1.0 = extinct
- **No Rounding**: All calculations use float precision, CSV shows float values

---

## Table of Contents

1. [Configuration Parameters](#configuration-parameters)
2. [Population Control](#population-control-parameters)
3. [Temperature System](#temperature-system)
4. [Species Structure](#species-structure)
5. [Thermal Performance](#thermal-performance)
6. [Biology Step Sequence](#biology-step-sequence)
7. [Feeding Mechanics](#feeding-mechanics)
8. [Death Mechanics](#death-mechanics)
9. [Reproduction Mechanics](#reproduction-mechanics)
10. [Simulation Loop](#simulation-loop)
11. [CSV Output Format](#csv-output-format)
12. [Formula Reference](#formula-reference)

---

## Configuration Parameters

### Time Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| `BiologyStep` | 1 | 1-5 | Days between biology calculations |
| `MaxYears` | 1 | 1-500 | Simulation duration in years |
| `DaysPerYear` | 365 | Fixed | Days in a year |

### Why BiologyStep = 1 (Daily) for Research

The original game used `BiologyStep = 5` for turn-based gameplay. For research simulation, **daily biology is more accurate**:

1. **Temperature Response**: Organisms respond to each day's temperature
2. **Feedback Loops**: Population changes immediately affect next day
3. **Cold/Heat Spikes**: Short temperature events are captured
4. **Research Accuracy**: Continuous dynamics match real ecosystems

**Configurable**: Set `BiologyStep = 5` to compare with original game behavior.

### Population Control Parameters

These settings prevent unrealistic infinite population growth. Without population limits, Tier 1 grows exponentially because reproduction scales with population while predation only scales with predator count.

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| `UseCarryingCapacity` | true | bool | Enable logistic growth model |
| `CarryingCapacityPerTier` | 5000 | 100-100000 | Max sustainable population per tier |
| `UseDensityDeath` | true | bool | Enable overcrowding mortality |
| `DensityDeathThreshold` | 4000 | 100-100000 | Population above which density deaths begin |
| `MaxDensityDeathRate` | 0.1 | 0-0.5 | Maximum daily death rate from overcrowding |

#### Carrying Capacity (Logistic Growth Model)

Reduces birth rate as tier population approaches environmental carrying capacity.

**Formula:**
```
growthFactor = max(0, 1 - tierPopulation / carryingCapacity)
actualBirths = rawBirths × growthFactor
```

**Effect:**
- At 0% capacity: 100% birth rate (no reduction)
- At 50% capacity: 50% birth rate  
- At 100% capacity: 0% birth rate (no new births)

**Biological Interpretation:** Simulates limited food, limited space, and intra-species competition. As population increases, resources become scarce and reproduction decreases.

#### Density-Dependent Mortality

Adds extra deaths when tier population exceeds comfortable threshold. Applies to ALL species in the tier, even thermally-healthy ones.

**Formula:**
```
if (tierPop > threshold):
    excessRatio = (tierPop - threshold) / threshold
    densityDeathRate = min(maxRate, excessRatio × maxRate)
    deaths = population × densityDeathRate × biologyStep
```

**Effect:**
- At 1× threshold: 0% density death rate
- At 2× threshold: maxRate death rate (default 10%)

**Biological Interpretation:** Simulates disease spread, overcrowding stress, and resource depletion. High population density leads to increased mortality.

#### Recommended Settings

For stable ecosystems with realistic dynamics:

| Scenario | CarryingCap | DensityThreshold | MaxDeathRate |
|----------|-------------|------------------|--------------|
| Small ecosystem | 1000 | 800 | 0.1 |
| **Default (balanced)** | **5000** | **4000** | **0.1** |
| Large ecosystem | 20000 | 15000 | 0.1 |
| Aggressive control | 5000 | 2500 | 0.15 |

---

## Temperature System

*Temperature calculation is handled by TemperatureCalculator class (previously implemented). This section documents the interface.*

### Interface

```csharp
float GetTemperature(int dayIndex)  // Returns temperature in Celsius for given day
```

### Parameters (Configurable via SimulationConfig)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `BaseTemperature` | 20°C | Mean annual temperature |
| `SeasonalAmplitude` | 10°C | Summer/winter swing |
| `ClimateChangeRate` | 1°C/year | Long-term warming trend |
| `DailyVariation` | 2°C | Day-to-day noise range |
| `MinBound` | -5°C | Minimum allowed temperature |
| `MaxBound` | 50°C | Maximum allowed temperature |

---

## Species Structure

### Trophic Tiers

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   TIER 3 (Apex Predators)      ← Eats Tier 2               │
│   [Future: Rooda, Sploof, Silu-Silu]                        │
│                                                             │
│         │                                                   │
│         ▼ EATS                                              │
│                                                             │
│   TIER 2 (Predators)           ← Eats Tier 1               │
│   Sheplik (Arctic, Common, Tropical)                        │
│                                                             │
│         │                                                   │
│         ▼ EATS                                              │
│                                                             │
│   TIER 1 (Primary Producers)   ← Does not eat              │
│   Hexapod (Arctic, Common, Tropical)                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Key Rule**: Each tier eats the tier directly below it. Tier 1 does not eat (photosynthesis).

### Thermal Variants

Each species has three thermal variants optimized for different temperatures:

| Variant | Optimal Temp | Optimal (Kelvin) | Comfortable Range |
|---------|--------------|------------------|-------------------|
| Arctic | ~5°C | 278.15K | -3°C to 7°C |
| Common | ~20°C | 293.15K | 12°C to 22°C |
| Tropical | ~35.5°C | 308.65K | 27°C to 37°C |

### Species Parameters (from CSV)

#### Hexapod (Tier 1 - Producer)

| Parameter | Value | Description |
|-----------|-------|-------------|
| `EatingAmount` | 0 | Does not eat (photosynthesis) |
| `ReproductionMultiplier` | 0.45 | High birth rate |
| `DeathThreshold` | 0.3 | Dies if FinalPerf < 0.3 |
| `DeathRate` | 0.6 | 60% die per step when below threshold |
| `MinimumDeaths` | 1 | At least 1 dies when dying |
| `ReproThreshold` | 0.25 | Can reproduce if FinalPerf >= 0.25 |

#### Sheplik (Tier 2 - Predator)

| Parameter | Value | Description |
|-----------|-------|-------------|
| `EatingAmount` | 1.5 | Prey consumed per creature per step |
| `ReproductionMultiplier` | 0.1 | Low birth rate (10x slower than Tier 1) |
| `DeathThreshold` | 0.3 | Dies if FinalPerf < 0.3 |
| `DeathRate` | 0.3 | 30% die per step when below threshold |
| `MinimumDeaths` | 1 | At least 1 dies when dying |
| `ReproThreshold` | 0.25 | Can reproduce if FinalPerf >= 0.25 |

### Thermal Curve Parameters (from CSV)

| Parameter | Arctic | Common | Tropical |
|-----------|--------|--------|----------|
| `OptimalTempK` | 278.15 | 293.15 | 308.65 |
| `ArrhenBreadth` | 5273.15 | 5273.15 | 5273.15 |
| `ArrhenLower` | 10273.15 | 10273.15 | 10273.15 |
| `ArrhenUpper` | 21273.15 | 21273.15 | 21273.15 |
| `LowerBoundK` | 270.15 | 285.15 | 300.15 |
| `UpperBoundK` | 280.15 | 295.15 | 310.15 |

### Initial Population (Default)

| Tier | Species | Variant | Initial Count |
|------|---------|---------|---------------|
| 1 | Hexapod | Arctic | 4 |
| 1 | Hexapod | Common | 4 |
| 1 | Hexapod | Tropical | 4 |
| 2 | Sheplik | Arctic | 2 |
| 2 | Sheplik | Common | 2 |
| 2 | Sheplik | Tropical | 2 |
| **Total** | | | **18** |

*Note: Minimum 2 required per species for reproduction.*

---

## Thermal Performance

### Formula (Arrhenius-based)

```
T = TemperatureCelsius + 273.15  (convert to Kelvin)

numerator = exp(B/OT - B/T) × (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)

ThermalPerf = numerator / denominator
ThermalPerf = Clamp(ThermalPerf, 0, 1)
```

Where:
- `T` = Current temperature in Kelvin
- `OT` = OptimalTempK
- `B` = ArrhenBreadth
- `L` = ArrhenLower
- `U` = ArrhenUpper
- `LB` = LowerBoundK
- `UB` = UpperBoundK

### Performance Curve Visualization

```
Performance
    1.0 ┤                    ****
        │                 ***    ***
        │               **          **
    0.8 ┤             **              **
        │            *                  *
        │           *                    *
    0.6 ┤          *                      *
        │         *                        *
        │        *                          *
    0.4 ┤       *                            *
        │      *                              *
    0.3 ┤─────*───────────────────────────────*───── Death Threshold
        │    *                                  *
   0.25 ┤───*─────────────────────────────────────*── Repro Threshold
        │  *                                      *
    0.0 ┼*──────────────────────────────────────────*────
             │              │              │
          Too Cold    Optimal Temp    Too Hot
```

### Example Performance Values at 20°C

| Variant | ThermalPerf | Status |
|---------|-------------|--------|
| Arctic | 0.12 | Below death threshold (dies) |
| Common | 0.98 | Optimal (thrives) |
| Tropical | 0.33 | Above death threshold (survives) |

---

## Biology Step Sequence

**CRITICAL: This exact order must be followed.**

```
┌─────────────────────────────────────────────────────────────────────┐
│  BIOLOGY STEP (runs every BiologyStep days)                         │
│                                                                      │
│  Input: Current temperature                                          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 1: CALCULATE THERMAL PERFORMANCE                               │
│                                                                      │
│  For EACH species:                                                   │
│      ThermalPerf = CalculateArrhenius(temperature, speciesParams)   │
│      FedRate = 1.0  (initialize)                                    │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 2: FEEDING (Bottom-up by tier)                                 │
│                                                                      │
│  For tier = 2 to MaxTier:                                           │
│      predators = species where Tier == tier AND Population > 0      │
│      prey = species where Tier == tier-1 AND Population > 0         │
│                                                                      │
│      If predators exist AND prey exist:                             │
│          totalDemand = Σ(pred.Pop × pred.EatingAmt × pred.ThermalPerf × BiologyStep) │
│          availablePrey = Σ(prey.Population)                         │
│                                                                      │
│          fedRate = min(1.0, availablePrey / totalDemand)            │
│          Apply fedRate to all predators of this tier                │
│                                                                      │
│          totalEaten = min(availablePrey, totalDemand)               │
│          Remove prey PROPORTIONALLY (see Feeding Mechanics)         │
│                                                                      │
│  Tier 1 always has FedRate = 1.0 (does not eat)                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 3: CALCULATE FINAL PERFORMANCE                                 │
│                                                                      │
│  For EACH species:                                                   │
│      FinalPerf = ThermalPerf × FedRate                              │
│                                                                      │
│  Tier 1: FinalPerf = ThermalPerf × 1.0 = ThermalPerf                │
│  Tier 2+: FinalPerf = ThermalPerf × FedRate (may be < ThermalPerf)  │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 4: DEATH CHECK                                                 │
│                                                                      │
│  For EACH species:                                                   │
│      If FinalPerf < DeathThreshold (0.3):                           │
│          deaths = max(MinDeaths, Population × DeathRate × BiologyStep) │
│          deaths = min(deaths, Population)                           │
│          Population -= deaths                                        │
│          Track: totalDeaths += deaths                               │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 5: DENSITY-DEPENDENT DEATH (if enabled)                        │
│                                                                      │
│  For each tier:                                                      │
│      tierPop = Σ(all species populations in tier)                   │
│                                                                      │
│      If tierPop > DensityDeathThreshold:                            │
│          excessRatio = (tierPop - threshold) / threshold            │
│          densityDeathRate = min(maxRate, excessRatio × maxRate)     │
│                                                                      │
│          For EACH species in tier:                                  │
│              deaths = Population × densityDeathRate × BiologyStep   │
│              Population -= deaths                                    │
│              Track: totalDeaths += deaths                           │
│                                                                      │
│  Simulates: disease, overcrowding stress, resource depletion        │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 6: REPRODUCTION CHECK                                          │
│                                                                      │
│  For EACH species:                                                   │
│      If Population >= 2 AND FinalPerf >= ReproThreshold (0.25):     │
│          births = Population × FinalPerf × ReproMult × BiologyStep  │
│                                                                      │
│          // Special rule for Tier 1                                 │
│          If Tier == 1 AND TotalTier2Population < 1.0:               │
│              births = births × 0.85  (15% reduction)                │
│                                                                      │
│          // Carrying Capacity (if enabled)                          │
│          If UseCarryingCapacity:                                    │
│              tierPop = Σ(all species populations in tier)           │
│              growthFactor = max(0, 1 - tierPop / carryingCapacity)  │
│              births = births × growthFactor                         │
│                                                                      │
│          Population += births                                        │
│          Track: totalBirths += births                               │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  END OF BIOLOGY STEP                                                 │
│                                                                      │
│  Output: totalEaten, totalDeaths, totalBirths, fedRates             │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Feeding Mechanics

### Key Principles

1. **Pool-based**: All predators of a tier share a common prey pool
2. **Any predator eats any prey**: Arctic Sheplik can eat Common Hexapod
3. **Proportional removal**: Prey removed based on population share
4. **Shared FedRate**: All predators of a tier get the same FedRate

### Demand Calculation

```
For each predator species:
    Demand = Population × EatingAmount × ThermalPerf × BiologyStep

TotalDemand = Σ(all predator demands in tier)
```

### FedRate Calculation

```
AvailablePrey = Σ(all prey populations in tier below)

If TotalDemand <= 0:
    FedRate = 1.0
Else If AvailablePrey <= 0:
    FedRate = 0.0
Else:
    FedRate = min(1.0, AvailablePrey / TotalDemand)
```

### Prey Removal (PROPORTIONAL)

```
TotalEaten = min(AvailablePrey, TotalDemand)

For each prey species:
    share = PreyPopulation / AvailablePrey
    removed = TotalEaten × share
    PreyPopulation -= removed
```

**Why Proportional?** Matches original game behavior. Larger populations contribute more to the prey pool, so they lose more when predators feed.

### Feeding Example

```
PREDATORS (Tier 2):
  Sheplik Common: Pop=4, ThermalPerf=0.98, EatingAmt=1.5
  Sheplik Arctic: Pop=2, ThermalPerf=0.12, EatingAmt=1.5
  BiologyStep = 1

PREY (Tier 1):
  Hexapod Common: Pop=60
  Hexapod Tropical: Pop=30
  Hexapod Arctic: Pop=10
  Total = 100

DEMAND:
  Sheplik Common: 4 × 1.5 × 0.98 × 1 = 5.88
  Sheplik Arctic: 2 × 1.5 × 0.12 × 1 = 0.36
  Total Demand = 6.24

FED RATE:
  FedRate = min(1.0, 100 / 6.24) = 1.0 (plenty of food)

PREY REMOVAL (Proportional):
  TotalEaten = min(100, 6.24) = 6.24
  
  Hexapod Common:   60/100 = 60% → loses 6.24 × 0.60 = 3.744 → Pop = 56.256
  Hexapod Tropical: 30/100 = 30% → loses 6.24 × 0.30 = 1.872 → Pop = 28.128
  Hexapod Arctic:   10/100 = 10% → loses 6.24 × 0.10 = 0.624 → Pop = 9.376
```

### Starvation Example

```
PREDATORS: Sheplik Common Pop=10, Demand = 10 × 1.5 × 0.98 × 1 = 14.7
PREY: Only 5 remaining

FedRate = min(1.0, 5 / 14.7) = 0.34 (not enough food!)

All prey consumed (TotalEaten = 5)
Sheplik FinalPerf = 0.98 × 0.34 = 0.33

Since 0.33 >= DeathThreshold (0.3): SURVIVES (barely)
Since 0.33 >= ReproThreshold (0.25): CAN REPRODUCE (slowly)
```

---

## Death Mechanics

### Trigger Condition

```
If FinalPerformance < DeathThreshold (0.3):
    Deaths occur
```

### Death Formula

```
deaths = max(MinimumDeaths, Population × DeathRate × BiologyStep)
deaths = min(deaths, Population)  // Cannot exceed population
Population -= deaths
```

**Note**: No rounding - deaths is float. Population can be fractional (e.g., 3.4 after deaths).

### Death Example

```
Hexapod Arctic at 35°C (way too hot):
  Population = 10.0
  ThermalPerf = 0.05
  FedRate = 1.0 (Tier 1)
  FinalPerf = 0.05
  
Since 0.05 < 0.3: DEATH OCCURS

deaths = max(1, 10.0 × 0.6 × 1) = max(1, 6.0) = 6.0
Population = 10.0 - 6.0 = 4.0
```

### Death Causes

| Cause | Condition |
|-------|-----------|
| Cold | ThermalPerf < threshold AND temp < optimal |
| Hot | ThermalPerf < threshold AND temp > optimal |
| Starvation | FedRate causes FinalPerf < threshold |
| Combined | Multiple factors |

---

## Reproduction Mechanics

### Requirements (ALL must be true)

1. `Population >= 2` (need pair to reproduce)
2. `FinalPerformance >= ReproThreshold (0.25)`

### Reproduction Formula

```
If requirements met:
    births = Population × FinalPerf × ReproductionMultiplier × BiologyStep
    
    // Special rule for Tier 1 only
    If Tier == 1 AND TotalTier2Population == 0:
        births = births × 0.85  // 15% reduction
    
    Population += births
```

**Note**: No rounding - births is float. This allows gradual population growth (e.g., 2.0 → 2.19 → 2.41).

### Why Tier 1 Penalty?

Without predators, Tier 1 would grow unchecked. The 15% reduction simulates:
- Intraspecific competition
- Resource limitations
- Natural population regulation

### Reproduction Example (Tier 1 - High Reproduction)

```
Hexapod Common at 20°C:
  Population = 20.0
  ThermalPerf = 0.98
  FedRate = 1.0
  FinalPerf = 0.98
  ReproMult = 0.45
  BiologyStep = 1

Requirements: Pop >= 2 ✓, FinalPerf >= 0.25 ✓

births = 20.0 × 0.98 × 0.45 × 1 = 8.82

If Tier 2 exists: Population = 20.0 + 8.82 = 28.82
If no Tier 2:     births = 8.82 × 0.85 = 7.497, Population = 27.497
```

### Reproduction Example (Tier 2 - Low Reproduction)

```
Sheplik Common (well-fed):
  Population = 4.0
  ThermalPerf = 0.98
  FedRate = 1.0
  FinalPerf = 0.98
  ReproMult = 0.1
  BiologyStep = 1

Requirements: Pop >= 2 ✓, FinalPerf >= 0.25 ✓

births = 4.0 × 0.98 × 0.1 × 1 = 0.392
Population = 4.0 + 0.392 = 4.392
```

**Note**: Tier 2 reproduces 4.5x slower than Tier 1 (ReproMult 0.1 vs 0.45).

---

## Simulation Loop

### Pseudocode

```
Initialize:
    day = 0
    populations = InitialPopulations (float)
    crashed = false

While day < MaxDays AND NOT crashed:
    day += 1
    
    // TEMPERATURE (every day)
    temperature = TemperatureCalculator.GetTemperature(day - 1)
    
    // BIOLOGY (every BiologyStep days)
    If day % BiologyStep == 0:
        biologyCycle += 1
        RunBiologyStep(temperature)
    
    // RECORD (every day)
    RecordDailyData(day, temperature, populations, biologyCycle)
    
    // CHECK CRASH
    If Tier1Population < 1 OR Tier2Population < 1:
        crashed = true
        crashDay = day

OutputResults()
```

### Crash Condition

Simulation ends when **any tier's total population drops below 1**:
- Tier 1 < 1: Ecosystem collapse (no food source)
- Tier 2 < 1: Predator extinction

Simulation ends when **any tier's total population drops below 1.0**:
- Tier 1 < 1.0: Ecosystem collapse (no food source)
- Tier 2 < 1.0: Predator extinction

---

## CSV Output Format

### Columns

| Column | Type | Description |
|--------|------|-------------|
| `Day` | int | Day number (1, 2, 3... 365) |
| `Year` | int | Year number (1, 2, 3...) |
| `Temperature` | float | Temperature in °C |
| `BiologyCycle` | int | Cumulative biology step count (0 if no biology this day) |
| `Tier1Pop` | float | Total Tier 1 population |
| `Tier2Pop` | float | Total Tier 2 population |
| `Tier1Arctic` | float | Hexapod Arctic population |
| `Tier1Common` | float | Hexapod Common population |
| `Tier1Tropical` | float | Hexapod Tropical population |
| `Tier2Arctic` | float | Sheplik Arctic population |
| `Tier2Common` | float | Sheplik Common population |
| `Tier2Tropical` | float | Sheplik Tropical population |
| `Eaten` | float | Prey consumed (biology days only) |
| `Deaths` | float | Deaths this step (biology days only) |
| `Births` | float | Births this step (biology days only) |
| `FedRate` | float | Tier 2 feeding satisfaction 0-1 (biology days only) |

### Example Output

```csv
Day,Year,Temperature,BiologyCycle,Tier1Pop,Tier2Pop,Tier1Arctic,Tier1Common,Tier1Tropical,Tier2Arctic,Tier2Common,Tier2Tropical,Eaten,Deaths,Births,FedRate
1,1,20.83,1,12.00,6.00,4.00,4.00,4.00,2.00,2.00,2.00,5.24,2.88,4.12,1.00
2,1,21.45,2,13.88,6.04,3.52,5.24,5.12,1.76,2.12,2.16,5.31,0.00,5.88,1.00
...
```

---

## Formula Reference

### Quick Reference Card

| Calculation | Formula |
|-------------|---------|
| **Thermal Performance** | `Arrhenius(T, params)` clamped to [0, 1] |
| **Demand** | `Pop × EatingAmt × ThermalPerf × BiologyStep` |
| **FedRate** | `min(1.0, AvailablePrey / TotalDemand)` |
| **Final Performance** | `ThermalPerf × FedRate` |
| **Deaths** | `max(MinDeaths, Pop × DeathRate × BiologyStep)` |
| **Births** | `Pop × FinalPerf × ReproMult × BiologyStep` |
| **Tier 1 Penalty** | `Births × 0.85` (when no Tier 2) |

### Key Thresholds

| Threshold | Value | Purpose |
|-----------|-------|---------|
| DeathThreshold | 0.3 | FinalPerf below this = death |
| ReproThreshold | 0.25 | FinalPerf above this = can reproduce |
| MinPopulation | 2 | Required to reproduce |
| ExtinctionThreshold | 1.0 | Population below this = extinct |
| Tier1Penalty | 0.85 | Reproduction reduction when no predators |

---

## Implementation Notes

### Population as Float

Populations are stored as **float** for calculation precision. This allows:
- Fractional accumulation over time (e.g., 2.0 → 2.19 → 2.41 → 2.65 → 2.91 → 3.19)
- Smooth population dynamics without rounding artifacts
- Small populations can eventually grow (0.19 births per day accumulates)

**CSV Output**: Float values shown with 2 decimal places (e.g., 12.45)

**Extinction**: Species is considered extinct when `Population < 1.0`

**Interpretation**: The float value represents the statistical average population. A value of 2.65 means "between 2 and 3 creatures, trending toward 3."

### Extinction Check

Species is considered extinct when `Population < 1.0` (not exactly 0, since populations are float).

### Random Number Generation

Use seeded RNG for reproducible simulations:
```csharp
var rng = new System.Random(seed);
```

### Future Extensions

- **Tier 3**: Apex predators (Rooda, Sploof, Silu-Silu)
- **Additional Species**: More Tier 1/2 species
- **Population Cap**: Maximum limits per species
- **Multi-year**: Extended simulation runs (MaxYears > 1)
