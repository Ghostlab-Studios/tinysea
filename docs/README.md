# TinySea Headless Simulation: Developer Documentation

This folder documents the TinySea headless ecosystem simulation, the non-visual research model that lives under `tinysea/Assets/scripts/Simulation/`. It is separate from the interactive Unity game under `tinysea/Assets/scripts/`. The audience is software developers who have never seen this codebase. The documents together hold enough detail to reimplement the simulation behavior.

Every non-trivial statement in these documents cites a source file and line range, for example `(EcosystemSimulator.cs:543-700)`. A reader can jump straight to the cited code to confirm any claim.

## Scope: Tier 1 now, Tier 2 is dormant legacy

The current shipping configuration runs Tier 1 (prey) only. The Tier-2 (predator) gate defaults to off: `EcosystemSimulator.Tier2Enabled` is `false` (`EcosystemSimulator.cs:245`) and `SimulationConfig.Tier2Enabled` is `false` (`SimulationConfig.cs:130`). When the gate is off, Tier-2 species are dropped at load (`EcosystemSimulator.cs:336`), Tier-2 CSV columns are suppressed (`SimulationRunner.cs:216-232`, `SimulationRunner.cs:270-286`), and the bulk CSV parser rejects any row with `tier != 0` (`CsvBatchParser.cs:318-319`). The Tier-2 predation code, including the Holling Type II functional response, remains in the engine from the original two-tier design. The documents describe Tier 1 completely. Where Tier-2 logic appears, it is marked as secondary legacy and the documents state plainly that current runs consider Tier 1 only.

## Source of truth

The C# source under `tinysea/Assets/scripts/Simulation/` and the ScriptableObject `.asset` files under `tinysea/Assets/` are the only source of truth. These documents are derived from that code. When behavior and a document disagree, the code is correct. To change documented behavior, edit the C# first, then update the affected document and its citations to match. Do not edit a document to describe behavior the code does not have.

## Reading order

Read top to bottom for a full pass. Each entry is one document with a one line description.

1. [architecture-overview.md](architecture-overview.md): What the simulation is, the two driver chains from scene entry to CSV output, and how the pieces fit together. Start here.
2. [simulation-spec.md](simulation-spec.md): The authoritative specification of the daily biology sequence, state variables, and design invariants.
3. [biology-and-formulas.md](biology-and-formulas.md): The ten biology steps in full: thermal performance, feeding, condition, the death pathways, and reproduction, with every formula and constant.
4. [temperature-model.md](temperature-model.md): The standalone temperature model: the five-component parametric sum, the timeseries override, and the clamp.
5. [run-scenario-batch.md](run-scenario-batch.md): The Run, Scenario, and day-loop nesting: seeding, the biology-step cadence, crash detection, and parallel vs sequential execution.
6. [bulk-system.md](bulk-system.md): The bulk batch system: uploading one CSV of many runs, per-row parsing and validation, and the ZIP of per-run outputs plus the bulk summary.
7. [configuration-reference.md](configuration-reference.md): Every configuration parameter and ScriptableObject field, with defaults, ranges, and validation rules exactly as the code sets them.
8. [csv-output-formats.md](csv-output-formats.md): The exact column layout of the scenario, aggregate, bulk-summary, and config CSV files, including the dynamic tier-variant rollup and the per-species columns.
9. [data-structures.md](data-structures.md): The internal data types: `SimSpecies`, `SpeciesData`, `StepRecord`, `ScenarioResult`, `AggregateResults`, and the per-species metric structs.
10. [ui-and-io.md](ui-and-io.md): The simulation UI and input/output: the results screen, the species editor, file upload, downloads (single file and ZIP), and the server upload path.
11. [diagrams/](diagrams/): Mermaid diagrams for the biology day sequence, temperature model, run/scenario/bulk nesting, and CSV output shape. See [diagrams/README.md](diagrams/README.md).

## Subject-to-source map

Each subject area maps to its primary C# file under `Assets/scripts/Simulation/`. These are the files to read or edit first for each topic. The code is the source of truth; the listed document describes it.

| Subject area | Primary C# file | Document |
|--------------|-----------------|----------|
| Daily biology sequence (10 steps), feeding, condition, deaths, reproduction | `EcosystemSimulator.cs` | biology-and-formulas.md, simulation-spec.md |
| Per-species state, thermal performance (Arrhenius), default-species factories | `SimSpecies.cs` | biology-and-formulas.md, data-structures.md |
| Scenario day loop, per-day recording, scenario CSV writer, per-species scenario metrics | `SimulationRunner.cs` | run-scenario-batch.md, csv-output-formats.md |
| Temperature model (parametric components, timeseries override, clamp) | `TemperatureCalculator.cs` | temperature-model.md |
| Standard-run entry point, scenario orchestration, parameter wiring onto the runner | `SimulationController.cs` | run-scenario-batch.md, architecture-overview.md |
| Bulk batch orchestration, ZIP packaging, bulk summary writer | `BulkSimulationController.cs` | bulk-system.md |
| Bulk CSV parsing, validation, template generation | `CsvBatchParser.cs` | bulk-system.md, csv-output-formats.md |
| One parsed bulk row (run config + species) | `BulkBatchConfig.cs` | bulk-system.md, configuration-reference.md |
| Standard-run configuration ScriptableObject (all parameters, validation) | `DataStructure/SimulationConfig.cs` | configuration-reference.md |
| Species record (serialized fields, variant resolution, match keys) | `DataStructure/SpeciesDatabase.cs` | configuration-reference.md, data-structures.md |
| Runtime species list ScriptableObject | `DataStructure/RunSpeciesList.cs` | configuration-reference.md, data-structures.md |
| Per-scenario and cross-scenario results, aggregate CSV writer, config CSV/JSON exporter | `DataStructure/ScenarioResult.cs` | csv-output-formats.md, data-structures.md |
| Daily and per-species step records, dynamic rollup column schema | `SimulationRunner.cs` (`StepRecord`, `PerSpeciesStepData`) | data-structures.md, csv-output-formats.md |
| Cooperative pause/stop signal for a run | `RunControl.cs` | run-scenario-batch.md |
| Results screen UI, progress, completion state | `UI/ResultsScreenUI.cs` | ui-and-io.md |
| Simulation input UI (parameters and run button) | `UI/SimulationInputUI.cs` | ui-and-io.md |
| Species editor UI and thermal curve editing | `UI/EditSpeciesUI.cs`, `UI/ThermalGraphEditor.cs` | ui-and-io.md |
| CSV file upload handling (drag-drop and picker) | `CsvUploadHandler.cs` | ui-and-io.md, bulk-system.md |
| Single-file download (WebGL jslib and editor fallback) | `WebGLDownload.cs` | ui-and-io.md |
| ZIP download (progressive, WebGL and editor fallback) | `WebGLZipDownload.cs` | ui-and-io.md |
| Server upload of result files (S3, WebGL non-editor) | `ServerUpload.cs` | ui-and-io.md |
| Output directory resolution per platform | `SavePaths.cs` | ui-and-io.md |
