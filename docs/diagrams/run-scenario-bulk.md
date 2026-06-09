# Diagram: Bulk to Run to Scenario to Day Hierarchy

This diagram shows the four nested execution levels of the headless simulation and the artifact each level emits. A Bulk is one uploaded CSV where each data row is one Run; a Run is one configuration that spawns N Scenarios with distinct seeds; a Scenario is one seeded execution of `SimulationRunner.Run()` that advances a day loop for `TotalDays` days; each biology day runs the ten-step `ProcessBiologyStep`, and each recorded day produces one `StepRecord` carrying a `PerSpeciesStepData` sub-row per species keyed by `FullName`. The per-scenario seed is `RandomSeed < 0 ? -1 : RandomSeed + i` where `i` is the zero-based scenario index (`SimulationController.cs:209`, `BulkSimulationController.cs:259`); a non-negative base seed makes the run reproducible, while `-1` selects clock-seeded non-reproducible mode. Standard runs come from a `SimulationConfig` ScriptableObject driven by `SimulationController`; bulk runs come from `BulkBatchConfig` rows driven by `BulkSimulationController`, which builds a throwaway in-memory `RunSpeciesList` per batch and never mutates the project asset. The current simulator is Tier 1 only; the bulk parser hard-rejects any Tier 2 row (`CsvBatchParser.cs:318-319`). The right column lists the output artifact at each level. For field-by-field parameter copies, threading, and CSV column layouts see `run-scenario-batch.md`, `bulk-system.md`, and `csv-output-formats.md`.

```mermaid
flowchart TD
    subgraph BULK["BULK  -  one uploaded CSV, each row = one Run  (BulkSimulationController.cs:19)"]
        direction TB
        UP["CsvUploadHandler.OnCsvFileReceived (cs:120)<br/>CsvBatchParser.TryParse -> List&lt;BulkBatchConfig&gt; (collect-all errors)"]
        UP --> BLOOP["RunAllBatches: for each batch b (cs:91-399)<br/>ConvertSpecies -> temp in-memory RunSpeciesList (cs:723)"]
    end

    BLOOP --> RUN

    subgraph RUN["RUN / BATCH  -  one config -> N scenarios, distinct seeds"]
        direction TB
        RSRC["Std: SimulationController + SimulationConfig (cs:111)<br/>Bulk: one BulkBatchConfig row (BulkBatchConfig.cs:41)"]
        RSRC --> SLOOP["for i in 0..NumberOfScenarios-1<br/>seed_i = RandomSeed &lt; 0 ? -1 : RandomSeed + i<br/>(Editor/standalone: Task.Run chunks of ProcessorCount-1; WebGL: sequential)"]
        SLOOP --> AGG["after all scenarios:<br/>AggregateResults.CalculateAggregates (ScenarioResult.cs:288)"]
    end

    SLOOP --> SCEN

    subgraph SCEN["SCENARIO  -  one seed, runs TotalDays days  (SimulationRunner.cs:347)"]
        direction TB
        CTOR["new SimulationRunner(seed)<br/>-> new TemperatureCalculator(seed) + new EcosystemSimulator(seed) (cs:382-387)"]
        CTOR --> INIT["Run(): load species (InitializeFromRunSpeciesList / default) (cs:405-411)"]
        INIT --> DLOOP["for dayIndex in 0..TotalDays-1 (cs:415)<br/>temp = TempCalc.GetTemperature(dayIndex)<br/>runBiology = (displayDay==1) || (displayDay % BiologyStep == 0)<br/>break early if HasCrashed() after a biology day"]
        DLOOP --> TOSCN["ToScenarioResult -> ScenarioResult (+ CsvData) (cs:982)"]
    end

    DLOOP --> DAY

    subgraph DAY["DAY"]
        direction TB
        BIO["if runBiology: ProcessBiologyStep(temp)  // 10 steps (EcosystemSimulator.cs:543)"]
        BIO --> REC["RecordStep -> one StepRecord per day (cs:464)"]
        REC --> PS["StepRecord.SpeciesData: one PerSpeciesStepData per species, keyed by FullName"]
    end

    %% Artifacts
    TOSCN --> A1[["scenario_N.csv"]]
    AGG --> A2[["aggregate.csv + config.csv (per run)"]]
    BLOOP --> A3[["GenerateBulkSummary -> bulk_summary.csv at ZIP root (cs:428)"]]
```
