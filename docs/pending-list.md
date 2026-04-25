# Pending List

Running list of unfixed simulation issues, deferred decisions, documentation gaps, and manuscript-prep items. Items fixed in v9 (Pmax wired into reproduction, drain, recovery) are **not** listed here — this file is only things still to do.

Sources cited for each item:
- **Brian M7** / **Brian M8** — from Brian's Apr 18 emails (`Tests/Brain/7/` and `Tests/Brain/8/`).
- **Review** — flagged in [`docs/simulation-review.md`](./simulation-review.md).
- **Marine** — added or escalated in [`docs/simulation-review-marine-feedback.md`](./simulation-review-marine-feedback.md).

Each item tagged **CRITICAL / HIGH / MEDIUM / LOW** for ship priority toward the MEE manuscript.

---

## A. Simulation bugs — not yet fixed (code changes needed)

### A1. Pooled FedRate across predators `CRITICAL`
Source: Review §3, Marine (escalated to critical).
[EcosystemSimulator.cs:586–596](../Assets/scripts/Simulation/EcosystemSimulator.cs) writes the same `fedRate = totalEaten / totalRawDemand` to every predator, regardless of per-predator `huntingSuccess`. Specialist and generalist predators with different hunting efficiencies end up with identical feeding satisfaction. Destroys the competitive signal.
**Fix**: `fedRate_i = min(1, huntingSuccess_i × (totalEaten / totalActualDemand))` so each predator keeps its own proportional share. Roughly a 10-line change in `ProcessFeedingWithAccumulator`.
**Blocks**: any multi-predator run, including Phase II breadth factorial.

### A2. Hidden warming trend from `WarmingBias > 1` `CRITICAL`
Source: Review §12, Marine.
[TemperatureCalculator.cs:92–94](../Assets/scripts/Simulation/TemperatureCalculator.cs). With `VariabilityMagnitude = 2`, `WarmingBias = 1.5`: `E[variation] = 2 × (1.5−1)/4 = 0.25 °C/year` of unattributed warming on top of `ClimateTrendPerYear`. Over 50 years: +12.5 °C hidden drift.
**Fix**: re-center the distribution — subtract `VariabilityMagnitude × (WarmingBias − 1) / 4` from each year's draw so `WarmingBias` skews the *distribution* without changing the *mean*. Or redefine `WarmingBias` semantics and document explicitly.
**Blocks**: any climate-trend figure; threatens interpretability of all long-horizon runs.

### A3. Processing-order first-mover bias in carrying cap `HIGH`
Source: Brian M8, Review §9.
[EcosystemSimulator.cs:914–920](../Assets/scripts/Simulation/EcosystemSimulator.cs). `tierPop = GetTierPopulation(1)` is evaluated live inside the per-species reproduction loop, so species processed earlier see a smaller tier population and get a higher `growthFactor`. First-listed species compounds an advantage into competitive exclusion over long runs.
**Fix**: snapshot `tierPop` once before the reproduction loop, **or** shuffle species order per day. Snapshot is simpler and deterministic; shuffle is closer to what Grimm & Railsback (2005) recommend for concurrent updates. Discuss on Monday with Brian + Tarik before committing to one.
**Blocks**: any competitive-exclusion interpretation.

### A4. Euler overshoot in Condition update at `BiologyStep > 1` `MEDIUM`
Source: Review §5, Marine (downgraded from ISSUE to CONCERN with validator check).
[EcosystemSimulator.cs:687–708](../Assets/scripts/Simulation/EcosystemSimulator.cs). At `BiologyStep = 5` with drain rate 0.15 and generalist Pmax 0.72, per-step factor can exceed 2, causing `Condition -= (Cond − target) × factor` to overshoot the target.
**Fix**: either add a validator that `BiologyStep == 1` (if that's the only supported setting), or switch to exact-exponential update: `Cond = target + (Cond − target) × exp(−rate × step)`.
**Blocks**: nothing at default settings; matters only if we ever use `BiologyStep > 1`.

### A5. Default `ReproThreshold = 0.25 < DeathThreshold = 0.3` `MEDIUM`
Source: Review §8, Marine (confirmed at SimSpecies.cs:128–131, 185–188).
Fallback factory defaults create an interval `(0.25, 0.30)` where a species is dying of low Condition while still producing "healthy"-branch reproduction. Biologically nonsense — reproductive tissue atrophies under nutritional stress (Reznick 1985). Brian's CSVs avoid this by setting `ReproThreshold = 0.6`.
**Fix options**: (a) swap defaults so `ReproThreshold > DeathThreshold`, or (b) add a validator warning at `SimulationConfig.IsValid` when `ReproThreshold < DeathThreshold`. Validator is the safer fix — doesn't break existing configs.

### A6. Recovery multiplier can drop below 1.0 for low-Pmax species `MEDIUM`
Source: Review §5.
`effectiveRecovery = RecoveryRate × (1 + target²) × Pmax`. At `target = 0.5`, `Pmax = 0.72`: multiplier = `1.25 × 0.72 = 0.9`, so recovery is slower than base rate. The quadratic "boost" no longer guarantees at-least-base-rate for generalists.
**Fix**: clamp the multiplier to `≥ 1` (`max(1, (1 + target²) × Pmax)`), or document that low-Pmax species recover sub-linearly as an intended property.

### A7. `NO_PREDATOR_PENALTY = 0.85` is ecologically backwards `MEDIUM`
Source: Review §9, Marine.
[SimSpecies.cs:37](../Assets/scripts/Simulation/SimSpecies.cs). When Tier 2 is absent, Tier 1 birth rate is multiplied by 0.85. Real trophic-cascade literature (Crooks & Soulé 1999; Estes et al. 2011) shows the opposite — prey release, not suppression. Also double-counts with carrying-capacity cap.
**Fix**: delete the penalty (simplest), or explicitly motivate with a cited mechanism. Removing it changes `SimSpecies.NO_PREDATOR_PENALTY` and the branch in `ApplyReproduction` that uses it.

### A8. Survivor fitness boost applied only to condition death `MEDIUM`
Source: Review §8, Marine.
The "dead were weakest → redistribute health" boost fires in `ApplyConditionDeath` ([line 780](../Assets/scripts/Simulation/EcosystemSimulator.cs)) but not in `ApplyNaturalDeathWithAccumulator` or predation. If natural death is random w.r.t. Condition (defensible), no boost is correct — but then apply the same logic to condition death (also no boost). Inconsistency between death types leaves a quiet quantitative effect.
**Fix**: marine scientist recommends removing the boost everywhere; the birth accumulator already prevents death spirals. Alternative: add it to natural death too (harder to justify biologically).

### A9. Newborn condition hard-coded at 0.5 `LOW`
Source: Review §9.
`NEWBORN_CONDITION = 0.5` is arbitrary. A healthy parent's offspring enter at half the parent's Condition. Marine scientist suggests `min(parent × 0.8, 0.5)` as more biologically grounded (maternal effects, Mousseau & Fox 1998).
**Fix**: make it configurable per species (add a field to `SimSpecies`) or compute as a function of parent Condition.

### A10. Autocorrelation coefficient hard-coded at 0.7/0.3 `LOW`
Source: Review §12.
[TemperatureCalculator.cs:114–115](../Assets/scripts/Simulation/TemperatureCalculator.cs). `v = 0.7 × v_yesterday + 0.3 × v_new`. Environmental autocorrelation (noise colour) is a first-order parameter of climate-variability ecology (Vasseur & Yodzis 2004; Ripa & Lundberg 1996). Currently fixed; users can't tune it.
**Fix**: expose as a `SimulationConfig` parameter and a bulk CSV column, with 0.7 as default.

### A11. `LETHAL_TRANSITION_WIDTH` global, not per-species `LOW`
Source: Review §2.
[SimSpecies.cs:39](../Assets/scripts/Simulation/SimSpecies.cs). 2 °C cosine fade near CTmin/CTmax is compile-time global. Real thermal-tolerance plasticity varies across species (Schulte et al. 2011).
**Fix**: promote to a per-species field, with current 2.0 as default. Low priority.

### A12. Seasonal-phase docstring wrong `LOW`
Source: Review §12, Marine (downgraded to MINOR).
[TemperatureCalculator.cs:63–65](../Assets/scripts/Simulation/TemperatureCalculator.cs) comment says "coldest at day 0, warmest at day 182" but the formula peaks at day 91 and troughs at day 273. Formula is fine; comment is wrong.
**Fix**: one-line comment edit.

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

### B6. Prey `FedRate` is always 1.0 — no bottom-up limitation `MEDIUM`
Source: Review §3, Marine.
Prey feed from the environment, always satisfied. Carrying-capacity cap is the only implicit resource limit. Misses bottom-up control dynamics.
**Action**: state explicitly as "closed top-down system" in methods. If the paper claims anything about bottom-up control, add a prey-resource axis.

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
MEE reviewer #2 will ask for SA on the load-bearing parameters: `ConditionDrainRate`/`ConditionRecoveryRate` ratio (currently 0.15/0.10 = 1.5×), `Pmax` scaling on/off, `NEWBORN_CONDITION`, `NO_PREDATOR_PENALTY` (if not deleted). Real empirical asymmetry varies widely (Sinclair et al. 2016; Ørsted et al. 2022).
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

### D1. Reply to Brian Mail 6 `URGENT`
Draft written earlier this session, not yet sent. Confirms Monday 2 PM meeting, acknowledges Pmax finding, commits to applying `* Pmax` after Condition.

### D2. Monday meeting with Brian + Tarik Gouhier `URGENT`
Agenda items to bring:
- Pmax fix landed in v9 (can show).
- Processing-order bug (A3) — decision needed on snapshot vs shuffle.
- Pooled-FedRate finding (A1) — new since last conversation, needs Brian's awareness.
- Hidden-warming issue (A2) — affects how we interpret existing Phase I data.
- Phase I–VI runs may need re-running post-fixes.

### D3. Decide which Phase I–VI runs to re-run post-fixes `HIGH`
After A1, A2, A3 are fixed, some earlier runs' interpretations may shift. Need to decide: all of Phase I–VI, or only the phases where multi-predator / climate-trend / large-N-species interactions dominate?

### D4. Commit the two review documents `LOW`
[`docs/simulation-review.md`](./simulation-review.md) and [`docs/simulation-review-marine-feedback.md`](./simulation-review-marine-feedback.md) are still uncommitted. Separate follow-up commit from the v9 Pmax fix.

---

## E. Code hygiene — optional, not on the critical path

### E1. Biology invariant tests
Source: Review §14 item 18.
No automated tests anywhere in the simulator. Property-based tests would catch regressions cheaply: `Condition ∈ [0, 1]`, `Pop ≥ 0`, accumulator residuals bounded, births/deaths conservation.
**Action**: Unity Test Framework package is already included. Add a small test class.

### E2. Remove `_thermalDeathAccumulators` dead field
Declared at [EcosystemSimulator.cs:88](../Assets/scripts/Simulation/EcosystemSimulator.cs), never used (thermal death is binary, no fractional accumulation needed).
**Action**: delete the field. Trivial.

### E3. Align spec doc with code for FinalPerformance use
The TINYSEA spec doc (now deleted) claimed reproduction uses FinalPerformance. New [`docs/simulation-spec.md`](./simulation-spec.md) already reflects v8/v9 reality, but if any external reference docs still claim the old behaviour, they need updating.

---

## Quick priority ranking (by ship impact)

1. **A1 Pooled FedRate** — changes multi-predator conclusions.
2. **A2 WarmingBias hidden warming** — changes climate-trend conclusions.
3. **A3 Processing-order bug** — changes competitive-exclusion conclusions.
4. **D1 Reply to Brian + D2 Monday meeting** — social gating.
5. **B1 Acclimation methods statement** — cheap, expected by reviewers.
6. **C1 Sensitivity analysis grid** — standard supplementary material.
7. **A5 Validator for ReproThreshold > DeathThreshold** — one-liner.
8. **A7 Delete NO_PREDATOR_PENALTY** — one-liner.
9. Everything else.
