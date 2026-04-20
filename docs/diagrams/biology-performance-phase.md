# Biology performance phase (Steps 1–5)

Source: `SimSpecies.CalculatePerformance`, `EcosystemSimulator.ProcessFeeding`, `EcosystemSimulator.UpdateCondition` (v9 Pmax rate scaling).

```mermaid
flowchart TD
    Temp["Temperature °C"] --> Offset["T_eff = T + sp.TemperatureDebuff"]
    Offset --> Fade["Cosine fade over<br/>LETHAL_TRANSITION_WIDTH = 2°C<br/>near CTminC / CTmaxC"]
    Fade --> Arr["Arrhenius in Kelvin:<br/>numerator = exp(B/OT − B/T) · (1 + exp(L/OT − L/LB) + exp(U/UB − U/OT))<br/>denominator = 1 + exp(L/T − L/LB) + exp(U/UB − U/T)<br/>perf = clamp(num/den, 0, 1)"]
    Arr --> Raw["RawThermalPerformance<br/>= perf × fade"]
    Raw --> TP["ThermalPerformance<br/>= Raw × Pmax"]

    TP --> PredDemand["Predator rawDemand<br/>= Pop × EatingAmount × ThermalPerf × BiologyStep"]
    PredDemand --> Holling["Holling II success<br/>holling = ratio / (ratio + halfSat)<br/>halfSat = NORMAL_PREY_RATIO · (1 − base) / base<br/>+ variance in [-HuntingVariance, +HuntingVariance]<br/>clamp to [0, 1]"]
    Holling --> Actual["actualDemand = rawDemand × huntingSuccess"]
    Actual --> Eaten["totalEaten = min(availablePrey, Σ actualDemand)"]
    Eaten --> FedRate["FedRate (predator) = totalEaten / totalRawDemand<br/>FedRate (prey) = 1.0 always"]

    Raw --> RFP["RawFinalPerformance<br/>= Raw × FedRate<br/>(Condition drain target)"]
    FedRate --> RFP

    RFP --> CondStep{"Condition vs target<br/>(pmaxSafe = max(Pmax, 1e-4))"}
    CondStep -- "Cond > target" --> Drain["severity = (1 - target)²<br/>effectiveDrain = ConditionDrainRate · (1 + severity) / pmaxSafe<br/>Condition -= (Condition - target) × effectiveDrain"]
    CondStep -- "Cond < target" --> Recov["boost = target²<br/>effectiveRecovery = ConditionRecoveryRate · (1 + boost) × pmaxSafe<br/>Condition += (target - Condition) × effectiveRecovery"]
    Drain --> Clamp["Condition = clamp(Condition, 0, 1)"]
    Recov --> Clamp
    Clamp --> FP["FinalPerformance<br/>= ThermalPerf × FedRate<br/>(logging only)"]
```

## Key invariants

- `Pmax` does **not** enter `target`. Target stays at `Raw × FedRate`, so Condition ceiling is 1.0 for every species regardless of Pmax.
- Drain is **asymmetric**: base drain `0.15` > base recovery `0.10` (per-day rates from `SimulationConfig`).
- Quadratic acceleration:
  - Drain multiplier = `1 + (1 − target)²` → reaches 2× at `target = 0`.
  - Recovery multiplier = `1 + target²` → reaches 2× at `target = 1`.
- Pmax rate scaling (v9):
  - Specialists (high Pmax, e.g. 0.9) drain `1 / 0.9 ≈ 1.11×` the base rate (slower relative reduction because denominator is closer to 1).
  - Generalists (low Pmax, e.g. 0.72) drain `1 / 0.72 ≈ 1.39×` the base rate (faster decline under stress).
  - Recovery inverts: specialists recover `× 0.9` ≈ 10% slower; generalists `× 0.72` ≈ 28% slower. (Multiplier ≤ 1 means the recovery term is damped when Pmax < 1.)

## Step 2 — feeding details

- Runs only if `Species.Any(Tier == 1)` and `Species.Any(Tier == 2)` with non-zero populations.
- `NORMAL_PREY_RATIO = 20` is the prey:predator ratio where Holling success equals the species' `HuntingEfficiency`.
- Per-predator `FedRate` is the same `fedRate` (totalEaten / totalRawDemand) — not split by predator. Variance is on hunting success, not on feeding satisfaction.
- Prey removals are distributed proportionally across prey variants via `_predationAccumulators[preyVariant.FullName]`.
