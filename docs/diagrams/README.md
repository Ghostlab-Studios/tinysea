# Simulation Diagrams

Mermaid diagrams for the TinySea headless ecosystem simulation. Each file holds one fenced `mermaid` block with a one-paragraph caption above it. The diagrams summarize flows whose authoritative detail lives in the sibling documents under `tinysea/docs/` and in the C# source under `tinysea/Assets/scripts/Simulation/`. The current shipping configuration is Tier 1 (prey) only; Tier 2 (predator) paths are marked as dormant legacy where they appear.

| Diagram | What it shows | Companion doc |
|---------|---------------|---------------|
| [biology-day-sequence.md](biology-day-sequence.md) | The ten ordered steps `EcosystemSimulator.ProcessBiologyStep` runs per biology day, with the snapshot/reset and end-of-day rollup. | `biology-and-formulas.md`, `simulation-spec.md` |
| [temperature-model.md](temperature-model.md) | How `TemperatureCalculator.GetTemperature` assembles one daily Celsius value: the timeseries override path and the five-component parametric sum with final clamp. | `temperature-model.md` |
| [run-scenario-bulk.md](run-scenario-bulk.md) | The Bulk to Run to Scenario to Day nesting, seeding rule, and the output artifact emitted at each level. | `run-scenario-batch.md`, `bulk-system.md` |
| [csv-output-shape.md](csv-output-shape.md) | The block layout of `scenario_N.csv`, `aggregate.csv`, and `bulk_summary.csv`, including the per-species column appendix and the tier-rollup invariant. | `csv-output-formats.md` |
