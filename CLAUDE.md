# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TinySea is a Unity 6 (6000.3.0f1) educational game/simulation built by Northeastern University GhostLab. Players manage a marine ecosystem by buying/selling organisms across a 3-tier food chain while temperature fluctuates seasonally and with climate change. The project has two modes: an interactive game and a headless ecosystem simulation for research.

## Build & Run

This is a Unity project — open in Unity Editor 6000.3.0f1. There is no CLI build pipeline. The primary build target is WebGL (see `Builds/` folder). The project uses the standard Unity C# compilation (Assembly-CSharp).

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
- `EcosystemSimulator` — 7-step biology sequence: thermal performance → feeding/predation → final performance → thermal death → reproduction → natural death → population rounding. Uses accumulators (birth, predation, natural death) for fractional population tracking
- `TemperatureCalculator` — Standalone temperature model (not the game's `Temperature` class)
- `SimSpecies` — Species data for simulation (different from `CharacterManager`)
- `SimulationConfig` (ScriptableObject) — All parameters: days per scenario, number of scenarios, temperature settings, carrying capacity, species list
- `SpeciesDatabase` / `RunSpeciesList` (ScriptableObjects) — Species data storage. `RunSpeciesList` is the preferred runtime format
- `ScenarioResult` / `AggregateResults` — Per-scenario and cross-scenario statistics
- `ConfigExporter` — Static helper with `ToCsv()`/`ToJson()` and `BuildConfigCsv()`/`BuildConfigJson()`. Config download button uses CSV format

**Species system:** Both game and simulation use Tier 1 (prey, e.g. Hexapod) and Tier 2 (predator, e.g. Sheplik), each with Arctic/Common/Tropical thermal variants. The game also has Tier 3. Thermal performance uses the same Arrhenius formula in both systems but with separate implementations.

**CSV output formats:**
- **Scenario CSV** — Each scenario file has `#config:key,value` lines (simulation params), then a `#species:` table (header + data rows with `#species:` prefix), then the daily step data. All `#` lines are ignored by R's `read.csv()`.
- **Aggregate CSV** — Section headers use `=== TITLE ===`, metadata lines use `# Key,Value`, then summary stats and a per-scenario table.
- **Config CSV** — Same `=== SECTION ===` style with `Parameter,Value` rows and a species table. Downloaded via "Download Config" button (CSV, not JSON). Also included in ZIP downloads as `config.csv`.
- Species columns must stay consistent across all three formats: Name, Variant (separate columns), all biology params, temps in both Kelvin and Celsius (`:F2`).

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
