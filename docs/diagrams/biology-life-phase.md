# Biology life phase (Steps 8–9)

Source: `EcosystemSimulator.ApplyReproduction` (v9: Pmax multiplier), `EcosystemSimulator.ApplyNaturalDeathWithAccumulator`.

```mermaid
flowchart TD
    Start([Start life phase]) --> Guard{"sp.Population &lt; MIN_POPULATION_FOR_REPRODUCTION (2)?"}
    Guard -- yes --> SkipRepro[skip reproduction]
    Guard -- no --> ThreshCheck{"ReproThreshold check"}
    ThreshCheck -- "thresh ≥ 1.0 (edge)" --> EdgeA["reproScale = STRUGGLING_REPRO_RATE × Condition"]
    ThreshCheck -- "thresh ≤ 0 (edge)" --> EdgeB["reproScale = Condition"]
    ThreshCheck -- "Cond ≥ thresh" --> Healthy["t = (Cond − thresh) / (1 − thresh)<br/>reproScale = STRUGGLING_REPRO_RATE + (1 − STRUGGLING_REPRO_RATE) × t<br/>ramps 0.10 → 1.0"]
    ThreshCheck -- "Cond &lt; thresh" --> Struggling["reproScale = STRUGGLING_REPRO_RATE × (Cond / thresh)<br/>ramps 0 → 0.10"]
    Healthy --> Clamp["reproScale = clamp(reproScale, 0, 1)"]
    Struggling --> Clamp
    EdgeA --> Clamp
    EdgeB --> Clamp
    Clamp --> Births["births = Pop × reproScale × ReproductionMultiplier × Pmax × BiologyStep"]
    Births --> T1NoPred{"Tier == 1 AND<br/>GetTierPopulation(2) &lt; MIN_ALIVE_POP?"}
    T1NoPred -- yes --> NPP["births *= NO_PREDATOR_PENALTY (0.85)"]
    T1NoPred -- no --> Cap{"Tier == 1 AND<br/>UseCarryingCapacity?"}
    NPP --> Cap
    Cap -- yes --> SoftCap["tierPop = GetTierPopulation(1)  (live — includes prior species' births)<br/>growthFactor = max(0, 1 − tierPop / CarryingCapacityPerTier)<br/>births *= growthFactor"]
    Cap -- no --> BAcc
    SoftCap --> BAcc["_birthAccumulators[sp.FullName] += births<br/>accumulated = _birthAccumulators[sp.FullName]"]
    BAcc --> Whole["wholeBirths = floor(accumulated)<br/>residual = accumulated − wholeBirths<br/>_birthAccumulators[sp.FullName] = residual"]
    Whole --> AddPop["sp.Population += wholeBirths"]
    AddPop --> Dilute{"wholeBirths > 0 AND<br/>Pop > 0?"}
    Dilute -- yes --> NewCond["newCond = (oldPop × oldCond + wholeBirths × NEWBORN_CONDITION) / newPop<br/>(NEWBORN_CONDITION = 0.5)"]
    Dilute -- no --> S9
    NewCond --> S9
    SkipRepro --> S9
    S9["Step 9: Natural Death<br/>rate = NaturalDeathRate ± uniform(-NaturalDeathVariance, +NaturalDeathVariance)<br/>rate = max(0, rate)<br/>rawDeaths = Pop × rate × BiologyStep"] --> NAcc["_naturalDeathAccumulators[sp.FullName] += rawDeaths<br/>apply floor(accum) whole deaths"]
    NAcc --> Next[next species]
```

## Reproduction key invariants

- **Two-region piecewise formula** for `reproScale`, continuous at `ReproThreshold` (both halves meet at `STRUGGLING_REPRO_RATE = 0.10`).
- **Pmax multiplier (v9)**: `births *= sp.Pmax`. A specialist (Pmax = 0.9) produces 25% more births at the same Condition as a generalist (Pmax = 0.72).
- **No predator penalty** is 0.85 — Tier 1 gets 15% fewer births when no predators exist, modelling ecosystem imbalance.
- **Soft carrying cap**: `growthFactor = max(0, 1 − tierPop / CarryingCapacityPerTier)`. Tier 1 only. `tierPop` is evaluated live, so the first species processed sees a slightly lower `tierPop` than the last — a known first-mover artefact.
- **Birth accumulator** carries fractional births. Matters at low `reproScale` or low population, where daily births < 1.
- **Newborn dilution**: adding offspring at `NEWBORN_CONDITION = 0.5` pulls the group's average Condition down toward 0.5 if many newborns enter at once.

## Natural death key invariants

- Independent of temperature, Condition, feeding, and predation.
- Rate is re-rolled per species per step from `[NaturalDeathRate − NaturalDeathVariance, NaturalDeathRate + NaturalDeathVariance]`.
- Uses its own accumulator; fractional deaths carry to the next day.
- Natural death does **not** give a survivor fitness boost (unlike condition death). It models stochastic mortality rather than health-driven.

## Step order within a single species

Species loops are nested: each step iterates all species before moving on. So reproduction (8) and natural death (9) for a given species are not adjacent in wall time — Step 8 completes for **every** species before Step 9 begins.
