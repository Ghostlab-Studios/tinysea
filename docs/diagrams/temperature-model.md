# Temperature model

Source: `TemperatureCalculator.cs` — `GetTemperature`, `GetSeasonalComponent`, `GetClimateTrend`, `GetInterannualVariation`, `GetDailyVariation`.

```mermaid
flowchart LR
    Base["BaseTemperature<br/>(default 20°C)"] --> Sum(("+"))
    Seasonal["Seasonal<br/>sin(2π · day / 365)<br/>× SeasonalAmplitude"] --> Sum
    Trend["Climate trend<br/>ClimateTrendPerYear × (day / 365)<br/>linear"] --> Sum
    Inter["Interannual variation<br/>per-year random offset<br/>(cold + warm) / 2<br/>cached in _yearVariations<br/>(skipped if UseInterannualVariation=false)"] --> Sum
    Daily["Daily variation<br/>R = BaseRandomness + RandomnessGrowthRate × year<br/>newRandom = uniform(-R, +R)"] --> AC{"UseAutocorrelation?"}
    AC -- yes --> Smooth["v = 0.7 × _previousDayVariation + 0.3 × newRandom"]
    AC -- no --> Raw["v = newRandom"]
    Smooth --> Sum
    Raw --> Sum
    Sum --> Clamp["clamp to<br/>[MinTemp, MaxTemp]"]
    Clamp --> Out["T(day) °C"]
```

## Component formulas (from source)

| Component | Formula | Behavior |
|-----------|---------|----------|
| Seasonal | `sin(2·π · day / 365) · SeasonalAmplitude` | Coldest at `day = 0`, warmest around `day = 182`. |
| Climate trend | `ClimateTrendPerYear × (day / 365f)` | Linear in simulated years. Cast to float before multiplication. |
| Interannual | `cold = uniform(-VariabilityMagnitude, 0)`; `warm = uniform(0, VariabilityMagnitude × WarmingBias)`; `variation = (cold + warm) / 2`; cached by year in `_yearVariations`. | Same value used for every day in a given year. |
| Daily raw | `newRandom = uniform(-1, +1) × currentRandomness` where `currentRandomness = BaseRandomness + RandomnessGrowthRate × year`. | Growing noise over time. |
| Daily smoothed | `variation = 0.7 × _previousDayVariation + 0.3 × newRandom` | Red-noise autocorrelation if enabled. `_previousDayVariation` is updated after each call. |
| Final | `clamp(sum_of_components, MinTemp, MaxTemp)` | Hard floor/ceiling. |

## Notes

- `DAYS_PER_YEAR` is a compile-time constant = 365. Leap years are not modelled.
- `GetYear(day)` returns `(day / 365) + 1` — year 1 = days 0–364.
- The RNG is seeded by the constructor (`TemperatureCalculator(seed)`). Passing `-1` uses system time (non-reproducible). `Reset(seed)` can re-seed between runs, clearing `_yearVariations` and `_previousDayVariation`.
- The calculator holds its own RNG independent of `EcosystemSimulator._rng` — temperature draws do not consume the biology RNG stream.
