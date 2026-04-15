flowchart LR
    START(["Start"]) --> S1

    subgraph PERF ["Performance Phase · Steps 1-5"]
        direction LR
        S1["Step 1<br/>THERMAL PERFORMANCE<br/>RawPerf = Arrhenius temp<br/>ThermalPerf = RawPerf x Pmax"]
        S2["Step 2<br/>FEEDING / PREDATION<br/>Holling Type II hunting<br/>fedRate = eaten / demand<br/>· predation accumulator ·"]
        S3["Step 3<br/>RAW FINAL PERFORMANCE<br/>RawFinalPerf = RawPerf x FedRate"]
        S4["Step 4<br/>UPDATE CONDITION<br/>Condition drifts toward RawFinalPerf"]
        S5["Step 5<br/>FINAL PERFORMANCE<br/>FinalPerf = ThermalPerf x FedRate"]
        S1 --> S2 --> S3 --> S4 --> S5
    end

    S5 --> S6

    subgraph DEATH ["Death Phase · Steps 6-7"]
        direction LR
        S6["Step 6<br/>THERMAL DEATH<br/>If RawPerf == 0: kill all<br/>Condition reset to 0"]
        S7["Step 7<br/>CONDITION DEATH<br/>Graduated severity below threshold<br/>· condition death accumulator ·<br/>· survivor fitness boost ·"]
        S6 --> S7
    end

    S7 --> S8

    subgraph LIFE ["Life Phase · Steps 8-9"]
        direction LR
        S8["Step 8<br/>REPRODUCTION<br/>reproScale from Condition<br/>· birth accumulator ·<br/>· newborn dilution ·"]
        S9["Step 9<br/>NATURAL DEATH<br/>deaths = pop x rate x bioStep<br/>· natural death accumulator ·"]
        S8 --> S9
    end

    S9 --> S10["Step 10<br/>POPULATION ROUNDING<br/>All pops round to integer"]

    S10 --> DONE(["Done"])

    style START fill:#2d6a4f,color:#fff
    style DONE fill:#2d6a4f,color:#fff
    style S1 fill:#1d3557,color:#fff
    style S2 fill:#1d3557,color:#fff
    style S3 fill:#1d3557,color:#fff
    style S4 fill:#1d3557,color:#fff
    style S5 fill:#1d3557,color:#fff
    style S6 fill:#9d0208,color:#fff
    style S7 fill:#9d0208,color:#fff
    style S8 fill:#2d6a4f,color:#fff
    style S9 fill:#e76f51,color:#fff
    style S10 fill:#264653,color:#fff
