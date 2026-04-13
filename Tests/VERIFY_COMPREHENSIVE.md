# Comprehensive Test Verification Checklist

Upload `test_comprehensive.csv` (20 runs, 99 total scenarios) as a bulk simulation.

## Quick Reference: What Each Run Tests

| # | Batch | Temp | Species | What to look for |
|---|-------|------|---------|-----------------|
| 1 | Std_Mild20 | 20C | 6 std | Common dominates, Arctic/Tropical lower |
| 2 | Std_Cold12 | 12C | 6 std | Arctic thrives, Tropical goes extinct |
| 3 | Std_Hot32 | 32C | 6 std | Tropical thrives, Arctic goes extinct |
| 4 | Extreme_Cold5 | 5C | 6 std | CRASH expected - too cold for everything |
| 5 | Tropical_30 | 30C | 6 std | Tropical dominates, others struggle |
| 6 | Warming_Slow | 20C+0.02/yr | 6 std | Gradual shift from Common to Tropical over 2yr |
| 7 | Warming_Fast | 20C+0.10/yr | 6 std | Likely crash - too fast to adapt |
| 8 | Cooling | 25C-0.05/yr | 6 std | Shift toward Arctic advantage |
| 9 | Short_30d | 20C | 6 std | Not enough time for much to happen |
| 10 | Long_5yr | 20C+0.01/yr | 6 std | Long-term slow warming dynamics |
| 11 | HighVar | 20C | 6 std | Wild daily swings - generalists (Common) should do better |
| 12 | NoCap | 20C | 6 std | No carrying capacity - prey explodes |
| 13 | HighDrain | 20C | 6 std | Higher condition drain - more die-offs |
| 14 | LowPop_Start | 20C | 6 std | Starting at 50 prey / 10 predator - fragile |
| 15 | Custom_Mix8 | 22C | 6+2 cust | Coral/Nudibranch Custom alongside standards |
| 16 | All_Custom10 | 22C | 10 cust | Zero standard species - all 10 custom |
| 17 | Many_Custom | 20C | 4+6 cust | No Arctic variants, 6 custom species |
| 18 | Prey_Only | 20C | 6 prey | No predators at all - prey fills up |
| 19 | ColdSp_HotEnv | 30C | 8 cust | Kelp (opt 12C) in 30C env - should die fast |
| 20 | HighN_10sc | 22C | 6+2 cust | 10 scenarios - tighter statistics |

---

## A. FILE STRUCTURE CHECKS

- [ ] ZIP contains 20 folders (one per batch)
- [ ] Each folder has: `aggregate.csv`, `config.csv`, `scenario_1.csv` through `scenario_N.csv`
- [ ] ZIP root has `bulk_summary.csv`
- [ ] Scenario counts match: rows 1-7,11-19 have 5 scenarios; rows 8-10 have 3; row 20 has 10

---

## B. NEW COLUMN CHECKS (SurvivedAvg / SurvivedMean)

### B1. Per-Run aggregate.csv (pick any run with partial extinction)
- [ ] Header is: `Species,Avg,SurvivedAvg,Min,Max,Extinct,Survived,ExtinctionRate`
- [ ] SurvivedAvg >= Avg for every species (it excludes zeros)
- [ ] When Extinct=0: SurvivedAvg should equal Avg (no zeros to exclude)
- [ ] When all scenarios extinct (Extinct=N, Survived=0): SurvivedAvg = 0.0
- [ ] When partial extinction: SurvivedAvg is noticeably higher than Avg

### B2. Bulk summary bulk_summary.csv
- [ ] PER-SPECIES AGGREGATE header is: `Species,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate`
- [ ] SurvivedMean >= GrandMean for every species
- [ ] Species that survive all 20 runs: SurvivedMean should be close to GrandMean
- [ ] Species that go extinct in many runs: SurvivedMean should be much higher than GrandMean

---

## C. CUSTOM SPECIES TRACKING

### C1. Custom species appear individually (not lumped)
- [ ] In Custom_Mix8 aggregate: `Coral_Custom` and `Nudibranch_Custom` each have their own row
- [ ] In All_Custom10 aggregate: all 10 species appear individually:
  - Coral_Custom, Kelp_Custom, Anemone_Custom, Jellyfish_Custom, Urchin_Custom
  - Nudibranch_Custom, Seastar_Custom, Lionfish_Custom, Crab_Custom, Octopus_Custom
- [ ] In Many_Custom aggregate: Hexapod_Common, Hexapod_Tropical, Sheplik_Common, Sheplik_Tropical + 6 customs
- [ ] In bulk_summary.csv: ALL unique species across all 20 runs appear in PER-SPECIES AGGREGATE

### C2. Custom variant columns in scenario CSVs
- [ ] Scenario CSVs have `Tier1Custom` and `Tier2Custom` columns
- [ ] For Custom_Mix8: Tier1Custom = Coral pop, Tier2Custom = Nudibranch pop
- [ ] For Std_Mild20 (no customs): Tier1Custom = 0, Tier2Custom = 0

### C3. T1Custom/T2Custom in Individual Scenarios table
- [ ] aggregate.csv INDIVIDUAL SCENARIOS table has T1Custom and T2Custom columns
- [ ] Values match sum of custom species in that tier

---

## D. BIOLOGY SANITY CHECKS

### D1. Temperature regime expectations
- [ ] Std_Cold12: Hexapod_Arctic avg >> Hexapod_Common avg >> Hexapod_Tropical avg (~0)
- [ ] Std_Hot32: Hexapod_Tropical avg >> others; Hexapod_Arctic extinct
- [ ] Std_Mild20: Hexapod_Common should dominate (broadest thermal curve, optimal ~24C)
- [ ] Extreme_Cold5: Crash Rate = 100% or near-total extinction

### D2. Climate trend effects
- [ ] Warming_Slow: Some shift visible but not catastrophic
- [ ] Warming_Fast: Higher crash rate or mass extinction vs Warming_Slow
- [ ] Cooling: Arctic variants do better than in Std_Mild20

### D3. System parameter effects
- [ ] NoCap: Final T1 populations > 5000 (exceeds default cap)
- [ ] HighDrain: More extinctions than Std_Mild20 (same temp, higher drain)
- [ ] LowPop_Start: More variable outcomes (small populations are stochastic)
- [ ] Short_30d: Populations close to starting values (not enough time)
- [ ] Prey_Only: No T2 at all, T1 should hit carrying cap quickly

### D4. Custom species biology
- [ ] All_Custom10 at 22C: Coral (opt 22) and Nudibranch (opt 22) should do best
- [ ] All_Custom10 at 22C: Kelp (opt 12) and Seastar (opt 12) should struggle/die
- [ ] ColdSp_HotEnv at 30C: Kelp (opt 12) should go extinct fast; Anemone (opt 28) / Lionfish (opt 28) should thrive

---

## E. AGGREGATE STATS MATH CHECKS

### E1. Pick one run (e.g., HighN_10sc with 10 scenarios) and verify:
- [ ] Avg = sum of final pops across all scenarios / 10
- [ ] SurvivedAvg = sum of non-zero final pops / count of non-zero scenarios
- [ ] Min = smallest final pop across all scenarios
- [ ] Max = largest final pop across all scenarios
- [ ] Extinct = count of scenarios where final pop = 0
- [ ] Survived = count where final pop > 0
- [ ] Extinct + Survived = total scenarios
- [ ] ExtinctionRate = Extinct / total

### E2. Bulk summary GrandMean verification (pick one species that appears in multiple runs):
- [ ] GrandMean = sum of that species' AvgSpeciesPop across all runs where it appears / count of those runs
- [ ] SurvivedMean = sum of SurvivedAvg from runs where species survived / count of those runs
- [ ] RunsExtinct + RunsSurvived = total runs where species appeared

---

## F. EDGE CASES

- [ ] Prey_Only: T2 population columns all zero, no crash (tier 2 was never populated)
- [ ] Short_30d: aggregate still generates properly even with minimal data
- [ ] Extreme_Cold5: Per-species section shows data even when all scenarios crash
- [ ] All_Custom10: No standard species in per-species section (no Hexapod_, no Sheplik_)
- [ ] Many_Custom: Species that only exist in this run still appear in bulk_summary.csv
- [ ] NoCap: Populations can exceed 5000 (no artificial ceiling)

---

## G. BULK SUMMARY STRUCTURE

- [ ] Header: `=== TINYSEA BULK SUMMARY (Across All Runs) ===`
- [ ] Total Runs = 20
- [ ] PER-RUN RESULTS table has 20 data rows
- [ ] PER-RUN table columns include CrashRate
- [ ] Species columns in PER-RUN table cover ALL species from ALL runs (union of all unique species)
- [ ] Runs with no custom species show 0.0 for custom species columns
- [ ] PER-SPECIES AGGREGATE has NO Min/Max columns (only GrandMean, SurvivedMean, extinction info)

---

## H. EXPECTED FAILURES / RED FLAGS

If you see ANY of these, something is wrong:
- SurvivedAvg < Avg for any species (impossible — it excludes zeros)
- SurvivedMean < GrandMean for any species (same reason)
- Custom species lumped as just "Custom" instead of "Coral_Custom", "Kelp_Custom" etc.
- Tier1Custom/Tier2Custom missing from scenario CSV headers
- Standard-only runs (Std_Mild20) showing Custom species in per-species section with pop > 0
- Extreme_Cold5 per-species section is empty (crashed scenarios should still show data)
- NoCap populations capped at exactly 5000 (carrying cap should be off)
- Min/Max columns appearing in bulk_summary.csv PER-SPECIES AGGREGATE
