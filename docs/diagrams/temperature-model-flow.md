flowchart TD
    START(["Calculate Day's Temperature"]) --> BASE

    subgraph COMPONENTS ["T(day) = Sum of 5 Components"]
        direction TB
        BASE["1 · Base<br/>BaseTemperature"]

        SEASONAL["2 · Seasonal<br/>sin(2π x day / 365) x SeasonalAmplitude"]

        TREND["3 · Climate Trend<br/>ClimateTrendPerYear x (day / 365)"]

        INTER["4 · Interannual · Fixed per year<br/>year = floor(day / 365)<br/>cold = rand x -VarMag<br/>warm = rand x VarMag x WarmBias<br/>yearVar = (cold + warm) / 2"]

        DAILY["5 · Daily Variation<br/>range = BaseRand + GrowthRate x year<br/>newRandom = rand(-1,1) x range"]

        BASE --> SEASONAL --> TREND --> INTER --> DAILY
    end

    DAILY --> AUTO{{"Autocorrelation?"}}
    AUTO -- Yes --> CALC["variation = 0.70 x prevDay + 0.30 x newRandom"]
    CALC --> STORE["Store for next day<br/>previousDay = variation"]
    AUTO -- No --> RAW["variation = newRandom"]

    STORE --> SUM
    RAW --> SUM

    SUM["T = Base + Seasonal + Trend + Interannual + variation"]

    SUM --> CLAMP["T = clamp(T, MinTemp, MaxTemp)"]

    CLAMP --> DONE(["Final Temperature (Celsius)"])

    style START fill:#e9c46a,color:#000
    style DONE fill:#e9c46a,color:#000
    style BASE fill:#264653,color:#fff
    style SEASONAL fill:#264653,color:#fff
    style TREND fill:#264653,color:#fff
    style INTER fill:#264653,color:#fff
    style DAILY fill:#264653,color:#fff
    style AUTO fill:#457b9d,color:#fff
    style CALC fill:#1d3557,color:#fff
    style STORE fill:#1d3557,color:#fff
    style RAW fill:#1d3557,color:#fff
    style SUM fill:#6a4c93,color:#fff
    style CLAMP fill:#e63946,color:#fff
