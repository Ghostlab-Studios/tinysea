# TinySea Simulation Code Review Findings

Scope: the simulation C# source under `Assets/scripts/Simulation` plus the simulation ScriptableObject `.asset` files under `Assets` (the simulation used in `simulation.unity`). The interactive game code and the `docs/` folder are out of scope; this review judged the code on its own terms and did not rely on any documentation.

Method: ten independent review dimensions, each producing raw findings, followed by an adversarial verification pass that re-read the cited code and confirmed, refuted, or marked each claim uncertain. This file keeps confirmed and uncertain findings, drops refuted ones (listed in the appendix), and deduplicates findings reported by multiple dimensions.

Reviewed at: (fill in)

## Fix status (applied 2026-06-09, branch simulation-T1Refactor)

Fixed in code this session (pending a Unity compile at time of writing). Design and reporting-definition items are left for the team and Brian.

- **Fixed:** F1 (asset `Tier2Enabled` set to 0), F2 (follows F1), F4 (invariant CSV number formatting), F8 (rate columns written as fractions, not `"20.0 %"`), F12 (NaN guard in thermal performance), F14 (carrying-capacity floor and temperature-bounds-order validation), F15 (`AddSpecies` overload now adds), F17 (fallback predators gated by `Tier2Enabled`), F19 (independent biology RNG seed), F21 (bulk summary uses the completed scenario count), F22 (blank, not 0, for no-survivor cross-run metrics). The bulk CSV error panel also now lists every error instead of capping at 10.
- **Deferred to design / Brian:** F3 (no-predator birth penalty), F5 (Pmax ceiling semantics), F6/F7/F10 (BiologyStep semantics), F11 (death-then-birth ordering), F13 (carrying-capacity cap shape), F20 (which extinction definition is canonical).
- **Deferred (low impact, do with compile verification):** F24 (float to double accumulators and dead-member cleanup).
- **Pending Unity scene work:** the bulk CSV error panel needs a ScrollRect and auto-size disabled on its text so the full list is readable at a fixed size (the code now emits the full list).

Tier 2 was disabled for the current build (F1 and F17). The predator algorithm stays in the engine so it can be re-enabled later.

## Severity legend

- blocker: breaks the simulation or its output for the shipped configuration; must fix before use.
- major: wrong or misleading behavior, or a real correctness or output hazard that bites under reachable configurations.
- minor: contained defect, inconsistency, or value-with-no-effect that does not corrupt the shipped run but misleads or can break under non-default settings.
- info: dead code, leftover, or noted-and-safe observation; cleanup or documentation only.

## Summary

Confirmed findings by severity:

| Severity | Count |
|----------|-------|
| blocker  | 0 |
| major    | 4 |
| minor    | 11 |
| info     | 9 |
| Total    | 24 |

Confirmed findings by category:

| Category | Count |
|----------|-------|
| tier-leftover | 4 |
| logic | 6 |
| numerical | 6 |
| inconsistency | 3 |
| dead-code | 3 |
| unused-input | 1 |
| units | 1 |

Note: the single most-reported issue is the shipped `Tier2Enabled: 1` asset value, which seven of ten dimensions raised against different downstream effects. It is consolidated into F1, with its distinct consequences (CSV schema, no-predator penalty) split into F2 and F3 where they are independent issues.

---

## Major findings

### F1. Shipped SimulationConfig.asset enables Tier 2 against the Tier-1-only intent

- Location: `Assets/Resources/SimulationConfig.asset:33`; field `SimulationConfig.cs:130`; consumed `SimulationController.cs:312,374`; gate `EcosystemSimulator.cs:331-336`; CSV gate `SimulationRunner.cs:802`.
- What the code does: the shipped asset serializes `Tier2Enabled: 1`. The C# field default is `false`, the inspector header says Tier 2 is disabled, the load gate `if (!Tier2Enabled && data.tier == 1) continue;` excludes predators only when the flag is false, and `bool tier2 = ... Ecosystem.Tier2Enabled` drives the Tier-2 CSV columns. `SimulationController` copies the asset value straight into `runner.Ecosystem.Tier2Enabled` in both run paths; nothing resets it at load.
- Why it is a red flag: the one asset that drives `simulation.unity` ships with the predator gate ON, contradicting the field default and the stated single-tier design. With the shipped `RunSpeciesList.asset` (all six species `tier: 0`) no predators are instantiated, so biology is unchanged, but the gate is open: a single `tier: 1` species added to the SO would be silently simulated instead of excluded, reactivating the dormant predator path. The CSV-schema consequence is split into F2. The asset path has no `tier != 0` rejection (only the CSV parser rejects it), and `SimulationInputUI` never exposes the flag, so it cannot be turned off from the simulation scene UI.
- Recommendation: set `Tier2Enabled: 0` in `SimulationConfig.asset` to match the code default and the single-tier intent. If Tier 2 is permanently out of scope, remove the serialized flag (and the predator branch) so the asset cannot drift true again, or add a `tier != 0` rejection on the RunSpeciesList path mirroring the CSV parser.
- Confidence: high.
- Verdict: confirmed. Asset line 33 is `Tier2Enabled: 1`; default false at `SimulationConfig.cs:130`; propagated at `SimulationController.cs:312,374`; all shipped species are `tier: 0`. One reported justification ("changes biology") was inaccurate and dropped: flipping the flag changes only the CSV schema for the shipped data, not the population numbers.

### F2. Tier2Enabled=1 emits a dozen permanently-zero Tier-2 CSV columns

- Location: `SimulationRunner.cs:802` (tier2 flag); `SimulationRunner.cs:209-313` (CsvHeader/ToCsvLine); summary at `SimulationRunner.cs:822,869-896`; root cause `SimulationConfig.asset:33`.
- What the code does: `ToCsvInternal` sets `bool tier2 = Ecosystem == null || Ecosystem.Tier2Enabled`. Because the asset has `Tier2Enabled=1`, every scenario CSV emits the full Tier-2 column set (`Tier2Pop`, `TempDeathsT2`, `ConditionDeathsT2`, `NaturalDeathsT2`, `BirthsT2`, `FedRateT2`, `AvgHuntingEff`, `AvgConditionT2`, `BirthAccumT2`, `NaturalDeathAccumT2`, `ConditionDeathAccumT2`, `ReproScaleT2`) plus a `Tier2Pop` summary column, all permanently zero.
- Why it is a red flag: the `tier2 == false` path exists specifically to suppress meaningless Tier-2 columns in a prey-only sim. The shipped config defeats that suppression, so every output file carries a wider schema than a genuine Tier-1-only run. An analyst diffing this output against a `tier2 == false` run gets a different column set, and the zero-filled columns are noise.
- Recommendation: fixing F1 (set `Tier2Enabled: 0`) drops these columns. Verify a fresh `scenario_N.csv` contains no `*T2` columns after the fix.
- Confidence: high.
- Verdict: confirmed. The `if (tier2)` branches in `CsvHeader`/`ToCsvLine` (lines 216-232, 270-286) and the summary blocks gate exactly these columns; with the asset flag true they are all written and zero.

### F3. No-predator birth penalty (0.85) is applied unconditionally to every prey species every step

- Location: `EcosystemSimulator.cs:1206-1215`; constant `SimSpecies.cs:55`; `GetTierPopulation` at `EcosystemSimulator.cs:1384`.
- What the code does: in `ApplyReproduction`, for every Tier-1 species the code reads `GetTierPopulation(2)` and, when it is below `MIN_ALIVE_POP` (1.0), multiplies births by `NO_PREDATOR_PENALTY = 0.85f`. In the prey-only build there are never any Tier-2 species, so `GetTierPopulation(2)` is always 0 and the 15 percent birth reduction fires on every biology step for every prey species.
- Why it is a red flag: this is a two-tier-era assumption ("prey overbreed when predators are absent, so penalize them") that no longer makes sense in a prey-only simulator. It is not an occasional event; it is a permanent, silent 0.85x scalar on all reproduction that biases every population trajectory and equilibrium downward, with nothing in the config or output surfacing that it is active. It is also not gated by `Tier2Enabled`, so it would persist even with the gate set to its intended false.
- Recommendation: decide intent explicitly. If the prey-only sim should not carry a no-predator penalty, gate the block on `Tier2Enabled` or remove it. If a baseline birth discount is genuinely wanted, fold it into `ReproductionMultiplier` so it is visible and tunable rather than hidden behind a now-always-true branch.
- Confidence: high.
- Verdict: confirmed. The branch and constant match; `GetTierPopulation(2)` sums only `Tier == 2` species, which never exist in this build, so the penalty is unconditional.

### F4. CSV writers format floats with current culture, corrupting output on comma-decimal locales

- Location: `SimulationRunner.cs:738-913`, `DataStructure/ScenarioResult.cs:588-925`, `BulkSimulationController.cs:428-715` (every `:F1`/`:F2`/`:F3`/`:F4`/`:P1` with no `CultureInfo`).
- What the code does: all three CSV writers format floats via bare string interpolation against the current thread culture. The input parser (`CsvBatchParser.cs:370,385,399,450-516`) and the UI deliberately use `CultureInfo.InvariantCulture` for both parse and format; the output writers do not.
- Why it is a red flag: on any locale whose decimal separator is a comma (most of Europe), every floating-point cell in `scenario_N.csv`, `aggregate.csv`, `config.csv`, and `bulk_summary.csv` is written with a comma. Because the field delimiter is also a comma, this both corrupts numeric parsing in R/pandas and shifts columns (`12.5` becomes `12,5`, two cells). The asymmetry (input invariant, output not) shows the rule was known and missed on the write side. This silently produces unreadable or misaligned output on non-US machines.
- Recommendation: format all numeric output with `CultureInfo.InvariantCulture` (mirror the pattern already in `CsvBatchParser`), or set the invariant culture on the thread for the duration of CSV generation.
- Confidence: high.
- Verdict: confirmed. Grep finds zero `CultureInfo`/`InvariantCulture` in the three writer files; every numeric interpolation is bare, while the parser and UI use invariant culture throughout. See F8 for the related `:P1` percent-format defect that compounds this.

---

## Minor findings

### F5. Pmax does not lower the realized Tier-1 performance ceiling

- Location: `EcosystemSimulator.cs:593-594,606-617,952-992`; `SimSpecies.cs:74-78,68,131-132`.
- What the code does: Step 1 sets `RawThermalPerformance` (the curve, no Pmax) and `ThermalPerformance = Raw * Pmax`. For Tier 1 the survival pathway uses `RawThermalPerformance`: `RawFinalPerformance = RawThermalPerformance * FedRate` is the Condition drain target, thermal death keys off `RawThermalPerformance == 0`, and condition death / reproduction scale key off Condition. Pmax never multiplies the curve height the Condition target sees; it enters only as a rate scaler (`drain /= Pmax`, `recovery *= Pmax`) and as a births multiplier. The Pmax-scaled `ThermalPerformance` is used only for predator demand (line 814, dead in Tier-1 build) and `FinalPerformance` (line 628, logging only).
- Why it is a red flag: Pmax is named and tooltipped "Maximum performance at optimal temperature" (`SimSpecies.cs:68`), implying it caps achievable performance height. For Tier 1 it does not cap the Condition target. A generalist (Pmax ~0.65) and a specialist (Pmax ~0.98) at the same optimal temperature drive Condition toward the same `RawFinalPerformance`, so their steady-state Condition and thermal-survival ceiling are governed only by rate scaling and the birth multiplier, not by the lower peak the curve is supposed to express. The generalist/specialist Pmax split is a central design variable (RunSpeciesList: Hexapod specialists 0.96-0.9843 vs Gelgi generalists 0.6481-0.6616), so this materially changes whether the lower curve height of generalists reduces survival as intended.
- Recommendation: confirm intent. If Pmax should lower the realized Tier-1 ceiling, make the Condition drain target `RawThermalPerformance * Pmax * FedRate`. If the rate-scaling-only treatment is deliberate (the simulator docblocks at `EcosystemSimulator.cs:67-77,944-946,1125-1130` say it is a v9 design choice), fix the `SimSpecies.cs:68` field tooltip so it does not read as a curve-height cap.
- Confidence: high.
- Verdict: confirmed. The pathway is exactly as described and `CalculatePerformance` returns the curve without Pmax. Whether it is a bug is a design-intent question; the discrepancy between the behavior and the field tooltip is real.

### F6. BiologyStep > 1 changes the trajectory, not just the speed

- Location: `SimulationRunner.cs:415-436,430`; rate scalers `EcosystemSimulator.cs:814,1063,1204,1285`; clamp `EcosystemSimulator.cs:1071`.
- What the code does: biology runs only on days where `displayDay == 1 || displayDay % BiologyStep == 0`, and every rate-based quantity inside `ProcessBiologyStep` is multiplied linearly by `BiologyStep`: predator demand (814), condition deaths (1063), births (1204), natural deaths (1285). Temperature is sampled once at the trigger day (`SimulationRunner.cs:428`) and applied with the full BiologyStep scaling, not averaged across the window.
- Why it is a red flag: linear scaling by BiologyStep is only correct for small rates. For per-capita processes it does not equal running N daily sub-steps (2 percent/day applied as one 10 percent hit every 5 days removes 10 percent, while five sequential 2 percent steps remove 9.6 percent and interleave with births differently). For larger rates the error grows and can exceed 100 percent in one step (DeathRate 0.6, severity ~1, BiologyStep 5 yields `rawDeaths` of 3x population before the `Math.Min(., population)` clamp at line 1071 saves it). A transient hot or cold day is amplified fivefold. So two runs differing only in BiologyStep are not comparable. The field is `Range(1,5)` in the inspector and exposed in the UI/bulk path; the shipped asset pins it to 1, so the impact is latent.
- Recommendation: restrict BiologyStep to 1 and remove the `* BiologyStep` scaling and modulo cadence (simplest, matches the single-step intent), or convert mortality to compounded survival `1-(1-rate)^BiologyStep` and document that BiologyStep changes dynamics.
- Confidence: high.
- Verdict: confirmed. All four scalers and the single-sample temperature are present as described.

### F7. BiologyStep day-1 special case over-counts the first window

- Location: `SimulationRunner.cs:430`.
- What the code does: `runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)`. With BiologyStep=5, biology fires on days 1, 5, 10, ..., so the first inter-biology gap is 4 days (day 1 to day 5), but the step on day 5 still scales rates by the full BiologyStep (5).
- Why it is a red flag: the day-1 special case creates one uneven interval at the start of every scenario; 5 days of births and deaths are applied across a 4-day gap, systematically over-counting the first biology window whenever BiologyStep > 1. Combined with F6 this makes the startup transient slightly wrong. At BiologyStep=1 the special case is a no-op (`day 1 % 1 == 0` anyway).
- Recommendation: drop the `displayDay == 1 ||` term, or trigger on `dayIndex % BiologyStep == 0` (0-based) so the first window is a full BiologyStep. Only relevant if BiologyStep > 1 is used.
- Confidence: high.
- Verdict: confirmed.

### F8. Rates written to CSV with the :P1 percent format are non-numeric

- Location: `DataStructure/ScenarioResult.cs:627,654,703,704`; `BulkSimulationController.cs:472,539,709,710`.
- What the code does: `CrashRate`, per-species `ExtinctionRate`, and cross-run `extinctionRate2`/`crashRate2` are written into numeric CSV columns using the .NET `P1` format, e.g. `CrashRate:P1` yields a string like `20.0 %`.
- Why it is a red flag: the `P` format multiplies by 100 and appends a culture-dependent space plus a percent sign, so a column consumed as a numeric rate contains a non-numeric string. R's `read.csv` and pandas read these as character, breaking downstream arithmetic. Under a comma-decimal locale the percent string can also contain a comma, corrupting row structure (compounding F4). Other cells in the same row are plain floats, so the format is internally inconsistent.
- Recommendation: emit rates as a bare fraction (`{CrashRate:F3}`) or as a percentage number without the `%` and space (`{(CrashRate*100):F1}`); keep all data cells machine-parseable.
- Confidence: high.
- Verdict: confirmed. `ScenarioResult.cs:627` is a `Label,Value` metadata row rather than a wide table cell, but the parsing and locale defect is identical for all cited sites.

### F9. eatingAmount: 3 on Tier-1 species is a no-op input

- Location: `RunSpeciesList.asset:25,62,99,136,173,210` (eatingAmount: 3); sole consumer `EcosystemSimulator.cs:814`; template `CsvBatchParser.cs:540`.
- What the code does: every shipped species is `tier: 0` with `eatingAmount: 3`. EatingAmount is read in exactly one biology-affecting place, `EcosystemSimulator.cs:814` (`pred.Population * pred.EatingAmount * ...`), inside the Tier-2 predator loop. The Tier-1 FedRate block (`EcosystemSimulator.cs:759-780`) never references it.
- Why it is a red flag: for a Tier-1-only run, EatingAmount has zero effect. `SimSpecies.cs:25` documents "Tier 1 = 0" and `CreateHexapod` sets it to 0f, but the live asset and the bulk template ship 3. A researcher tuning eatingAmount expecting it to change Tier-1 consumption sees no effect; the value is echoed into the `#species:` CSV table (`SimulationRunner.cs:772`, `ScenarioResult.cs:1168`) so it appears in output while doing nothing. Leftover from the predator/prey era.
- Recommendation: set eatingAmount to 0 for Tier-1 species in `RunSpeciesList.asset` and the bulk template, or have the loader zero it for tier 0; better, drop the column from the Tier-1 input surface.
- Confidence: high.
- Verdict: confirmed.

### F10. BiologyStep > 1 makes condition and natural death saturate non-linearly

- Location: `EcosystemSimulator.cs:1056-1071` (condition death), `:1285,:1299` (natural death).
- What the code does: condition death computes `rawDeaths = Population * severity * DeathRate * BiologyStep`; natural death computes `deaths = Population * rate * BiologyStep`. With the shipped DeathRate=0.6 and BiologyStep=5, `severity * DeathRate * BiologyStep` reaches `severity * 3.0`, demanding up to 300 percent of the population in one event, clamped to population via `Math.Min(wholeDeaths, (long)pop)` at lines 1071 and 1299.
- Why it is a red flag: at BiologyStep > 1 the linear `rate * BiologyStep` scaling is a coarse discretization; one bad multi-day step can wipe the entire group at once instead of spreading the die-off, and the clamp discards the un-applied excess so effective mortality saturates at 100 percent non-linearly in BiologyStep. Results at BiologyStep 1 vs 5 are not consistent for the same biology. This is the death-side instance of the general F6 issue; the shipped asset uses BiologyStep=1 so current impact is nil.
- Recommendation: if BiologyStep > 1 is a supported mode, use compounded survival `1-(1-rate)^BiologyStep`, or document that BiologyStep > 1 changes mortality semantics.
- Confidence: medium.
- Verdict: confirmed.

### F11. Condition-death survivor boost inflates the same step's births

- Location: `EcosystemSimulator.cs:1082-1086` (survivor boost) then `EcosystemSimulator.cs:1204` (reproduction), same biology step.
- What the code does: Step 7 condition death applies a survivor fitness boost `sp.Condition = oldCondition * oldPop / newPop` (capped at 1.0). Step 8 reproduction, in the same step, reads `sp.Condition` to compute `reproScale`. So a die-off that just happened raises Condition, and the boosted Condition immediately increases that step's births.
- Why it is a red flag: the boost is intended as anti-death-spiral self-correction, but because reproduction runs after it in the same step, a mass condition die-off can paradoxically increase that step's births. When many die, `oldPop/newPop` is large and Condition can jump from below the threshold to near 1.0, flipping `reproScale` from the struggling branch to near-maximal in one step. This couples death and birth in a way that can mask a collapse in the per-step birth numbers.
- Recommendation: confirm the ordering is intended. If the boost is meant only to prevent next-step spiral, apply it after reproduction or use a pre-death Condition snapshot for that step's reproduction.
- Confidence: medium.
- Verdict: confirmed. The ordering and coupling are as described; whether it is intended is a design question.

### F12. Authoritative thermal curve lacks the NaN/divide guard its UI mirrors have

- Location: `SimSpecies.cs:116-132`; guarded mirrors `ThermalGraphEditor.cs:357,364` and `ThermalParameterController.cs:562-575`; unguarded mirror `ThermalGraphUI.cs:165-171`; bulk input bounds `CsvBatchParser.cs:348`.
- What the code does: `CalculatePerformance` converts to Kelvin then evaluates the Arrhenius numerator/denominator with no guard for `T <= 0`, denominator `== 0`, or a non-finite result. With large Arrhenius constants far from the optimum the `Math.Exp` terms overflow to Infinity; when both numerator and denominator overflow, `perf = Infinity/Infinity = NaN`, and `Math.Min(1.0, NaN)`/`Math.Max(0.0, NaN)` both return NaN (NaN is not clamped away), so NaN escapes. It then flows into `RawThermalPerformance`, Condition, `ReproScale`, and the per-species CSV metric columns. The bulk CSV path validates only `upper_bound_c > lower_bound_c` and never bounds `arrhen_breadth/lower/upper` or `opt_temp_c`, so a research user can supply curve constants that trigger this.
- Why it is a red flag: the authoritative biology implementation is the only one of the relevant copies that relies on `Math.Min`/`Math.Max` to clamp, which does not strip NaN. For shipped species the lethal fade returns 0 before the Arrhenius block runs (CTminC 0/2/4), so this is not a shipped-config defect, but adversarial custom params produce NaN that silently corrupts the Condition / ThermalPerf / ReproScale / FinalPerf metric columns.
- Recommendation: after computing `perf`, clamp explicitly: `if (double.IsNaN(perf) || perf < 0) perf = 0; else if (perf > 1) perf = 1;`. Do not rely on `Math.Min`/`Math.Max` to remove NaN. Bounding `opt_temp_c`/breadth in `CsvBatchParser` would also help.
- Confidence: high.
- Verdict: confirmed, with two scope corrections from verification. (1) Population is not poisoned: every population mutation goes through `(long)` casts and on this runtime `(long)NaN == 0`, so a NaN performance value leaves the population finite (and `ApplyThermalDeath` then zeroes it). The real damage is to the per-species metric columns, not the population columns. (2) The original claim that "all three UI mirrors" guard is wrong; `ThermalGraphUI.cs` divides without a guard like the simulator. The missing-guard / clamp-does-not-strip-NaN defect is genuine.

### F13. Carrying capacity throttle is indirect; the 100xK guard is per-species not per-tier

- Location: births `EcosystemSimulator.cs:1204` (no direct K bound, soft cap removed `1217-1222`); food throttle `EcosystemSimulator.cs:759-769`; per-species cap `EcosystemSimulator.cs:668,675-679`.
- What the code does: Tier-1 births are `Population * reproScale * ReproMult * Pmax * BiologyStep` with no K-based cap. Carrying capacity throttles only indirectly: high pop gives `foodDensity = max(0, 1-pop/K) = 0`, hence `FedRate = 0`, hence the Condition drain target falls and `reproScale` shrinks. The only firm ceiling is the defensive `popCap = 100 * CarryingCapacityPerTier`, applied per species in Step 10 (not to the tier total). With the 6-species asset the aggregate Tier-1 ceiling is `6 * 500,000 = 3,000,000`, while the comment frames `100*K` as a single ecosystem ceiling.
- Why it is a red flag: carrying capacity does not bound growth on the step it is exceeded; it relies on the Condition feedback loop, so population can overshoot K before settling. The per-species guard does not bound the ecosystem total at `100*K` as the comment at lines 661-666 implies.
- Recommendation: if the intent is to bound the tier total, cap on `GetTier1Population()` or divide `popCap` by live species count; otherwise reword the comment to "per species". Confirm the startup transient stays well below `100*K` for the shipped config.
- Confidence: medium.
- Verdict: uncertain on the original "load-bearing ceiling / hard overshoot" framing; confirmed on the mechanics. Verification simulated the shipped aggregate (K=5000, ReproMult=0.45, Pmax ~0.97, near-optimal temps, no-predator penalty 0.85): drain accelerates quadratically (`effectiveDrain = drainRate*(1+severity)/Pmax`, ~0.30/day at target 0), population peaks near 1.003xK on ~day 16, then settles to ~0.7xK. The `100*K` guard is essentially never engaged at shipped parameters, so it is a defensive catch, not the operative equilibrium. The per-species-vs-tier comment mismatch and the indirect-throttle description are both accurate.

### F14. CarryingCapacity Range(100) lower bound and temperature-bounds ordering are not enforced at runtime

- Location: `SimulationInputUI.cs:291-293,324`; `SimulationConfig.cs:58,167-171,146-195`; floor `EcosystemSimulator.cs:760`.
- What the code does: `CarryingCapacityTier1` has `[Range(100, 100000)]` and `IsValid()` hard-fails when it is `<= 0`. The runtime UI reads it with `TryReadFloat` and no min/max and writes it straight to config. `TemperatureBoundsMin`/`Max` are read with no bounds and never checked for `min < max` in the UI path or in `IsValid()`.
- Why it is a red flag: the `> 0` requirement is in fact enforced (the controller calls `IsValid()` before any scenario runs and aborts), so a 0 or negative cap does not reach the engine. But the `[Range]` lower bound of 100 is enforced nowhere, so a value in (0, 100) such as 50 passes and runs; and `TemperatureBoundsMax < Min` is accepted silently. The shipped values (5000 / 0 / 40) are all valid, so this is a validation-coverage gap, not a shipped-config fault.
- Recommendation: mirror the field validation in `SimulationInputUI` and/or `IsValid`: reject `CarryingCapacityTier1 < 100` and enforce `TemperatureBoundsMin < TemperatureBoundsMax`.
- Confidence: medium.
- Verdict: uncertain. The original claim that a `<= 0` value reaches a floored engine is wrong (`IsValid()` blocks it at the controller before any run). The genuine residual gaps are the unenforced Range-100 floor and the missing bounds-order check, which are real but milder than stated.

### F15. AddSpecies Custom overload never adds the species to the list

- Location: `SpeciesDatabase.cs:601-646`; working overload `SpeciesDatabase.cs:437-491`.
- What the code does: the second `AddSpecies` overload (the `string displayname` Custom-species one) builds a fully populated `SpeciesData data` object but the body ends at line 645 without ever calling `speciesList.Add(data)`. The first overload ends with `speciesList.Add(data)` at line 490.
- Why it is a red flag: if any editor path calls this Custom overload expecting a species to be added, nothing happens; the constructed object is discarded on return. It is `#if UNITY_EDITOR` only and unreachable from the current `PopulateDefaultData` (which uses the enum overload), so it does no harm today, but it is a latent defect in a file central to ScriptableObject population and reads as an incomplete copy of the working overload.
- Recommendation: add the missing `speciesList.Add(data);` before the closing brace at `SpeciesDatabase.cs:645`, or delete the unused overload if Custom species are never populated through this method.
- Confidence: high.
- Verdict: confirmed. The overload runs to line 646 with no `Add`, and grep shows it is never called.

---

## Info findings

### F16. Tier-2 predator code path is dead in the shipped build

- Location: `EcosystemSimulator.cs:728-897,924-932` (Holling II feeding, prey removal, `CalculateHollingEfficiency`); `SimSpecies.cs:204-258` (`CreateSheplik`); default seeding `EcosystemSimulator.cs:512-514`.
- What the code does: the full Tier-2 pipeline (Holling Type II efficiency, raw/actual demand, scarcity factor, per-predator FedRate, proportional prey removal) plus `CreateSheplik` (which builds predators with EatingAmount 1.5, CTmaxC up to 80 C, Arctic CTminC -30) exist solely for Tier 2. `predators = Species.Where(s => s.Tier == 2)` is always empty in the shipped config, so `ProcessFeedingWithAccumulator` returns at the `predators.Count == 0` early-out before any predation runs.
- Why it is a red flag: about 170 lines of biology plus the EatingAmount/HuntingEfficiency/HuntingVariance fields and several inert asset values (F1, F9) exist to service a tier the build forbids. The `CTmaxC=80`/`CTminC=-30` limits are physically implausible and never reached. The math itself is correct (verified: scarcity factor min-clamped to 1, divisors guarded, `totalEaten = min(availablePrey, demand)`); the concern is dead weight that makes the simulator look like it still models predation and that F1 could reactivate.
- Recommendation: if Tier 2 is permanently out of scope, remove the predator path, `CreateSheplik`, and the `*T2` fields; if it may return, fence it behind `Tier2Enabled` and add a test that the flag and any tier-2 input are rejected together.
- Confidence: high.
- Verdict: confirmed (dead, not buggy).

### F17. InitializeDefaultSpecies seeds Tier-2 predators without consulting Tier2Enabled

- Location: `EcosystemSimulator.cs:501-523` vs gate `331-336`.
- What the code does: `InitializeDefaultSpecies` (the fallback used when no `RunSpeciesList` is supplied) unconditionally adds 3 Hexapod and 3 Sheplik species, ignoring `Tier2Enabled`. The `RunSpeciesList` path skips Tier-2 species when the gate is off; the default path does not.
- Why it is a red flag: inconsistent tier handling between the two init paths. If the fallback ever fires in the intended Tier-1-only build it injects Sheplik predators the rest of the system assumes cannot exist, reactivating the dead predator path and producing predation the user never configured. Under the shipped config `RunSpecies` is assigned, so this is a latent landmine, not an active bug.
- Recommendation: make `InitializeDefaultSpecies` respect `Tier2Enabled` (skip the `CreateSheplik` calls when off), or drop the Sheplik defaults to match the single-tier catalog.
- Confidence: high.
- Verdict: confirmed.

### F18. CrashTier carries no information in a prey-only sim

- Location: `EcosystemSimulator.cs:1392-1398` (`GetCrashedTier`); surfaced `SimulationRunner.cs:444`, `ScenarioResult.cs:23,757`.
- What the code does: `GetCrashedTier` returns 0 (all dead), 1 (Tier 1 gone), or 2 (Tier 2 gone). With no Tier-2 species `_tier2WasPopulated` is always false, so `return 2` is unreachable; and because the crash trigger is total population == 0 with only Tier 1 present, the only reachable non-(-1) result is the all-dead case (0). `CrashTier` is still written to CSV/results as a meaningful per-tier field.
- Why it is a red flag: `CrashTier` implies a multi-tier food web that does not exist here, which can mislead analysts. The `return 2` branch is dead two-tier code.
- Recommendation: drop `CrashTier` from output (or document that it is always 0/-1) and remove the unreachable Tier-2 branch.
- Confidence: medium.
- Verdict: confirmed.

### F19. Temperature and biology RNGs share the same seed, correlating the two noise streams

- Location: `SimulationRunner.cs:382-387`; `TemperatureCalculator.cs:45`; `EcosystemSimulator.cs:302`; consumers `TemperatureCalculator.cs:163-164,181` and `EcosystemSimulator.cs:809,1278`.
- What the code does: within one scenario the same `seed` is passed to `new TemperatureCalculator(seed)` and `new EcosystemSimulator(seed)`, each constructing its own `System.Random(seed)`. .NET `System.Random` with the same seed emits the same sequence. The temperature stream draws for interannual (2/year) and daily variation (1/day); the biology stream draws for hunting variance (dead in Tier-1) and natural-death variance.
- Why it is a red flag: the two streams are the same sequence consumed roughly in parallel, so day N's temperature noise and day N's natural-death variance come from the same PRNG position, inducing a deterministic correlation between environmental and biological randomness. For a research sim that intends them independent, this is hidden coupling that can bias variance and covariance estimates. The shipped species have nonzero `naturalDeathVariance` (0.01), so the biology draw fires each day.
- Recommendation: derive distinct sub-seeds for the two RNGs (for example `seed` and `seed + 0x9E3779B9`) so the streams are statistically independent while remaining reproducible.
- Confidence: medium.
- Verdict: confirmed. Low impact while Tier 2 is off and biology RNG use is minimal.

### F20. Two divergent "extinct" definitions coexist in the aggregate file

- Location: `ScenarioResult.cs:409` (PerSpeciesExtinct: final-day pop <= 0) vs `ScenarioResult.cs:477,522-544,911-922` (ExtinctionTiming.NEvents: first day reaching zero after being alive).
- What the code does: the aggregate reports two per-species extinct counts under different definitions. `PerSpeciesExtinct` counts `FinalSpeciesPopulations[key] <= 0`. `ExtinctionTiming.NEvents` counts `ExtinctionDay != -1`, which requires the species to have been alive first (`SimulationRunner.cs:1107-1109`).
- Why it is a red flag: a species configured with initial count 0 has final pop 0 (counted in `PerSpeciesExtinct`) but was never alive, so `extinctionDay` stays -1 and it is not in `NEvents`. The two sections then disagree on extinction count/rate for the same species in the same file, with no note that the definitions differ. For species that start alive, the `MIN_POPULATION_FOR_REPRODUCTION = 2` guard prevents recovery from 0, so the two agree.
- Recommendation: pick one extinction definition or clearly label the two (final-day-extinct vs ever-extinct); at minimum handle the zero-initial-population case consistently.
- Confidence: medium.
- Verdict: confirmed.

### F21. Failed scenarios silently drop from the bulk denominators

- Location: `BulkSimulationController.cs:299-303,349,470-472`; `ScenarioResult.cs:292,355,624`.
- What the code does: if a scenario task throws, it is logged and skipped (`result == null` continue), so `batchResults.Scenarios` can hold fewer entries than `batch.NumScenarios`. `CalculateAggregates` sets `TotalScenarios = Scenarios.Count` and computes CrashRate over completed scenarios, while `BulkRunSummary.NumScenarios` keeps the requested count. The per-run table prints `run.NumScenarios` (requested) in the Scenarios column while `Survived + Crashed` and the aggregate "Scenarios Run" use the completed count.
- Why it is a red flag: the Scenarios column can exceed `Survived + Crashed`, and the aggregate denominator silently drops the failure with no error marker in the output, biasing crash/extinction rates. Only manifests when a Task actually throws.
- Recommendation: record the failed-scenario count in the output, or make the denominator explicit (requested vs completed) so a partial run is visible.
- Confidence: medium.
- Verdict: confirmed.

### F22. Cross-run empty-case fill of 0 is indistinguishable from a real near-zero value

- Location: `BulkSimulationController.cs:592-653`, empty-case fill `635-637`; `ScenarioResult.cs:513-518`.
- What the code does: cross-run `GrandMeanCondition`/`BirthRate`/`PopCv` average each run's `SurvivedMean` over runs with `NSurvived > 0`; if no run has any surviving scenario for that species, the values are emitted as 0 (and the StdDev collapses to 0).
- Why it is a red flag: a species that held, for example, Condition ~0.4 for most of its life in every scenario but always eventually crashed reports `MeanCondition 0.000`, which reads as "zero condition" rather than "no surviving runs". The sentinel 0 is indistinguishable from a genuine low physiological value. The header carries `RunsSurvived` so a careful reader can disambiguate, but the fill value is a real-looking number.
- Recommendation: emit an explicit sentinel (blank or NA) when `runsSurvivedCount == 0` so "no data" is distinguishable from a real low mean.
- Confidence: medium.
- Verdict: confirmed.

### F23. Dead Tier-2 aggregate and condition computations (always zero, never written)

- Location: `ScenarioResult.cs:254-258,266-268,326-362` (Tier-2 aggregate fields); `EcosystemSimulator.cs:1336-1337` (AvgConditionT2).
- What the code does: `CalculateAggregates` fully computes Tier-2 final-population stats and Tier-2 condition grand means, and `EcosystemSimulator` computes `AvgConditionT2 = popT2 > 0 ? sumT2/popT2 : 0f`. Because no Tier-2 species ever load, `popT2` is always 0, so these are always exactly 0. `ToAggregateCsv` never writes any Tier-2 column (it emits only Tier-1), and the daily `AvgConditionT2` column is gated by `if (tier2)`.
- Why it is a red flag: dead two-tier residue; a parallel set of Tier-2 statistics that always read zero, computed and stored on every scenario and aggregate but never output. Wasted work and a schema that implies predators exist. Note: the stale comments at `ScenarioResult.cs:585,774,791` mention a `Tier2Pop` aggregate column that is not actually written.
- Recommendation: remove the Tier-2 aggregate and condition fields and their accumulation, or gate them on `Tier2Enabled`, to match the single-tier output reality.
- Confidence: high.
- Verdict: confirmed.

### F24. Dead leftovers: _thermalDeathAccumulators, MIN_FINAL_PERF_FOR_NATURAL_DEATH, SetSeed/Reset, GetYear, float accumulators, always-zero Tier-2 summary stats

- Location: `EcosystemSimulator.cs:153,485,494,223` (`_thermalDeathAccumulators`); `SimSpecies.cs:56` (`MIN_FINAL_PERF_FOR_NATURAL_DEATH`); `EcosystemSimulator.cs:308-311` (`SetSeed`) and `TemperatureCalculator.cs:51-56` (`Reset`); `TemperatureCalculator.cs:201-204` (`GetYear`); accumulator dictionaries `EcosystemSimulator.cs:150-154`; Tier-2 summary stats `SimulationRunner.cs:949,964-971,1015-1021`.
- What the code does: this groups several confirmed dead/leftover items that are harmless individually. `_thermalDeathAccumulators` is allocated, cleared, and initialized but never read or written (thermal death is an instant whole-population kill; line 223 already labels it dead). `MIN_FINAL_PERF_FOR_NATURAL_DEATH` is a public const never referenced now that natural death is flat-rate. `SetSeed`/`Reset`/`GetYear` have zero call sites (each scenario builds fresh instances seeded via constructors; year is computed inline at `SimulationRunner.cs:426`, duplicating `GetYear`). The four event accumulators use `float`, whose epsilon at populations of 10^5-10^6 (which the `100*K` guard permits) swamps the sub-integer residual carry the accumulators exist to preserve. `GetSummary`/`AggregateResults` compute Max/Min/Final Tier-2 population stats that are permanently 0.
- Why it is a red flag: each implies behavior or precision the code no longer has, which can mislead a future editor. The float accumulators degrade the fractional-conservation guarantee in the high-population regime, though the absolute error is tiny relative to population and, per F13, the shipped run settles near ~0.7xK where the regime is rarely entered.
- Recommendation: delete the unused members and the always-zero Tier-2 stats (or gate the latter on `Tier2Enabled`); switch the accumulator dictionaries to `double` (cheap, removes the precision cliff) or document that fractional carry is only exact below ~10^5; route the year convention through `GetYear` if it is kept.
- Confidence: high.
- Verdict: confirmed for all listed items. The float-accumulator precision concern is low practical impact.

---

## Input variable impact

Every bulk CSV column and config field, with where it is parsed and used and its effect on the simulated output. None and low impact entries are at the top so the inert and weak inputs are visible first. "Source" is `bulk-csv` (per-row CSV input) or `config` (SimulationConfig SO / standard mode). Several none-impact rows are tier-transition or game-era leftovers and overlap the findings above.

| Name | Source | Parsed at | Used at | Impact | Note |
|------|--------|-----------|---------|--------|------|
| spN_eating (EatingAmount) | bulk-csv | CsvBatchParser.cs:251 | EcosystemSimulator.cs:814 (Tier-2 only) | none | Read only on the Tier-2 predator demand path; tier must be 0, so inert. See F9. |
| spN_hunt_var (HuntingVariance) | bulk-csv | CsvBatchParser.cs:259 | EcosystemSimulator.cs:809 (Tier-2 only) | none | Variance on predator hunting success only; Tier-1 FedRate has no random term. Inert. |
| use_carrying_cap (DEPRECATED) | bulk-csv | CsvBatchParser.cs:198 | NONE | none | Deprecated v11.1; only logs a warning, never stored. Carrying capacity is unconditionally on. |
| index (SpeciesData) | config | BulkSimulationController.cs:735 | NONE in sim core | none | Set on every species, read only by the editor UI. Inert ordering field. |
| eating/reproduction/deathThreshold/deathRate/thermalBreadth Stars | config | RunSpeciesList.asset | NONE in sim core | none | Game-UI star ratings; not members of SimSpecies. Leftover from the interactive game. |
| temperatureThresholdText / reproductionRateText / description | config | RunSpeciesList.asset | NONE in sim core | none | Game-UI display strings; not consumed by the simulation. |
| icon (SpeciesData Sprite) | config | RunSpeciesList.asset | NONE in sim core | none | Sprite ref for game/edit UI only. Inert for headless sim. |
| SpeciesDatabase back-ref (RunSpeciesList) | config | RunSpeciesList.asset:15 | NONE in sim core | none | Null (fileID 0) in shipped asset; runner reads speciesList directly. Inert. |
| speciesName / variant enum (SpeciesData) | config | BulkSimulationController.cs:726-731 | EcosystemSimulator.cs:341-342,353-355 (fallback only) | low | Shipped data always populates labels, so enums are labels-of-last-resort and feed the internal ThermalVariant bucket (never written to output). Do not set biology params on the bulk path. |
| spN_tier | bulk-csv | CsvBatchParser.cs:249 | EcosystemSimulator.cs:356 | low | Must equal 0 or the row is rejected; only one legal value, carries no user information. Two-tier leftover. |
| Tier2Enabled | config | SimulationConfig.cs:130 (asset 1) | SimulationController.cs:312,374; EcosystemSimulator.cs:331-336; SimulationRunner.cs:802 | low | Shipped asset is TRUE against the false default; with zero Tier-2 species the only effect is emitting empty Tier-2 CSV columns. See F1/F2. |
| warming_bias / WarmingBias | bulk-csv / config | CsvBatchParser.cs:180 / SimulationConfig.cs:98 | TemperatureCalculator.cs:164-166 | low | Mean is subtracted out (biasMean), so it adds no warming trend; only skews interannual distribution shape. Fully gated off when interannual variation is false. Asset 0. |
| randomness_growth / RandomnessGrowthRate | bulk-csv / config | CsvBatchParser.cs:182 / SimulationConfig.cs:108 | TemperatureCalculator.cs:178 | low | Adds growth*year to daily noise amplitude; zero in year 1, asset 0. Slow noise creep on multi-year runs only. |
| variability_mag / VariabilityMagnitude | bulk-csv / config | CsvBatchParser.cs:179 / SimulationConfig.cs:95 | TemperatureCalculator.cs:163-164 | low | Interannual range; gated by interannual_variation (off in asset) AND 0 in asset, so doubly inert as shipped. |
| spN_natural_death_var (NaturalDeathVariance) | bulk-csv | CsvBatchParser.cs:257 | EcosystemSimulator.cs:1278 | low | Symmetric zero-mean noise on the natural death rate; Max(0,...) floor slightly biases the realized mean upward at large variance. Live on Tier-1. |
| batch_name | bulk-csv | CsvBatchParser.cs:173 | BulkSimulationController.cs:154,323,472 | medium | Output identity / ZIP folder name; required + unique. No effect on simulated numbers. |
| spN_name | bulk-csv | CsvBatchParser.cs:247 | SimSpecies.FullName; BulkSimulationController.cs:739 | medium | Identity / output key and merge match-key; blank skips the slot. No direct biology effect. |
| spN_variant | bulk-csv | CsvBatchParser.cs:248 | BulkSimulationController.cs:731,738 | medium | Display label; also picks pmax/ctmin/ctmax defaults only when those optional columns are blank. If supplied, purely a label. |
| daily_var_range / DailyVariationRange | bulk-csv / config | CsvBatchParser.cs:181 / SimulationConfig.cs:105 | TemperatureCalculator.cs:178-181 | medium | Base daily noise amplitude; damped to ~42% by autocorrelation when on (see appendix). Overridden by a temperature timeseries. |
| autocorrelated / Autocorrelated | bulk-csv / config | CsvBatchParser.cs:183 / SimulationConfig.cs:102 | TemperatureCalculator.cs:184-192 | medium | true = 70% previous-day + 30% new (smooth); false = white noise. Changes texture and effective magnitude of daily variation. |
| interannual_variation / InterannualVariation | bulk-csv / config | CsvBatchParser.cs:184 / SimulationConfig.cs:91 | TemperatureCalculator.cs:148-150 | medium | Master gate for variability_mag AND warming_bias; off in shipped asset, so both are dead as shipped. |
| temp_min / TemperatureBoundsMin | bulk-csv / config | CsvBatchParser.cs:185 / SimulationConfig.cs:112 | TemperatureCalculator.cs:74,84 | medium | Hard floor clamp on daily temperature; bites only when temp would fall below it. |
| temp_max / TemperatureBoundsMax | bulk-csv / config | CsvBatchParser.cs:186 / SimulationConfig.cs:115 | TemperatureCalculator.cs:74,84 | medium | Hard ceiling clamp; can mask lethal CTmax events if set too low. |
| condition_drain_rate / ConditionDrainRate | bulk-csv / config | CsvBatchParser.cs:190 / SimulationConfig.cs:68 | EcosystemSimulator.cs:963,974 | medium | Global Condition drain; fallback when species spN_condition_drain_rate is negative. Affects condition-death timing and reproduction throttle. |
| condition_recovery_rate / ConditionRecoveryRate | bulk-csv / config | CsvBatchParser.cs:191 / SimulationConfig.cs:74 | EcosystemSimulator.cs:964,985 | medium | Global Condition recovery; fallback for negative per-species recovery. How fast populations rebound. |
| spN_condition_drain_rate | bulk-csv | CsvBatchParser.cs:276 (default -1 inherit) | EcosystemSimulator.cs:963 | medium | Per-species drain override; negative inherits the global. Blank column inherits. |
| spN_condition_recovery_rate | bulk-csv | CsvBatchParser.cs:277 (default -1 inherit) | EcosystemSimulator.cs:964 | medium | Per-species recovery override; negative inherits the global. |
| temperature_timeseries_file | bulk-csv | CsvBatchParser.cs:193 | SimulationController.cs:317-334; TemperatureCalculator.cs:66-74 | medium | When present and the file exists, REPLACES the entire parametric temperature model. Editor/standalone only; in WebGL File.Exists fails and it falls back with a warning, so inert in WebGL. |
| spN_hunt_eff (HuntingEfficiency) | bulk-csv | CsvBatchParser.cs:258 | EcosystemSimulator.cs:769 | medium | For Tier 1 it IS live: linearly scales feeding from the food pool, capping max FedRate/Condition. Semantic repurposed from hunting to resource extraction. |
| spN_arrhen_lower (ArrhenLower) | bulk-csv | CsvBatchParser.cs:262 | SimSpecies.cs:119,126-127 | medium | Shapes cold-side fall-off of the Arrhenius curve; subtler than breadth/Topt. |
| spN_arrhen_upper (ArrhenUpper) | bulk-csv | CsvBatchParser.cs:263 | SimSpecies.cs:120,126-127 | medium | Shapes warm-side fall-off; subtler than breadth/Topt. |
| spN_lower_bound_c (LowerBoundK) | bulk-csv | CsvBatchParser.cs:264 | SimSpecies.cs:121,126-127 | medium | Reference temp for the cold-side Arrhenius term. Distinct from lethal CTmin. |
| spN_upper_bound_c (UpperBoundK) | bulk-csv | CsvBatchParser.cs:265 | SimSpecies.cs:122,126-127 | medium | Reference temp for the warm-side Arrhenius term. Distinct from lethal CTmax. |
| spN_temp_offset (TemperatureDebuff) | bulk-csv | CsvBatchParser.cs:274 (default 0) | SimSpecies.cs:97 | medium | Per-species shift of experienced temperature, applied before fade and Arrhenius. Default 0 (no effect). |
| ConditionDrainRate / ConditionRecoveryRate (config dup of above) | config | SimulationConfig.cs:68,74 | EcosystemSimulator.cs:963-964,974,985 | medium | Standard-mode globals; bulk mode uses the CSV columns. |
| days / DaysPerScenario | bulk-csv / config | CsvBatchParser.cs:174 / SimulationConfig.cs:37 | SimulationRunner.cs:415 | high | Scenario length. Bulk mode ignores DaysPerScenario in favor of the CSV days column. |
| num_scenarios / NumberOfScenarios | bulk-csv / config | CsvBatchParser.cs:175 / SimulationConfig.cs:44 | scenario loop + seed offset | high | Number of seeded replicates; changes statistics and the RNG offset per scenario. |
| base_temp / BaseTemperature | bulk-csv / config | CsvBatchParser.cs:176 / SimulationConfig.cs:80 | TemperatureCalculator.cs:77 | high | Mean temperature; dominant driver of every species' thermal performance. Overridden by a temperature timeseries. |
| seasonal_amp / SeasonalAmplitude | bulk-csv / config | CsvBatchParser.cs:177 / SimulationConfig.cs:84 | TemperatureCalculator.cs:128 | high | Amplitude of the sinusoidal seasonal swing; strong near CTmin/CTmax. Overridden by a timeseries. |
| climate_trend / ClimateTrend | bulk-csv / config | CsvBatchParser.cs:178 / SimulationConfig.cs:88 | TemperatureCalculator.cs:134-137 | high | The only long-term-mean component (deg C/year). Asset is 0, so no warming as shipped; the single field to change for climate scenarios. Negligible over a single 365-day run. |
| carrying_cap_t1 / CarryingCapacityTier1 | bulk-csv / config | CsvBatchParser.cs:187 / SimulationConfig.cs:59 | EcosystemSimulator.cs:760-761,668 | high | Shared Tier-1 food-pool size; sets equilibrium population scale and the 100x overflow ceiling. Always on. |
| spN_pop | bulk-csv | CsvBatchParser.cs:250 | EcosystemSimulator.cs:357 | high | Initial population; affects early-transient food density and establishment. |
| spN_repro_mult (ReproductionMultiplier) | bulk-csv | CsvBatchParser.cs:252 | EcosystemSimulator.cs:1204 | high | Linear multiplier on births every reproducing step; directly scales growth rate. |
| spN_death_thresh (DeathThreshold) | bulk-csv | CsvBatchParser.cs:253 | EcosystemSimulator.cs:1059,1062 | high | Impactful but MISLABELED: comments say "thermal death", but it is compared against Condition and drives graduated condition-death severity. Instant thermal death keys off RawThermalPerf==0, not this. |
| spN_death_rate (DeathRate) | bulk-csv | CsvBatchParser.cs:254 | EcosystemSimulator.cs:1063 | high | Max fraction lost per step to condition death (scaled by severity). MISLABELED "thermal death" in comments; governs condition death. |
| spN_repro_thresh (ReproThreshold) | bulk-csv | CsvBatchParser.cs:255 | EcosystemSimulator.cs:1169-1190 | high | Condition inflection point for the graduated reproduction scale. Shapes the birth response to health. |
| spN_natural_death_rate (NaturalDeathRate) | bulk-csv | CsvBatchParser.cs:256 | EcosystemSimulator.cs:1279,1285 | high | Flat per-step background mortality; a persistent floor reproduction must beat. |
| spN_opt_temp_c (OptimalTempK) | bulk-csv | CsvBatchParser.cs:260 | SimSpecies.cs:117,125-127 | high | Topt of the thermal curve; sets where peak performance sits vs base_temp. Converted C to K. |
| spN_arrhen_breadth (ArrhenBreadth) | bulk-csv | CsvBatchParser.cs:261 | SimSpecies.cs:118,125 | high | Width/peak sharpness of the thermal curve (specialist vs generalist). Major fall-off determinant. |
| spN_pmax (Pmax) | bulk-csv | CsvBatchParser.cs:271 | EcosystemSimulator.cs:594,974,985,1204 | high | Multi-channel: Condition rate scaling and births multiplier. Note F5: does NOT cap the Tier-1 Condition target height. Clamped >= 1e-4 before division. |
| spN_ctmin (CTminC) | bulk-csv | CsvBatchParser.cs:272 | SimSpecies.cs:100-107 | high | Critical thermal minimum; at/below it RawThermalPerf=0 triggers INSTANT total wipeout. |
| spN_ctmax (CTmaxC) | bulk-csv | CsvBatchParser.cs:273 | SimSpecies.cs:100-112 | high | Critical thermal maximum; at/above it performance=0, instant wipeout. temp_max clamp can prevent the modeled temp from reaching it. |
| BiologyStep | config | SimulationConfig.cs:29 (asset 1) | SimulationRunner.cs:430; EcosystemSimulator.cs:814,1063,1204,1285 | high | Coarse Euler step size: gates biology cadence AND scales all per-step rates. >1 changes the trajectory, not just speed (F6/F7/F10). Not a bulk CSV column; bulk runs use the inspector value. |
| RunSpecies | config | SimulationConfig.cs:123 | SimulationRunner.cs:403-405; EcosystemSimulator.InitializeFromRunSpeciesList | high | Standard-mode species roster (6 Tier-0 entries shipped). Bulk mode replaces it with a CSV-built list. |
| RandomSeed | config | SimulationConfig.cs:139 (asset 12345) | seed = RandomSeed + scenarioIndex | high | Base RNG seed; -1 = system time. Used in both standard and bulk modes (bulk reads it from the inspector, not the CSV). Changes the whole stochastic realization. |

---

## Refuted or low confidence appendix

Claims that verification knocked down or substantially narrowed. Kept for traceability.

- Refuted: Kelvin divide-by-zero producing NaN for OptimalTempK/T/LowerBoundK == 0 (`SimSpecies.cs:116-127`). Tested: only UpperBoundK == 0 yields NaN, and that case overlaps F12's general overflow finding. The other three cited cases produce a clamped 0 or 1, not NaN. The "no lower bound on opt_temp_c" observation is true but does not cause the claimed NaN.
- Refuted: lethal-fade negative-denominator garbage when CTmaxC < CTminC (`SimSpecies.cs:99-112`). Tested: the two zeroing guards (`tC <= CTminC`, `tC >= CTmaxC`) cover the entire real line for an inverted window, so the divide-by-tw cosine branches never execute; fade is always 0. The "no validation prevents CTmaxC <= CTminC" point is factually true but causes no incorrect behavior.
- Refuted: `(long)deaths` cast at `EcosystemSimulator.cs:1024` writing `long.MinValue` from NaN/Inf into the per-species thermal-death counter. On this runtime `(long)NaN == 0` and `(long)+Inf == long.MaxValue`; only `(long)-Inf == long.MinValue`. And `deaths = sp.Population` is always finite (the NaN, when it occurs, is in the performance value, not the count). A real style inconsistency, but the corruption mechanism does not occur.
- Low confidence / latent only: Step 8 births / Step 10 cap ordering (`EcosystemSimulator.cs:646-686`) lets `(long)Population` casts run on uncapped values before the `100*K` clamp. Accurate as a defensive-ordering description, but with carrying capacity always on, integer births cannot drive a sane population to 9.2e18 in one step, so there is no demonstrable runtime fault.
- Low confidence / latent only: speciesMeta lookup key `{name}_{vlabel}` vs `SimSpecies.FullName` fallback divergence (`ScenarioResult.cs:594-610`). Real difference between the two empty-label fallback rules, but the validated bulk path requires a non-empty variant, so it is not currently reachable; an empty label would mislabel Variant/Tier as Unknown/?.
- Narrowed: cross-scenario `ComputeAggStat` float variance (`ScenarioResult.cs:484-520`). Confirmed float sums risk cancellation for population-magnitude metrics, but the only population-magnitude StdDev actually emitted to CSV is `MeanPop_StdDev`; the per-scenario stats paths use double. Folded into the float-precision theme; visible CSV symptom is one column.
- Narrowed: GetSummary min (pop >= 1) vs ComputePopulationStats min (includes 0) (`SimulationRunner.cs:963-965` vs `657-663`). Both definitions exist in the data model, but the lowest-nonzero value is not written to any CSV (results UI only), so the aggregate file itself does not show the conflict.
- Refuted (justification only): the claim that flipping `Tier2Enabled` changes biology for the shipped data. It changes only the CSV column schema (F2); population numbers are unchanged because no Tier-2 species exist and the no-predator penalty (F3) is not gated by the flag.
- Noted and safe (not findings): the seasonal phase comment at `TemperatureCalculator.cs:124` is wrong ("coldest at day 0" but the sinusoid puts the mean at day 0); the final temperature clamp can mask a positive climate trend on multi-decade runs once it saturates at MaxTemp; the shipped asset zeroes the climate/interannual fields so the warming features the calculator advertises are inert by default; `HasCrashed` uses float `== 0` but is safe because Step 10 rounds populations to integers (a NaN population would evade detection, but per F12 the population never actually goes NaN). The narrow-band lethal-fade midpoint artifact (`SimSpecies.cs:99-114`, fadeFactor stays 1.0 at the center of a sub-4-degree band) affects only pathological custom configs; shipped CTmax-CTmin is 35.
