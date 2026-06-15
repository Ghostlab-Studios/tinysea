# Diagram: Daily Temperature Assembly

This flowchart shows how `TemperatureCalculator.GetTemperature(int day)` produces the single ambient temperature in degrees Celsius for one day (`TemperatureCalculator.cs:61-85`). The argument `day` is a zero-based index; day 0 is the first simulated day. There are two paths. If a non-empty daily series was loaded with `LoadTimeseries`, the method returns the series value indexed by `day % Count` (looping when the run is longer than the series) and clamps it, consuming no RNG (`TemperatureCalculator.cs:66-75`). Otherwise the parametric model sums five components and clamps the result to `[MinTemp, MaxTemp]` (`TemperatureCalculator.cs:77-84`). Of the five components, only the climate trend carries a non-zero long-term mean; the seasonal sine averages to zero over a year, and the interannual and daily terms are zero-mean by construction. The model is tier-independent: it produces one ambient temperature for the whole ecosystem. Per-species `TemperatureDebuff` is applied later, inside the thermal curve, and is not part of this diagram; see `temperature-model.md` and `biology-and-formulas.md` for variables, defaults, and the RNG-consumption order (one daily draw per day, plus two interannual draws on the first day of each new year).

```mermaid
flowchart TD
    START["GetTemperature(day)  // day is 0-based"] --> TSQ{"_timeseries != null ?<br/>(set only by LoadTimeseries for a non-empty list)"}

    TSQ -- yes --> TS["Timeseries override (cs:66-75)<br/>value = _timeseries[day % _timeseries.Count]  // loops if run longer than series<br/>no RNG consumed; seed irrelevant on this path"]
    TS --> CLAMP

    TSQ -- no --> BASE["BaseTemperature  (constant mean, °C)"]

    BASE --> SEAS["+ Seasonal(day)  (cs:126-129)<br/>sin(2*pi*day/365) * SeasonalAmplitude<br/>zero annual mean; phase fixed, day 0 = ascending zero crossing"]

    SEAS --> TREND["+ ClimateTrend(day)  (cs:134-138)<br/>ClimateTrendPerYear * (day / 365.0)   // float division, continuous<br/>the only term with a non-zero long-term mean"]

    TREND --> INTER["+ InterannualVariation(day)  (cs:148-170)<br/>if UseInterannualVariation false: 0<br/>else year = day/365 (int); drawn once per year, cached in _yearVariations<br/>coldPart = rng.NextDouble() * -VariabilityMagnitude<br/>warmPart = rng.NextDouble() * VariabilityMagnitude * WarmingBias<br/>biasMean = VariabilityMagnitude*(WarmingBias-1)/4   // no RNG<br/>value = (coldPart + warmPart)/2 - biasMean   // zero-mean; float precision"]

    INTER --> DAILY["+ DailyVariation(day)  (cs:175-196)<br/>year = day/365 (int); currentRandomness = BaseRandomness + RandomnessGrowthRate*year<br/>newRandom = (rng.NextDouble()*2 - 1) * currentRandomness<br/>if UseAutocorrelation: variation = prev*AutocorrelationCoefficient + newRandom*(1 - AutocorrelationCoefficient)  // AR(1), default coeff 0.7 = legacy 0.7/0.3; 0 = white noise<br/>else: variation = newRandom<br/>_previousDayVariation = variation  (persisted for next day)"]

    DAILY --> SUM["T(day) = sum of the five components above"]

    SUM --> CLAMP["clamp: T = Math.Max(MinTemp, Math.Min(MaxTemp, T))  (cs:84)<br/>applied last; same clamp applies to the timeseries value"]

    CLAMP --> OUT["return T  (°C, passed to ProcessBiologyStep on biology days)"]
```
