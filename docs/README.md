# TinySea Headless Simulation — Documentation

Source-of-truth docs for the headless ecosystem simulator used for research runs. Everything here is derived directly from the C# source in `Assets/scripts/Simulation/` and is intended to stay in sync with that source — if a doc and the source disagree, trust the source and fix the doc.

This document set covers only the headless simulation. The interactive Unity game (scenes `main_menu`, `mainscene`, `tutorial`) uses a separate biology path (`CharacterManager`, `PlayerManager`, `ThermalCurve`) and is not documented here.

## Document set

| File | Contents |
|------|---------|
| [simulation-spec.md](./simulation-spec.md) | Authoritative specification. Biology sequence (10 steps), Arrhenius TPC, Condition system, Pmax flow, scenario loop, temperature model, accumulators, design invariants. |
| [simulation-variables.xlsx](./simulation-variables.xlsx) | Spreadsheet reference for every parameter, field, and constant. Seven sheets: Overview, Config (single-run), Bulk CSV Globals, Per-Species Params, Runtime State, Constants, Fallback Defaults. |
| [csv-formats.md](./csv-formats.md) | CSV schemas for input (bulk upload) and output (per-scenario, aggregate, bulk summary, config export). Column-by-column with types and meaning. |
| [diagrams/](./diagrams/) | Mermaid diagrams — one flow per file. See [diagrams/README.md](./diagrams/README.md) for the index. |

## Diagrams — individual files

Each diagram is its own markdown so edits are focused and diffs are readable.

- [diagrams/bulk-hierarchy.md](./diagrams/bulk-hierarchy.md) — Bulk CSV → Runs → Scenarios → outputs
- [diagrams/scenario-flow.md](./diagrams/scenario-flow.md) — Per-scenario day loop
- [diagrams/temperature-model.md](./diagrams/temperature-model.md) — Daily temperature components
- [diagrams/biology-overview.md](./diagrams/biology-overview.md) — 10-step biology sequence
- [diagrams/biology-performance-phase.md](./diagrams/biology-performance-phase.md) — Steps 1–5 (Arrhenius, feeding, Condition)
- [diagrams/biology-death-phase.md](./diagrams/biology-death-phase.md) — Steps 6–7 (thermal + condition death)
- [diagrams/biology-life-phase.md](./diagrams/biology-life-phase.md) — Steps 8–9 (reproduction + natural death)
- [diagrams/pmax-flow.md](./diagrams/pmax-flow.md) — Where Pmax enters the pipeline (v9)
- [diagrams/csv-output-shape.md](./diagrams/csv-output-shape.md) — CSV section layout for scenario/aggregate/bulk-summary
- [diagrams/accumulator-pattern.md](./diagrams/accumulator-pattern.md) — Fractional-event accumulators

## Source-of-truth files

If you are changing behaviour, edit the C# first, then update the doc. All paths relative to `tinysea/`:

| Area | File |
|------|------|
| Biology sequence | `Assets/scripts/Simulation/EcosystemSimulator.cs` |
| Per-species fields & Arrhenius formula | `Assets/scripts/Simulation/SimSpecies.cs` |
| Temperature model | `Assets/scripts/Simulation/TemperatureCalculator.cs` |
| Scenario orchestration & CSV output | `Assets/scripts/Simulation/SimulationRunner.cs` |
| Per-scenario result / stats | `Assets/scripts/Simulation/DataStructure/ScenarioResult.cs` |
| Scenario config schema (Inspector) | `Assets/scripts/Simulation/DataStructure/SimulationConfig.cs` |
| Species list runtime container | `Assets/scripts/Simulation/DataStructure/RunSpeciesList.cs` |
| Bulk CSV row schema (1 row = 1 run) | `Assets/scripts/Simulation/BulkBatchConfig.cs` |
| Bulk CSV parser | `Assets/scripts/Simulation/CsvBatchParser.cs` |
| Bulk orchestration | `Assets/scripts/Simulation/BulkSimulationController.cs` |
| Single-run Unity entry point | `Assets/scripts/Simulation/SimulationController.cs` |

## Conventions

- All temperatures are in **°C** unless a field name ends in `K` (Kelvin). The Arrhenius formula evaluates in Kelvin internally.
- `Tier 1` = prey (CSV tier column uses 0, code uses 1 — `CsvBatchParser` converts).
- `Tier 2` = predator (CSV tier column uses 1, code uses 2).
- "Pmax" and "peak height" are the same parameter: the `Pmax` field on `SimSpecies`.
- Version tags in code comments (`v6`, `v7`, `v8`, `v9`) refer to simulation-logic revisions, not Unity versions.
- `FullName = "{Name}_{Variant}"` is the key used by all accumulators and per-species CSV columns.

## How to regenerate these docs

All files in this set are handwritten from source. There is no generator.

- For markdown edits: read the affected source file(s), then edit the doc in place. Keep heading levels consistent — readers search across files.
- For [simulation-variables.xlsx](./simulation-variables.xlsx): open in Excel or LibreOffice and edit rows directly. If adding a new field, add a row to the appropriate sheet with Type, Default, Range/Validation, Units, and Meaning columns filled in.

A separate PDF generator (`gen_pdf.py`) exists for producing printable Simulation Guide PDFs from independent hardcoded content; it does **not** read these files.
