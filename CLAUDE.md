# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TinySea is a Unity 6 (6000.3.8f1) educational game/simulation built by Northeastern University GhostLab. Players manage a marine ecosystem by buying/selling organisms across a 3-tier food chain while temperature fluctuates seasonally and with climate change. The project has two modes: an interactive game and a headless ecosystem simulation for research.

## Build & Run

This is a Unity project — open in Unity Editor 6000.3.8f1. There is no CLI build pipeline. The primary build target is WebGL (see `Builds/` folder). The project uses the standard Unity C# compilation (Assembly-CSharp).

Key packages: Newtonsoft JSON, TextMesh Pro, Unity UI (uGUI), Vector Graphics, AI Navigation. WebGL input handled by `Assets/WebGLSupport/`.

## Scenes (Build Order)

- `Assets/scenes/main_menu.unity` — Main menu (build index 0)
- `Assets/scenes/mainscene.unity` — Primary gameplay scene (build index 1)
- `Assets/scenes/tutorial.unity` / `tutorialscene.unity` — Tutorial scenes
- `Assets/scenes/simulation.unity` — Headless simulation mode

## Architecture

### Interactive Game (`Assets/scripts/`)

**Turn-based ecosystem loop** driven by `PlayerManager`:
1. Player presses Next Turn → `PlayerManager.nextTurn()`
2. `Temperature` animates and picks new temperature (via `TemperatureTrend` sinusoidal + climate model)
3. Each `CharacterManager` updates performance using its `ThermalCurve` (Arrhenius formula, Kelvin input)
4. `PlayerManager` runs `Predation()` → higher-tier species eat lower-tier species proportionally
5. Each `CharacterManager.ReproduceOrDie()` — reproduce if performance > threshold, die if below death threshold
6. Population capped at `maxFishes`

**Key classes:**
- `PlayerManager` — Central game state: money, species list, turn sequencing, ecosystem pyramid UI. Species are `List<CharacterManager>` organized in groups of 9 (3 tiers × 3 organisms × 3 variants)
- `CharacterManager` — Per-species stats (population, thermal curve, food chain level, reproduction/death rates). Variant enum: Arctic/Common/Tropical
- `Temperature` — Current temperature state, forecast UI, animated temperature selection
- `TemperatureTrend` — Temperature generation model: `tBase + tClimate + tRand` using sinusoidal seasons. Static fields `clim`, `yrRange`, `rand` are set by `LevelLoader`
- `ThermalCurve` — Arrhenius-based thermal performance curve. `getCurve(tempKelvin)` returns 0-1 performance
- `SwimmingHolder` — Manages visual creature GameObjects (spawning/death animations) synced to `CharacterManager.speciesAmount`
- `SwimmingCreature` — Individual creature with boids-like flocking behavior (attract/avoid/align/flee/hunt), coroutine-based to manage CPU
- `ShopManager` — Buy/sell UI. Organisms indexed as `tierBase + icon + (variant * 3)` where tierBase is `activeTier * 9`
- `LevelManager` / `LevelLoader` — Level system with `ILevelEvent` interface for objectives. `LevelLoader.levelToLoad = -1` means freeplay mode. Climate data set via static fields on `Temperature` and `TemperatureTrend`

**Data collection:**
- `SessionRecorder` (singleton) — Queues CSV data and sends to remote server via UnityWebRequest
- `SessionIDGenerator` — Random 15-char alphanumeric session ID
- `SessionTimer` — Tracks round/session time

### Headless Simulation (`Assets/scripts/Simulation/`)

A separate, non-visual ecosystem simulation for research/analysis. Does NOT use MonoBehaviour for the core logic.

**Key classes:**
- `SimulationController` (MonoBehaviour) — Entry point. Reads `SimulationConfig` ScriptableObject, runs multiple scenarios via coroutine, shows results on `ResultsScreenUI`
- `SimulationRunner` — Pure C# simulation loop. Runs for `TotalDays` days, records `StepRecord` per day, outputs CSV. `ToCsv()` embeds full config as `#config:key,value` comment lines and species as a `#species:` CSV table (R-compatible — `#` is the default comment char in `read.csv`)
- `EcosystemSimulator` — 10-step biology sequence (v12-per-species-tracking): thermal performance → feeding/predation (Tier 1 FedRate from food-pool density + Tier 2 Holling II with **per-predator FedRate**, v11) → raw final performance → update Condition (rates scaled by Pmax) → final performance → thermal death → condition death → reproduction (Pmax × condition-driven; no soft cap on births) → natural death → population rounding. Uses accumulators (birth, predation, natural death, condition death) for fractional population tracking. **Carrying capacity is always on** as of v11.1 — no toggle. Rationale (per `EcosystemSimulator.cs:121-125`): Tier 1 species without a resource ceiling grow without bound, which is biologically meaningless and triggered integer-overflow in birth accumulators around day ~50 of any cap-off scenario. **Per-species event counters added in v12** (`LastBirthsBySpecies`, `LastTempDeathsBySpecies`, etc.) keyed by `FullName`, alongside accumulator accessors (`GetBirthAccum(fullName)` etc.). Rationale: research analysts need to track each Custom species individually, not have them all roll up into a single Tier1Custom/Tier2Custom bucket. Crash detection: `totalPop == 0`. See [`docs/simulation-spec.md`](./docs/simulation-spec.md) for the authoritative spec.
- `TemperatureCalculator` — Standalone temperature model (not the game's `Temperature` class)
- `SimSpecies` — Species data for simulation (different from `CharacterManager`)
- `SimulationConfig` (ScriptableObject) — All parameters: days per scenario, number of scenarios, temperature settings, carrying capacity, species list
- `SpeciesDatabase` / `RunSpeciesList` (ScriptableObjects) — Species data storage. `RunSpeciesList` is the preferred runtime format
- `ScenarioResult` / `AggregateResults` — Per-scenario and cross-scenario statistics
- `ConfigExporter` — Static helper with `ToCsv()`/`ToJson()` and `BuildConfigCsv()`/`BuildConfigJson()`. Lives at the bottom of `Assets/scripts/Simulation/DataStructure/ScenarioResult.cs` (line ~961, same file — not its own .cs). Config download button uses CSV format

**Species system:** Both game and simulation use Tier 1 (prey, e.g. Hexapod) and Tier 2 (predator, e.g. Sheplik). The `SpeciesVariant`/`ThermalVariant` enums remain `{Common, Tropical, Arctic, Custom}` internally, but as of Batch 1A–1B the **display variant is free-text**: a `variantLabel` string is carried through `SpeciesData → SimSpecies → FullName`/output, so bulk rows can name a variant anything (e.g. `v2_M2`). The canonical default family is now **Cold/Warm/Hot** (Hexapod reparameterized to a constant-shape, Topt-shifted family: Cold 20 °C / Warm 22 °C / Hot 24 °C); the legacy names **Arctic/Common/Tropical are still accepted as aliases** (Cold=Arctic, Warm=Common, Hot=Tropical) via `SpeciesData.ResolveVariantEnum`, so existing batches stay byte-identical. The game also has Tier 3. Thermal performance uses the same Arrhenius formula in both systems but with separate implementations.

**Terminology (see also Aggregation Levels below):**

| Term | Definition | Code class | Output |
|------|-----------|------------|--------|
| **Scenario** | One simulation execution with one random seed, running for DaysPerScenario days. The atomic unit. | `SimulationRunner`, `ScenarioResult` | `scenario_N.csv` |
| **Run** | One configuration (species + environment params) that generates N scenarios with different seeds. In standard mode: the `SimulationConfig`. In bulk mode: one CSV row. | `SimulationController`, `AggregateResults` | `aggregate.csv` per run |
| **Bulk** | A collection of runs uploaded as a single CSV file. Each row = one run. | `BulkSimulationController`, `BulkBatchConfig` | ZIP with per-run folders + `bulk_summary.csv` |

Note: In code, "batch" and "run" are used interchangeably for the same concept (one CSV row / one config). `BulkBatchConfig` = one run's config. `BulkRunSummary` = one run's summary in the bulk context.

**Aggregation levels:**

| Level | What it aggregates | Output file | Valid stats |
|-------|-------------------|-------------|-------------|
| Scenario Result | One sim (daily data) | `scenario_N.csv` | Raw per-day populations, deaths, births. **v12: per-species columns appended** (Pop, Cond, ThermalPerf, FedRate, Births, Eaten, all death types, BirthRate, ReproScale, accumulators) named `{SanitizedFullName}_{Field}`. |
| Run Aggregate | Scenarios within 1 run | `aggregate.csv` | Avg/SurvivedAvg/Min/Max per species (real population values), extinction rate per species. **v12: 3 new sections** — `PER-SPECIES FINAL YEAR METRICS` (last 365 days: mean Condition, per-capita birth rate, population CV, mean pop), `PER-SPECIES FULL-RUN METRICS` (same metrics over entire run), `PER-SPECIES STABILITY METRICS` (min/max/final pop, extinction/crash rates and mean days). |
| Bulk Summary | Runs within 1 bulk upload | `bulk_summary.csv` | GrandMean + SurvivedMean per species, per-run extinction rate. Min/Max are NOT valid here (they would be min/max of averages, not real population values). **v12: 3 new sections** — `PER-RUN PER-SPECIES FINAL YEAR` (run × species detail), `CROSS-RUN PER-SPECIES FINAL YEAR` (mean of per-run means), `CROSS-RUN STABILITY`. **v12.3**: cross-run Condition/BirthRate/PopCv now averaged over surviving runs only (non-survivors contribute misleading zeros or sentinel 1.0 values). |

**CSV output formats:**
- **Scenario CSV** — Each scenario file has `#config:key,value` lines (simulation params), then a `#species:` table (header + data rows with `#species:` prefix), then the daily step data. All `#` lines are ignored by R's `read.csv()`. Includes `Tier1Custom`/`Tier2Custom` columns for custom species. **v12: 17 per-species columns appended** to each daily row, ordered by `(Tier asc, FullName asc)`. Tier-rollup invariant: per-species values sum to tier-level values.
- **Aggregate CSV** — Section headers use `=== TITLE ===`, metadata lines use `# Key,Value`, then summary stats, per-species stats (with extinction counts), and a per-scenario table. The `PER-SPECIES POPULATION STATS` section lists every species individually (including each custom species by name) with Avg/SurvivedAvg/Min/Max/Extinct/Survived/ExtinctionRate. `SurvivedAvg` = average population only across scenarios where the species survived (final pop > 0). **v12 adds three new sections**: `PER-SPECIES FINAL YEAR METRICS`, `PER-SPECIES FULL-RUN METRICS`, `PER-SPECIES STABILITY METRICS`.
- **Bulk Summary CSV** — At ZIP root. Has a per-run results table (one row per CSV row, with per-species avg populations and crash rate) and a per-species aggregate with GrandMean + SurvivedMean + extinction rate across runs. `SurvivedMean` = average of run-level survived averages across runs where the species had any presence. No Min/Max at this level. **v12 adds three new sections**: `PER-RUN PER-SPECIES FINAL YEAR`, `CROSS-RUN PER-SPECIES FINAL YEAR`, `CROSS-RUN STABILITY`. **v12.3 fix** (`BulkSimulationController.cs:561-573`): cross-run aggregation of Condition/BirthRate/PopCv now averages across surviving runs only — non-surviving runs would otherwise contribute either real 0s or sentinel values (Condition stuck at its initial 1.0 because biology never updated it), producing misleading aggregates. Population grand-means still include zero-pop runs (meaningful for "typical population including failures").
- **Config CSV** — Same `=== SECTION ===` style with `Parameter,Value` rows and a species table. Downloaded via "Download Config" button (CSV, not JSON). Also included in ZIP downloads as `config.csv`.
- Species columns must stay consistent across all three formats: Name, Variant (separate columns), all biology params, temps in both Kelvin and Celsius (`:F2`).
- **v12 column-name sanitization**: per-species column prefixes use `SanitizeColumnName(FullName)` — ASCII-only, non-alphanumeric replaced with `_`, leading digit gets `_` prefix, duplicate sanitized names get `_2`, `_3` suffix.

## Conventions

- C# scripts use a mix of camelCase and PascalCase for methods (legacy code). Public fields are common (Unity serialization pattern).
- `#if UNITY_EDITOR` guards are used for prefab instantiation (`PrefabUtility.InstantiatePrefab` in editor, `GameObject.Instantiate` in builds).
- Species indexing in the game: groups of 9 per tier (3 organisms × 3 variants). The `currentOrganismID` formula is `tierBase + icon + (variant * 3)`.
- Temperature values are in Celsius for display but converted to Kelvin (+273) for thermal curve calculations.
- ScriptableObjects are created via `[CreateAssetMenu]` under the `TinySea/` menu.

## Editor Tools

- `UI > Anchor Around Object` — Custom menu item (`Assets/Editor/UIGUIMenu.cs`) to set RectTransform anchors to match current position.
- `SimulationConfig` inspector provides `[ContextMenu]` actions: "Run Simulation", "Log Config", "Export Config JSON", "Open Output Folder".

## WebGL Downloads

File downloads in WebGL use a jslib plugin (`Assets/plugins/WebGL/TinySea.jslib`) with `Blob` + anchor click. ZIP downloads use JSZip loaded from CDN. C# wrappers: `WebGLDownload` (single file), `WebGLZipDownload` (ZIP). In Editor/standalone builds, files save to `Application.persistentDataPath` and optionally open with the OS default app.
