# Simulation Diagrams

One Mermaid diagram per file, each focused on a single flow. Render inline in GitHub, VS Code, Obsidian, or any Mermaid-capable viewer.

All diagrams are derived from the C# source in `Assets/scripts/Simulation/`. If the code changes, update the diagram — do not rely on external references.

## Combined PDF (v12)

A single combined PDF of all 10 diagrams (rendered via Mermaid CLI 11.14 at 2× scale, identical visual style to mermaid.live's default theme) is at:

- **[`rendered/v12/TinySea-Simulation-Diagrams.pdf`](./rendered/v12/TinySea-Simulation-Diagrams.pdf)** — 13 pages: title + TOC + 10 diagram pages + sources page.

To regenerate after editing any `.md`:

```bash
# From repo root, render each .md to a high-res PNG (one-time, also after edits):
for f in tinysea/docs/diagrams/*.md; do
  name=$(basename "$f" .md)
  [ "$name" = "README.md" ] && continue
  node_modules/.bin/mmdc.cmd -i "$f" -o "tinysea/docs/diagrams/rendered/v12/${name}.png" \
    -w 1800 -s 2 --backgroundColor white -t default --quiet
done
# Then bundle into the combined PDF:
python tinysea/docs/build_diagrams_pdf.py
```

The build script lives at [`tinysea/docs/build_diagrams_pdf.py`](../build_diagrams_pdf.py) and reads the rendered PNGs from `rendered/v12/`.

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
- **Species order** in CSV columns is `(Tier asc, FullName asc)` (`SimulationRunner.cs:706-708`); inside the biology pipeline iteration follows `Species` insertion order from `RunSpeciesList`. The biology steps are order-independent in v10+.
- **Pmax** is clamped to `max(Pmax, 1e-4)` before any divisions in the Condition update (`pmaxSafe`) to guard against divide-by-zero (`EcosystemSimulator.cs:895`).
- **BiologyStep** controls how many simulated days elapse per biology evaluation (default 1). When `BiologyStep > 1`, biology runs only on `dayIndex == 0` or when `(dayIndex + 1) % BiologyStep == 0` — temperature still advances every day, and a row is recorded every day; on skipped days, all event/birth/death/accumulator counters are 0. All rate formulas inside biology multiply by `BiologyStep` so multi-day cycles produce equivalent expected events.
- **Model version**: as of v12.2, the value emitted in `#config:model_version` is `v12-per-species-tracking`. The CSV-section split (PER-RUN RESULTS - TIER LEVEL / PER SPECIES, three-header-row INDIVIDUAL SCENARIOS / SUMMARY STATISTICS) is a v12.2 sub-revision; the simulator version string is unchanged.
