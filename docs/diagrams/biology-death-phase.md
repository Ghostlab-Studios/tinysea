flowchart LR
    START(["Death Phase · Steps 6-7"]) --> S6CHECK

    subgraph STEP6 ["Step 6 · Thermal Death"]
        direction LR
        S6CHECK{{"RawThermalPerf == 0?"}}
        S6CHECK -- No --> S6SAFE["No thermal deaths"]
        S6CHECK -- Yes --> S6KILL["Kill entire population<br/>deaths = population<br/>population = 0<br/>condition = 0"]
        S6SAFE --> S6OUT["Continue to Step 7"]
        S6KILL --> S6OUT
    end

    S6OUT --> S7CHECK

    subgraph STEP7 ["Step 7 · Condition Death"]
        direction LR
        S7CHECK{{"Condition less than<br/>DeathThreshold?"}}
        S7CHECK -- No --> S7SAFE["No condition deaths"]
        S7CHECK -- Yes --> S7SEV["severity = (deathThreshold - condition) / deathThreshold"]
        S7SEV --> S7RAW["rawDeaths = pop x severity x deathRate x bioStep"]
        S7RAW --> S7ACC["CONDITION DEATH ACCUMULATOR<br/>accum += rawDeaths<br/>wholeDeaths = floor accum<br/>accum -= wholeDeaths"]
        S7ACC --> S7APPLY["pop -= wholeDeaths"]
        S7APPLY --> S7BOOST["Survivor fitness boost:<br/>newCondition = oldCondition x oldPop / newPop<br/>Cap at 1.0"]
        S7SAFE --> S7OUT["Continue to Life Phase"]
        S7BOOST --> S7OUT
    end

    S7OUT --> DONE(["To Life Phase · Steps 8-9"])

    style START fill:#9d0208,color:#fff
    style DONE fill:#9d0208,color:#fff
    style S6CHECK fill:#457b9d,color:#fff
    style S6SAFE fill:#264653,color:#fff
    style S6KILL fill:#9d0208,color:#fff
    style S6OUT fill:#264653,color:#fff
    style S7CHECK fill:#457b9d,color:#fff
    style S7SAFE fill:#264653,color:#fff
    style S7SEV fill:#9d0208,color:#fff
    style S7RAW fill:#9d0208,color:#fff
    style S7ACC fill:#6a4c93,color:#fff
    style S7APPLY fill:#9d0208,color:#fff
    style S7BOOST fill:#e9c46a,color:#000
    style S7OUT fill:#264653,color:#fff
