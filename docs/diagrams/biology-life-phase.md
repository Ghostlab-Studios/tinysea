# Biology life phase (Steps 8–9)

Source: `EcosystemSimulator.ApplyReproduction` (v9: Pmax multiplier; v10: soft-cap-on-births deleted, newborns inherit parent Condition), `EcosystemSimulator.ApplyNaturalDeathWithAccumulator`.

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
    T1NoPred -- no --> BAcc
    NPP --> BAcc["_birthAccumulators[sp.FullName] += births<br/>accumulated = _birthAccumulators[sp.FullName]"]
    BAcc --> Whole["wholeBirths = floor(accumulated)<br/>residual = accumulated − wholeBirths<br/>_birthAccumulators[sp.FullName] = residual"]
    Whole --> AddPop["sp.Population += wholeBirths<br/>(newborns inherit group Condition; v10 — no explicit dilution step,<br/>population-weighted average is unchanged when newborns match group)"]
    AddPop --> S9
    SkipRepro --> S9
    S9["Step 9: Natural Death<br/>rate = NaturalDeathRate ± uniform(-NaturalDeathVariance, +NaturalDeathVariance)<br/>rate = max(0, rate)<br/>rawDeaths = Pop × rate × BiologyStep"] --> NAcc["_naturalDeathAccumulators[sp.FullName] += rawDeaths<br/>apply floor(accum) whole deaths"]
    NAcc --> Next[next species]
```

## Reproduction key invariants

- **Two-region piecewise formula** for `reproScale`, continuous at `ReproThreshold` (both halves meet at `STRUGGLING_REPRO_RATE = 0.10`).
- **Pmax multiplier (v9)**: `births *= sp.Pmax`. A specialist (Pmax = 0.9) produces 25% more births at the same Condition as a generalist (Pmax = 0.72).
- **No-predator penalty** is 0.85 — Tier 1 gets 15% fewer births when no predators exist, modelling ecosystem imbalance.
- **(v10) Soft cap on births DELETED.** Tier 1 is now throttled indirectly via the Condition pathway: high pop → low food density (Step 2a) → low FedRate → low RawFinalPerformance target → Condition drains → reproScale shrinks AND condition deaths fire. The processing-order bug from the live `tierPop` read is gone with this code path.
- **Birth accumulator** carries fractional births. Matters at low `reproScale` or low population, where daily births < 1.
- **(v10) Newborn Condition = parent group Condition.** No fixed `NEWBORN_CONDITION = 0.5` constant. Parent's Condition already encodes recent food / hunting history via lagged drain dynamics, so multiplying again by today's FedRate would double-count. Mathematically: the population-weighted average is unchanged when newborns match the group, so no explicit Condition update step is needed. Newborn vulnerability emerges from same-drain-no-head-start dynamics in subsequent days.

## Natural death key invariants

- Independent of temperature, Condition, feeding, and predation.
- Rate is re-rolled per species per step from `[NaturalDeathRate − NaturalDeathVariance, NaturalDeathRate + NaturalDeathVariance]`.
- Uses its own accumulator; fractional deaths carry to the next day.
- Natural death does **not** give a survivor fitness boost (unlike condition death). It models stochastic mortality rather than health-driven.

## Step order within a single species

Species loops are nested: each step iterates all species before moving on. So reproduction (8) and natural death (9) for a given species are not adjacent in wall time — Step 8 completes for **every** species before Step 9 begins.
