flowchart LR
    START(["Performance Phase · Steps 1-5"]) --> S1

    subgraph STEP1 ["Step 1 · Thermal Performance"]
        direction LR
        S1["RawThermalPerf = Arrhenius temp with cosine fade at CTmin/CTmax"]
        S1B["ThermalPerf = RawThermalPerf x Pmax"]
        S1C["Init FedRate = 1<br/>Init HuntingSuccess = 1"]
        S1 --> S1B --> S1C
    end

    S1C --> RATIO

    subgraph STEP2 ["Step 2 · Feeding / Predation"]
        direction LR
        RATIO["ratio = totalPreyPop / totalPredatorPop<br/>NORMAL_PREY_RATIO = 20"]
        HALF["Per predator:<br/>halfSat = 20 x 1 - baseEff / baseEff"]
        EFF["hollingEff = ratio / ratio + halfSat<br/>huntingSuccess = clamp hollingEff + randVariance · 0 · 1"]
        DEMAND["rawDemand = pop x eatingAmount x thermalPerf x bioStep<br/>actualDemand = rawDemand x huntingSuccess"]
        EATEN["totalEaten = min availablePrey · sum actualDemand<br/>fedRate = min 1 · totalEaten / sum rawDemand"]
        PREY["Per prey species:<br/>share = preyPop / totalPreyPop<br/>preyLost = totalEaten x share"]
        PACC["PREDATION ACCUMULATOR<br/>accum += preyLost<br/>wholeDeaths = floor accum<br/>accum -= wholeDeaths<br/>preyPop -= wholeDeaths"]
        RATIO --> HALF --> EFF --> DEMAND --> EATEN --> PREY --> PACC
    end

    PACC --> S3

    subgraph STEP3 ["Step 3 · Raw Final Performance"]
        direction LR
        S3["RawFinalPerf = RawThermalPerf x FedRate"]
    end

    S3 --> S4TARGET

    subgraph STEP4 ["Step 4 · Update Condition"]
        direction LR
        S4TARGET["target = RawFinalPerf"]
        S4CHECK{{"Condition > target?"}}
        S4DRAIN["DRAINING<br/>effectiveDrain = ConditionDrainRate<br/>if target less than 0.2:<br/>  severity = 1 - target / 0.2<br/>  severity = severity squared<br/>  effectiveDrain x= 1 + severity x 4<br/>condition -= condition - target x effectiveDrain"]
        S4RECOVER["RECOVERING<br/>effectiveRecovery = ConditionRecoveryRate<br/>if target greater than 0.7:<br/>  boost = target - 0.7 / 0.3<br/>  boost = boost squared<br/>  effectiveRecovery x= 1 + boost x 4<br/>condition += target - condition x effectiveRecovery"]
        S4CLAMP["Clamp condition to 0 · 1"]
        S4TARGET --> S4CHECK
        S4CHECK -- Yes --> S4DRAIN --> S4CLAMP
        S4CHECK -- No --> S4RECOVER --> S4CLAMP
    end

    S4CLAMP --> S5

    subgraph STEP5 ["Step 5 · Final Performance"]
        direction LR
        S5["FinalPerf = ThermalPerf x FedRate"]
    end

    S5 --> DONE(["To Death Phase · Steps 6-7"])

    style START fill:#1d3557,color:#fff
    style DONE fill:#1d3557,color:#fff
    style S1 fill:#264653,color:#fff
    style S1B fill:#264653,color:#fff
    style S1C fill:#264653,color:#fff
    style RATIO fill:#264653,color:#fff
    style HALF fill:#264653,color:#fff
    style EFF fill:#264653,color:#fff
    style DEMAND fill:#264653,color:#fff
    style EATEN fill:#457b9d,color:#fff
    style PREY fill:#264653,color:#fff
    style PACC fill:#6a4c93,color:#fff
    style S3 fill:#264653,color:#fff
    style S4TARGET fill:#264653,color:#fff
    style S4CHECK fill:#457b9d,color:#fff
    style S4DRAIN fill:#1d3557,color:#fff
    style S4RECOVER fill:#1d3557,color:#fff
    style S4CLAMP fill:#264653,color:#fff
    style S5 fill:#264653,color:#fff
