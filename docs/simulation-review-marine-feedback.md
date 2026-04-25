# Marine Scientist's Review

Independent feedback on [`simulation-review.md`](./simulation-review.md) from a marine-ecology / physiological-ecology perspective. Produced by a reviewer-persona agent given the review document and direct access to the source code to spot-check claims. The reviewer's stance is biology-first — code quality is out of scope.

## 1. Overall stance

The simulator is **broadly sound in its mechanistic spine** — Sharpe–Schoolfield/Arrhenius thermal performance, Holling Type II functional response, an asymmetric condition-loss/recovery kernel grounded in the Buckley/Huey heat-debt literature, and stochastic seasonal + interannual + red-noise temperature. Those are the right building blocks for a climate-variability/specialist–generalist study, and the separation of instant thermal kills from chronic condition death is a genuine improvement over the binary-cliff approach common in pedagogical ecology simulators.

That said, a handful of the review's flagged issues would, if left unfixed, quietly change the sign of study conclusions rather than just shift numbers. The hidden warming from `WarmingBias`, the pooled FedRate across co-occurring predators, and the first-mover bias in the carrying cap are each capable of producing spurious specialist-vs-generalist differentials. And there are several biological omissions the review doesn't discuss — most importantly thermal acclimation, density-dependent mortality, and the lack of a resource base for prey — that MEE reviewers will absolutely ask about.

## 2. Verdict on each flagged concern

**#1 — Pooled FedRate across predators (ISSUE).**
**Agree. Severity correct; arguably too low.** The code at lines 586–596 confirms a single `fedRate = totalEaten / totalRawDemand` is written to every predator. This collapses the mechanistic role of `HuntingEfficiency` in any multi-predator run. Ecologically this is the classic distinction between scramble and contest competition (Nicholson 1954; Case 2000, *An Illustrated Guide to Theoretical Ecology*). If you care about specialist–generalist coexistence under shared prey — which the stated research goal says you do — this *is* the competitive signal, and the model currently doesn't have it. I'd tag this as **critical**. Per-predator bookkeeping is the standard textbook treatment (Murdoch et al. 2003, *Consumer-Resource Dynamics*, PUP).

**#2 — Hidden warming from `WarmingBias > 1` (ISSUE).**
**Agree strongly. Severity correct.** Confirmed at line 93 of `TemperatureCalculator.cs`: `warmPart = rng × VariabilityMagnitude × WarmingBias` with `coldPart` bounded below by `-VariabilityMagnitude`. The authors' arithmetic is right — this injects ~0.25°C/yr of unattributed warming at default settings. For a study that *measures* climate sensitivity, this is not a cosmetic bug; it conflates two axes the paper intends to separate. Re-centering the distribution is the right fix. For prior art on how climate-variability papers separate mean shift from variance/skew, see Vasseur et al. (2014, *Proc R Soc B* 281:20132612) and Lawson et al. (2015, *Ecol Lett* 18:1234).

**#3 — First-mover bias in carrying cap (ISSUE).**
**Agree. Severity correct.** Lines 914–920 confirm `tierPop = GetTierPopulation(1)` is queried *after* prior species' births have been committed earlier in the same day-loop, so list order matters. This is a genuine methodological flaw rather than a calibration question — it will produce competitive exclusion purely from species-list ordering in long runs. Snapshotting tier pop before the reproduction loop is standard practice; alternatively, random shuffling each step (Grimm & Railsback 2005, *Individual-based Modeling and Ecology*, PUP, §6.4 "concurrent updating") is a well-established remedy.

**#4 — Euler overshoot at `BiologyStep > 1` (ISSUE).**
**Partial agree. Severity too high in practice.** The math is correct, but the default is `BiologyStep = 1` and there's no evidence of validated science at higher values. Tag as **CONCERN with a validator check**, or document that `BiologyStep = 1` is the only supported setting. The exact-exponential form `Cond = target + (Cond − target) × exp(−rate × step)` is the correct fix if they ever want to support coarser steps.

**#5 — Seasonal phase comment wrong (ISSUE).**
**Agree. Severity too high — this is MINOR.** Line 63 of `TemperatureCalculator.cs` does mis-describe the phase. But the formula is consistent with itself; this is a documentation bug that will confuse code readers, not a biological error. Call it MINOR. That said: a real marine modeller would normally phase the sine so that day 0 is either the winter solstice or January 1 — the current formulation means "day 0" is neutral spring, which is an arbitrary choice worth thinking about when you write the methods.

**#6 — `NO_PREDATOR_PENALTY = 0.85` (CONCERN).**
**Agree. Severity correct.** This is ecologically backwards. Mesopredator release and prey release are the expected responses to predator removal (Crooks & Soulé 1999 *Nature* 400:563; Estes et al. 2011 *Science* 333:301). A 15% birth penalty when predators vanish is inconsistent with decades of trophic-cascade literature. Delete it or re-motivate. One defensible mechanism would be *genetic load* (Lynch & Conery 2003) — weaker individuals no longer culled — but the model doesn't track individuals, so that's post-hoc.

**#7 — Default `ReproThreshold = 0.25 < DeathThreshold = 0.3` (CONCERN).**
**Agree. Severity correct.** Confirmed in `SimSpecies.CreateHexapod`/`CreateSheplik` (lines 128–131, 185–188). A species "actively dying of low condition while still producing healthy-branch reproduction" is nonsense physiologically — reproductive tissue is among the first to atrophy under nutritional stress (Reznick 1985 *Oikos* 44:257; Bunnell et al. 2007 *Trans Am Fish Soc* 136:67). Easy validator fix.

**#8 — Recovery multiplier `(1 + target²) × Pmax` can be < 1 (CONCERN).**
**Agree. Severity correct.** The authors correctly identify a sign problem: the quadratic "boost" plus Pmax scaling can produce sub-baseline recovery for low-Pmax generalists. This breaks the stated intent that generalists recover *equally fast or slower* than specialists, not that they recover *sub-linearly relative to base*. This interacts with concern #2 on Jensen's inequality papers (Martin & Huey 2008 *Am Nat* 171:E102; Denny 2017 *JEB* 220:139) — the asymmetry between rising and falling phases is exactly what generates Jensen-type effects at the ensemble level, and getting the numerical asymmetry wrong for a specific cohort will propagate.

**#9 — Recovery at very low target / no "point of no return" (CONCERN).**
**Partial agree. Severity about right.** Gosselin et al. (2021 *J Exp Biol* 224:jeb240119, "Are the thermal limits of freshwater fishes realistic?") is the right citation — they make the case that CTmax/CTmin exposures cause cumulative damage that isn't reversible at benign temperatures. More fundamentally, Sokolova (2013 *Integr Comp Biol* 53:597) on "bioenergetics-based framework" for cumulative stress damage is the key synthesis. However, for the stated research question (specialist-generalist under variability) this probably isn't load-bearing — the short-term dynamics dominate before "point of no return" semantics matter. INFO/CONCERN is correct.

**#10 — Survivor fitness boost inconsistency (CONCERN).**
**Agree. Severity correct.** The "dead were weakest" assumption is defensible for condition-driven death but not for random natural death (accidents, old age). In population biology this is the distinction between *condition-dependent* and *condition-independent* mortality (Clutton-Brock et al. 1987 *Nature* 325:797; Coulson et al. 2001 *Science* 292:1528). The inconsistency across death types is a real concern — either apply to none, or separate the fractions that are condition-dependent. I'd push toward "apply to none" since the accumulator is already conservative; the survivor-boost mechanic is a *correction* to a bug that probably doesn't exist once deaths are fractional.

**#11 — No prey-variant preference (CONCERN).**
**Agree. Severity correct — arguably too low for the stated research goal.** Prey switching (Murdoch 1969 *Ecol Monogr* 39:335) is one of the major mechanisms that stabilise multi-prey systems. If you're modeling Arctic/Common/Tropical prey variants and asking what climate does to predator–prey coexistence, *no preference* is a substantive assumption, not a minor simplification. The thermal mismatch between a cold-adapted predator and a warm-water prey community is central to the marine range-shift literature (Pinsky et al. 2013 *Science* 341:1239; Cheung et al. 2013 *Nature* 497:365). Recommend elevating this to an explicit scoping statement in the methods.

**#12 — Prey `FedRate` always 1.0 (CONCERN).**
**Agree. Severity correct.** Legitimate scope choice; document it as "closed top-down system, no bottom-up limitation." But see my concern about density-dependence below.

**#13 — `NEWBORN_CONDITION = 0.5` arbitrary (CONCERN).**
**Agree. Severity correct.** The authors correctly identify that this creates a feedback dependent on reproduction rate. Real-world parallel: maternal effects and provisioning (Mousseau & Fox 1998 *TREE* 13:403; Marshall et al. 2010 *Ecology* 91:2862). Making newborn condition a function of parent condition (e.g., `min(parent × 0.8, 0.5)`) would be more defensible than a flat 0.5.

**#14 — Reproduction rates very fast (CONCERN).**
**Agree. Severity correct.** At 0.4/day per-capita, this is appropriate for copepods or early-life-stage invertebrates (r-selected marine zooplankton), not for reef fish or crustaceans. Just document the intended taxa. If the paper is pitched as "marine ecosystem" in general terms, reviewers will ask for a generation-time calibration.

**#15 — Autocorrelation fixed at 0.7/0.3 (CONCERN).**
**Agree. Severity correct.** Red-noise colour is a first-order parameter of the climate-variability question (Ripa & Lundberg 1996 *Proc R Soc B* 263:1751; García-Carreras & Reuman 2011 *J Anim Ecol* 80:1042). Expose it or justify 0.7 with a citation to observed SST autocorrelation at the timescale you're modelling (e.g., Vasseur & Yodzis 2004 *Ecology* 85:1146 estimate ocean colour at β ≈ 0.5–1.0).

**#16 — `LETHAL_TRANSITION_WIDTH = 2°C` global (MINOR).**
**Agree. Severity correct.** Species-level variation in the "cooking zone" is real (Schulte et al. 2011 *Integr Comp Biol* 51:691 on thermal tolerance plasticity), but this isn't where the paper will rise or fall.

**#17 — 365 days, no leap year (MINOR).**
**Agree. Severity correct.** Nobody will care. Keep.

**#18 — No biology invariant tests (INFO).**
**Agree. Severity correct.** This is code hygiene, not biology.

## 3. What the review missed

Several biological phenomena aren't discussed and *do* matter for the stated research goal:

- **No thermal acclimation / plasticity.** Most marine ectotherms shift their TPCs over days–weeks of exposure (Seebacher et al. 2015 *Nat Clim Change* 5:61; Gunderson & Stillman 2015 *Proc R Soc B* 282:20150401). A fixed TPC over decade-scale simulations is a substantive assumption that changes which species "win" under climate variability — acclimatisers should outperform fixed-curve specialists. If the paper argues the specialist–generalist axis, it should explicitly say it's modelling the **fully-plastic-is-a-third-axis-we-ignore** case, or add a simple acclimation term (e.g., a slow OptimalTempK tracking of ambient per Angilletta 2009, *Thermal Adaptation*, OUP).

- **No density-dependent natural mortality.** The model uses a flat `NaturalDeathRate`. Real marine populations show strong density-dependent mortality, especially at high densities (Rose et al. 2001 *Fish Fish* 2:293; Hixon & Webster 2002 in *Coral Reef Fishes*). At high populations near the carrying cap, disease and competition drive mortality upward. Without this, extinction dynamics under stress will be too abrupt.

- **No genetic variation or adaptation.** Over 20–50 year runs under warming, heritable shifts in thermal tolerance are potentially larger than the warming signal itself in fast-generation taxa (Sgrò et al. 2010 *Annu Rev Entomol* 56:143; Kelly 2019 *Curr Opin Insect Sci* 35:49). Defensible to omit for a process-focused study, but must be stated as a scope limit.

- **No age/size structure.** Reproduction and mortality are both strongly size-structured in marine systems (Beverton & Holt 1957; more recently Barneche et al. 2018 *Science* 360:642 on maternal size–reproductive output scaling). A flat per-capita reproduction rate ignores this. For a 2-tier abstract model this is defensible but worth stating.

- **No phenology.** Because reproduction is daily and condition-gated rather than seasonal, the model can't represent mismatch dynamics (Edwards & Richardson 2004 *Nature* 430:881; Durant et al. 2007 *Clim Res* 33:271). This is arguably *the* central topic in marine climate ecology. If the paper makes claims about "seasonal mismatch" under warming, this omission is fatal.

- **No behavioural thermoregulation (beyond `TemperatureDebuff`).** `TemperatureDebuff` as a static field half-models this. Real thermoregulation is adaptive (Huey 1974 *Science* 184:1001; Kearney et al. 2009 *PNAS* 106:3835).

- **No stoichiometry/nutrient feedback.** Fine for this scope.

For the stated research goal (**specialist–generalist tradeoff + Jensen's inequality in food webs under climate variability**), the *must-address* missing items are: (a) **acclimation/plasticity as an acknowledged scope boundary**, (b) **density-dependent natural mortality** (at least a toggle), and (c) **phenology statement** if reproduction timing matters to any claim. The rest can be scoped out in the methods.

## 4. What peer review at MEE will likely catch

Reviewer #2 almost certainly asks:

1. **"Why is the thermal performance curve static?"** Any MEE reviewer working in ectotherm physiology will ask about acclimation. You need a defensive paragraph citing Angilletta 2009, Seebacher 2015, Gunderson & Stillman 2015 explaining why fixed TPCs are a reasonable simplification *for your question*, not why they're realistic.

2. **"Sensitivity analysis on the condition drain/recovery ratio."** You've set 0.15/0.10 ≈ 1.5× asymmetry citing Buckley, Huey & Ma (2025). Reviewers will want to know how the key results change at 1.2×, 2.0×, 3.0×, because the empirical asymmetry varies widely across taxa (Sinclair et al. 2016 *Ecol Lett* 19:1372 on cold tolerance recovery; Ørsted et al. 2022 *J Anim Ecol* 91:1485 on heat-tolerance recovery in insects).

3. **"Is the prey population cap ecologically motivated?"** A hard `CarryingCapacityPerTier` with no connection to resource dynamics will be flagged as phenomenological. Either name it a logistic closure term and own it, or give it a resource interpretation.

4. **"Why does daily variance grow with year?"** `RandomnessGrowthRate` is a defensible choice (IPCC SRES/CMIP6 scenarios do project increased SST variance), but it's not cited as such. Add Bathiany et al. (2018 *Sci Adv* 4:eaar5809) or Olonscheck et al. (2021 *Nat Clim Change* 11:422).

5. **"Stochastic replication."** The number of scenarios per configuration matters; if the paper reports means across only 10–20 replicates, reviewers will push for 100+ and formal CI bands (Cariboni et al. 2007 *Environ Model Softw* 22:1509 on GSA for ecological models; Saltelli et al. 2019 *Environ Model Softw* 114:29).

6. **"No ODD protocol?"** MEE publishes many simulation papers and effectively requires ODD or ODD+D (Grimm et al. 2020 *JASSS* 23:7). If the methods aren't in that format, expect a request.

## 5. Must-fix before MEE submission

Ranked by severity × probability of being caught:

1. **Pooled FedRate (concern #1).** Non-negotiable. Multi-predator runs are currently not modeling what the authors think they are modeling. Fix per-predator bookkeeping.

2. **Hidden warming from `WarmingBias` (concern #2).** Non-negotiable for any climate-scenario figure. Re-center the interannual distribution.

3. **Processing-order bug in carrying cap (concern #3).** Already on the Monday list. Fix before any figure is generated.

4. **Acclimation omission — methods statement.** Not a code fix; a scoping statement in the methods citing Angilletta 2009, Seebacher 2015, and explaining why fixed TPCs are justified for *this question*. Without it, the paper is vulnerable to an entire class of reviewer objections.

5. **Sensitivity analysis grid on the condition-dynamics parameters.** Drain/recovery ratio, Pmax scaling on/off, `NEWBORN_CONDITION`, and `NO_PREDATOR_PENALTY` (or delete #6 as I'd recommend). A small SA table in supplementary is standard and heads off reviewer #2.

Items #7 (default `ReproThreshold < DeathThreshold`), #11 (no prey preference), and #12 (prey `FedRate = 1`) are defensible as scope choices *if stated explicitly* in the methods. Everything else in the review is second-order or stylistic from a biological standpoint.
