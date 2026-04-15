flowchart LR
    START(["Life Phase · Steps 8-9"]) --> S8POP

    subgraph STEP8 ["Step 8 · Reproduction"]
        direction LR
        S8POP{{"Pop >= 2?"}}
        S8POP -- No --> S8SKIP["No reproduction<br/>MIN_POPULATION = 2"]

        S8POP -- Yes --> S8COND{{"Condition >= ReproThreshold?"}}

        S8COND -- Yes --> S8ABOVE["reproScale = 0.10 + 0.90 x<br/>(condition - threshold) / (1 - threshold)"]

        S8COND -- No --> S8BELOW["reproScale = 0.10 x condition / threshold<br/>STRUGGLING_REPRO_RATE = 0.10"]

        S8ABOVE --> S8BIRTHS["births = pop x reproScale x reproMult x bioStep"]
        S8BELOW --> S8BIRTHS

        S8BIRTHS --> S8PRED{{"Tier 1 and no predators?"}}
        S8PRED -- Yes --> S8PEN["births x= 0.85<br/>NO_PREDATOR_PENALTY"]
        S8PRED -- No --> S8CAP
        S8PEN --> S8CAP{{"Tier 1 and carrying cap?"}}
        S8CAP -- Yes --> S8GROW["births x= max 0 · 1 - tierPop / carryingCap"]
        S8CAP -- No --> S8ACC
        S8GROW --> S8ACC

        S8ACC["BIRTH ACCUMULATOR<br/>accum += births<br/>wholeBirths = floor accum<br/>accum -= wholeBirths"]
        S8ACC --> S8ADD["pop += wholeBirths"]
        S8ADD --> S8DIL["Newborn dilution:<br/>condition = oldPop x oldCond + wholeBirths x 0.5 / newPop<br/>NEWBORN_CONDITION = 0.5"]
    end

    S8SKIP --> S9RATE
    S8DIL --> S9RATE

    subgraph STEP9 ["Step 9 · Natural Death"]
        direction LR
        S9RATE["rate = max 0 · naturalDeathRate + randVariance"]
        S9RAW["deaths = pop x rate x bioStep"]
        S9ACC["NATURAL DEATH ACCUMULATOR<br/>accum += deaths<br/>wholeDeaths = floor accum<br/>accum -= wholeDeaths"]
        S9APPLY["pop -= wholeDeaths"]
        S9RATE --> S9RAW --> S9ACC --> S9APPLY
    end

    S9APPLY --> DONE(["To Step 10 · Population Rounding"])

    style START fill:#2d6a4f,color:#fff
    style DONE fill:#2d6a4f,color:#fff
    style S8POP fill:#457b9d,color:#fff
    style S8SKIP fill:#264653,color:#fff
    style S8COND fill:#457b9d,color:#fff
    style S8ABOVE fill:#2d6a4f,color:#fff
    style S8BELOW fill:#2d6a4f,color:#fff
    style S8BIRTHS fill:#2d6a4f,color:#fff
    style S8PRED fill:#457b9d,color:#fff
    style S8PEN fill:#e76f51,color:#fff
    style S8CAP fill:#457b9d,color:#fff
    style S8GROW fill:#e76f51,color:#fff
    style S8ACC fill:#6a4c93,color:#fff
    style S8ADD fill:#2d6a4f,color:#fff
    style S8DIL fill:#e9c46a,color:#000
    style S9RATE fill:#264653,color:#fff
    style S9RAW fill:#e76f51,color:#fff
    style S9ACC fill:#6a4c93,color:#fff
    style S9APPLY fill:#e76f51,color:#fff
