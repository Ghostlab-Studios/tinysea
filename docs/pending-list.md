# Pending List

Running list of unfixed simulation issues, deferred decisions, documentation gaps, and manuscript-prep items. Items fixed in v9 (Pmax wired into reproduction, drain, recovery) and v10 (Tier 1 food-pool FedRate, soft-cap-on-births deleted, newborn = parent Condition) are **not** listed here as open items — this file is only things still to do. Resolved items are marked superseded/resolved in place rather than removed, so the history is traceable.

Sources cited for each item:
- **Brian M7** / **Brian M8** — from Brian's Apr 18 emails (`Tests/Brain/7/` and `Tests/Brain/8/`).
- **Review** — flagged in [`docs/simulation-review.md`](./simulation-review.md).
- **Marine** — added or escalated in [`docs/simulation-review-marine-feedback.md`](./simulation-review-marine-feedback.md).

Each item tagged **CRITICAL / HIGH / MEDIUM / LOW** for ship priority toward the MEE manuscript.

---

## A. Simulation bugs — not yet fixed (code changes needed)

### A1. ~~Pooled FedRate across predators~~ `RESOLVED IN v11`
Source: Review §3, Marine (escalated to critical).
v11: per-predator FedRate now reflects each species' own hunting effort. `scarcityFactor = totalEaten / totalActualDemand` (1.0 when prey abundant; <1.0 when demand exceeds supply). `fedRate_i = min(1, huntingSuccess_i × scarcityFactor)`. Specialist hunters get higher FedRate than generalists in mixed-HE runs — competitive exclusion between predator species finally works as designed. `LastFedRateT2` (CSV column) is now a population-weighted average, semantically shifted from pooled scalar. Reduces to v10 pooled formula exactly when there's only one predator species (no regression).

### A2. ~~Hidden warming trend from `WarmingBias > 1`~~ `FIXED — commit e4119fe`
Source: Review §12, Marine.
[TemperatureCalculator.cs:92–94](../Assets/scripts/Simulation/TemperatureCalculator.cs). With `VariabilityMagnitude = 2`, `WarmingBias = 1.5`: `E[variation] = 2 × (1.5−1)/4 = 0.25 °C/year` of unattributed warming on top of `ClimateTrendPerYear`. Over 50 years: +12.5 °C hidden drift.
**Resolution**: each year's draw now subtracts `biasMean = VariabilityMagnitude × (WarmingBias − 1) / 4` so the interannual variation is zero-mean by construction. `WarmingBias` controls only the *shape* of the distribution (warm tail wider than cold tail when `bias > 1`); long-term trend is owned solely by `ClimateTrendPerYear`. No change when `WarmingBias = 1.0`.
**Doc updates also landed**: `simulation-spec.md` §3, `diagrams/temperature-model.md`, `csv-formats.md` `warming_bias` column.

### A3. ~~Processing-order first-mover bias in carrying cap~~ `SUPERSEDED BY v10`
Source: Brian M8, Review §9.
The previous live-`tierPop` read inside `ApplyReproduction`'s carrying-cap-on-births block was the source of this bug. **v10 deleted the entire soft-cap-on-births block**: Tier 1 reproduction is now throttled indirectly through the Condition pathway (high pop → low food density → low FedRate → Condition drains → reproScale shrinks AND condition deaths fire). The bug doesn't have a place to live anymore. Neither snapshot nor shuffle was needed.

### A4. Euler overshoot in Condition update at `BiologyStep > 1` `MEDIUM`
Source: Review §5, Marine (downgraded from ISSUE to CONCERN with validator check).
[EcosystemSimulator.cs:887–922](../Assets/scripts/Simulation/EcosystemSimulator.cs). Still uses Euler step: `sp.Condition -= (sp.Condition - target) * effectiveDrain` (line 906) and `+= (target - sp.Condition) * effectiveRecovery` (line 917). At `BiologyStep = 5` with drain rate 0.15 and generalist Pmax 0.72, the effective drain (`0.15 × (1 + severity²) / 0.72 × 5 ≈ 1.04 to 2.08`) can exceed 1 and overshoot the target.
**Fix**: either add a validator that `BiologyStep == 1` (if that's the only supported setting), or switch to exact-exponential update: `Cond = target + (Cond − target) × exp(−rate × step)`.
**Blocks**: nothing at default settings; matters only if we ever use `BiologyStep > 1`.

### A5. Default `ReproThreshold = 0.25 < DeathThreshold = 0.3` `MEDIUM`
Source: Review §8, Marine (confirmed at SimSpecies.cs:128–131, 185–188).
Fallback factory defaults create an interval `(0.25, 0.30)` where a species is dying of low Condition while still producing "healthy"-branch reproduction. Biologically nonsense — reproductive tissue atrophies under nutritional stress (Reznick 1985). Brian's CSVs avoid this by setting `ReproThreshold = 0.6`.
**Fix options**: (a) swap defaults so `ReproThreshold > DeathThreshold`, or (b) add a validator warning at `SimulationConfig.IsValid` when `ReproThreshold < DeathThreshold`. Validator is the safer fix — doesn't break existing configs.

### A6. Recovery multiplier can drop below 1.0 for low-Pmax species `MEDIUM`
Source: Review §5.
[EcosystemSimulator.cs:914–917](../Assets/scripts/Simulation/EcosystemSimulator.cs). Still: `effectiveRecovery = RecoveryRate × (1 + target²) × Pmax`. At `target = 0.5`, `Pmax = 0.72`: multiplier = `1.25 × 0.72 = 0.9`, so recovery is slower than base rate. The quadratic "boost" no longer guarantees at-least-base-rate for generalists.
**Fix**: clamp the multiplier to `≥ 1` (`max(1, (1 + target²) × Pmax)`), or document that low-Pmax species recover sub-linearly as an intended property.

### A7. `NO_PREDATOR_PENALTY = 0.85` is ecologically backwards `MEDIUM`
Source: Review §9, Marine.
[SimSpecies.cs:44](../Assets/scripts/Simulation/SimSpecies.cs) declares `public const float NO_PREDATOR_PENALTY = 0.85f`; applied at [EcosystemSimulator.cs:1143](../Assets/scripts/Simulation/EcosystemSimulator.cs) inside `ApplyReproduction`. When Tier 2 is absent, Tier 1 birth rate is multiplied by 0.85. Real trophic-cascade literature (Crooks & Soulé 1999; Estes et al. 2011) shows the opposite — prey release, not suppression. Also double-counts with carrying-capacity cap.
**Fix**: delete the penalty (simplest), or explicitly motivate with a cited mechanism. Removing it changes `SimSpecies.NO_PREDATOR_PENALTY` and the branch in `ApplyReproduction` that uses it.

### A8. Survivor fitness boost applied only to condition death `MEDIUM`
Source: Review §8, Marine.
The "dead were weakest → redistribute health" boost fires in `ApplyConditionDeath` ([EcosystemSimulator.cs:1010–1017](../Assets/scripts/Simulation/EcosystemSimulator.cs): `sp.Condition = oldCondition * oldPop / sp.Population; sp.Condition = Math.Min(1f, sp.Condition);`) but not in `ApplyNaturalDeathWithAccumulator` (lines 1201–1250) or predation. If natural death is random w.r.t. Condition (defensible), no boost is correct — but then apply the same logic to condition death (also no boost). Inconsistency between death types leaves a quiet quantitative effect.
**Fix**: marine scientist recommends removing the boost everywhere; the birth accumulator already prevents death spirals. Alternative: add it to natural death too (harder to justify biologically).

### A9. ~~Newborn condition hard-coded at 0.5~~ `RESOLVED IN v10`
Source: Review §9.
`NEWBORN_CONDITION = 0.5` constant was removed. v10: newborns inherit the species' current group Condition. The parent's Condition already encodes recent food density / hunting success via lagged drain dynamics, so multiplying again would double-count. No fixed constant, no food-density multiplier — same logic for Tier 1 and Tier 2. Newborn vulnerability emerges from same-drain-no-head-start dynamics in subsequent days.
**Watchpoint W3**: if observed regrowth from crashes is unrealistically rapid in v10 outputs, add `α < 1` baseline neonatal vulnerability in v11.

### A10. Autocorrelation coefficient hard-coded at 0.7/0.3 `LOW`
Source: Review §12.
[TemperatureCalculator.cs:131](../Assets/scripts/Simulation/TemperatureCalculator.cs). `variation = _previousDayVariation * 0.7f + newRandom * 0.3f`. Environmental autocorrelation (noise colour) is a first-order parameter of climate-variability ecology (Vasseur & Yodzis 2004; Ripa & Lundberg 1996). Currently fixed; users can't tune it.
**Fix**: expose as a `SimulationConfig` parameter and a bulk CSV column, with 0.7 as default.

### A11. `LETHAL_TRANSITION_WIDTH` global, not per-species `LOW`
Source: Review §2.
[SimSpecies.cs:46](../Assets/scripts/Simulation/SimSpecies.cs). `private const float LETHAL_TRANSITION_WIDTH = 2.0f;` — 2 °C cosine fade near CTmin/CTmax is compile-time global. Real thermal-tolerance plasticity varies across species (Schulte et al. 2011).
**Fix**: promote to a per-species field, with current 2.0 as default. Low priority.

### A12. Seasonal-phase docstring wrong `LOW`
Source: Review §12, Marine (downgraded to MINOR).
[TemperatureCalculator.cs:67–73](../Assets/scripts/Simulation/TemperatureCalculator.cs) comment says "coldest at day 0, warmest at day 182" but the formula `sin(2π·day/365)` is zero at day 0, peaks at day 91, and troughs at day 273. Formula is fine; comment is wrong.
**Fix**: one-line comment edit.

### A13. `AvgConditionT1`/`T2` and `FinalConditionT1`/`T2` read but never written `MEDIUM` (data-integrity bug)
Source: Code audit 2026-05-01.
[ScenarioResult.cs:57–60](../Assets/scripts/Simulation/DataStructure/ScenarioResult.cs) declares the four condition fields on `ScenarioResult`; `AggregateResults.AvgConditionT1/T2/AvgFinalConditionT1/T2` (lines 268–271) accumulate them in `CalculateAggregates` (lines 314–315, 331–332) and emit them in the aggregate CSV's `=== CONDITION STATS ===` section (lines 658–662). But [SimulationRunner.cs:895–936](../Assets/scripts/Simulation/SimulationRunner.cs) `ToScenarioResult` never populates these fields — they remain at C# default `0f`. The `EcosystemSimulator` already exposes `AvgConditionT1`/`AvgConditionT2` (line 204–205) which `StepRecord` captures per day at SimulationRunner.cs:459–460, but those are *daily snapshot* values, not the scenario-level mean / final.
**Effect**: every `aggregate.csv`'s `=== CONDITION STATS ===` block currently emits four zeros. Downstream R/pandas pipelines reading those four cells get garbage.
**Fix**: in `ToScenarioResult`, populate from `_records` (e.g. mean of `r.AvgConditionT1` across the run, last-day value for `FinalConditionT1`). One block, ~10 lines.

### A14. `BulkSpeciesConfig.CTmaxC` default `50.0f` is inconsistent with global default `LOW` (unreachable but confusing)
Source: Code audit 2026-05-01.
[BulkBatchConfig.cs:30](../Assets/scripts/Simulation/BulkBatchConfig.cs) declares `public float CTmaxC = 50.0f` while [SimSpecies.cs:59](../Assets/scripts/Simulation/SimSpecies.cs) declares `CTmaxC = 40.0f` and [SpeciesDatabase.cs:106/111/116](../Assets/scripts/Simulation/DataStructure/SpeciesDatabase.cs) `GetVariantThermalDefaults` returns 40.0 for every variant. The 50.0 default never reaches users because the parser path at [CsvBatchParser.cs:261–265](../Assets/scripts/Simulation/CsvBatchParser.cs) overrides it with the variant default — but it is misleading for code readers and a footgun if a future call path skips the parser.
**Fix**: change to `40.0f` to match. Trivial.

### A15. `SpeciesData.GetVariantThermalDefaults` returns identical Pmax for Tropical and Arctic `LOW` (semantics)
Source: Code audit 2026-05-01.
[SpeciesDatabase.cs:103–117](../Assets/scripts/Simulation/DataStructure/SpeciesDatabase.cs) returns `pmax=0.85, ctMin=0, ctMax=40` for both **Tropical** and **Arctic**, and `pmax=0.65, ctMin=0, ctMax=40` for **Common** and **Custom**. CTmin/CTmax are also identical across all four variants. Variant differentiation comes only from Arrhenius optimal-temp parameters, not from Pmax or thermal lethal limits.
**Question**: is this intentional ("specialists with same Pmax, different Topt") or a copy-paste leftover from when Tropical had its own value? The doc tables in [csv-formats.md](../docs/csv-formats.md) and the `SimSpecies.CreateHexapod`/`CreateSheplik` factories (which set CTmin=-30/CTmax=20 for Arctic, CTmin=0/CTmax=80 for Tropical) imply per-variant differentiation.
**Fix**: review intent. Either set explicit Pmax and CT-limits per variant in `GetVariantThermalDefaults`, or document that the Pmax/CT values are deliberately uniform here while only Arrhenius params differ.

### A16. `SimulationRunner.GetSummary` excludes zero values from `MinTier1Pop`/`MinTier2Pop` `LOW` (semantic surprise)
Source: Code audit 2026-05-01.
[SimulationRunner.cs:876–878](../Assets/scripts/Simulation/SimulationRunner.cs): `if (r.Tier1Pop < minT1 && r.Tier1Pop >= 1) minT1 = r.Tier1Pop;` — when a tier hits zero the day count is excluded from the min calculation, so a tier that crashes still reports `MinTier*Pop` as the lowest *non-zero* day's population, not the actual minimum. The condition `>= 1` was likely intended to skip pre-population days but it also skips post-extinction days.
**Fix**: either accept the semantic and rename the column to `MinNonZeroPop`, or drop the `>= 1` guard so zeros count and let downstream code distinguish "pre-init" from "extinct" via other signals. Either is fine; the current behaviour is undocumented.

---

## A.v10. Watchpoints introduced by v10 (observe in prototype)

### W1. v9 Pmax-on-drain × v10 food-pool double-dip on generalists `MEDIUM`
Drain rate already divides by Pmax (v9); the drain target now depends on food density too (v10). Generalists (low Pmax) in crowded conditions are hit on both axes — bigger gap from target AND faster drain rate. Biologically defensible (specialists *should* outperform generalists under stress) but the magnitude could be too aggressive.
**Action**: run a generalist-dominant scenario; measure crash dynamics. If unrealistically aggressive, mitigation = soften v9's `/Pmax` to `/sqrt(Pmax)`.

### W2. Tier 2 indirect oscillations (Lotka–Volterra cycles) `MEDIUM`
Predators see new prey oscillation patterns under v10. Lotka–Volterra-flavour cycles may emerge that didn't before. Could produce realistic predator-prey cycles, or could produce repeated predator extinction.
**Action**: run multi-tier scenarios. If predators repeatedly go extinct, may need a Tier 2 mortality dampener — but expect this is real ecology surfacing rather than a bug.

### W3. Newborn dynamics under no-α formula `MEDIUM`
Under `newborn = parent_condition` (v10), populations may regrow from crashes faster than realistic because newborns inherit full parent Condition rather than a vulnerability baseline.
**Action**: observe regrowth dynamics. If too fast, add `α < 1` baseline (e.g. `newborn = α × parent_condition` with α ≈ 0.7–0.8) in v11.

### W4. Initial-condition shock when pop > cap `LOW`
Should resolve gracefully via Condition system over ~8–10 days (population drains via condition death as food density stays at 0). Verify in practice; the validator already warns at scenario init when over-cap.

---

## B. Known design simplifications — need methods-section statements, not code changes

These are acceptable scope boundaries if and only if they're declared explicitly in the manuscript.

### B1. No thermal acclimation / plasticity `HIGH`
Source: Marine (missed by Review).
Fixed TPCs across decade-scale runs is a substantive assumption. Most marine ectotherms shift TPCs over days–weeks (Seebacher et al. 2015; Gunderson & Stillman 2015). MEE reviewer #2 will ask.
**Action**: defensive paragraph in methods citing Angilletta (2009), Seebacher et al. (2015), explaining why fixed TPCs are justified *for this research question*, not claimed as realistic.

### B2. No density-dependent natural mortality `HIGH`
Source: Marine.
`NaturalDeathRate` is flat. Real marine populations show strong density-dependence near carrying capacity (Rose et al. 2001; Hixon & Webster 2002).
**Action**: either add an optional density-dependent multiplier as a togglable parameter, or state the constant-rate assumption in methods with justification.

### B3. No phenology — reproduction is day-by-day, not seasonal `HIGH`
Source: Marine.
Condition-gated reproduction runs every day. Can't represent seasonal-mismatch dynamics (Edwards & Richardson 2004; Durant et al. 2007) — central topic in marine climate ecology. If the paper makes any seasonal-mismatch claim, this is fatal.
**Action**: scope it out in the methods. If seasonal mismatch is central to the paper's claims, reconsider.

### B4. No age/size structure `MEDIUM`
Source: Marine.
Per-capita reproduction and mortality are flat across age/size classes. Strong size-structure effects in real marine systems (Barneche et al. 2018).
**Action**: state in methods. For the specialist–generalist question this is defensible.

### B5. No genetic variation or heritable adaptation `MEDIUM`
Source: Marine.
Over 20–50 year runs under warming, heritable shifts in thermal tolerance can be larger than the warming signal in fast-generation taxa (Kelly 2019).
**Action**: state in methods as a scope limit.

### B6. ~~Prey `FedRate` is always 1.0 — no bottom-up limitation~~ `RESOLVED IN v10`
Source: Review §3, Marine.
v10: Tier 1 FedRate is now density-dependent — `FedRate_T1 = min(1, HuntingEfficiency × food_density)`, where `food_density = max(0, 1 − tier1Pop/CarryingCapacityPerTier)`. Linear (not Holling II) because plankton-style passive extractors don't have search/handling phases. Bottom-up dynamics now flow through the simulator: high pop → low food density → low FedRate → Condition drains → reproScale shrinks AND condition deaths fire. Logistic-overshoot dynamics emerge naturally.

### B7. No prey-variant preference by predators `MEDIUM`
Source: Review §3, Marine (escalated for specialist–generalist research goal).
Prey are taken proportional-to-population only. Prey switching (Murdoch 1969) is one of the main stabilising mechanisms in multi-prey systems; range-shift literature (Pinsky et al. 2013; Cheung et al. 2013) leans heavily on preference.
**Action**: either add a per-variant preference weight, or state explicitly in methods that preference is out of scope.

### B8. Reproduction rates are very fast `LOW`
Source: Review §9, Marine.
Per-capita 0.4/day at Condition=1.0 is appropriate for copepods/larvae, not reef fish or crustaceans. Generation-time compressed.
**Action**: name the intended taxon range in methods. Provide a generation-time calibration paragraph if claiming general "marine ecosystem" applicability.

### B9. No behavioural thermoregulation `LOW`
Source: Marine.
`TemperatureDebuff` is a static per-species offset — it models body-temp offset (Harley et al.) but not adaptive thermoregulation (Kearney et al. 2009). Organisms can't seek preferred temperatures.
**Action**: state in methods; for sessile marine invertebrates this is fine.

---

## C. Manuscript preparation — peer-review-driven items

### C1. Sensitivity analysis grid `HIGH`
Source: Marine.
MEE reviewer #2 will ask for SA on the load-bearing parameters: `ConditionDrainRate`/`ConditionRecoveryRate` ratio (currently 0.15/0.10 = 1.5×), `Pmax` scaling on/off, `NO_PREDATOR_PENALTY` (if not deleted), and (new in v10) `CarryingCapacityPerTier` size, plus `HuntingEfficiency_T1` if we ever explore HE < 1. Real empirical asymmetry varies widely (Sinclair et al. 2016; Ørsted et al. 2022).
**Action**: build a small factorial sensitivity-analysis bulk CSV and report results in supplementary.

### C2. ODD protocol methods section `HIGH`
Source: Marine.
MEE effectively requires ODD or ODD+D for simulation papers (Grimm et al. 2020, *JASSS* 23:7).
**Action**: structure the methods section as ODD+D. We don't have this yet.

### C3. Justify `RandomnessGrowthRate` with CMIP6 citation `MEDIUM`
Source: Marine.
Daily-variation growing with year is defensible per Bathiany et al. (2018) and Olonscheck et al. (2021), but not currently cited.
**Action**: add the citations when writing methods.

### C4. Name carrying capacity as logistic closure, not ecology `MEDIUM`
Source: Marine.
`CarryingCapacityPerTier` is a hard parameter with no resource model behind it. Reviewers will flag as phenomenological.
**Action**: either call it "logistic closure term" in methods and own it, or add a resource interpretation.

### C5. Number of stochastic replicates `MEDIUM`
Source: Marine.
Brian's current CSVs use 50 scenarios/run; reviewers may push for 100+ depending on variance observed (Cariboni et al. 2007; Saltelli et al. 2019).
**Action**: verify CI width is acceptable at current n; increase if not.

---

## D. Social / meeting items

### D1. ~~Reply to Brian Mail 6~~ `STALE — historical only`
Originally `URGENT`. Dated April 2025; obsolete relative to current date (2026-05-01). Resolved by time — see commit history for the actual reply / v9 Pmax fix landing.

### D2. ~~Monday meeting with Brian + Tarik Gouhier (Apr 27)~~ `STALE — historical only`
Originally `URGENT`. Apr 27, 2025 has passed; agenda items have either been resolved (Pmax in v9, A1 pooled FedRate in v11, A2 WarmingBias in commit `e4119fe`, A3 superseded by v10) or rolled into D3.

### D3. Decide which Phase I–VI runs to re-run post-fixes `HIGH`
After A1, A2, A3 are fixed, some earlier runs' interpretations may shift. Need to decide: all of Phase I–VI, or only the phases where multi-predator / climate-trend / large-N-species interactions dominate? Now that A1/A2/A3 are all resolved (v10/v11/commit `e4119fe`), this is unblocked but still open.

### D4. ~~Commit the two review documents~~ `RESOLVED — commit 04db349`
Both `docs/simulation-review.md` and `docs/simulation-review-marine-feedback.md` are committed (commit `04db349`, "Add simulation review, marine-scientist feedback, and pending list").

---

## E. Code hygiene — optional, not on the critical path

### E1. Biology invariant tests
Source: Review §14 item 18.
No automated tests anywhere in the simulator. Property-based tests would catch regressions cheaply: `Condition ∈ [0, 1]`, `Pop ≥ 0`, accumulator residuals bounded, births/deaths conservation.
**Action**: Unity Test Framework package is already included. Add a small test class.

### E2. Remove `_thermalDeathAccumulators` dead field
Declared at [EcosystemSimulator.cs:153](../Assets/scripts/Simulation/EcosystemSimulator.cs), cleared at line 438, initialized per species at line 447. Never written or read elsewhere — thermal death is binary, no fractional accumulation needed. Comment at line 223 already calls it dead code: "_thermalDeathAccumulators is unused dead code in v11.1 — no accessor."
**Action**: delete the field, the clear, and the per-species init. Trivial.

### E3. Align spec doc with code for FinalPerformance use
The TINYSEA spec doc (now deleted) claimed reproduction uses FinalPerformance. New [`docs/simulation-spec.md`](./simulation-spec.md) already reflects v8/v9 reality, but if any external reference docs still claim the old behaviour, they need updating.

### E4. Remove `SimSpecies.MIN_FINAL_PERF_FOR_NATURAL_DEATH` dead constant `LOW`
Source: Code audit 2026-05-01.
[SimSpecies.cs:45](../Assets/scripts/Simulation/SimSpecies.cs) declares `public const float MIN_FINAL_PERF_FOR_NATURAL_DEATH = 0.1f;` with the comment "Floor to prevent division by zero". A repo-wide grep finds zero references to it anywhere else. Stale relic from before v6, when natural death was performance-scaled. v6 changed natural death to flat-rate (see [EcosystemSimulator.cs:1212](../Assets/scripts/Simulation/EcosystemSimulator.cs) "Flat rate — natural death is independent of performance"), so the floor became dead.
**Action**: delete the constant. Trivial.

### E5. `SimulationRunner.SaveToFile` hardcodes `tinysea_v6_` filename prefix `LOW`
Source: Code audit 2026-05-01.
[SimulationRunner.cs:829](../Assets/scripts/Simulation/SimulationRunner.cs) writes `string filename = $"tinysea_v6_{timestamp}{crashSuffix}.csv";` — the prefix is the literal string `tinysea_v6_` despite the simulator now being at v12. A repo-wide grep finds `SaveToFile` is declared but never called: main flows (`SimulationController` and `BulkSimulationController`) all go through `ToCsvInternal` + `WebGLZipDownload`/`ServerUpload`, so the path is unused.
**Action**: either delete `SaveToFile` (after confirming no offline editor scripts call it) or update the prefix to `tinysea_` (no version) so it stops lying. Trivial.

### E6. v11.1 left `_thermalDeathAccumulators` referenced in code despite being dead `LOW` (consolidates with E2)
Same field as E2. Declared at [EcosystemSimulator.cs:153](../Assets/scripts/Simulation/EcosystemSimulator.cs); cleared in `ClearAccumulators` (line ~438); initialized per species in `InitializeAccumulators` (line ~447). Comment at line 223 acknowledges it's dead. Listed as a separate item only because the dead-code reference is surfaced via initializer wiring, not just declaration; eliminating it requires touching three sites, not one. Resolves with E2.

---

## Quick priority ranking (by ship impact)

1. **A13 AvgCondition fields never written** — `MEDIUM` data-integrity bug; `=== CONDITION STATS ===` block in every aggregate.csv is currently zeros. Fix before next bulk run handed off for analysis.
2. **B1 Acclimation methods statement** — cheap, expected by reviewers (manuscript text only, not code).
3. **C1 Sensitivity analysis grid** — standard supplementary material.
4. **A5 Validator for ReproThreshold > DeathThreshold** — one-liner.
5. **A7 Delete NO_PREDATOR_PENALTY** — one-liner; or formally re-justify with citation.
6. **D3 Decide which Phase I–VI runs to re-run** — unblocked now that A1/A2/A3 are resolved; needs a call.
7. **W1–W4 watchpoints** — observe in v10 + v11 prototype, decide on follow-ups for v12+.
8. **C2 ODD protocol methods section** — required by MEE for simulation papers.
9. **B2 / B3 / B7 design statements** — methods-section text only.
10. **E2 / E4 / E5 / E6 hygiene** — trivial dead-code deletions; bundle into one cleanup commit.
11. **A14 / A15 / A16** — `LOW`, defer to a sweep with E-section cleanup.
12. **A4 / A6 / A10 / A11** — `MEDIUM` / `LOW` simulator-internals; address only if observed in runs.
13. **A12 docstring fix** — one-line edit when next touching `TemperatureCalculator.cs`.
14. **D4 commit review documents** — bookkeeping.

### Resolved (kept here for traceability)
- ~~**A1 Pooled FedRate**~~ — **RESOLVED** in v11.
- ~~**A2 WarmingBias hidden warming**~~ — **DONE** (commit `e4119fe`).
- ~~**A3 Processing-order bug**~~ — **SUPERSEDED** by v10 (soft-cap-on-births deleted).
- ~~**B6 Prey FedRate always 1.0**~~ — **RESOLVED** in v10.
- ~~**A9 Newborn condition fixed at 0.5**~~ — **RESOLVED** in v10.
- ~~**D1 Reply to Brian Mail 6**~~ — stale (Apr 2025).
- ~~**D2 Monday meeting (Apr 27)**~~ — stale (Apr 2025).

## Tier 2 carrying capacity — REJECTED
Brian's Mail 9 asked whether predators should have an explicit cap as a fixed % of Tier 1 (10% Lindeman, 15–20% gameplay). **Decision: no.** Predators are limited via the food chain (Tier 1 → Holling II → FedRate → Condition → reproduction). Adding an explicit Tier 2 cap would double-count and obscure the emergent trophic dynamic. Reply this in the next email to Brian.
