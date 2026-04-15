flowchart TD
    START(["Bulk Simulation Start"]) --> UPLOAD["Upload CSV File<br/>Each row defines one run configuration"]

    UPLOAD --> PARSE["Parse CSV<br/>Each row becomes a batch<br/>with environment settings and species"]

    PARSE --> BATCH_START

    subgraph BATCH_LOOP ["For Each CSV Row — one run"]
        direction TB
        BATCH_START["Load Row Configuration<br/>Temperature · ecosystem · species parameters"]

        BATCH_START --> CONVERT["Set Up Species<br/>Read species from CSV columns<br/>Convert temperatures from Celsius to Kelvin"]

        CONVERT --> SCENARIOS["Run N Scenarios<br/>Each with a different random seed"]

        SCENARIOS --> AGG["Calculate Run Statistics<br/>Per-species: Avg · SurvivedAvg · Min · Max<br/>Extinction rate per species<br/>Crash rate across scenarios"]

        AGG --> SAVE["Save Run Results<br/>Per-scenario CSV files<br/>Aggregate statistics<br/>Configuration record"]

        SAVE --> NEXT_BATCH{{"More rows?"}}
        NEXT_BATCH -- Yes --> BATCH_START
    end

    NEXT_BATCH -- No --> BULK_SUMMARY

    BULK_SUMMARY["Generate Bulk Summary"]

    BULK_SUMMARY --> SUMMARY_CONTENTS

    subgraph SUMMARY_CONTENTS ["Bulk Summary Contents"]
        direction TB
        RUN_TABLE["Per-Run Results<br/>One row per CSV row<br/>Scenarios · survived · crashed · crash rate<br/>Base temp · climate trend<br/>Average population per species"]

        RUN_TABLE --> SP_AGG["Per-Species Aggregate Across All Runs<br/>Grand mean population<br/>Survived mean · only runs where species lived<br/>Extinction rate across all runs"]
    end

    SP_AGG --> DOWNLOAD["Download Results<br/>ZIP with one folder per run<br/>+ bulk_summary.csv at the root"]

    DOWNLOAD --> DONE(["Bulk Complete"])

    style START fill:#2d6a4f,color:#fff
    style DONE fill:#2d6a4f,color:#fff
    style UPLOAD fill:#e9c46a,color:#000
    style PARSE fill:#e9c46a,color:#000
    style NEXT_BATCH fill:#457b9d,color:#fff
    style SCENARIOS fill:#1d3557,color:#fff
    style BULK_SUMMARY fill:#6a4c93,color:#fff
    style RUN_TABLE fill:#6a4c93,color:#fff
    style SP_AGG fill:#6a4c93,color:#fff
    style AGG fill:#264653,color:#fff
    style SAVE fill:#264653,color:#fff
    style CONVERT fill:#264653,color:#fff
    style DOWNLOAD fill:#264653,color:#fff
