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
    Crashed -- yes --> MarkCrash["HasCrashed = true<br/>CrashDay = displayDay<br/>CrashTier = GetCrashedTier()<br/>break loop"]
    Crashed -- no --> NextDay["dayIndex += 1"]
    NextDay --> DayLoop
    DayLoop -- no --> Emit
    MarkCrash --> Emit
    Emit["ToCsvInternal:<br/>#config: header (19 lines)<br/>#species: table<br/>daily StepRecord rows<br/>#summary: stats (3 header rows + 4 stat rows, v12.2)<br/>#extinction: per-species timing (v12.2)"]
    Emit --> Return["ToScenarioResult →<br/>emits per-day CSV + stats to controller"]
    Return --> End([End scenario])
```

## Notes

- **BiologyStep** defaults to 1. Values `2–5` skip biology on non-matching days but still advance temperature and record a row (with death/birth/accumulator fields = 0 on skipped days).
- **Day numbering**: `dayIndex` is 0-based internally; `StepRecord.Day` is 1-based (displayDay). `Year` = `(dayIndex / 365) + 1`.
- **Crash detection** (`EcosystemSimulator.cs:1319-1323`): `Ecosystem.HasCrashed()` is exact-equality `totalPop == 0` (sum of Tier 1 + Tier 2). No `MIN_ALIVE_POP` floor, no init-state guard. Tier attribution lives separately in `GetCrashedTier()` (`EcosystemSimulator.cs:1325-1331`), which uses `_tier1WasPopulated` / `_tier2WasPopulated` to decide whether to return tier 0 (all-dead), 1, 2, or `-1` (no relevant collapse). This matches simulation-spec §5.
- **Early break**: scenarios that crash stop recording further days; subsequent rows of the CSV simply don't exist. Downstream tooling should handle variable-length scenarios.
- **CSV emission** is deferred — records are kept in memory during the run and serialised at the end (via `ToCsvInternal`). Then the result is streamed out (see bulk-hierarchy).
