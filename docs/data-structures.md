# Internal Data Structures

This document describes the in-memory data model of the TinySea headless ecosystem
simulation: the types that hold ecosystem state during a run, the keys used to identify
species across those types, and every accumulator field that carries fractional state
between days. It is written from the C# source under
`tinysea/Assets/scripts/Simulation/`. Every non-trivial claim cites a `file:line` range so
you can jump to the code.

Scope note. The shipping configuration runs Tier 1 (prey) only. The default for both
`EcosystemSimulator.Tier2Enabled` (EcosystemSimulator.cs:245) and the engine load gate
(EcosystemSimulator.cs:336) drops Tier 2 species. Tier 2 (predator) fields and code paths
still exist from the original two-tier design. This document describes Tier 1 in full and
marks Tier 2 fields as legacy where they appear. Treat any field whose name ends in `T2`,
or that is documented as predator-only, as currently inactive.

The biology equations that produce these values and the per-day execution order that writes
these fields are given in full in this document: the thermal performance formula in section
3.5, the per-day driver loop and `BiologyStep` in section 13, and the exact ten-step biology
sequence in section 14. This document is self-contained for reconstructing the data model and
the per-step arithmetic.

Related documents (extended rationale and adjacent subsystems; cross reference):

- Deeper biology rationale and derivations: `biology-and-formulas.md`.
- Scenario and batch orchestration around the per-day loop: `run-scenario-batch.md`.
- Temperature inputs: `temperature-model.md`.
- How these structures serialize to CSV: `csv-output-formats.md`.
- Configuration objects that seed a run: `configuration-reference.md`.
- The bulk batch path: `bulk-system.md`.

## 1. Ownership map

Each structure owns a distinct slice of run state. The table lists every structure this
document covers, where it is defined, its lifetime, and what it holds.

| Structure | Defined in | Lifetime | Holds |
|---|---|---|---|
| `SimSpecies` | SimSpecies.cs:11 | One per species, lives for the whole run | Live population (float), thermal/biology parameters, per-step runtime scratch values, persistent `Condition` |
| Accumulator dicts | EcosystemSimulator.cs:150-154 | Whole run, keyed by `FullName` | Fractional residuals of births, predation, natural death, condition death |
| Per-species event counters | EcosystemSimulator.cs:213-220 | Reset each biology step, keyed by `FullName` | Whole-event counts for the current day (births, each death pathway, etc.) plus `StartPopBySpecies` |
| Tier rollup scalars | EcosystemSimulator.cs:157-205 | Reset/recomputed each biology step | Tier-level start/end populations, `Last*` death and birth totals, accumulator totals, average condition |
| `PerSpeciesStepData` | SimulationRunner.cs:20 | One per species per recorded day, value type | Snapshot of one species on one day (population, condition, performances, daily events, accumulator residuals) |
| `StepRecord` | SimulationRunner.cs:75 | One per simulated day, lives in `_records` for the run | One day of tier-level state plus a `SpeciesData` dict of `PerSpeciesStepData` keyed by `FullName` |
| `SimulationSummary` | SimulationRunner.cs:1206 | One per scenario, transient | Final/min/max tier populations and temperature extremes |
| `PerSpeciesScenarioMetrics` | ScenarioResult.cs:102 | One per species per scenario, keyed by `FullName` | Final-year and full-run means, population CV, min/max, extinction/crash day, final-year death counts |
| `ScenarioResult` | ScenarioResult.cs:10 | One per scenario, kept in `AggregateResults.Scenarios` | Scenario identity, outcome, tier summary stats, per-species dicts, embedded CSV text |
| `AggStat` | ScenarioResult.cs:146 | Inside `PerSpeciesAggregate` | Mean/StdDev/Min/Max plus survived-only mean/stddev of one metric across scenarios |
| `ExtinctionStat` | ScenarioResult.cs:160 | Inside `PerSpeciesAggregate` | Counts and day statistics of an extinction-style event across scenarios |
| `PerSpeciesAggregate` | ScenarioResult.cs:174 | One per species per run, keyed by `FullName` | One `AggStat` per metric plus extinction/crash `ExtinctionStat` |
| `AggregateResults` | ScenarioResult.cs:204 | One per run | Run config snapshot, cross-scenario summary stats, per-species dicts, list of `ScenarioResult` |
| `SpeciesData` | SpeciesDatabase.cs:33 | Serialized config record (ScriptableObject list) | On-disk species definition that initializes a `SimSpecies` |
| `RunSpeciesList` | RunSpeciesList.cs:5 | ScriptableObject | The species list a run is initialized from |
| `RunControl` | RunControl.cs:9 | One per standard run | Volatile pause/stop flags read by the day loop |

The flow at a high level: `RunSpeciesList` holds `SpeciesData` records.
`EcosystemSimulator.InitializeFromRunSpeciesList` (EcosystemSimulator.cs:317) converts each
`SpeciesData` into a live `SimSpecies`. Each biology step mutates the `SimSpecies` objects
and writes the accumulator dicts and per-species event counters.
`SimulationRunner.RecordStep` (SimulationRunner.cs:464) reads all of that into one
`StepRecord` per day. At the end of the run, `ToScenarioResult` (SimulationRunner.cs:982)
post-processes the `_records` list into a `ScenarioResult`, and the controller rolls the
`ScenarioResult` objects up into `AggregateResults`.

## 2. The identity key: `FullName`

Every per-species dictionary in the simulation is keyed by a single string, `FullName`.

`FullName` is a computed property on `SimSpecies` (SimSpecies.cs:89):

```text
FullName = IsNullOrEmpty(VariantLabel) ? Name : Name + "_" + VariantLabel
```

`Name` and `VariantLabel` are plain string fields on `SimSpecies` (SimSpecies.cs:14,18).
When a species is built from config, `Name` is set to `speciesLabel` if present, otherwise
the `speciesName` enum name; `VariantLabel` is set to `variantLabel` if present, otherwise
the `variant` enum name (EcosystemSimulator.cs:353,355). Examples: `Hexapod_Cold`,
`Coral_M2`. If `VariantLabel` is empty, `FullName` is just `Name` with no suffix.

Two points a reimplementation must respect:

1. The `ThermalVariant` enum (`Arctic`, `Common`, `Tropical`, `Custom`, SimSpecies.cs:3) is
   internal only. Its names are never written to output. All output identity flows through
   `VariantLabel` and `FullName` (SimSpecies.cs:84-88).
2. `FullName` is not guaranteed collision-free across distinct species. Two species can
   produce the same `FullName`, and two distinct `FullName`s can sanitize to the same CSV
   column. Within a run, duplicate species whose name and variant labels normalize equal
   are merged at load time before they ever get separate dictionary entries
   (EcosystemSimulator.cs:341-349, using `SpeciesData.SpeciesNameMatchKey` and
   `SpeciesData.VariantMatchKey`, SpeciesDatabase.cs:216-226,243-253). The normalization
   each match key applies is: lowercase every character, then keep only `[a-z0-9]`, dropping
   spaces, dashes, underscores, and all other punctuation. So `Coral M2` and `coral-m2`
   normalize to the same key and merge, while `topic3` and `topic4` do not. The match key is
   `SpeciesNameMatchKey(name) + "_" + VariantMatchKey(variantLabel)`
   (EcosystemSimulator.cs:343). The merge keeps the first-seen species in list order and adds
   the later record's `count` to its population (`existingSp.Population += data.count`,
   EcosystemSimulator.cs:346); every other parameter, thermal curve, and biology rate comes
   from the first-seen record, and the later record's parameters are discarded. CSV column
   collisions that survive merging (distinct `FullName`s that sanitize equal) are
   disambiguated with `_2`, `_3` suffixes at write time (SimulationRunner.cs:292-303); that
   suffix logic is described in `csv-output-formats.md`.

`SanitizeColumnName(fullName)` (SimulationRunner.cs:322-337) is the function that turns a
`FullName` into a CSV-safe column prefix. It replaces every character outside
`[A-Za-z0-9_]` with `_`, prepends `_` if the result starts with a digit, and returns
`"Unknown"` for null or empty input. The sanitized form is used only for column names; the
in-memory dictionary keys always use the raw `FullName`. When two distinct sanitized prefixes
collide, the disambiguation order is the species iteration order of the column writer, which
is `(Tier ascending, FullName ascending)`: the first species in that order keeps the bare
sanitized name and each later collider gets the next free `_2`, `_3`, ... suffix
(SimulationRunner.cs:292-303). That order is fixed, so the bare-versus-suffixed assignment is
reproducible. The full column schema and ordering live in `csv-output-formats.md`.

## 3. `SimSpecies`: the live species state

`SimSpecies` (SimSpecies.cs:11) is the mutable object that represents one species for the
entire run. `EcosystemSimulator.Species` is the `List<SimSpecies>` of all live species
(EcosystemSimulator.cs:139). Population is stored as a `float` for fractional precision
during calculation. At the end of each biology step (Step 10) the value is first clamped to a
hard overflow cap of `100 * CarryingCapacityPerTier` and then rounded away from zero to an
integer (SimSpecies.cs:7-8; EcosystemSimulator.cs:667-681). The cap is a defensive guard
against runaway overshoot before the condition feedback throttles reproduction; see Step 10
in section 14.

### 3.1 Identity and population fields

| Field | Type | Meaning |
|---|---|---|
| `Name` | string | Species name, basis of `FullName` (SimSpecies.cs:14) |
| `Variant` | `ThermalVariant` | Internal enum bucket, never written to output (SimSpecies.cs:15) |
| `VariantLabel` | string | Free-text variant label, the only variant string in output (SimSpecies.cs:18) |
| `Tier` | int | 1 = prey, 2 = predator. 1-based (SimSpecies.cs:19) |
| `Population` | float | Live population. Extinct when it rounds to 0 (SimSpecies.cs:22) |

Tier numbering differs between layers. `SimSpecies.Tier` is 1-based (SimSpecies.cs:19).
`SpeciesData.tier` is 0-based, 0 = prey and 1 = predator (SpeciesDatabase.cs:51). The
conversion `Tier = data.tier + 1` happens at load (EcosystemSimulator.cs:356).

### 3.2 Biology parameters (set once at load, read each step)

| Field | Default | Meaning |
|---|---|---|
| `EatingAmount` | 0 (field); set from config | Per-individual consumption, used by both tiers. Tier 2: prey eaten per predator per step. Tier 1: resource points each individual draws from the shared pool, floored at 1 in the consumption sum (EcosystemSimulator.cs:776), so a higher appetite drains the pool faster; at the floored 0 default it behaves as appetite 1. The C# field has no initializer (SimSpecies.cs:25), so an unset instance is 0. Loaded from `eatingAmount` (EcosystemSimulator.cs:358); SpeciesData has no asset default, so a blank source value is 0 |
| `ReproductionMultiplier` | 0 (field); set from config | Birth rate multiplier. Bare field declaration, no initializer (SimSpecies.cs:26). Loaded from `reproductionMultiplier` (EcosystemSimulator.cs:359); SpeciesData has no asset default, so a blank value is 0 |
| `DeathThreshold` | 0 (field); SpeciesData asset default 0.3 | Condition below this triggers graduated condition death. The `SimSpecies` field is a bare declaration with no initializer, so its real C# default is 0f (SimSpecies.cs:27); the 0.3 comes from `SpeciesData.deathThreshold` (SpeciesDatabase.cs:54) and is copied in at load (EcosystemSimulator.cs:360). The `ApplyConditionDeath` gate is `sp.Condition >= sp.DeathThreshold` (EcosystemSimulator.cs:1059). Note: the inline comment on SimSpecies.cs:27 is stale. It reads "FinalPerf below this triggers thermal death", but the code compares `Condition` against `DeathThreshold` for condition death (EcosystemSimulator.cs:1056-1062), not FinalPerf for thermal death |
| `DeathRate` | 0 (field); set from config | Fraction dying when condition death fires. Bare field declaration, no initializer (SimSpecies.cs:28); loaded from `deathRate` (EcosystemSimulator.cs:361), SpeciesData has no asset default, so a blank value is 0. The inline comment on SimSpecies.cs:28 ("thermal death") is stale for the same reason as `DeathThreshold`; the field feeds graduated condition death (EcosystemSimulator.cs:1063) |
| `ReproThreshold` | 0 (field); SpeciesData asset default 0.25 | Condition inflection point for the reproduction ramp. The `SimSpecies` field is a bare declaration with no initializer, so its real C# default is 0f (SimSpecies.cs:29); the 0.25 comes from `SpeciesData.reproThreshold` (SpeciesDatabase.cs:57) and is copied in at load (EcosystemSimulator.cs:362) |
| `ConditionDrainRate` | -1 | Per-species condition drain rate. Negative means inherit the global rate (SimSpecies.cs:36) |
| `ConditionRecoveryRate` | -1 | Per-species condition recovery rate. Negative means inherit the global rate (SimSpecies.cs:37) |
| `NaturalDeathRate` | 0.02 | Flat per-step natural mortality rate (SimSpecies.cs:40) |
| `NaturalDeathVariance` | 0.01 | Half-width of a symmetric uniform variance on the natural death rate. Each step the simulator draws `variance = (_rng.NextDouble() * 2 - 1) * NaturalDeathVariance`, uniform in `[-NaturalDeathVariance, +NaturalDeathVariance]`, adds it to `NaturalDeathRate`, then clamps the sum to be non-negative (EcosystemSimulator.cs:1278-1279). `_rng` is the scenario's seeded `System.Random` (SimSpecies.cs:41) |
| `HuntingEfficiency` | 0.75 (field initializer) | Base foraging success at the normal availability ratio; drives the shared Holling II curve (`ComputeForagingSuccess`) for every tier. Tier 1 searches the resource pool, Tier 2 hunts prey, used in the Tier 1 `FedRate` formula in section 3.3. The field initializer is `0.75f` (SimSpecies.cs:52). At load the field is copied straight from `SpeciesData.huntingEfficiency` (EcosystemSimulator.cs:365) with no Tier-based override, so a `SimSpecies` holds exactly whatever the config supplies. The value 1.0 for Tier 1 is a data convention, not a code path: the canonical Tier 1 species set `huntingEfficiency = 1.0f` in `SpeciesDatabase` (SpeciesDatabase.cs:338,515), at which the Holling curve returns 1 (EcosystemSimulator.cs:971). No code forces 1.0 for Tier 1 at any line; a Tier 1 species configured with 0.75 keeps 0.75 |
| `HuntingVariance` | 0.15 | Half-width of a symmetric uniform variance on foraging success, applied to all tiers (0 = deterministic). When `> 0`, the simulator draws `variance = (_rng.NextDouble() * 2 - 1) * HuntingVariance`, uniform in `[-HuntingVariance, +HuntingVariance]`, adds it to the Holling efficiency, then clamps the result to `[MIN_HUNTING_SUCCESS, MAX_HUNTING_SUCCESS]` (EcosystemSimulator.cs:934-937). `_rng` is the scenario's seeded `System.Random` (SimSpecies.cs:53) |

Thermal-curve parameters in Kelvin (SimSpecies.cs:60-65): `OptimalTempK`, `ArrhenBreadth`,
`ArrhenLower`, `ArrhenUpper`, `LowerBoundK`, `UpperBoundK`.

Peak and lethal-limit parameters (SimSpecies.cs:68-71):

| Field | Default | Meaning |
|---|---|---|
| `Pmax` | 1.0 (field); SpeciesData asset default 0.65 | Peak performance at optimal temperature, [0,1]. Scales thermal performance, condition rates, and births. Field initializer 1.0 (SimSpecies.cs:68); for config-built species the load copies `SpeciesData.pmax`, asset default 0.65 (SpeciesDatabase.cs:105; EcosystemSimulator.cs:373) |
| `CTminC` | -5.0 (field); SpeciesData asset default 0.0 | Critical thermal minimum in Celsius. At or below, performance is 0. The field initializer is -5.0 (SimSpecies.cs:69), but every config-built species is overwritten from `SpeciesData.ctMinC` at load (EcosystemSimulator.cs:374), whose asset default is 0.0 (SpeciesDatabase.cs:107), so the config value wins and the -5.0 only applies to a bare `SimSpecies` never run through config |
| `CTmaxC` | 40.0 (field and asset agree) | Critical thermal maximum in Celsius. At or above, performance is 0. Field initializer 40.0 (SimSpecies.cs:70); load copies `SpeciesData.ctMaxC`, asset default also 40.0 (SpeciesDatabase.cs:109; EcosystemSimulator.cs:375) |
| `TemperatureDebuff` | 0 | Per-species offset in degrees Celsius, added to the experienced temperature. It is applied as the first line of `CalculatePerformance`, `temperatureCelsius += TemperatureDebuff` (SimSpecies.cs:97), so it shifts the input before the CTmin/CTmax lethal fade and before the Celsius-to-Kelvin conversion at SimSpecies.cs:116. A positive value makes the species behave as if the water were warmer (SimSpecies.cs:71) |

Constants on the type (SimSpecies.cs:56-57), with where each is used:

| Constant | Value | Where used |
|---|---|---|
| `MIN_FINAL_PERF_FOR_NATURAL_DEATH` | 0.1 | Declared but unused. No biology step references it; natural death (Step 9) applies a flat rate with no performance floor (EcosystemSimulator.cs:1270-1319). Treat it as inert |
| `LETHAL_TRANSITION_WIDTH` | 2.0 (private) | Inside `CalculatePerformance`. It is the half-width in degrees Celsius of the cosine fade that ramps thermal performance to 0 as temperature approaches `CTminC` or `CTmaxC`, capped at half the lethal range (SimSpecies.cs:99-112) |

The runtime gate `MIN_ALIVE_POP = 1.0` (EcosystemSimulator.cs:248) is the simulator-wide
threshold for "alive": a species whose `Population` is below 1.0 is skipped by feeding,
condition update, condition death, and natural death is skipped only when `Population <= 0`,
and the species is excluded from the population-weighted `AvgCondition` and `LastFedRate`
means (EcosystemSimulator.cs:730-731,954,1058,1272,1329-1337).

Reproduction does not use this gate. It has a separate, stricter constant
`MIN_POPULATION_FOR_REPRODUCTION = 2f` on `EcosystemSimulator` (EcosystemSimulator.cs:274).
`ApplyReproduction` returns immediately and produces zero births when
`Population < MIN_POPULATION_FOR_REPRODUCTION` (EcosystemSimulator.cs:1152-1156). The two
thresholds are independent: a species with `Population` in the half-open interval `[1.0, 2.0)`
is alive for feeding, condition update, condition death, and natural death, but produces no
births that step. A reimplementation must gate births on the value 2.0, not on the 1.0 alive
gate, or a recovering or near-extinct species at population 1 will reproduce when the real
code keeps it sterile until it climbs back to 2.

The thermal-performance method `CalculatePerformance(temperatureCelsius)` (SimSpecies.cs:95-133)
returns the dimensionless [0,1] thermal performance without `Pmax`; `Pmax` is applied
externally by the simulator (EcosystemSimulator.cs:594). The exact formula is given in
section 3.5 below.

### 3.3 Runtime scratch values (recomputed each biology step)

These fields are overwritten during each call to `ProcessBiologyStep`. They are the bridge
between biology steps and the per-day record. All performance, condition, and fed-rate
values are dimensionless in [0,1].

| Field | Default | Written by | Meaning |
|---|---|---|---|
| `RawThermalPerformance` | 0 (no initializer) | Step 1 reset+write (EcosystemSimulator.cs:593) | Arrhenius performance with lethal fade, without `Pmax`. Recomputed for every species at the top of Step 1 |
| `ThermalPerformance` | 0 (no initializer) | Step 1 reset+write (EcosystemSimulator.cs:594) | `RawThermalPerformance * Pmax`. Drives predator demand and logging. Recomputed every Step 1 |
| `FedRate` | 1 (field initializer `FedRate = 1f`, SimSpecies.cs:77) | Step 1 reset to 1 (cs:595); Step 2 sets the live value (cs:792 Tier 1, cs:857 Tier 2) or forces 0 for dead/low-pop species (cs:798) and for predators with no prey (cs:785) | Feeding satisfaction in [0,1]. Tier 1: `FedRate = min(1, gatherSuccess * foodDensity)` where `gatherSuccess = ComputeForagingSuccess(sp, resourceRatio)` (Holling II + variance) and `foodDensity = max(0, 1 - tier1Consumption / capSafe)` (EcosystemSimulator.cs:777-792; see note below the table). Tier 2: Holling II per-predator value |
| `RawFinalPerformance` | 0 (no initializer) | Step 3 (EcosystemSimulator.cs:608) | `RawThermalPerformance * FedRate`. The condition drain target. Uses the raw (non-`Pmax`) thermal value on purpose, see the note below the table |
| `FinalPerformance` | 0 (no initializer) | Step 5 (EcosystemSimulator.cs:628) | `ThermalPerformance * FedRate`. Logging/CSV only, never read by a later step. Uses the `Pmax`-scaled `ThermalPerformance`, see the note below the table |
| `CurrentHuntingSuccess` | 1 in practice on a biology day (no field initializer, so the bare C# default is 0, SimSpecies.cs:79) | Step 1 reset to 1 (cs:596); Step 2 sets the predator value (cs:811) | This step's hunting success. Tier 2 species get a real value in Step 2; Tier 1 species are reset to 1 in Step 1 and never overwritten, so they stay at 1 on any day biology runs |
| `Condition` | 1.0 (field initializer `Condition = 1.0f`, SimSpecies.cs:80) | Step 4 (EcosystemSimulator.cs:952-992) | Persistent health [0,1]. Not reset each step; see the paragraph below |

Meaning of the Default column. For `RawThermalPerformance`, `ThermalPerformance`,
`RawFinalPerformance`, and `FinalPerformance` the listed default is only the C# field
default before the first biology step. On every biology day these four are recomputed from
scratch (Steps 1, 3, 5), so the default never survives into a record once biology has run.
`FedRate` and `CurrentHuntingSuccess` are different: both are reset at the top of Step 1 each
biology day (to 1), so the value seen in a record is the Step-1 reset value unless a later
step overwrote it. `FedRate` additionally has a field initializer of 1 (SimSpecies.cs:76)
that holds before the first step; `CurrentHuntingSuccess` has no initializer, so its bare
default is 0, but Step 1 sets it to 1 on the first biology day before anything reads it. There is
no carried-forward case at the simulator level. The only carry-forward is in the per-day
record: on a day where biology does not run, `SimulationRunner.RecordStep` reuses the
species' current `FedRate` field for the `FedRate` column (SimulationRunner.cs:567), which is
the value Step 1 last wrote. So a Tier 1 species that is never processed because it is
extinct (population below `MIN_ALIVE_POP = 1.0`, EcosystemSimulator.cs:248) has its `FedRate`
forced to 0 in Step 2 (EcosystemSimulator.cs:798) on every biology day, and reports 0, not 1.

`foodDensity` (Tier 1 only). It is the supply cap of the single Tier 1 food pool on the
current day, dimensionless in [0,1], the Tier 1 analog of Tier 2's scarcity factor. It and
the search ratio are computed once per biology step before the per-species `FedRate` loop
(EcosystemSimulator.cs:765-782):

```text
tier1Pop         = max(0, total live Tier 1 population)     // sum of Tier 1 Population, clamped >= 0
capSafe          = max(CarryingCapacityPerTier, 1)          // floor of 1 to guard misconfig
tier1Consumption = sum over live Tier 1 of Population * max(1, EatingAmount)   // appetite floored at 1
foodDensity      = max(0, 1 - tier1Consumption / capSafe)   // 1 when empty, 0 at/over capacity
resourceRatio    = capSafe / max(tier1Pop, 1)               // Holling search ratio
```

`CarryingCapacityPerTier` is the per-tier carrying capacity from config (always on as of
v11.1, validated > 0). `foodDensity` falls with total consumption (`Population * EatingAmount`,
not head count) and hits 0 once consumption reaches `capSafe`, so a higher appetite occupies
the pool with fewer individuals. `resourceRatio` drives the shared Holling foraging curve;
appetite stays out of it, entering only through `foodDensity`, mirroring Tier 2's `preyRatio`.
The full Step 2 feeding equations, including the per-species `FedRate` and the legacy Tier 2
Holling II path, are in section 14, Step 2. The in-code comment block explains the shared
foraging model (EcosystemSimulator.cs:745-782).

Raw versus `Pmax`-scaled final performance. `RawFinalPerformance` (the condition target,
Step 3) multiplies the raw thermal value by `FedRate`, while `FinalPerformance` (logging,
Step 5) multiplies the `Pmax`-scaled `ThermalPerformance` by `FedRate`. This split is
intentional. `Pmax` is applied to the condition drain and recovery rates, not to the
condition target, so Condition keeps the same [0,1] meaning across species and the shared
`ReproThreshold`/`DeathThreshold` need no per-species tuning (EcosystemSimulator.cs:943-946,
956). A reimplementation must keep these two fields separate and must not feed the
`Pmax`-scaled `FinalPerformance` into the condition update. `FinalPerformance` exists only
for CSV and logs (EcosystemSimulator.cs:619-630).

`Condition` is the one runtime field that is not fully recomputed each step. It is
initialized to 1.0 (SimSpecies.cs:80, set again at load EcosystemSimulator.cs:379) and then
moved incrementally toward `RawFinalPerformance` each step. Per step it either drains (when
`Condition > RawFinalPerformance`) or recovers (otherwise); both moves are a fraction of the
gap to the target, the fraction scaled by a quadratic severity term and by `Pmax`
(EcosystemSimulator.cs:966-989). The per-species `ConditionDrainRate`/`ConditionRecoveryRate`
set that fraction, falling back to the simulator-global rates when negative
(EcosystemSimulator.cs:963-964). The full drain/recover equations, the quadratic severity and
boost terms, the asymmetric `Pmax` placement, and the global-rate defaults (0.15 drain, 0.10
recovery) are in section 14, Step 4. `Condition` persists across days, which is why a species
that has gone extinct keeps its last `Condition` value: once `Population` drops below
`MIN_ALIVE_POP` (= 1.0, EcosystemSimulator.cs:248,954) the update is skipped. Condition death
also redistributes the surviving health pool: after the kill the survivors' condition is
rescaled `new_condition = old_condition * old_population / new_population` and capped at 1,
on the assumption that the dead were the weakest members (EcosystemSimulator.cs:1082-1085).

### 3.4 Factory methods and their default parameters

`CreateHexapod(variant, initialPopulation)` (SimSpecies.cs:140) and
`CreateSheplik(variant, initialPopulation)` (SimSpecies.cs:207) build default Tier 1 and
Tier 2 species. They are used only by `EcosystemSimulator.InitializeDefaultSpecies`
(EcosystemSimulator.cs:501-523), the fallback path when no `RunSpeciesList` is supplied
(SimulationRunner.cs:407-411). Normal runs build species from config, not from these
factories. `CreateHexapod` assigns variant labels `Cold`/`Warm`/`Hot` to the
`Arctic`/`Common`/`Tropical` enum slots (SimSpecies.cs:169,180,189).

These factories are the only definition of the default-species parameters, so a
reimplementation of the no-`RunSpeciesList` path must reproduce them exactly. The values are
hard-coded in the factory bodies and are listed in full below.

`CreateHexapod` (Tier 1 prey). Variant-independent fields set on every Hexapod
(SimSpecies.cs:142-163):

| Field | Value |
|---|---|
| `Name` | `"Hexapod"` |
| `Tier` | 1 |
| `EatingAmount` | 0 (floored to 1 per individual in the pool draw, EcosystemSimulator.cs:776) |
| `ReproductionMultiplier` | 0.45 |
| `DeathThreshold` | 0.3 |
| `DeathRate` | 0.6 |
| `ReproThreshold` | 0.25 |
| `NaturalDeathRate` | 0.02 |
| `NaturalDeathVariance` | 0.01 |
| `HuntingEfficiency` | 1.0 (foraging success >= 1 makes the shared Holling curve return 1, EcosystemSimulator.cs:971) |
| `HuntingVariance` | 0 (deterministic foraging, draws no RNG) |
| `ArrhenBreadth` | 5000 |
| `ArrhenLower` | 16000 (then overwritten per variant) |
| `ArrhenUpper` | 43800 (then overwritten per variant) |
| `ConditionDrainRate` | -1 (field default, inherit global) |
| `ConditionRecoveryRate` | -1 (field default, inherit global) |
| `TemperatureDebuff` | 0 (field default) |

Per-variant Hexapod fields set by the `switch (variant)` block (SimSpecies.cs:166-199). The
`VariantLabel` column is the free-text label written to output; the enum slot in the first
column never reaches output:

| Enum slot | `VariantLabel` | `OptimalTempK` | `LowerBoundK` | `UpperBoundK` | `ArrhenLower` | `ArrhenUpper` | `Pmax` | `CTminC` | `CTmaxC` |
|---|---|---|---|---|---|---|---|---|---|
| Arctic | `Cold` | 293.15 (20 C) | 292.40 | 293.90 | 15998 | 43798 | 0.9843 | 0 | 35 |
| Common | `Warm` | 295.15 (22 C) | 294.40 | 295.90 | 16000 (inherited) | 43800 (inherited) | 0.972 | 2 | 37 |
| Tropical | `Hot` | 297.15 (24 C) | 296.40 | 297.90 | 16002 | 43802 | 0.96 | 4 | 39 |

The Common branch does not reassign `ArrhenLower`/`ArrhenUpper`, so they keep the
variant-independent 16000/43800 set at construction (SimSpecies.cs:179-186). The Arctic and
Tropical branches overwrite them with the per-variant translated values shown
(SimSpecies.cs:173-174,193-194).

`CreateSheplik` (Tier 2 predator, legacy, secondary). Tier 2 is disabled in current runs
(section scope note); these values are documented for completeness only. Variant-independent
fields (SimSpecies.cs:209-227):

| Field | Value |
|---|---|
| `Name` | `"Sheplik"` |
| `Tier` | 2 |
| `EatingAmount` | 1.5 |
| `ReproductionMultiplier` | 0.1 |
| `DeathThreshold` | 0.3 |
| `DeathRate` | 0.3 |
| `ReproThreshold` | 0.25 |
| `NaturalDeathRate` | 0.01 |
| `NaturalDeathVariance` | 0.005 |
| `HuntingEfficiency` | 0.75 |
| `HuntingVariance` | 0.15 |
| `ArrhenBreadth` | 5273.15 |
| `ArrhenLower` | 10273.15 |
| `ArrhenUpper` | 21273.15 |

`CreateSheplik` does not set `VariantLabel`, so a default Sheplik's `FullName` falls back to
the enum name via the `IsNullOrEmpty(VariantLabel)` branch of `FullName` (SimSpecies.cs:89).
Per-variant Sheplik fields (SimSpecies.cs:229-255):

| Enum slot | `OptimalTempK` | `LowerBoundK` | `UpperBoundK` | `Pmax` | `CTminC` | `CTmaxC` |
|---|---|---|---|---|---|---|
| Arctic | 278.15 | 270.15 | 280.15 | 1.0 | -30 | 20 |
| Common | 293.15 | 285.15 | 295.15 | 0.9 | -5 | 40 |
| Tropical | 308.65 | 300.15 | 310.15 | 1.0 | 0 | 80 |

### 3.5 Thermal performance formula (`CalculatePerformance`)

`CalculatePerformance(temperatureCelsius)` (SimSpecies.cs:95-133) is the per-step thermal
performance function. It returns a dimensionless value in [0,1] and does not apply `Pmax`;
the simulator multiplies by `Pmax` afterward (EcosystemSimulator.cs:594). Every downstream
biology step (condition target, thermal death, reproduction scale) depends on this value, so
a reimplementation must reproduce it exactly. The input `temperatureCelsius` is the day's
water temperature in degrees Celsius. The whole method executes in this order.

Step A, apply the per-species temperature offset (SimSpecies.cs:97). `TemperatureDebuff` is in
degrees Celsius and is added to the input before anything else, so it shifts the experienced
temperature for both the lethal fade and the Arrhenius term:

```text
temperatureCelsius += TemperatureDebuff
```

Step B, compute the cosine lethal-fade factor (SimSpecies.cs:99-114). `CTminC` and `CTmaxC`
are the critical thermal minimum and maximum in degrees Celsius. `LETHAL_TRANSITION_WIDTH`
is the private constant 2.0 (degrees Celsius, SimSpecies.cs:57). The transition width `tw` is
that constant capped at half the lethal range so the two fade shoulders cannot overlap on a
narrow-range species:

```text
halfRange = (CTmaxC - CTminC) / 2
tw        = min(LETHAL_TRANSITION_WIDTH, halfRange)     // degrees Celsius

fadeFactor = 1
if temperatureCelsius <= CTminC:
    fadeFactor = 0
else if temperatureCelsius < CTminC + tw:
    fadeFactor = 0.5 * (1 + cos(PI * (CTminC + tw - temperatureCelsius) / tw))

if temperatureCelsius >= CTmaxC:
    fadeFactor = 0
else if temperatureCelsius > CTmaxC - tw:
    fadeFactor *= 0.5 * (1 + cos(PI * (temperatureCelsius - (CTmaxC - tw)) / tw))

if fadeFactor <= 0:
    return 0                                            // lethal: no Arrhenius evaluation
```

The cold shoulder assigns `fadeFactor` (overwriting the initial 1). The hot shoulder
multiplies into `fadeFactor` (`*=`), so a temperature inside both shoulders, possible only
when `tw` was capped to `halfRange`, multiplies the two ramps. The cold endpoint is 0 exactly
at `CTminC` and rises along a raised cosine to 1 at `CTminC + tw`. The hot endpoint is 1 at
`CTmaxC - tw` and falls to 0 exactly at `CTmaxC`. At or beyond either lethal limit the method
returns 0 immediately and never evaluates the Arrhenius ratio.

Step C, convert to Kelvin (SimSpecies.cs:116). The Arrhenius term uses absolute temperature;
the conversion adds 273.15, not 273:

```text
T = temperatureCelsius + 273.15     // Kelvin
```

Step D, evaluate the double-sigmoid Arrhenius ratio (SimSpecies.cs:118-129). All six thermal
parameters are in Kelvin: `OT = OptimalTempK`, `B = ArrhenBreadth`, `L = ArrhenLower`,
`U = ArrhenUpper`, `LB = LowerBoundK`, `UB = UpperBoundK`. The numerator and denominator are
computed in `double`:

```text
numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)
perf        = numerator / denominator
```

The factor `exp(B/OT - B/T)` is the bare Arrhenius rate normalized to equal 1 at
`T = OptimalTempK`. The two parenthesized sigmoid sums impose the low-temperature
(`L`, `LB`) and high-temperature (`U`, `UB`) deactivation shoulders; the numerator evaluates
both sigmoids at the optimal temperature `OT` so the whole ratio is normalized to its optimum.

Step E, clamp and apply the fade (SimSpecies.cs:131-132). The Arrhenius ratio is clamped to
[0,1] and then multiplied by the Step B fade factor. `Pmax` is not applied here:

```text
return clamp(perf, 0, 1) * fadeFactor
```

The result is `RawThermalPerformance` (Step 1 of the biology sequence writes it,
EcosystemSimulator.cs:593). `ThermalPerformance = RawThermalPerformance * Pmax` is computed
separately by the simulator (EcosystemSimulator.cs:594). Because Step E returns early at the
lethal fade, `RawThermalPerformance` is exactly 0 at or beyond `CTminC`/`CTmaxC`, which is the
precise predicate the thermal-death step keys on (section 14, Step 6).

## 4. Source records: `SpeciesData` and `RunSpeciesList`

A run is initialized from a `RunSpeciesList` (RunSpeciesList.cs:5), a ScriptableObject
holding `List<SpeciesData> speciesList` (RunSpeciesList.cs:11) and a reference to the
originating `SpeciesDatabase`. `SpeciesData` (SpeciesDatabase.cs:33) is the serialized,
on-disk species definition. It is the input that `InitializeFromRunSpeciesList` reads to
populate each `SimSpecies` (EcosystemSimulator.cs:333-387).

`SpeciesData` carries the same biology and thermal parameters as `SimSpecies`, plus UI-only
fields. The field-name mapping a reimplementation must follow:

| `SimSpecies` field | `SpeciesData` field | Note |
|---|---|---|
| `Name` | `speciesLabel` (fallback `speciesName.ToString()`) | SpeciesDatabase.cs:44; EcosystemSimulator.cs:353 |
| `VariantLabel` | `variantLabel` (fallback `variant.ToString()`) | SpeciesDatabase.cs:41; EcosystemSimulator.cs:355 |
| `Tier` | `tier + 1` | 0-based to 1-based (SpeciesDatabase.cs:51; EcosystemSimulator.cs:356) |
| `Population` | `count` | EcosystemSimulator.cs:357 |
| `EatingAmount` | `eatingAmount` (no asset default; blank source is 0) | The `SpeciesData` field has no initializer (SpeciesDatabase.cs:52), so a missing value is 0f. Copied at EcosystemSimulator.cs:358. For Tier 1 it sets the per-individual pool draw, floored at 1 in the consumption sum (EcosystemSimulator.cs:776), so a 0 behaves as appetite 1; the shipped Tier 1 list uses 3 |
| `ReproductionMultiplier` | `reproductionMultiplier` (no asset default; blank source is 0) | No initializer on `SpeciesData` (SpeciesDatabase.cs:53), missing value is 0f. Copied at EcosystemSimulator.cs:359 |
| `DeathThreshold` | `deathThreshold` (asset default 0.3) | SpeciesDatabase.cs:54; copied at EcosystemSimulator.cs:360 |
| `DeathRate` | `deathRate` (no asset default; blank source is 0) | No initializer on `SpeciesData` (SpeciesDatabase.cs:55), missing value is 0f. Copied at EcosystemSimulator.cs:361 |
| `ReproThreshold` | `reproThreshold` (default 0.25) | SpeciesDatabase.cs:57 |
| `ConditionDrainRate` | `conditionDrainRate` (asset default 0.15) | SpeciesDatabase.cs:63 |
| `ConditionRecoveryRate` | `conditionRecoveryRate` (asset default 0.10) | SpeciesDatabase.cs:64 |
| `NaturalDeathRate` | `naturalDeathRate` (default 0.02) | SpeciesDatabase.cs:71 |
| `NaturalDeathVariance` | `naturalDeathVariance` (default 0.01) | SpeciesDatabase.cs:73 |
| `HuntingEfficiency` | `huntingEfficiency` (default 0.75) | SpeciesDatabase.cs:78 |
| `HuntingVariance` | `huntingVariance` (default 0.15) | SpeciesDatabase.cs:80 |
| `OptimalTempK` | `optimalTempK` | SpeciesDatabase.cs:95 |
| `ArrhenBreadth` | `arrhenBreadth` | SpeciesDatabase.cs:96 |
| `ArrhenLower` | `arrhenLower` | SpeciesDatabase.cs:97 |
| `ArrhenUpper` | `arrhenUpper` | SpeciesDatabase.cs:98 |
| `LowerBoundK` | `lowerBoundK` | SpeciesDatabase.cs:99 |
| `UpperBoundK` | `upperBoundK` | SpeciesDatabase.cs:100 |
| `Pmax` | `pmax` (asset default 0.65) | SpeciesDatabase.cs:105 |
| `CTminC` | `ctMinC` (default 0.0) | SpeciesDatabase.cs:107 |
| `CTmaxC` | `ctMaxC` (default 40.0) | SpeciesDatabase.cs:109 |
| `TemperatureDebuff` | `TemperatureDebuff` (asset default 0.0) | SpeciesDatabase.cs:56; copied at EcosystemSimulator.cs:376 |

Note on conflicting field versus asset defaults. Several `SimSpecies` fields carry a C# field
initializer that differs from the `SpeciesData` asset default of the same parameter. The load
copies the `SpeciesData` value unconditionally for every config-built species
(EcosystemSimulator.cs:351-380), so the `SpeciesData`/config value always wins and the
`SimSpecies` field initializer only matters for a `SimSpecies` built outside config (the
factory path in section 3.4, which sets its own values anyway). The cases are `Pmax` (field
1.0 versus asset 0.65), `CTminC` (field -5.0 versus asset 0.0), and `HuntingEfficiency`
(field 0.75, no Tier-based override). For `EatingAmount`, `ReproductionMultiplier`, and
`DeathRate` there is no asset default at all: the `SpeciesData` declarations have no
initializer, so a blank source produces 0f, and that 0f is what the `SimSpecies` field gets.
A reimplementation should treat the config value as authoritative and use 0 as the fallback
for those three.

Note on the condition-rate sentinel. `SimSpecies.ConditionDrainRate`/`ConditionRecoveryRate`
default to `-1` to mean "inherit the global rate" (SimSpecies.cs:36-37), but
`SpeciesData.conditionDrainRate`/`conditionRecoveryRate` default to explicit `0.15`/`0.10`
so the Unity inspector never shows a bare `-1` (SpeciesDatabase.cs:60-64). A blank
per-species column in a bulk CSV import keeps the `-1` sentinel; that path is described in
`bulk-system.md`. The `variant` enum on `SpeciesData` (SpeciesDatabase.cs:38) is the wider
7-value `SpeciesVariant` (SpeciesDatabase.cs:21); it is mapped onto the 4-value
`ThermalVariant` at load by `ConvertVariant` (EcosystemSimulator.cs:457-478) for the
`SimSpecies.Variant` field. As stated in section 2, neither enum reaches output. Field
semantics and defaults are detailed in `configuration-reference.md`.

## 5. Accumulators: fractional residuals across days

Population is tracked as `float` during a step, but events are applied as whole integers so
populations stay countable. The fractional remainder of each event is not discarded; it is
stored in an accumulator dictionary and carried to the next day. This lets a per-step rate
that produces, for example, 0.4 births per step accumulate into one whole birth every few
days rather than rounding to zero every day.

### 5.1 The accumulator dictionaries

Five private dictionaries on `EcosystemSimulator`, each `Dictionary<string, float>` keyed by
`FullName` (EcosystemSimulator.cs:150-154):

| Dictionary | Used by step | Public accessor |
|---|---|---|
| `_birthAccumulators` | Reproduction (step 8) | `GetBirthAccum(fullName)` (EcosystemSimulator.cs:224) |
| `_naturalDeathAccumulators` | Natural death (step 9) | `GetNaturalDeathAccum(fullName)` (EcosystemSimulator.cs:226) |
| `_predationAccumulators` | Predation (step 2) | `GetPredationAccum(fullName)` (EcosystemSimulator.cs:230) |
| `_conditionDeathAccumulators` | Condition death (step 7) | `GetConditionDeathAccum(fullName)` (EcosystemSimulator.cs:228) |
| `_thermalDeathAccumulators` | none | none; dead code |

`_thermalDeathAccumulators` is initialized and cleared (EcosystemSimulator.cs:153,485,494)
but never read or written by any biology step and has no accessor. Thermal death is an
instant whole-population kill, not a fractional event (EcosystemSimulator.cs:1015-1024), so
it needs no accumulator. Treat `_thermalDeathAccumulators` as inert (EcosystemSimulator.cs:223).

Lifecycle. The dictionaries are cleared on initialization (`ClearAccumulators`,
EcosystemSimulator.cs:480-487) and seeded with a 0 entry per species
(`InitializeAccumulators`, EcosystemSimulator.cs:489-496). They are not reset per day. The
residual persists for the whole run.

### 5.2 The accumulator pattern

Every accumulating event follows the same four-line shape. Births
(EcosystemSimulator.cs:1228-1238) are representative:

```text
accumulator[fullName] += rawAmount          // add this step's fractional amount
accumulated = accumulator[fullName]
wholeEvents = (long)Math.Floor(accumulated)  // extract whole events
accumulator[fullName] = accumulated - wholeEvents  // keep the fractional remainder
```

`wholeEvents` is then applied to `Population`. `long` is used (not `int`) so the floor does
not overflow at extreme populations (EcosystemSimulator.cs:1231-1234). Death events
additionally clamp `wholeEvents` to the current population so a species cannot go negative,
for example `wholeDeaths = Math.Min(wholeDeaths, (long)sp.Population)`
(EcosystemSimulator.cs:1071,1299; predation EcosystemSimulator.cs:887). The same pattern
appears in predation (EcosystemSimulator.cs:879-884), condition death
(EcosystemSimulator.cs:1067-1070), and natural death (EcosystemSimulator.cs:1291-1296).

The `rawAmount` fed into each accumulator is the event's per-step fractional count. The full
derivations, including the definition of `reproScale` and `severity`, are in section 14 (Step
8 births, Step 2 predation, Step 7 condition death, Step 9 natural death); the source
expression for each of the four live accumulators is:

| Event (step) | `rawAmount` expression | Code |
|---|---|---|
| Births (8) | `Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep` | EcosystemSimulator.cs:1247,1260 |
| Predation (2) | per prey species, `totalEaten * (Population / availablePrey)`, the prey's proportional share of the day's total catch | EcosystemSimulator.cs:872-879 |
| Condition death (7) | `Population * severity * DeathRate * BiologyStep`, where `severity = (DeathThreshold - Condition) / DeathThreshold` | EcosystemSimulator.cs:1062-1067 |
| Natural death (9) | `Population * effectiveRate * BiologyStep`, where `effectiveRate = max(0, NaturalDeathRate + uniform variance)` | EcosystemSimulator.cs:1278-1291 |

`BiologyStep` is the days-per-biology-step multiplier (default 1); its value, the biology-day
predicate, and the full per-day execution order are in section 13.

Clamp versus remainder. The order of operations matters and is the same for all three death
events. The simulator floors the accumulator, writes the sub-1.0 remainder back, and only
then clamps `wholeDeaths` down to the current `Population`:

```text
accumulator[fullName] += rawAmount
accumulated = accumulator[fullName]
wholeDeaths = (long)Math.Floor(accumulated)
accumulator[fullName] = accumulated - wholeDeaths   // remainder fixed here, < 1.0
wholeDeaths = Math.Min(wholeDeaths, (long)Population) // clamp happens AFTER
```

Because the remainder is fixed before the clamp, any whole units the clamp discards (when
`wholeDeaths` exceeded `Population`) are dropped, not returned to the accumulator. The
accumulator holds only the fractional residual below 1.0. So a death event that is clamped
loses the surplus whole deaths permanently; they do not carry to the next day. This is
visible at births versus deaths: births never clamp (no upper bound on population from a
birth), so the births path matches the four-line shape exactly, while the three death paths
add the post-clamp line (EcosystemSimulator.cs:1070-1071 condition death, 1296-1299 natural
death, 884-887 predation).

After all per-species steps, `UpdateAccumulatorTotals` (EcosystemSimulator.cs:1343-1378)
sums the residuals by tier into the scalar `*AccumT1`/`*AccumT2` fields below, for CSV
output.

### 5.3 Tier-level accumulator totals

These public scalar fields on `EcosystemSimulator` are recomputed each step from the
dictionaries (EcosystemSimulator.cs:195-201,1343-1378). They are the sum of the per-species
residuals within a tier:

| Field | Tier | Source dictionary |
|---|---|---|
| `BirthAccumT1`, `BirthAccumT2` | 1, 2 | `_birthAccumulators` (EcosystemSimulator.cs:1357-1358) |
| `NaturalDeathAccumT1`, `NaturalDeathAccumT2` | 1, 2 | `_naturalDeathAccumulators` (EcosystemSimulator.cs:1363-1364) |
| `PredationAccumT1` | 1 | `_predationAccumulators` (EcosystemSimulator.cs:1369) |
| `ConditionDeathAccumT1`, `ConditionDeathAccumT2` | 1, 2 | `_conditionDeathAccumulators` (EcosystemSimulator.cs:1374-1375) |

There is no `PredationAccumT2`: only Tier 1 is eaten, so only prey accumulate predation
residual (EcosystemSimulator.cs:1367-1370). The `T2` variants are legacy and stay 0 in
Tier-1-only runs.

## 6. Per-species event counters (current day)

Separate from the persistent accumulators, `EcosystemSimulator` exposes per-species counters
that hold whole-event counts for the current day only. They are reset at the top of every
`ProcessBiologyStep` (EcosystemSimulator.cs:566-584) and populated as each event fires.

| Dictionary | Value type | Set by | Holds |
|---|---|---|---|
| `LastBirthsBySpecies` | long | step 8 (EcosystemSimulator.cs:1263) | Whole births this day |
| `LastTempDeathsBySpecies` | long | step 6 (EcosystemSimulator.cs:1024) | Thermal deaths this day |
| `LastConditionDeathsBySpecies` | long | step 7 (EcosystemSimulator.cs:1093) | Condition deaths this day |
| `LastNaturalDeathsBySpecies` | long | step 9 (EcosystemSimulator.cs:1313) | Natural deaths this day |
| `LastEatenBySpecies` | long | step 2 (EcosystemSimulator.cs:892) | Prey eaten this day. Tier 1 only |
| `LastReproScaleBySpecies` | float | step 8 (EcosystemSimulator.cs:1198) | Reproduction scale [0,1] this day |
| `LastFedRateBySpecies` | float | step 2 (EcosystemSimulator.cs:801,861) | This day's `FedRate` |
| `StartPopBySpecies` | long | top of step (EcosystemSimulator.cs:583) | Rounded population at start of day |

All are keyed by `FullName` (EcosystemSimulator.cs:210). `StartPopBySpecies` is the
start-of-day snapshot used to compute per-capita birth rate; it is taken before any biology
runs (EcosystemSimulator.cs:583). The dictionaries are declared get-only properties
initialized once (EcosystemSimulator.cs:213-220); they are emptied and refilled each step
rather than reassigned.

Invariant. The sum of each per-species counter across species equals the matching
tier-level `Last*` scalar, for example `sum(LastBirthsBySpecies) == LastBirthsT1 +
LastBirthsT2` (EcosystemSimulator.cs:212). The tier-level `Last*` scalars are listed next.

### 6.1 Tier-level `Last*` and population scalars

`EcosystemSimulator` also keeps tier-rollup scalars, reset each step
(EcosystemSimulator.cs:550-562) and accumulated as events fire. The death and birth totals
are declared `float`, but for every pathway except thermal death they accumulate only whole
event counts (`+= wholeBirths` / `+= wholeDeaths`, which are `long`), so they hold exact
integer values: births (EcosystemSimulator.cs:1259), condition death (cs:1090), natural death
(cs:1309), and predation `LastEatenT1` (cs:890). The one exception is thermal death:
`LastTempDeathsT1`/`T2` accumulate `+= deaths` where `deaths` is the species' full
pre-rounding float `Population` (EcosystemSimulator.cs:1015,1021-1022). Step 10 rounding has
not run yet, so on a day a species crosses a lethal limit that population can be fractional,
and the tier-level thermal-death total can therefore hold a non-integer value. The matching
per-species counter truncates with `(long)deaths` (EcosystemSimulator.cs:1024), so on a
thermal-death day the per-species `LastTempDeathsBySpecies` sum can be slightly below the
tier-level float by the dropped fractional part. Every other pathway keeps the per-species
and tier sums exactly equal.

| Field group | Fields | Meaning |
|---|---|---|
| Start/end population | `StartPopT1`, `StartPopT2`, `EndPopT1`, `EndPopT2` (EcosystemSimulator.cs:157-160) | Tier totals snapshotted at start (cs:546-547) and end (cs:692-693) of the step |
| Tier 1 deaths/births | `LastEatenT1`, `LastTempDeathsT1`, `LastConditionDeathsT1`, `LastNaturalDeathsT1`, `LastBirthsT1` (EcosystemSimulator.cs:164-169) | Day totals by pathway |
| Tier 2 deaths/births (legacy) | `LastTempDeathsT2`, `LastConditionDeathsT2`, `LastNaturalDeathsT2`, `LastBirthsT2` (EcosystemSimulator.cs:172-175) | Day totals, predator-only |
| Tier 1 feeding | `LastFedRateT1`, `LastFoodDensityT1` (EcosystemSimulator.cs:181-182) | Population-weighted mean FedRate and the day's food density |
| Tier 2 feeding (legacy) | `LastFedRateT2`, `LastAvgHuntingEfficiency` (EcosystemSimulator.cs:176-177) | Predator feeding metrics |
| Reproduction scale | `LastReproScaleT1`, `LastReproScaleT2` (EcosystemSimulator.cs:185-186) | Per-tier reproduction scale |
| Average condition | `AvgConditionT1`, `AvgConditionT2` (EcosystemSimulator.cs:204-205) | Population-weighted mean condition, computed at step end (cs:1324-1338) |

`LastTotalDeaths` and `LastTotalBirths` are computed properties summing the above
(EcosystemSimulator.cs:189-192). `LastFedRateT1` is a population-weighted mean over live
Tier 1 species (EcosystemSimulator.cs:763-780). `AvgConditionT1`/`T2` skip species below
`MIN_ALIVE_POP` and weight by population (EcosystemSimulator.cs:1329-1337).

## 7. `PerSpeciesStepData`: one species on one day

`PerSpeciesStepData` (SimulationRunner.cs:20) is a value-type struct, chosen over a class to
avoid per-day heap allocation at roughly 365 days times N species
(SimulationRunner.cs:16-18). One instance is built per species per recorded day inside
`RecordStep` (SimulationRunner.cs:561-580) and stored in `StepRecord.SpeciesData` keyed by
`FullName`.

| Field | Type | Source in `RecordStep` | Meaning |
|---|---|---|---|
| `Population` | long | `SafePopToLong(sp.Population)` (SimulationRunner.cs:563) | Rounded population at end of day |
| `Condition` | float | `sp.Condition` (cs:564) | Health [0,1] |
| `ThermalPerf` | float | `sp.RawThermalPerformance` (cs:565) | Thermal performance without `Pmax`. This is the raw value, not the `Pmax`-scaled `ThermalPerformance`; see the raw-versus-`Pmax` note in section 3.3 |
| `FinalPerf` | float | `sp.FinalPerformance` (cs:566) | Logging-only final performance. This is the `Pmax`-scaled `ThermalPerformance * FedRate`, deliberately different from the raw `RawFinalPerformance` that drives condition; see the raw-versus-`Pmax` note in section 3.3 |
| `FedRate` | float | `LastFedRateBySpecies[fn]`, fallback `sp.FedRate` (cs:567) | This day's fed rate |
| `HuntingEff` | float | `sp.CurrentHuntingSuccess` if Tier 2 else 0 (cs:568) | Tier 2 hunting success. For Tier 1 the record writes a hard 0 here, even though the `SimSpecies.CurrentHuntingSuccess` field itself sits at 1 (reset in Step 1, never overwritten for Tier 1). The 0 is a record convention to keep the column meaningful only for predators, not a reading of the field |
| `Births` | long | `LastBirthsBySpecies[fn]`, 0 on non-biology days (cs:559,569) | Births this day |
| `TempDeaths` | long | `LastTempDeathsBySpecies[fn]` (cs:570) | Thermal deaths this day |
| `ConditionDeaths` | long | `LastConditionDeathsBySpecies[fn]` (cs:571) | Condition deaths this day |
| `NaturalDeaths` | long | `LastNaturalDeathsBySpecies[fn]` (cs:572) | Natural deaths this day |
| `Eaten` | long | `LastEatenBySpecies[fn]` (cs:573) | Prey eaten this day. Tier 1 only |
| `BirthRate` | float | `births / startPop` if `startPop > 0` else 0, where `startPop` is the effective start population after the fallback below (cs:553-557,574) | Per-capita births. The denominator is the effective `startPop`, not the raw snapshot |
| `ReproScale` | float | `LastReproScaleBySpecies[fn]` (cs:575) | Reproduction scale [0,1] |
| `BirthAccum` | float | `GetBirthAccum(fn)` (cs:576) | Birth accumulator residual after this step |
| `NaturalDeathAccum` | float | `GetNaturalDeathAccum(fn)` (cs:577) | Natural death residual |
| `ConditionDeathAccum` | float | `GetConditionDeathAccum(fn)` (cs:578) | Condition death residual |
| `PredationAccum` | float | `GetPredationAccum(fn)` (cs:579) | Predation residual. Tier 1 only |

`BirthRate` resolves its denominator in two steps (SimulationRunner.cs:553-557,574). First it
reads `startPop` from `StartPopBySpecies` via `GetOrZeroLong`. If that snapshot is 0 but the
species is alive (`sp.Population > 0`), `startPop` is replaced by the current rounded
population. Then `BirthRate = startPop > 0 ? births / startPop : 0`. So there is one rule, not
two: the denominator is the start-of-day snapshot, but when that snapshot is 0 for a still
alive species it falls back to the current rounded population, and `BirthRate` is only 0 when
the effective `startPop` is still 0, which means the species is dead this day. The table row
and this paragraph describe the same single computation. On a day where biology does
not run (governed by the biology-day predicate in section 13), the daily event counts
(`Births`, all death types, `Eaten`, `ReproScale`) are set to 0, while the carried-forward
fields (`Population`, `Condition`, `FedRate`, the accumulator residuals) keep their current
values (SimulationRunner.cs:559,569-575). These 17 fields map one-to-one to the 17 per-species
CSV columns; the CSV layout is in `csv-output-formats.md`.

## 8. `StepRecord`: one simulated day

`StepRecord` (SimulationRunner.cs:75) is a class (one heap allocation per day). The runner
keeps every day's record in `private List<StepRecord> _records` (SimulationRunner.cs:364),
appended once per simulated day by `RecordStep` (SimulationRunner.cs:583). `GetRecords`
returns a copy (SimulationRunner.cs:600).

### 8.1 Fields

Time and environment (SimulationRunner.cs:78-85): `Day` (1-based display day), `Year`
(1-based), `Temperature` (Celsius), `BiologyCycle` (the biology-step counter, 0 on
non-biology days).

Population (SimulationRunner.cs:88-99):

| Field | Type | Meaning |
|---|---|---|
| `StartPop` | long | Total start population, `StartPopT1 + StartPopT2` (cs:488) |
| `EndPop` | long | Total end population (cs:489) |
| `Tier1Pop` | long | Tier 1 total (cs:494) |
| `Tier2Pop` | long | Tier 2 total, legacy (cs:495) |
| `TierVariantPop` | `Dictionary<string,long>` | Per `(tier, variant-label)` rollup population, keyed by column name `Tier{tier}_{SanitizedLabel}` (cs:99) |

`TierVariantPop` is the dynamic rollup that replaced the legacy fixed
`Tier1Arctic/Common/Tropical/Custom` buckets (SimulationRunner.cs:95-99). It is filled in
the per-species loop of `RecordStep`, one entry per distinct `(tier, label)` present that
day (SimulationRunner.cs:548-551). The label used is `sp.VariantLabel`, or `sp.Name` when the
variant label is empty, and it is passed through the same `StepRecord.SanitizeColumnName`
used for per-species columns, so the key is `Tier{tier}_{SanitizeColumnName(label)}`
(SimulationRunner.cs:548-549). `BuildVariantRollupColumns` produces the header from the same
label rule and the same sanitizer (SimulationRunner.cs:172,187), so for the common case of
distinct labels the dictionary key and the header column are byte-identical. One mismatch
exists for true sanitizer collisions: `BuildVariantRollupColumns` deduplicates on the raw
`(tier, label)` pair and appends `_2`, `_3` to colliding sanitized header names
(SimulationRunner.cs:173-174,184-195), but the `TierVariantPop` key has no such suffix
(SimulationRunner.cs:549), so two distinct labels that sanitize equal collapse to one
dictionary key holding their summed population while the header still shows two columns. This
only happens when two different variant labels reduce to the same `[A-Za-z0-9_]` form within a
tier; with the canonical Cold/Warm/Hot labels it never occurs. The full column schema and the
collision rule are in `csv-output-formats.md`.

Death tracking (SimulationRunner.cs:102-109), all long: `EatenT1`, `TempDeathsT1`,
`TempDeathsT2`, `ConditionDeathsT1`, `ConditionDeathsT2`, `NaturalDeathsT1`,
`NaturalDeathsT2`, `TotalDeaths`. `TotalDeaths` is the sum of all seven pathway counts
(SimulationRunner.cs:476-478).

Birth tracking (SimulationRunner.cs:112-113): `BirthsT1`, `BirthsT2`.

Feeding (SimulationRunner.cs:116-121): `FedRateT2`, `AvgHuntingEff` (legacy predator),
`FedRateT1`, `FoodDensityT1`.

Condition (SimulationRunner.cs:124-125): `AvgConditionT1`, `AvgConditionT2`.

Accumulator snapshots (SimulationRunner.cs:128-134): `BirthAccumT1`, `BirthAccumT2`,
`NaturalDeathAccumT1`, `NaturalDeathAccumT2`, `ConditionDeathAccumT1`,
`ConditionDeathAccumT2`, `PredationAccumT1`. These copy the tier-level accumulator totals
from section 5.3 (SimulationRunner.cs:529-535).

Reproduction (SimulationRunner.cs:137-138): `ReproScaleT1`, `ReproScaleT2`.

Per-species (SimulationRunner.cs:143): `SpeciesData`, a
`Dictionary<string, PerSpeciesStepData>` keyed by `FullName`. The tier-level fields above
are the sums of these per-species values, the tier-rollup invariant
(SimulationRunner.cs:14-15,141-142).

### 8.2 Helpers on `StepRecord`

`StepRecord` also defines static helpers used by the CSV writer. They are documented in
`csv-output-formats.md`; named here so a reader knows where they live:

- `BuildVariantRollupColumns(orderedSpecies, tier2)` (SimulationRunner.cs:159) builds the
  ordered list of dynamic rollup column names.
- `ToCsvLine(orderedSpecies, tier2)` (SimulationRunner.cs:209) and `CsvHeader(orderedSpecies,
  tier2)` (SimulationRunner.cs:264) emit one daily row and the header. When `tier2` is false
  the fixed Tier-2 columns are omitted.
- `SanitizeColumnName(fullName)` (SimulationRunner.cs:322), described in section 2.

### 8.3 Conversion helpers in the runner

`RecordStep` uses three private helpers (SimulationRunner.cs:588-598):

- `GetOrZeroLong(dict, key)` returns the long value or 0 (cs:588).
- `GetOrFallbackFloat(dict, key, fallback)` returns the float value or a fallback (cs:591).
- `SafePopToLong(pop)` rounds a float population to long, converting non-finite values
  (NaN, infinity) to 0 instead of the `long.MinValue` that a raw cast would produce (cs:597).
  `EcosystemSimulator` keeps a private mirror of this for its own rounding
  (EcosystemSimulator.cs:296).

## 9. `SimulationSummary`: scenario rollup

`SimulationSummary` (SimulationRunner.cs:1206) is a transient class produced once per
scenario by `GetSummary` (SimulationRunner.cs:934-977) from the `_records` list. It feeds
the corresponding fields of `ScenarioResult`.

| Field | Meaning |
|---|---|
| `TotalDays` | Number of days actually recorded (cs:940) |
| `TotalBiologyCycles` | Number of biology steps run (cs:941) |
| `Crashed`, `CrashDay`, `CrashTier` | Crash outcome copied from the runner (cs:942-944) |
| `FinalTier1Pop`, `FinalTier2Pop` | Tier totals from the last record (cs:948-949) |
| `MaxTier1Pop`, `MinTier1Pop`, `MaxTier2Pop`, `MinTier2Pop` | Tier extremes across all days; min ignores 0 (cs:962-971) |
| `AvgTemperature`, `MinTemperature`, `MaxTemperature` | Temperature stats across all days (cs:972-974) |

The min-population logic only counts days where the tier population is at least 1
(SimulationRunner.cs:963,965), so `MinTier1Pop` is the smallest non-extinct daily value, not
0, unless the tier was never alive. The "alive" test used here, and in every extinction and
min computation in this document, is the recorded rounded population being at least 1 (the
`long` value, equivalently population that did not round to 0). That is the rounding-to-0 rule
of Step 10, not the engine's `MIN_ALIVE_POP = 1.0` float gate. The two agree for any
population at or above 1.0, and differ only for a fractional population in `[0.5, 1.0)`, which
rounds to 1 (counts as alive in records) but is below the `MIN_ALIVE_POP` float gate (skipped
by biology). Records always use the rounded-long test.

## 10. `ScenarioResult`: one scenario's full output

`ScenarioResult` (ScenarioResult.cs:10) is the per-scenario output object, built by
`ToScenarioResult` (SimulationRunner.cs:982-1037) and kept in
`AggregateResults.Scenarios` (ScenarioResult.cs:283). It is the atomic result unit; one
scenario produces one `ScenarioResult` and one `scenario_N.csv`.

### 10.1 Fields

| Group | Fields | Meaning |
|---|---|---|
| Identity | `ScenarioIndex` (1-based), `RandomSeed` | ScenarioResult.cs:13-14. `RandomSeed` is the runner's `UsedSeed` (SimulationRunner.cs:1008) |
| Timing | `TotalDays`, `BiologyCycles` | ScenarioResult.cs:17-18 |
| Outcome | `Crashed`, `CrashDay` (-1 if none), `CrashTier` (-1 if none) | ScenarioResult.cs:21-23 |
| Final population | `FinalTier1Pop`, `FinalTier2Pop` | ScenarioResult.cs:26-27 |
| Per-species final pop | `FinalSpeciesPopulations` (`Dictionary<string,long>` by `FullName`) | ScenarioResult.cs:30; built by `CaptureSpeciesPopulations` (SimulationRunner.cs:1039-1045) |
| Per-species metrics | `SpeciesMetrics` (`Dictionary<string,PerSpeciesScenarioMetrics>` by `FullName`) | ScenarioResult.cs:35 |
| Population extremes | `MaxTier1Pop`, `MinTier1Pop`, `MaxTier2Pop`, `MinTier2Pop` | ScenarioResult.cs:38-41 |
| Temperature | `AvgTemperature`, `MinTemperature`, `MaxTemperature` | ScenarioResult.cs:44-46 |
| Condition | `AvgConditionT1`, `AvgConditionT2`, `FinalConditionT1`, `FinalConditionT2` | ScenarioResult.cs:49-52 |
| Per-column pop stats | `PopMean`, `PopMax`, `PopMin`, `PopStdDev` | ScenarioResult.cs:55-58 |
| Extinction timing | `ExtinctionDay` (`Dictionary<string,int>` by `Tier{n}_{variantLabel}`) | ScenarioResult.cs:63 |
| Embedded CSV | `CsvData` (the full scenario CSV text) | ScenarioResult.cs:66 |

`AvgConditionT1`/`T2` are the full-run mean of the per-day average condition
(SimulationRunner.cs:992-1002); `FinalConditionT1`/`T2` are the simulator's last
`AvgCondition` (SimulationRunner.cs:1028-1029). The four `Pop*` dictionaries come from
`ComputePopulationStats` (SimulationRunner.cs:622-736) and are keyed by both tier-total
names (`Tier1Pop`, `Tier2Pop`), the dynamic rollup column names, and per-species `FullName`
(SimulationRunner.cs:648-732). `PopMean`/`PopStdDev` are `double`; `PopMax`/`PopMin` are
`long`. `ExtinctionDay` keys are the dynamic rollup columns only, value -1 if the
tier-variant never reached 0 after being alive. The scan over `_records` sets `wasAlive` once
the rollup column's rounded population exceeds 0, then records the first later day the column
is 0 (`pop > 0` then `pop == 0`, SimulationRunner.cs:681-696), the same rounded-long "alive"
rule as section 9. `CsvData` is
the entire scenario CSV produced by `ToCsvInternal` (SimulationRunner.cs:1035); in bulk runs
this string is streamed out and nulled to release memory, see `bulk-system.md`.

Two display helpers exist: `GetSummaryLine` (ScenarioResult.cs:71) and `GetStatusIcon`
(ScenarioResult.cs:83, returns `"X"` or `"OK"`).

### 10.2 `PerSpeciesScenarioMetrics`

`PerSpeciesScenarioMetrics` (ScenarioResult.cs:102) is one species' post-processed metrics
for one scenario, computed by `ComputePerSpeciesScenarioMetrics`
(SimulationRunner.cs:1060-1186) in a single pass over `_records`. These metrics are not
clamped by carrying capacity; they expose physiological signals that final-day population
masks (ScenarioResult.cs:93-97).

| Field | Meaning |
|---|---|
| `FullName` | Identity key (ScenarioResult.cs:104) |
| `FinalPopulation` | Last recorded population (cs:105; SimulationRunner.cs:1100) |
| `MeanConditionFullRun`, `MeanConditionFinalYear` | Mean condition over all days / last 365 days. Computed over alive days only (cs:109-111; SimulationRunner.cs:1128-1132) |
| `MeanBirthRateFullRun`, `MeanBirthRateFinalYear` | Mean per-capita birth rate, alive days only (cs:113-114) |
| `PopCvFullRun`, `PopCvFinalYear` | Population coefficient of variation, StdDev/Mean (cs:117-118; helper `ComputeCvFromSums` SimulationRunner.cs:1192) |
| `MeanPopulationFinalYear` | Mean population over the last 365 days (cs:121) |
| `FinalYearTempDeaths`, `FinalYearConditionDeaths`, `FinalYearNaturalDeaths`, `FinalYearPredationDeaths` | Total deaths by pathway summed over the last 365 days. Predation uses `Eaten` (Tier 1 only) (cs:123-128; SimulationRunner.cs:1148-1151) |
| `MinPopulation`, `MaxPopulation` | Population extremes over the whole run (cs:131-132) |
| `ExtinctionDay` | First day the rounded `Population` is 0 after the species had been alive, else -1. "Alive" here means the recorded rounded population (`d.Population`, a `long`) was at least 1 on some earlier day; the scan is `wasAlive |= d.Population > 0` then first `d.Population == 0` (cs:135; SimulationRunner.cs:1106-1109) |
| `CrashDay` | First day the rounded `Population` drops below `max(CRASH_FLOOR, (long)(startPop * CRASH_FRACTION))`, else -1. `startPop` is this species' rounded population on the first recorded day of the scenario (`d.Population` at record index 0, SimulationRunner.cs:1099). The crash scan only runs when that day-1 `startPop > 0`, and walks all recorded days in order (cs:136; SimulationRunner.cs:1111-1117) |
| `Survived` | Computed: `FinalPopulation > 0` (cs:138) |

Constants in the runner that govern these (SimulationRunner.cs:1052-1058):
`FINAL_YEAR_DAYS = 365`, `CRASH_FRACTION = 0.05`, `CRASH_FLOOR = 10`. The final-year window
is the last 365 recorded days; for runs shorter than 365 days the slice covers all days, so
final-year metrics equal full-run metrics (ScenarioResult.cs:98-99,
SimulationRunner.cs:1067,1160). Condition and birth rate are averaged over alive days only,
because once a species hits 0 the recorded `Condition` sticks at its last value and would
distort the mean (SimulationRunner.cs:1119-1132); population means include the zero days,
which are real data for an extinct species.

## 11. `AggregateResults`: one run

`AggregateResults` (ScenarioResult.cs:204) holds the cross-scenario rollup for one run (one
configuration, N scenarios). It owns `List<ScenarioResult> Scenarios` (ScenarioResult.cs:283)
and is filled by `CalculateAggregates` (ScenarioResult.cs:288-424).

### 11.1 Field groups

Run counts (ScenarioResult.cs:207-210): `TotalScenarios`, `CompletedScenarios`,
`SurvivedScenarios`, `CrashedScenarios`.

Config snapshot (ScenarioResult.cs:213-247): `DaysPerScenario`, `BiologyStep`,
`RandomSeed`, `CarryingCapacity`, `ConditionDrainRate`, `ConditionRecoveryRate`,
`BaseTemperature`, `SeasonalAmplitude`, `ClimateTrend`, `InterannualVariation`,
`VariabilityMagnitude`, `WarmingBias`, `Autocorrelated`, `DailyVariationRange`,
`RandomnessGrowthRate`, `TemperatureBoundsMin`, `TemperatureBoundsMax`, and a reference to
the run's `RunSpecies` (`RunSpeciesList`). These mirror the configuration object and are the
source for the config CSV/JSON; the meaning of each is in `configuration-reference.md`.
`CompletedAt` (DateTime) and `BatchName` (the source run name, ScenarioResult.cs:250) round
out the metadata.

Population averages across surviving scenarios (ScenarioResult.cs:253-258):
`AvgFinalTier1Pop`, `AvgFinalTier2Pop`, `MinFinalTier1Pop`, `MaxFinalTier1Pop`,
`MinFinalTier2Pop`, `MaxFinalTier2Pop`.

Crash stats (ScenarioResult.cs:261-262): `CrashRate`, `AvgCrashDay`.

Condition stats (ScenarioResult.cs:265-268): `AvgConditionT1`, `AvgConditionT2`,
`AvgFinalConditionT1`, `AvgFinalConditionT2`.

Per-species dicts, all keyed by `FullName` (ScenarioResult.cs:272-277):
`PerSpeciesAvg`, `PerSpeciesMin`, `PerSpeciesMax` (float),
`PerSpeciesExtinct`, `PerSpeciesSurvived` (int counts of scenarios),
`PerSpeciesSurvivedAvg` (float, average population over surviving scenarios only).

Rich per-species aggregate (ScenarioResult.cs:280): `PerSpeciesMetrics`, a
`Dictionary<string, PerSpeciesAggregate>` by `FullName`.

### 11.2 How aggregates are computed

`CalculateAggregates` (ScenarioResult.cs:288) iterates `Scenarios` once for the tier-level
counts and condition sums (ScenarioResult.cs:309-336), then a second pass over each
scenario's `FinalSpeciesPopulations` to build the simple per-species dicts
(ScenarioResult.cs:371-420). Crashed scenarios are excluded from the survived-population
averages but included in crash and condition stats (ScenarioResult.cs:315-335). Finally it
calls `BuildPerSpeciesAggregate` (ScenarioResult.cs:431-482), which unions the `FullName`
keys across every scenario's `SpeciesMetrics` and reduces each metric into a
`PerSpeciesAggregate`.

### 11.3 `PerSpeciesAggregate`, `AggStat`, `ExtinctionStat`

`PerSpeciesAggregate` (ScenarioResult.cs:174) wraps the cross-scenario reduction of one
species:

| Field | Type | Meaning |
|---|---|---|
| `FullName` | string | Identity key (cs:176) |
| `N` | int | Scenarios contributing (cs:177) |
| `NSurvived` | int | Scenarios where final pop > 0 (cs:178) |
| `MeanConditionFullRun` ... `FinalPopulation` | `AggStat` | One per metric on `PerSpeciesScenarioMetrics` (cs:180-194) |
| `ExtinctionTiming`, `CrashTiming` | `ExtinctionStat` | Timing reductions (cs:195-196) |

`AggStat` (ScenarioResult.cs:146) is the per-metric reduction: `Mean`, `StdDev`, `Min`,
`Max`, plus `SurvivedMean` and `SurvivedStdDev` computed over surviving scenarios only. It is
filled by `ComputeAggStat` (ScenarioResult.cs:484-520), which accumulates sum and
sum-of-squares in one pass over the per-scenario metric values. `Mean = sum / n` and the
variance is the population variance `E[x^2] - E[x]^2 = (sqSum / n) - Mean^2`, divided by `n`,
not `n - 1` (ScenarioResult.cs:507-508). `StdDev` is `sqrt(variance)` when `variance > 0` and
exactly 0 otherwise; the guard does not take an absolute value, it clamps any zero or negative
variance straight to a 0 standard deviation (ScenarioResult.cs:509). `SurvivedMean` and
`SurvivedStdDev` use the identical formula over the surviving subset with divisor `nSurvived`
(ScenarioResult.cs:513-518). Edge cases: when `n == 0` all of `Mean`, `StdDev`, `Min`, `Max`
keep the struct default 0 and `Min`/`Max` are never assigned. When `nSurvived == 0` the whole
survived block is skipped, so `SurvivedMean` and `SurvivedStdDev` stay at the struct default
0. When `nSurvived == 1` the single value gives `SurvivedMean` equal to that value and
`SurvivedStdDev` exactly 0 (variance is `v^2 - v^2 = 0`). `Min`/`Max` start from
`float.MaxValue`/`float.MinValue`, so for `n >= 1` they always hold a real observed value.

`ExtinctionStat` (ScenarioResult.cs:160) reduces a per-scenario day value (`ExtinctionDay`
or `CrashDay`) across scenarios: `NEvents` (scenarios where the event happened, day != -1),
`NNonEvents` (day == -1), and `MinDay`/`MaxDay`/`MeanDay` over the event scenarios only, each
-1 when there are no events. It is filled by `ComputeExtinctionStat`
(ScenarioResult.cs:522-544).

The CSV serialization of all of these (the `ToAggregateCsv` method, ScenarioResult.cs:588,
and `ConfigExporter`, ScenarioResult.cs:968) is covered in `csv-output-formats.md`. The
cross-run rollup of multiple `AggregateResults` for the bulk path lives in `bulk-system.md`.

## 12. `RunControl`: pause/stop signal

`RunControl` (RunControl.cs:9) is a small class with two `volatile bool` fields, `Paused`
and `Stopped` (RunControl.cs:11-12). The runner reads it once per day at the top of the day
loop (SimulationRunner.cs:419-423): while `Paused` and not `Stopped`, the loop sleeps in 10 ms
increments without consuming RNG or advancing state, so a paused-then-resumed run is
byte-identical to an uninterrupted one with the same seed; `Stopped` breaks the loop. The
fields are volatile because scenarios run on background threads while the flags are toggled
from the main thread (RunControl.cs:4-7). `RunControl` is used by the standard run only; the
bulk path polls a UI flag instead, see `ui-and-io.md` and `bulk-system.md`.

The byte-identical guarantee assumes the only source of nondeterminism is the seeded RNG
stream, and that holds for every recorded value. No per-day `StepRecord` field, no
`PerSpeciesStepData` field, and no `PerSpeciesScenarioMetrics` or `ScenarioResult` metric is
derived from wall-clock time, thread scheduling, or elapsed real time; they are all functions
of the simulation state and the RNG draws. The one wall-clock field in the result model is
`AggregateResults.CompletedAt` (ScenarioResult.cs:247), set after the run, and it is run
metadata, not a simulation output, so it does not affect the byte-identical-resume property.
The other two `DateTime.Now` reads are export-time stamps only: the output filename timestamp
(SimulationRunner.cs:922) and the `exportedAt` / `# Exported` lines in the config export
(ScenarioResult.cs:1023,1132). Neither enters the recorded data.

## 13. The per-day driver loop and `BiologyStep`

The structures above are written by a fixed per-day loop in `SimulationRunner.Run`
(SimulationRunner.cs:392-459). This section gives the loop and the `BiologyStep` multiplier
in full because every per-step rate in section 14 is scaled by `BiologyStep`, and the loop
decides which days run biology at all.

`BiologyStep` is a public `int` field on `SimulationRunner`, default 1 (SimulationRunner.cs:355).
`Run` copies it onto the simulator before the loop, `Ecosystem.BiologyStep = BiologyStep`
(SimulationRunner.cs:400). It is the number of simulated days represented by one biology
step. At the default of 1, biology runs every day. The simulator reads it as a `float`
multiplier inside every rate equation (births, all three death pathways, predator demand);
those uses are listed in section 14.

`TotalDays` is the loop bound, a public `int`, default 365 (SimulationRunner.cs:354). The loop
is 0-based over `dayIndex` and derives display values from it (SimulationRunner.cs:415-438):

```text
for dayIndex in 0 .. TotalDays-1:
    displayDay = dayIndex + 1                                  // 1-based day for records
    year       = (dayIndex / DAYS_PER_YEAR) + 1                // integer division, 1-based
    temp       = TempCalc.GetTemperature(dayIndex)             // Celsius, uses 0-based index
    runBiology = (displayDay == 1) || (displayDay % BiologyStep == 0)
    if runBiology:
        biologyCycleCounter += 1
        Ecosystem.ProcessBiologyStep(temp)                     // the ten steps, section 14
    RecordStep(displayDay, year, temp, runBiology)             // always records a row
    if runBiology and Ecosystem.HasCrashed():                  // crash test only on biology days
        HasCrashed = true; CrashDay = displayDay; CrashTier = Ecosystem.GetCrashedTier()
        break
```

Points a reimplementation must match:

1. The biology-day predicate is `displayDay == 1 || displayDay % BiologyStep == 0`
   (SimulationRunner.cs:430). Day 1 always runs biology even if `1 % BiologyStep != 0`. At the
   default `BiologyStep == 1` every day is a biology day, so the special-case for day 1 is
   only observable when `BiologyStep > 1`.
2. `year` uses integer division of the 0-based `dayIndex` by `DAYS_PER_YEAR`
   (SimulationRunner.cs:426). `DAYS_PER_YEAR` is a constant on `TemperatureCalculator`; its
   value and the temperature model are in `temperature-model.md`.
3. `GetTemperature(dayIndex)` is called with the 0-based index, not the 1-based `displayDay`
   (SimulationRunner.cs:428).
4. A row is recorded for every day via `RecordStep`, biology day or not. On a non-biology day
   the daily event counts are forced to 0 while carried-forward fields persist; this is the
   carry-forward behavior already described in sections 3.3 and 7.
5. The crash test runs only on biology days (SimulationRunner.cs:440). `HasCrashed()` is true
   when the total live population is 0 (`totalPop == 0`), and the loop breaks on the first such
   day, so the run can record fewer than `TotalDays` rows. The cooperative pause/stop check at
   the top of the loop is described in section 12.

## 14. The ten-step biology sequence (exact equations)

`EcosystemSimulator.ProcessBiologyStep(temperatureCelsius)` runs ten steps in a fixed order
every biology day. Sections 5 and 6 already cover the accumulator and counter bookkeeping;
this section gives the exact arithmetic of each step that the gaps above leave implicit, so
the numeric output can be reproduced from this document alone. All performance, condition,
and fed-rate values are dimensionless in [0,1]. `BiologyStep` is the day-count multiplier from
section 13. `Pmax` is the per-species peak performance in [0,1]. The shared global rates used
as fallbacks are:

| Global rate | Property | Default | Inherited when |
|---|---|---|---|
| Condition drain | `EcosystemSimulator.ConditionDrainRate` | 0.15 (EcosystemSimulator.cs:240) | `SimSpecies.ConditionDrainRate < 0` (EcosystemSimulator.cs:963) |
| Condition recovery | `EcosystemSimulator.ConditionRecoveryRate` | 0.10 (EcosystemSimulator.cs:241) | `SimSpecies.ConditionRecoveryRate < 0` (EcosystemSimulator.cs:964) |

Per-species `ConditionDrainRate`/`ConditionRecoveryRate` default to the `-1` sentinel
(section 3.2). The resolution is per step inside `UpdateCondition`: a non-negative per-species
value is used directly, otherwise the simulator-global rate above is used
(EcosystemSimulator.cs:963-964). A blank per-species column in a bulk CSV import keeps `-1`
and therefore runs at 0.15 drain / 0.10 recovery.

### Step 1, thermal performance

For every species, the simulator resets the per-step scratch fields and recomputes thermal
performance (EcosystemSimulator.cs:593-596):

```text
RawThermalPerformance = CalculatePerformance(temperatureCelsius)   // section 3.5, no Pmax
ThermalPerformance    = RawThermalPerformance * Pmax
FedRate               = 1                                           // reset; Step 2 overwrites
CurrentHuntingSuccess = 1                                           // reset; Step 2 overwrites for Tier 2
```

### Step 2, feeding and predation

Tier 1 (prey, primary). One shared food pool, foraged with the same Holling II mechanism as
Tier 2 (via `ComputeForagingSuccess`). The supply cap `foodDensity` and the search ratio are
computed once before the per-species loop (EcosystemSimulator.cs:765-782), then each live Tier 1
species takes a fed rate from its foraging success capped by supply (EcosystemSimulator.cs:785-802):

```text
tier1Pop         = max(0, total live Tier 1 Population)
capSafe          = max(CarryingCapacityPerTier, 1)
tier1Consumption = sum over live Tier 1 of Population * max(1, EatingAmount)
foodDensity      = max(0, 1 - tier1Consumption / capSafe)   // 1 empty, 0 at/over capacity
resourceRatio    = capSafe / max(tier1Pop, 1)               // Holling search ratio

for each Tier 1 species sp:
    if sp.Population >= MIN_ALIVE_POP:                   // 1.0
        gatherSuccess = ComputeForagingSuccess(sp, resourceRatio)   // Holling II + variance
        sp.FedRate    = min(1, gatherSuccess * foodDensity)
    else:
        sp.FedRate = 0                                   // dead/low-pop record 0
```

`CarryingCapacityPerTier` is the per-tier carrying capacity from config, always on as of
v11.1 and validated > 0; its default on the simulator is 5000 (EcosystemSimulator.cs:237).
`ComputeForagingSuccess` is `CalculateHollingEfficiency(HuntingEfficiency, resourceRatio)`
(returns 1 when efficiency >= 1) plus a random `HuntingVariance` term when that variance is > 0
(EcosystemSimulator.cs:931-940). At `HuntingEfficiency = 1`, `HuntingVariance = 0`,
`EatingAmount = 1` this reduces to `FedRate = foodDensity`, identical to the old linear model.
The in-code comment block explains the shared foraging model (EcosystemSimulator.cs:745-782).

Tier 2 (predator, legacy, secondary). Current runs disable Tier 2, so this path is inactive
by default; it is documented because it is part of the model and runs when Tier 2 is enabled.
Constants (EcosystemSimulator.cs:269-271): `NORMAL_PREY_RATIO = 20`, `MIN_HUNTING_SUCCESS = 0`,
`MAX_HUNTING_SUCCESS = 1`. The Holling Type II efficiency is
`CalculateHollingEfficiency(baseEff, preyRatio)` (EcosystemSimulator.cs:924-932):

```text
if preyRatio <= 0:  return 0
if baseEff   <= 0:  return 0
if baseEff   >= 1:  return 1
halfSaturation = NORMAL_PREY_RATIO * (1 - baseEff) / baseEff
efficiency     = preyRatio / (preyRatio + halfSaturation)
```

where `preyRatio = availablePrey / totalPredators` over live prey and predators
(EcosystemSimulator.cs:792-796). For each predator (EcosystemSimulator.cs:805-824):

```text
hollingEff     = CalculateHollingEfficiency(pred.HuntingEfficiency, preyRatio)
variance       = (rng.NextDouble() * 2 - 1) * pred.HuntingVariance        // uniform +/- HuntingVariance
huntingSuccess = clamp(hollingEff + variance, MIN_HUNTING_SUCCESS, MAX_HUNTING_SUCCESS)
pred.CurrentHuntingSuccess = huntingSuccess
rawDemand      = pred.Population * pred.EatingAmount * pred.ThermalPerformance * BiologyStep
actualDemand   = rawDemand * huntingSuccess
```

The day's total catch and each predator's fed rate (EcosystemSimulator.cs:831,849-857):

```text
totalEaten     = min(availablePrey, sum(actualDemand))
scarcityFactor = (sum(actualDemand) > 0) ? min(1, totalEaten / sum(actualDemand)) : 1
for each predator pred:
    pred.FedRate = min(1, pred.CurrentHuntingSuccess * scarcityFactor)
```

Prey are then removed proportionally to each prey species' share of the live prey pool, using
the predation accumulator (`rawAmount = totalEaten * Population / availablePrey`, section 5.2,
EcosystemSimulator.cs:868-895).

### Step 3, raw final performance

For every species (EcosystemSimulator.cs:608):

```text
RawFinalPerformance = RawThermalPerformance * FedRate     // condition target; no Pmax
```

This uses the raw (non-`Pmax`) thermal value on purpose; see the raw-versus-`Pmax` note in
section 3.3.

### Step 4, condition update

`UpdateCondition(sp)` moves `Condition` toward the Step 3 target. It is skipped for species
below `MIN_ALIVE_POP` (EcosystemSimulator.cs:954). The target is `RawFinalPerformance`; the
move is a fraction of the gap to the target, the fraction scaled by a quadratic acceleration
term and by `Pmax`. `Pmax` is clamped to a small positive floor to avoid division by zero
(EcosystemSimulator.cs:960). Exact equations (EcosystemSimulator.cs:956-989):

```text
target    = sp.RawFinalPerformance
pmaxSafe  = max(sp.Pmax, 1e-4)
drainRate = (sp.ConditionDrainRate    >= 0) ? sp.ConditionDrainRate    : ConditionDrainRate     // 0.15 default
recovery  = (sp.ConditionRecoveryRate >= 0) ? sp.ConditionRecoveryRate : ConditionRecoveryRate  // 0.10 default

if sp.Condition > target:                                    // draining
    severity        = (1 - target) * (1 - target)            // 0 at target=1, 1 at target=0
    effectiveDrain  = drainRate * (1 + severity) / pmaxSafe
    sp.Condition   -= (sp.Condition - target) * effectiveDrain
else:                                                         // recovering
    boost              = target * target                     // 0 at target=0, 1 at target=1
    effectiveRecovery  = recovery * (1 + boost) * pmaxSafe
    sp.Condition      += (target - sp.Condition) * effectiveRecovery

sp.Condition = clamp(sp.Condition, 0, 1)
```

The quadratic terms make the move accelerate as conditions worsen (drain) or improve
(recovery): the drain multiplier `1 + severity` ranges from 1 at the optimal target to 2 at a
zero target, and the recovery multiplier `1 + boost` ranges from 1 at a zero target to 2 at a
target of 1. The `Pmax` placement is asymmetric and must be reproduced exactly: drain divides
by `pmaxSafe` (high-`Pmax` specialists drain slower), recovery multiplies by `pmaxSafe`
(specialists recover faster). `Pmax` never enters the target, so `Condition` keeps the same
[0,1] meaning across species and the shared `ReproThreshold`/`DeathThreshold` need no
per-species tuning.

### Step 5, final performance (logging only)

For every species (EcosystemSimulator.cs:628):

```text
FinalPerformance = ThermalPerformance * FedRate     // Pmax-scaled; logging/CSV only
```

`FinalPerformance` is never read by a later step (section 3.3).

### Step 6, thermal death

`ApplyThermalDeath(sp)` is an instant whole-population kill at lethal temperature. The trigger
predicate is `RawThermalPerformance == 0`; a species survives when `RawThermalPerformance > 0`
(EcosystemSimulator.cs:1008-1012). Because Step 3.5 returns exactly 0 at or beyond
`CTminC`/`CTmaxC`, this fires precisely at the lethal limits. When it fires
(EcosystemSimulator.cs:1014-1024):

```text
if sp.Population < MIN_ALIVE_POP:  return            // already extinct
if sp.RawThermalPerformance > 0:   return            // survives

deaths        = sp.Population                         // full float population, pre-rounding
sp.Population = 0
sp.Condition  = 0                                     // condition is forced to 0 on thermal death
LastTempDeaths{T1|T2} += deaths                       // tier scalar gets the full float
LastTempDeathsBySpecies[FullName] += (long)deaths     // per-species counter truncates
```

Two side effects a reimplementation must keep. First, `Condition` is set to 0, not left at its
last value, so a species that somehow regained population later would start from 0 condition.
Second, the tier-level total adds the full float `deaths` while the per-species counter adds
`(long)deaths`; on a day a fractional population crosses a lethal limit the two can differ by
the dropped fractional part, the one documented exception to the per-species-sums-to-tier
invariant (section 6.1). There is no accumulator for thermal death; it is a whole-population
event (section 5.1).

### Step 7, condition death

`ApplyConditionDeath(sp)` is graduated mortality below the condition threshold. It is skipped
when `Population < MIN_ALIVE_POP` or when `Condition >= DeathThreshold`
(EcosystemSimulator.cs:1058-1059). The raw death count uses the condition death accumulator
(section 5.2). Equations (EcosystemSimulator.cs:1062-1085):

```text
severity  = (DeathThreshold - Condition) / DeathThreshold      // 0 at threshold, 1 at Condition=0
rawDeaths = Population * severity * DeathRate * BiologyStep
// accumulator pattern (section 5.2) -> wholeDeaths, clamped to Population

if wholeDeaths > 0:
    oldPop       = Population
    oldCondition = Condition
    Population   = max(0, Population - wholeDeaths)
    if Population > 0:
        Condition = min(1, oldCondition * oldPop / Population)  // survivor fitness redistribution
```

The survivor redistribution rescales the surviving health pool on the assumption that the dead
were the weakest members, then caps at 1 (section 3.3).

### Step 8, reproduction

`ApplyReproduction(sp)` is gated on the stricter constant
`MIN_POPULATION_FOR_REPRODUCTION = 2f`: it returns with zero births when `Population < 2`
(EcosystemSimulator.cs:1152-1156). This is the gate from gap 1 and section 3.2, not the 1.0
alive gate. When the species passes the gate, the reproduction scale `reproScale` is a
continuous piecewise function of `Condition`. The inflection value is the constant
`STRUGGLING_REPRO_RATE = 0.10` (EcosystemSimulator.cs:282), the value `reproScale` takes
exactly at `Condition == ReproThreshold`. The four branches (EcosystemSimulator.cs:1168-1191):

```text
if ReproThreshold >= 1:                              // edge: threshold at max
    reproScale = STRUGGLING_REPRO_RATE * Condition
else if ReproThreshold <= 0:                         // edge: no threshold
    reproScale = Condition
else if Condition >= ReproThreshold:                 // healthy ramp
    t          = (Condition - ReproThreshold) / (1 - ReproThreshold)
    reproScale = STRUGGLING_REPRO_RATE + (1 - STRUGGLING_REPRO_RATE) * t
else:                                                // struggling ramp
    reproScale = STRUGGLING_REPRO_RATE * (Condition / ReproThreshold)

reproScale = clamp(reproScale, 0, 1)
```

The healthy branch ramps `reproScale` linearly from 0.10 at `Condition == ReproThreshold` to
1.0 at `Condition == 1`. The struggling branch ramps it linearly from 0 at `Condition == 0` to
0.10 at `Condition == ReproThreshold`. The two branches meet at 0.10, so there is no
discontinuity at the threshold. Only a truly dead species (`Condition == 0`) gets
`reproScale == 0`. The raw birth count then uses the birth accumulator (section 5.2) with
(EcosystemSimulator.cs:1247,1260):

```text
births = Population * reproScale * ReproductionMultiplier * Pmax * BiologyStep
// accumulator pattern (section 5.2) -> wholeBirths added to Population (no clamp)
```

Newborns inherit the species' current group `Condition` with no separate dilution step
(EcosystemSimulator.cs:1240-1253), so the population-weighted average condition is unchanged
by births and no explicit condition write is needed.

### Step 9, natural death

`ApplyNaturalDeathWithAccumulator(sp)` is a flat per-step mortality, skipped only when
`Population <= 0` (EcosystemSimulator.cs:1272). It uses the natural death accumulator
(section 5.2). Equations (EcosystemSimulator.cs:1278-1285):

```text
variance      = (rng.NextDouble() * 2 - 1) * NaturalDeathVariance    // uniform +/- NaturalDeathVariance
baseRate      = max(0, NaturalDeathRate + variance)
effectiveRate = baseRate                                              // flat, no performance floor
deaths        = Population * effectiveRate * BiologyStep
// accumulator pattern (section 5.2) -> wholeDeaths, clamped to Population
```

`MIN_FINAL_PERF_FOR_NATURAL_DEATH` (0.1) is declared but unused here; natural death has no
performance floor (section 3.2).

### Step 10, population rounding

After all per-species steps, each species' `float` `Population` is clamped to the overflow cap
`100 * CarryingCapacityPerTier` and then rounded away from zero to an integer
(EcosystemSimulator.cs:667-681). The clamp constant and rounding mode are stated in section
15, item 1, and the float-to-long handling in section 3.

## 15. Lifetime and invariant summary

1. `Population` is `float` throughout a step. Step 10 mutates it twice: first it clamps any
   population above `100 * CarryingCapacityPerTier` down to that cap (the constant
   `MAX_POP_MULTIPLE_OF_K = 100f`, `popCap = MAX_POP_MULTIPLE_OF_K * CarryingCapacityPerTier`,
   `sp.Population = popCap`, EcosystemSimulator.cs:667-679), then it rounds away from zero to
   an integer (`Math.Round(..., MidpointRounding.AwayFromZero)`, EcosystemSimulator.cs:681).
   The clamp is an overflow guard for the early transient when condition feedback has not yet
   throttled reproduction; without it `Population` could exceed `long.MaxValue` and produce
   `long.MinValue` sentinels in the CSV. `StepRecord` and `PerSpeciesStepData` store the
   rounded `long`.
2. Accumulator dictionaries (section 5) persist for the whole run and carry fractional event
   residuals between days. Per-species event counters (section 6) and tier `Last*` scalars
   (section 6.1) are reset every biology step.
3. Every per-species dictionary is keyed by `FullName` (section 2). The same key threads
   through accumulators, event counters, `StepRecord.SpeciesData`,
   `ScenarioResult.FinalSpeciesPopulations`/`SpeciesMetrics`, and
   `AggregateResults.PerSpecies*`.
4. Tier-rollup invariant: per-species values in `StepRecord.SpeciesData` sum to the matching
   `TierVariantPop` entry, which sum to the tier total (`Tier1Pop`/`Tier2Pop`)
   (EcosystemSimulator.cs:212; SimulationRunner.cs:14-15,141-142).
5. `_thermalDeathAccumulators` is dead code with no accessor (EcosystemSimulator.cs:223).
   Thermal death is a whole-population instant kill needing no fractional residual.
6. Tier 2 fields (`*T2`, `*Tier2*`, predator-only counters) are legacy. Default runs disable
   Tier 2 at load (EcosystemSimulator.cs:336), so these fields stay 0 and their CSV columns
   are suppressed.
7. Two distinct population thresholds gate biology. `MIN_ALIVE_POP = 1.0`
   (EcosystemSimulator.cs:248) gates feeding, condition update, condition death, and the
   condition/fed-rate averages. `MIN_POPULATION_FOR_REPRODUCTION = 2.0`
   (EcosystemSimulator.cs:274) gates births only (section 14, Step 8). A species at population
   1 is alive but sterile. Natural death is gated separately on `Population > 0`
   (EcosystemSimulator.cs:1272).
8. Thermal death (section 14, Step 6) fires exactly when `RawThermalPerformance == 0`, zeroes
   `Population`, and also forces `Condition = 0` (EcosystemSimulator.cs:1016-1017). It is the
   only step that resets `Condition` rather than moving it incrementally, and the only
   pathway whose tier-level float total can exceed the truncated per-species `long` sum
   (section 6.1).
9. Condition drain and recovery default to the simulator-global rates 0.15 and 0.10
   (EcosystemSimulator.cs:240-241) whenever the per-species rate is the negative `-1` sentinel
   (EcosystemSimulator.cs:963-964); a blank bulk-CSV condition-rate column inherits these.
