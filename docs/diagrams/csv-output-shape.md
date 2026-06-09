# Diagram: Scenario, Aggregate, and Bulk Summary CSV Layout

This diagram shows the block layout of the three CSV files the simulation writes. `scenario_{N}.csv` is one file per Scenario, built by `SimulationRunner.ToCsvInternal` (`SimulationRunner.cs:743`); it has four parts in fixed order: `#config:` comment lines, a `#species:` input table, the daily data block (the only part R reads, since every `#` line is a comment), and trailing `#summary:`/`#extinction:` comment blocks. `aggregate.csv` is one file per Run, built by `AggregateResults.ToAggregateCsv` (`ScenarioResult.cs:588`); it is a mixed-format file with `=== TITLE ===` section dividers and `# Key,Value` metadata, not one rectangular table. `bulk_summary.csv` is one file at the ZIP root per Bulk upload, built by `BulkSimulationController.GenerateBulkSummary` (`BulkSimulationController.cs:428`). The current shipping mode is Tier 1 only: every Tier 2 column is suppressed when `Tier2Enabled` is `false` (`SimulationRunner.cs:216-232`, `SimulationRunner.cs:270-286`). The daily block appends 17 per-species columns per species, prefixed by `SanitizeColumnName(FullName)` and ordered `(Tier asc, FullName asc)`; the tier-rollup invariant is that per-species `_Pop` columns sum to the dynamic `Tier{n}_{label}` rollup column, which sum to `Tier{n}Pop`. Sections marked v12 appear only when per-species rich metrics exist. For the exact header strings, value formats, and per-column sources see `csv-output-formats.md`.

```mermaid
flowchart TB
    subgraph SCN["scenario_N.csv  -  per Scenario  (SimulationRunner.ToCsvInternal, cs:743)"]
        direction TB
        SC1["1. #config: block (cs:749-767)<br/>#config:model_version,v12-per-species-tracking<br/>#config:days_per_scenario, random_seed, biology_step, ...<br/>#config:base_temperature, climate_trend_per_year, warming_bias, ...<br/>#config:carrying_capacity_tier1, condition_drain_rate, condition_recovery_rate"]
        SC2["2. #species: table (cs:770-793)  one header + one row per input species<br/>#species:Name,Variant,Tier,InitialCount,...,Pmax,CTminC,CTmaxC,TemperatureDebuff<br/>temps in both Kelvin and Celsius (Kelvin-273.15, :F2)"]
        SC3["3. Daily data block  (one row per recorded day; R reads only this)<br/>fixed: Day,Year,Temperature,BiologyCycle,StartPop,EndPop,Tier1Pop[,Tier2Pop]<br/>dynamic rollup: Tier{n}_{label} per (tier,label), ordinal sort (cs:159-199)<br/>tier events: EatenT1,TempDeathsT1,...,TotalDeaths,BirthsT1,FedRateT1,FoodDensityT1,...,ReproScaleT1<br/>per-species x N: {SanitizeColumnName(FullName)}_{Field} x 17, order (Tier asc, FullName asc)<br/>invariant: sum(_Pop) = Tier{n}_{label} = Tier{n}Pop"]
        SC4["4. Trailing #summary: + #extinction: comment blocks (cs:810-909)<br/>#summary:Statistic / Variant / Tier / Mean / Max / Min / StdDev<br/>#extinction:Species,Variant,Tier,DayReachedZero"]
        SC1 --> SC2 --> SC3 --> SC4
    end

    subgraph AGG["aggregate.csv  -  per Run  (AggregateResults.ToAggregateCsv, cs:588)"]
        direction TB
        AG1["=== TINYSEA AGGREGATE RESULTS === + # Key,Value header block"]
        AG2["=== SUMMARY ===  Scenarios Run, Survived, Crashed, Crash Rate[, Avg Crash Day]"]
        AG3["=== POPULATION STATS (Survived Only) ===  Avg/Min/Max Final (Tier 1)"]
        AG4["=== PER-SPECIES POPULATION STATS (All Scenarios) ===<br/>Species,Variant,Tier,Avg,SurvivedAvg,Min,Max,Extinct,Survived,ExtinctionRate"]
        AG5["=== CONDITION STATS ===  Avg Condition (all), Avg Final Condition (survived)"]
        AG6["v12 (only if PerSpeciesMetrics non-empty):<br/>=== PER-SPECIES FINAL YEAR METRICS === (last 365 days)<br/>=== PER-SPECIES FULL-RUN METRICS ===<br/>=== PER-SPECIES STABILITY METRICS ==="]
        AG7["=== INDIVIDUAL SCENARIOS ===  one row per scenario (per-species columns)"]
        AG1 --> AG2 --> AG3 --> AG4 --> AG5 --> AG6 --> AG7
    end

    subgraph BLK["bulk_summary.csv  -  per Bulk, ZIP root  (GenerateBulkSummary, cs:428)"]
        direction TB
        BK1["=== TINYSEA BULK SUMMARY (Across All Runs) === + # Model Version / Total Runs / Generated"]
        BK2["=== PER-RUN RESULTS - TIER LEVEL ===  Run,Scenarios,Survived,Crashed,CrashRate,BaseTemp,ClimateTrend,{species...}"]
        BK3["=== PER-RUN RESULTS - PER SPECIES ===  Run,Species,Variant,Tier,AvgPop,SurvivedAvgPop"]
        BK4["=== PER-SPECIES AGGREGATE (Across All Runs) ===<br/>Species,Variant,Tier,GrandMean,SurvivedMean,RunsExtinct,RunsSurvived,ExtinctionRate"]
        BK5["v12 (only if any run carries PerSpeciesMetrics):<br/>=== PER-RUN PER-SPECIES FINAL YEAR ===<br/>=== CROSS-RUN PER-SPECIES FINAL YEAR (Mean of per-run means) ===<br/>=== CROSS-RUN STABILITY ==="]
        BK1 --> BK2 --> BK3 --> BK4 --> BK5
    end

    SC3 -. "N scenario files aggregate into one run file" .-> AG7
    AG4 -. "per-run summaries aggregate into one bulk file" .-> BK4
```
