# Scenario flow

Source: `SimulationRunner.Run`, `SimulationRunner.RecordStep`, `EcosystemSimulator.HasCrashed`.

```mermaid
flowchart TD
    Start([Start scenario]) --> Init["Initialize species<br/>from RunSpeciesList<br/>(fallback: InitializeDefaultSpecies)"]
    Init --> TempInit["Seed TemperatureCalculator<br/>seed = BaseSeed + scenarioIndex"]
    TempInit --> DayLoop{"dayIndex &lt; TotalDays?"}
    DayLoop -- yes --> Temp["temp = TempCalc.GetTemperature(dayIndex)"]
    Temp --> RunBio{"dayIndex == 0 OR<br/>(dayIndex+1) % BiologyStep == 0?"}
    RunBio -- yes --> Biology["EcosystemSimulator.ProcessBiologyStep(temp)<br/>(10 sub-steps; see biology-overview)"]
    RunBio -- no --> Record
    Biology --> Record["RecordStep:<br/>temp, populations (long), condition,<br/>births, deaths, accumulators,<br/>reproScale, hunting stats"]
    Record --> Crashed{"biology ran AND<br/>Ecosystem.HasCrashed()?"}
    Crashed -- yes --> MarkCrash["HasCrashed = true<br/>set CrashDay, CrashTier<br/>break loop"]
    Crashed -- no --> NextDay["dayIndex += 1"]
    NextDay --> DayLoop
    DayLoop -- no --> Emit
    MarkCrash --> Emit
    Emit["ToCsvInternal:<br/>#config: header<br/>#species: table<br/>daily StepRecord rows<br/>#summary: stats<br/>#extinction: per-variant timing"]
    Emit --> Return["ToScenarioResult →<br/>emits per-day CSV + stats to controller"]
    Return --> End([End scenario])
```

## Notes

- **BiologyStep** defaults to 1. Values `2–5` skip biology on non-matching days but still advance temperature and record a row (with death/birth/accumulator fields = 0 on skipped days).
- **Day numbering**: `dayIndex` is 0-based internally; `StepRecord.Day` is 1-based (displayDay). `Year` = `(dayIndex / 365) + 1`.
- **Crash detection**: `Ecosystem.HasCrashed()` returns true when total population is below `MIN_ALIVE_POP` and at least one tier was populated at init (so we don't report a crash for scenarios that intentionally start empty).
- **Early break**: scenarios that crash stop recording further days; subsequent rows of the CSV simply don't exist. Downstream tooling should handle variable-length scenarios.
- **CSV emission** is deferred — records are kept in memory during the run and serialised at the end (via `ToCsvInternal`). Then the result is streamed out (see bulk-hierarchy).
