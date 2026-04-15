# TinySea Simulation Flow Diagrams

Mermaid charts documenting the simulation system. Each `.md` file contains raw Mermaid code — copy the contents and paste into [mermaid.live](https://mermaid.live) to view.

## Hierarchy

```
Bulk Simulation (multiple runs from one CSV file)
  └── Scenario (one simulation with one random seed)
        ├── Temperature Model (how daily temperature is generated)
        └── Biology Step Overview (10 steps in 3 phases)
              ├── Performance Phase (Steps 1-5)
              ├── Death Phase (Steps 6-7)
              └── Life Phase (Steps 8-9)
```

## Charts

| File | What it covers |
|------|----------------|
| **Top Level** | |
| `scenario-flow.md` | One simulation: initialize, day loop, crash check, results |
| `bulk-flow.md` | CSV upload, multiple runs, statistics, ZIP download |
| **Biology** | |
| `biology-step-flow.md` | 10-step overview: 3 phases left to right |
| `biology-performance-phase.md` | Steps 1-5: thermal perf, Holling Type II feeding, condition update |
| `biology-death-phase.md` | Steps 6-7: thermal death, graduated condition death |
| `biology-life-phase.md` | Steps 8-9: piecewise reproduction, natural death |
| **Environment** | |
| `temperature-model-flow.md` | 5-component temperature: base, seasonal, trend, interannual, daily |

## Color Legend

| Color | Meaning |
|-------|---------|
| Green | Start / End / Reproduction |
| Dark Blue | Core computation |
| Yellow | Fitness boost / Dilution |
| Teal | Infrastructure / Skip |
| Red | Death |
| Purple | Accumulators |
| Steel Blue | Decision points |
| Orange | Natural death / Penalties |
