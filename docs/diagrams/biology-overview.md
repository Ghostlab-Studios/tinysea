# Biology step — 10-step overview

Source: `EcosystemSimulator.ProcessBiologyStep(float temperature)`.

```mermaid
flowchart TD
    Start([ProcessBiologyStep temp]) --> S1["1. Thermal Performance<br/>SimSpecies.CalculatePerformance<br/>Arrhenius + CTmin/CTmax cosine fade<br/>RawThermalPerf; ThermalPerf = Raw × Pmax"]
    S1 --> S2["2. Feeding / Predation<br/>Holling Type II demand<br/>_predationAccumulators<br/>sets FedRate on predators"]
    S2 --> S3["3. Raw Final Performance<br/>RawFinalPerf = Raw × FedRate<br/>(Condition drain target)"]
    S3 --> S4["4. Update Condition<br/>asymmetric drift toward target<br/>rates scaled by Pmax (pmaxSafe)"]
    S4 --> S5["5. Final Performance<br/>FinalPerf = ThermalPerf × FedRate<br/>(computed for logging; not read downstream)"]
    S5 --> S6["6. Thermal Death<br/>instant wipeout when RawThermalPerf == 0"]
    S6 --> S7["7. Condition Death<br/>graduated severity below DeathThreshold<br/>survivor fitness boost<br/>_conditionDeathAccumulators"]
    S7 --> S8["8. Reproduction<br/>reproScale(Condition) × Pmax<br/>Tier 1 penalty, soft carrying cap<br/>_birthAccumulators + newborn dilution"]
    S8 --> S9["9. Natural Death<br/>flat NaturalDeathRate ± variance<br/>_naturalDeathAccumulators"]
    S9 --> S10["10. Population Rounding<br/>round away-from-zero"]
    S10 --> End([End step])
```

## Ordering and iteration

Every step iterates `Species` in the order they were added to `RunSpeciesList`. A species processed earlier in a step may influence the denominator for a species processed later (notably the soft carrying cap in Step 8, which reads `GetTierPopulation(1)` live after previous species' births have been added).

## What each step writes back

| Step | Writes | Notes |
|------|--------|-------|
| 1 | `RawThermalPerformance`, `ThermalPerformance` | Per species. |
| 2 | `FedRate` (predators), `CurrentHuntingSuccess`, `LastFedRateT2`, `LastAvgHuntingEfficiency`, `LastEatenT1`, accumulator updates on prey | Prey `FedRate` stays 1.0. |
| 3 | `RawFinalPerformance` | Per species. |
| 4 | `Condition` (clamped 0–1), `AvgConditionT1`/`AvgConditionT2` later | `oldCondition` logged. |
| 5 | `FinalPerformance` | Never read again in this pipeline. |
| 6 | `Population → 0`, `Condition → 0` when lethal | Also increments `LastTempDeathsT1`/`T2`. |
| 7 | `Population -= wholeDeaths`, `Condition` boosted, `LastConditionDeathsT1`/`T2`, `_conditionDeathAccumulators` | Survivor boost caps Condition at 1.0. |
| 8 | `Population += wholeBirths`, `Condition` diluted by newborns at 0.5, `LastBirthsT1`/`T2`, `_birthAccumulators`, `LastReproScaleT1`/`T2` | Tier 1 gets `NO_PREDATOR_PENALTY` and carrying-capacity multiplier. |
| 9 | `Population -= wholeNaturalDeaths`, `LastNaturalDeathsT1`/`T2`, `_naturalDeathAccumulators` | No Condition effect. |
| 10 | `Population = Math.Round(Population, AwayFromZero)` | All populations integer-valued between days. |

After Step 10, `ComputeAverageCondition()` + `UpdateAccumulatorTotals()` + `EndPopT1/T2` are updated for the CSV record.
