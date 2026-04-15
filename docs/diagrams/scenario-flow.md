flowchart TD
    START(["Scenario Start"]) --> SEED["Initialize with random seed"]

    SEED --> CONFIG["Load Configuration<br/>Temperature settings · Ecosystem rules · Species list"]

    CONFIG --> SPECIES["Initialize Species<br/>Set starting populations<br/>Set all species parameters"]

    SPECIES --> GET_TEMP

    subgraph DAY_LOOP ["Day Loop — day 1 to TotalDays"]
        direction TB
        GET_TEMP["Calculate Day's Temperature"]

        GET_TEMP --> BIO_CHECK{{"Biology runs this day?"}}

        BIO_CHECK -- Yes --> BIOLOGY["Run Biology Step<br/>10-step ecosystem update"]

        BIO_CHECK -- No --> RECORD

        BIOLOGY --> RECORD["Record Day's Data"]

        RECORD --> CRASH{{"Ecosystem Crashed?"}}

        CRASH -- Yes --> CRASH_LOG["Log Crash"]
        CRASH -- No --> NEXT_DAY{{"More days?"}}

        NEXT_DAY -- Yes --> GET_TEMP
    end

    CRASH_LOG --> RESULTS
    NEXT_DAY -- No --> RESULTS

    RESULTS["Build Scenario Results<br/>Final populations per species<br/>Population statistics<br/>Extinction timing per species"]

    RESULTS --> CSV["Generate Scenario CSV"]

    CSV --> DONE(["Scenario Complete"])

    style START fill:#2d6a4f,color:#fff
    style DONE fill:#2d6a4f,color:#fff
    style BIOLOGY fill:#1d3557,color:#fff
    style CRASH_LOG fill:#9d0208,color:#fff
    style CRASH fill:#e63946,color:#fff
    style BIO_CHECK fill:#457b9d,color:#fff
    style NEXT_DAY fill:#457b9d,color:#fff
    style GET_TEMP fill:#e9c46a,color:#000
    style RECORD fill:#264653,color:#fff
    style RESULTS fill:#6a4c93,color:#fff
    style CSV fill:#6a4c93,color:#fff
