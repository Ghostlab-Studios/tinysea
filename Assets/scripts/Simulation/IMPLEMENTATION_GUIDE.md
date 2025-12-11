# TinySea v5 Implementation Guide

## Overview

This document explains how to integrate the v5 simulation code into your Unity project.

---

## Files to Replace

Copy these 6 files from `v5_complete/` to your Unity project's `Assets/Scripts/` folder:

| File | Description |
|------|-------------|
| `SimSpecies.cs` | Species data class with constants |
| `SpeciesDatabase.cs` | ScriptableObject for species configuration |
| `EcosystemSimulator.cs` | Main simulation logic (7 steps) |
| `SimulationRunner.cs` | CSV output and runner |
| `SimulationConfig.cs` | ScriptableObject for simulation settings |
| `SimulationController.cs` | Unity MonoBehaviour to run simulation |

**Unchanged files** (keep your existing versions):
- `TemperatureCalculator.cs`
- `ThermalGraphUI.cs`
- `SpeciesUIController.cs`

**Note:** Make sure to backup your existing files before replacing.

---

## What Changed from v4 to v5

### Removed Features
- ❌ **Density Death** - Completely removed
- ❌ `DensityDeathsT1`, `DensityDeathsT2` CSV columns

### Added Features
- ✅ **Predation Accumulator** - Fractional prey deaths carry over
- ✅ **Natural Death Accumulator** - Fractional natural deaths carry over
- ✅ **Performance-Scaled Natural Death** - Poor performance = higher mortality
- ✅ **Division by Zero Safeguard** - `max(0.1, FinalPerformance)` floor
- ✅ **Accumulator CSV Columns** - Track accumulator states
- ✅ **Long data type for populations** - Prevents integer overflow

### Kept Features
- ✅ **Carrying Capacity** - Soft limit that slows reproduction (does NOT kill)

### Modified Features
- 🔄 **Biology Sequence** - Now 7 steps (was 8, removed density death)
- 🔄 **Tier 1 Penalty** - Moved to constant in SimSpecies class
- 🔄 **CSV Output** - New accumulator columns, no density columns, uses long for populations

---

## Biology Sequence (7 Steps)

```
Step 1: Thermal Performance
        Calculate ThermalPerf using Arrhenius formula

Step 2: Feeding/Predation
        - Tier 2 hunts Tier 1 with hunting efficiency
        - Prey removed PROPORTIONALLY with PREDATION ACCUMULATOR
        - Calculate FedRate

Step 3: Final Performance
        FinalPerf = ThermalPerf × FedRate

Step 4: Thermal Death
        IF FinalPerf < 0.3:
          deaths = max(MinimumDeaths, Population × DeathRate)
        NO ACCUMULATOR - MinimumDeaths ensures at least 1 dies

Step 5: Reproduction
        IF Population >= 2 AND FinalPerf >= 0.25:
          births = Pop × FinalPerf × ReproMultiplier
          IF Tier 1 AND no predators: births × 0.85
          Apply CARRYING CAPACITY: births × (1 - tierPop/capacity)
        Uses BIRTH ACCUMULATOR

Step 6: Natural Death
        - Calculate base rate with variance
        - Scale by performance: effectiveRate = baseRate × (1 / safeFinalPerf)
        - safeFinalPerf = max(0.1, FinalPerformance)
        Uses NATURAL DEATH ACCUMULATOR

Step 7: Population Rounding
        All populations rounded to integers
```

---

## Key Constants

These are defined in `SimSpecies.cs`:

```csharp
public const float NO_PREDATOR_PENALTY = 0.85f;           // 15% birth reduction
public const float MIN_FINAL_PERF_FOR_NATURAL_DEATH = 0.1f; // Division safeguard
```

---

## Accumulator System

### How It Works

Accumulators carry over fractional values until they reach >= 1.0:

```
Step 1: 0.14 births → accumulator = 0.14 → 0 actual births
Step 2: 0.14 births → accumulator = 0.28 → 0 actual births
...
Step 8: 0.14 births → accumulator = 1.12 → 1 actual birth, accumulator = 0.12
```

### Three Accumulators

| Accumulator | Purpose |
|-------------|---------|
| **Birth** | Slow reproducers (Tier 2) can grow over time |
| **Predation** | Rare variants (1 Arctic among 99 Common) eventually get eaten |
| **Natural Death** | Small populations (1 creature) eventually die |

### Why No Thermal Death Accumulator?

Thermal death uses `MinimumDeaths = 1`, which guarantees at least 1 creature dies when the condition triggers. No accumulation needed.

---

## Performance-Scaled Natural Death

### Formula

```csharp
safeFinalPerf = max(0.1, FinalPerformance)
effectiveRate = baseRate × (1 / safeFinalPerf)
```

### Examples

| FinalPerf | safeFinalPerf | Base 2% | Effective Rate |
|-----------|---------------|---------|----------------|
| 1.0 | 1.0 | 2% | 2% (healthy) |
| 0.5 | 0.5 | 2% | 4% (struggling) |
| 0.35 | 0.35 | 2% | 5.7% (barely surviving) |
| 0.0 | 0.1 | 2% | 20% (critical, capped) |

### Why This Matters

An Arctic Hexapod at 20°C has `FinalPerf = 0.35`:
- Above death threshold (0.3), so no thermal death
- But 5.7% natural death rate instead of 2%
- At population = 1, accumulates 0.057 per step
- Dies after ~18 steps

**No more immortal creatures!**

---

## CSV Output Changes

### Removed Columns
- `DensityDeathsT1`
- `DensityDeathsT2`

### Added Columns
| Column | Type | Description |
|--------|------|-------------|
| `BirthAccumT1` | float | Birth accumulator total for Tier 1 |
| `BirthAccumT2` | float | Birth accumulator total for Tier 2 |
| `NaturalDeathAccumT1` | float | Natural death accumulator for Tier 1 |
| `NaturalDeathAccumT2` | float | Natural death accumulator for Tier 2 |
| `PredationAccumT1` | float | Predation accumulator for Tier 1 |

### Full Column Order
```
Day,Year,Temperature,BiologyCycle,
StartPop,EndPop,
Tier1Pop,Tier2Pop,
Tier1Arctic,Tier1Common,Tier1Tropical,
Tier2Arctic,Tier2Common,Tier2Tropical,
EatenT1,TempDeathsT1,TempDeathsT2,
NaturalDeathsT1,NaturalDeathsT2,
TotalDeaths,
BirthsT1,BirthsT2,
FedRateT2,AvgHuntingEff,
BirthAccumT1,BirthAccumT2,
NaturalDeathAccumT1,NaturalDeathAccumT2,
PredationAccumT1
```

---

## SpeciesDatabase Setup

After replacing the files:

1. Open your SpeciesDatabase asset in Unity
2. Right-click → "Populate Default Data"
3. This creates 6 species (3 Hexapod variants + 3 Sheplik variants)

### Default Parameters

**Tier 1 (Hexapod):**
- Initial count: 4
- Natural death: 2% ±1%
- Hunting: N/A

**Tier 2 (Sheplik):**
- Initial count: 2
- Natural death: 3% ±1.5%
- Hunting: 75% ±15%

---

## Integration Checklist

- [ ] Backup existing files
- [ ] Copy 6 files to `Assets/Scripts/`
- [ ] Re-create SimulationConfig asset (old one has removed settings)
- [ ] Re-populate SpeciesDatabase
- [ ] Test run simulation
- [ ] Verify CSV output has new columns
- [ ] Verify no density columns in CSV

---

## Troubleshooting

### Compiler errors about missing properties
If you see errors like `'EcosystemSimulator' does not contain a definition for 'UseCarryingCapacity'`, make sure you replaced **all 6 files** including `SimulationConfig.cs` and `SimulationController.cs`.

### "Missing DensityDeathsT1/T2"
If you have other scripts that reference density death columns, update them to remove those references.

### "Population grows too fast"
Without carrying capacity, populations can grow large. The predator-prey dynamics and natural death should balance this. If not, adjust the `ReproductionMultiplier` values in SpeciesDatabase.

---

## Expected Behavior

1. **Early simulation:** All 6 variants start, Arctic/Tropical struggle at 20°C
2. **Thermal stress:** Arctic/Tropical have higher natural death (performance-scaled)
3. **Stragglers eliminated:** Last 1-2 Arctic/Tropical die via accumulator
4. **Common dominates:** Tier 1 and Tier 2 Common thrive
5. **Predator-prey cycles:** Classic oscillations as predators eat prey
6. **No immortals:** Every creature eventually dies if conditions are poor
