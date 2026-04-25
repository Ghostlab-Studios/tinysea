# Biology step — 10-step overview

Source: `EcosystemSimulator.ProcessBiologyStep(float temperature)`.

```mermaid
flowchart TD
    Start([ProcessBiologyStep temp]) --> S1["1. Thermal Performance<br/>SimSpecies.CalculatePerformance<br/>Arrhenius + CTmin/CTmax cosine fade<br/>RawThermalPerf; ThermalPerf = Raw × Pmax"]
    S1 --> S2["2. Feeding / Predation<br/>Tier 1: FedRate = min(1, HE × food_density), linear (v10)<br/>Tier 2: Holling II demand + _predationAccumulators<br/>sets FedRate on Tier 1 AND Tier 2"]
    S2 --> S3["3. Raw Final Performance<br/>RawFinalPerf = Raw × FedRate<br/>(Condition drain target — varies with food density for Tier 1 in v10)"]
    S3 --> S4["4. Update Condition<br/>asymmetric drift toward target<br/>rates scaled by Pmax (pmaxSafe)"]
    S4 --> S5["5. Final Performance<br/>FinalPerf = ThermalPerf × FedRate<br/>(computed for logging; not read downstream)"]
    S5 --> S6["6. Thermal Death<br/>instant wipeout when RawThermalPerf == 0"]
    S6 --> S7["7. Condition Death<br/>graduated severity below DeathThreshold<br/>survivor fitness boost<br/>_conditionDeathAccumulators"]
    S7 --> S8["8. Reproduction (v10: no soft cap on births)<br/>reproScale(Condition) × Pmax<br/>Tier 1 NO_PREDATOR_PENALTY only<br/>_birthAccumulators<br/>newborns inherit parent group Condition"]
    S8 --> S9["9. Natural Death<br/>flat NaturalDeathRate ± variance<br/>_naturalDeathAccumulators"]
    S9 --> S10["10. Population Rounding<br/>round away-from-zero"]
    S10 --> End([End step])
```

## Ordering and iteration

Every step iterates `Species` in the order they were added to `RunSpeciesList`. In v10, the only previously order-dependent step (Step 8 carrying-cap-on-births) was deleted, so Step 8 is now order-independent. The remaining steps were always order-independent — they don't read state that previous species in the same step have written.

## What each step writes back

| Step | Writes | Notes |
|------|--------|-------|
| 1 | `RawThermalPerformance`, `ThermalPerformance` | Per species. |
| 2 | `FedRate` (Tier 1 AND Tier 2), `CurrentHuntingSuccess`, `LastFedRateT1`, `LastFedRateT2`, `LastFoodDensityT1`, `LastAvgHuntingEfficiency`, `LastEatenT1`, accumulator updates on prey | v10: Tier 1 FedRate = min(1, HE × food_density), no longer hardcoded to 1.0. |
| 3 | `RawFinalPerformance` | Per species. |
| 4 | `Condition` (clamped 0–1), `AvgConditionT1`/`AvgConditionT2` later | `oldCondition` logged. |
| 5 | `FinalPerformance` | Never read again in this pipeline. |
| 6 | `Population → 0`, `Condition → 0` when lethal | Also increments `LastTempDeathsT1`/`T2`. |
| 7 | `Population -= wholeDeaths`, `Condition` boosted, `LastConditionDeathsT1`/`T2`, `_conditionDeathAccumulators` | Survivor boost caps Condition at 1.0. |
| 8 | `Population += wholeBirths`, `LastBirthsT1`/`T2`, `_birthAccumulators`, `LastReproScaleT1`/`T2` | v10: only Tier 1 modifier left is `NO_PREDATOR_PENALTY` (carrying-cap-on-births deleted). Newborns inherit group Condition (no explicit dilution step). |
| 9 | `Population -= wholeNaturalDeaths`, `LastNaturalDeathsT1`/`T2`, `_naturalDeathAccumulators` | No Condition effect. |
| 10 | `Population = Math.Round(Population, AwayFromZero)` | All populations integer-valued between days. |

After Step 10, `ComputeAverageCondition()` + `UpdateAccumulatorTotals()` + `EndPopT1/T2` are updated for the CSV record.
