# Simulation Diagrams

One Mermaid diagram per file, each focused on a single flow. Render inline in GitHub, VS Code, Obsidian, or any Mermaid-capable viewer.

All diagrams are derived from the C# source in `Assets/scripts/Simulation/`. If the code changes, update the diagram — do not rely on external references.

## Diagram index

| File | Covers | Primary source |
|------|--------|----------------|
| [bulk-hierarchy.md](./bulk-hierarchy.md) | Bulk CSV → Runs → Scenarios → output files. | `BulkSimulationController.cs` |
| [scenario-flow.md](./scenario-flow.md) | The per-scenario day loop, biology dispatch, crash detection, CSV emission. | `SimulationRunner.cs` |
| [temperature-model.md](./temperature-model.md) | Daily temperature = base + seasonal + trend + interannual + daily noise, clamped. | `TemperatureCalculator.cs` |
| [biology-overview.md](./biology-overview.md) | The 10-step biology sequence invoked each day. | `EcosystemSimulator.ProcessBiologyStep` |
| [biology-performance-phase.md](./biology-performance-phase.md) | Steps 1–5 in detail: Arrhenius, feeding, Condition update. | `EcosystemSimulator.cs`, `SimSpecies.cs` |
| [biology-death-phase.md](./biology-death-phase.md) | Steps 6–7: thermal death (instant) and condition death (graduated + survivor boost). | `EcosystemSimulator.ApplyThermalDeath`, `ApplyConditionDeath` |
| [biology-life-phase.md](./biology-life-phase.md) | Steps 8–9: reproduction (reproScale × Pmax, accumulator, penalties) and natural death. | `EcosystemSimulator.ApplyReproduction`, `ApplyNaturalDeathWithAccumulator` |
| [pmax-flow.md](./pmax-flow.md) | Every place Pmax enters the pipeline, post-v9. | `EcosystemSimulator.cs` |
| [csv-output-shape.md](./csv-output-shape.md) | Per-scenario, aggregate, and bulk-summary CSV section layout. | `SimulationRunner.ToCsv`, `ScenarioResult.ToAggregateCsv`, `BulkSimulationController.GenerateBulkSummary` |
| [accumulator-pattern.md](./accumulator-pattern.md) | Fractional-event accumulator used by births, condition deaths, natural deaths, predation. | `EcosystemSimulator.cs` |

## Conventions

- **Tier 1** = prey; **Tier 2** = predator.
- **Species order** is the order species appear in `RunSpeciesList`. Many biology steps iterate in this order.
- **Pmax** is clamped to `max(Pmax, 1e-4)` before any divisions in the Condition update (`pmaxSafe`) to guard against divide-by-zero.
- **BiologyStep** controls how many simulated days elapse per biology evaluation (default 1). All rate formulas multiply by `BiologyStep`.
