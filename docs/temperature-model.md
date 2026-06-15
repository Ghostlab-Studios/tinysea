# Temperature Model

This document describes how the headless TinySea simulation produces the single daily temperature value in degrees Celsius that drives every species' thermal performance each day. The whole model lives in one class, `TemperatureCalculator` (`TemperatureCalculator.cs:15-205`). The class is plain C#, has no Unity `MonoBehaviour` dependency, and is owned by `SimulationRunner` (`SimulationRunner.cs:350,385`).

Temperature is the only external forcing in the simulation. It is computed once per day, before biology runs, and passed to `EcosystemSimulator.ProcessBiologyStep(float tempC)` (`SimulationRunner.cs:428,435`). How that temperature becomes per species performance (per species `temp_offset`, lethal limits, the Arrhenius curve) is the subject of `biology-and-formulas.md` and is not repeated here. How the parameters reach this class from configuration is the subject of `configuration-reference.md`; this document states the values and the arithmetic.

The model is Tier independent. It produces one ambient temperature for the whole ecosystem regardless of how many species or tiers exist. The current simulation runs Tier 1 (prey) only, but the temperature path is identical for any tier.

## 1. Two modes: parametric model and timeseries override

`GetTemperature(int day)` has two code paths (`TemperatureCalculator.cs:61-85`). `day` is a zero based day index. Day 0 is the first simulated day.

1. **Timeseries override.** If a non empty daily temperature series has been loaded (`_timeseries != null`), the method returns the series value for the day, looping if the series is shorter than the run, then clamps to the configured bounds (`TemperatureCalculator.cs:66-75`). An empty or null list is treated as no series: `LoadTimeseries` stores the list only when it is non null and non empty, otherwise sets `_timeseries` back to null (`TemperatureCalculator.cs:91-95`), so `_timeseries != null` can never be true for an empty list and loading an empty list then querying falls through to the parametric model. See section 7.
2. **Parametric model.** Otherwise it sums five components and clamps the result (`TemperatureCalculator.cs:77-85`). This is the default path and the focus of sections 2 through 6.

The parametric formula is:

```
T(day) = BaseTemperature
       + Seasonal(day)
       + ClimateTrend(day)
       + InterannualVariation(day)
       + DailyVariation(day)
T(day) = clamp(T(day), MinTemp, MaxTemp)          // applied after the sum
```

All five terms are in degrees Celsius. `BaseTemperature` is the constant mean. The other four are described below. The clamp is the last operation and uses `Math.Max(MinTemp, Math.Min(MaxTemp, temp))` (`TemperatureCalculator.cs:84`).

Trend ownership (`TemperatureCalculator.cs:9-13`): `ClimateTrend` is the only component that contributes a non zero long term mean. `Seasonal` averages to 0 over a full year. `InterannualVariation` and `DailyVariation` are zero mean by construction. This separation is intentional so that `WarmingBias` skews only the shape of interannual draws and not the trend (see section 4).

## 2. Parameters: units, defaults, and where they come from

Every parameter is a public field on `TemperatureCalculator` with a hard coded default. At run time `SimulationRunner` does not set these directly; the caller copies values from configuration onto `runner.TempCalc` before `Run()` is invoked. There are two callers.

Standard runs copy from the `SimulationConfig` ScriptableObject in `SimulationController.RunSingleScenario` (`SimulationController.cs:353-363`). Bulk runs copy from a per row `BulkBatchConfig` in `SimulationController.RunSingleScenarioFromBatch` (`SimulationController.cs:295-305`). Both callers set the same twelve fields. The mapping is in the table below.

| TemperatureCalculator field | Type | Class default (`TemperatureCalculator.cs`) | Units / meaning | SimulationConfig field (default) | BulkBatchConfig field |
|---|---|---|---|---|---|
| `BaseTemperature` | float | 20 (line 18) | Constant mean temperature, degrees C | `BaseTemperature` (20, `SimulationConfig.cs:80`) | `BaseTemp` |
| `SeasonalAmplitude` | float | 5 (line 19) | Peak deviation of the seasonal sine, degrees C | `SeasonalAmplitude` (5, `SimulationConfig.cs:84`) | `SeasonalAmp` |
| `ClimateTrendPerYear` | float | 1 (line 20) | Linear warming slope, degrees C per year | `ClimateTrend` (1, `SimulationConfig.cs:88`) | `ClimateTrend` |
| `VariabilityMagnitude` | float | 2 (line 21) | Year to year variation range, degrees C | `VariabilityMagnitude` (2, `SimulationConfig.cs:95`) | `VariabilityMag` |
| `WarmingBias` | float | 1.5 (line 22) | Dimensionless skew of the warm tail of the interannual draw | `WarmingBias` (1.5, `SimulationConfig.cs:98`) | `WarmingBias` |
| `BaseRandomness` | float | 5 (line 23) | Half width of the daily uniform noise in year 0, degrees C | `DailyVariationRange` (5, `SimulationConfig.cs:105`) | `DailyVarRange` |
| `RandomnessGrowthRate` | float | 0.5 (line 24) | Growth of daily noise half width, degrees C per year | `RandomnessGrowthRate` (0.5, `SimulationConfig.cs:108`) | `RandomnessGrowth` |
| `UseInterannualVariation` | bool | true (line 25) | Enable the interannual term | `InterannualVariation` (true, `SimulationConfig.cs:91`) | `InterannualVariation` |
| `UseAutocorrelation` | bool | true (line 26) | Smooth daily noise against the previous day | `Autocorrelated` (true, `SimulationConfig.cs:102`) | `Autocorrelated` |
| `AutocorrelationCoefficient` | float | 0.7 (line 27) | AR(1) coefficient on the daily noise when `UseAutocorrelation` is on; 0 = white noise. Valid range 0..1 | `AutocorrelationCoefficient` (0.7, `SimulationConfig.cs:106`, `[Range(0,1)]`) | `AutocorrelationCoefficient` |
| `MinTemp` | float | -5 (line 28) | Hard floor applied after the sum, degrees C | `TemperatureBoundsMin` (-5, `SimulationConfig.cs:112`) | `TempMin` |
| `MaxTemp` | float | 40 (line 29) | Hard ceiling applied after the sum, degrees C | `TemperatureBoundsMax` (50, `SimulationConfig.cs:115`) | `TempMax` |

Notes on the defaults:

- The class field defaults and the `SimulationConfig` field defaults agree for every parameter except `MaxTemp`. The class default is 40 (`TemperatureCalculator.cs:29`); the `SimulationConfig` default is 50 (`SimulationConfig.cs:115`). In practice the caller always overwrites the class field, so the active ceiling is whatever the config or bulk row supplies. The class field default of 40 only takes effect if a `TemperatureCalculator` is constructed and used without a caller copying config onto it.
- The shipped `SimulationConfig.asset` overrides several inspector defaults. Its serialized values are: `BaseTemperature` 20, `SeasonalAmplitude` 5, `ClimateTrend` 0, `InterannualVariation` 0 (disabled), `VariabilityMagnitude` 0, `WarmingBias` 0, `Autocorrelated` 1 (enabled), `DailyVariationRange` 5, `RandomnessGrowthRate` 0, `TemperatureBoundsMin` 0, `TemperatureBoundsMax` 40 (`SimulationConfig.asset:21-31`). With those asset values the live model reduces to a base plus seasonal sine plus autocorrelated daily noise, with no climate trend and no interannual term. A developer reproducing the shipped behavior must use the asset values, not the C# field defaults.

Which parameter set is authoritative for reproduction: to reproduce shipped behavior, use the `SimulationConfig.asset` values listed in the bullet above. The C# field defaults (the middle column of the table) and the `SimulationConfig` inspector defaults (the fifth column) are only fallbacks that take effect when no config is copied onto the calculator, which does not happen on any normal run. The three parameter sets exist because the C# field is the construction-time default, the `SimulationConfig` field initializer is the inspector default for a freshly created asset, and the shipped `.asset` is the serialized override actually used; when they disagree, the shipped `.asset` wins for the standard run.

There is no field level range validation inside `TemperatureCalculator`. The only validation is in `SimulationConfig.IsValid` (`SimulationConfig.cs:146-195`), and it does not check any temperature parameter; it checks days, scenario count, species presence, and carrying capacity only. The inspector applies `[Range]` attributes to unrelated fields, but the temperature fields in `SimulationConfig` carry no `[Range]` attribute, so any float is accepted. Bulk rows are parsed with `GetFloat`/`GetBool`, which reject empty or non numeric cells with a row error but impose no min/max (`CsvBatchParser.cs:176-186`, `CsvBatchParser.cs:376-418`).

Degenerate inputs are not validated and produce well defined but surprising outputs that a strict reimplementation must match:

- `MinTemp > MaxTemp`: the clamp is `Math.Max(MinTemp, Math.Min(MaxTemp, temp))` (`TemperatureCalculator.cs:84`). `Math.Min(MaxTemp, temp)` caps at the smaller `MaxTemp`, then `Math.Max(MinTemp, ...)` floors at the larger `MinTemp`, which always wins. The result is that every day returns exactly `MinTemp`.
- Negative `VariabilityMagnitude`: flips the sign of both interannual draws, so `coldPart` becomes non negative and `warmPart` non positive, inverting the warm and cold tails. `biasMean` flips sign correspondingly. The term stays zero mean but the asymmetry reverses.
- Negative `WarmingBias`: `warmPart = NextDouble() * (VariabilityMagnitude * WarmingBias)` becomes non positive, so both parts pull cold; `biasMean = VariabilityMagnitude*(WarmingBias-1)/4` goes more negative, which the subtraction adds back as a positive offset to re center the mean at zero.
- Negative `ClimateTrendPerYear`: produces linear cooling rather than warming (already noted in Section 4); nothing forbids it.

## 3. Seasonal component

`GetSeasonalComponent(int day)` returns a sine wave (`TemperatureCalculator.cs:126-129`):

```
Seasonal(day) = sin(2 * pi * day / DAYS_PER_YEAR) * SeasonalAmplitude
```

`DAYS_PER_YEAR` is the constant 365 (`TemperatureCalculator.cs:30`). `SeasonalAmplitude` is in degrees C. The computation is done in double precision (`Math.Sin`, `Math.PI`) and cast to float on return.

Properties of this term:

- It is zero at `day = 0`, rises to `+SeasonalAmplitude` near `day = 91.25` (one quarter of 365, where the argument equals pi/2), returns to zero near `day = 182.5`, falls to `-SeasonalAmplitude` near `day = 273.75`, and returns to zero at `day = 365`.
- Over any whole number of years it averages to zero, so it adds no long term mean.
- The period is exactly 365 days. There is no leap year handling. Year boundaries for the other terms also use 365 (see sections 4 and 5), so all per year logic is consistent at 365.

The code comment at `TemperatureCalculator.cs:124` describes the curve as "coldest at day 0, warmest at day 182". That string appears exactly once in the file; the doc comment at `TemperatureCalculator.cs:199` is about year numbering ("Get year number from day (Year 1 = days 0-364)") and says nothing about the seasonal curve. The seasonal comment does not match the arithmetic above: at day 0 the term is at its mean (zero), not its minimum, and the maximum occurs near day 91, not day 182. The formula, not the comment, is authoritative.

There is no calendar anchor. Day 0 corresponds to the ascending zero crossing of the seasonal sine, which is the mean temperature in the warming phase, not to any calendar date and not to the coldest or warmest point. The phase is fixed by `sin(2*pi*day/365)` with no offset term (`TemperatureCalculator.cs:128`). A reimplementer must not assume day 0 is January 1, winter, or any season.

## 4. Climate trend component

`GetClimateTrend(int day)` returns a linear ramp (`TemperatureCalculator.cs:134-138`):

```
years = day / 365.0                         // float division
ClimateTrend(day) = ClimateTrendPerYear * years
```

`ClimateTrendPerYear` is in degrees C per year. The division `day / (float)DAYS_PER_YEAR` is a continuous float, so the trend increases smoothly every day, not in yearly steps. At `day = 365` the trend equals exactly `ClimateTrendPerYear`. This is the only term with a non zero long term mean (`TemperatureCalculator.cs:9-13`). A negative `ClimateTrendPerYear` produces linear cooling; nothing forbids negative values.

## 5. Interannual variability and the warming bias correction

`GetInterannualVariation(int day)` returns a single offset that is constant for an entire year and drawn once per year (`TemperatureCalculator.cs:148-170`).

If `UseInterannualVariation` is false the term is zero for every day (`TemperatureCalculator.cs:150`).

Otherwise the year index is `year = day / 365` (integer division, so year 0 is days 0 through 364). The draw is memoized in a `Dictionary<int,float> _yearVariations` keyed by year index (`TemperatureCalculator.cs:34,152-167`). The first time a year is seen, two RNG draws (`coldPart` then `warmPart`) and one deterministic correction (`biasMean`) produce the year's value; every later day in that year returns the cached value (`TemperatureCalculator.cs:169`). The two draws are at `TemperatureCalculator.cs:163-164`; `biasMean` at line 165 consumes no RNG. Exactly two draws occur, cold then warm. This matches the explicit count in this section's RNG-consumption note below.

The draw, exactly as coded (`TemperatureCalculator.cs:163-166`):

```
float  coldPart  = (float)(_rng.NextDouble() * -VariabilityMagnitude)              // uniform in (-VariabilityMagnitude, 0]
float  warmPart  = (float)(_rng.NextDouble() * VariabilityMagnitude * WarmingBias) // uniform in [0, VariabilityMagnitude*WarmingBias)
float  biasMean  = VariabilityMagnitude * (WarmingBias - 1f) / 4f
_yearVariations[year] = (coldPart + warmPart) / 2f - biasMean
```

Precision boundary, which is load bearing for byte identical reproduction. All three intermediates `coldPart`, `warmPart`, and `biasMean` are declared `float`, and the result stored in `_yearVariations[year]` is `float` (`TemperatureCalculator.cs:163-166`). The two RNG products are computed in double, because `_rng.NextDouble()` returns a `double` and the multiplications promote to double, but each product is cast to `float` immediately on assignment to `coldPart` and `warmPart`. That cast rounds the double to the nearest IEEE 754 single precision value BEFORE the addition `coldPart + warmPart` is performed. The addition, the division by `2f`, and the subtraction of `biasMean` therefore all run in single precision. A reimplementation that keeps `coldPart`, `warmPart`, and `biasMean` as doubles and only rounds the final `yearValue` to float will produce a different low order bit pattern in the offset and will not be byte identical, even though the statistical distribution is the same. The exact sequence of operations a port must follow is:

1. `cd = _rng.NextDouble() * (-(double)VariabilityMagnitude)`        // double
2. `coldPart = (float)cd`                                            // round to single
3. `wd = _rng.NextDouble() * (double)VariabilityMagnitude * (double)WarmingBias` // double
4. `warmPart = (float)wd`                                            // round to single
5. `biasMean = (float)VariabilityMagnitude * ((float)WarmingBias - 1f) / 4f`     // single throughout
6. `yearValue = (coldPart + warmPart) / 2f - biasMean`              // single throughout

Step 5 already operates entirely on `float` operands in the source (`VariabilityMagnitude`, `WarmingBias`, the literals `1f` and `4f` are all `float`), so `biasMean` is computed in single precision with no intermediate double. Steps 1 through 4 are the only places a double appears, and the cast at steps 2 and 4 truncates it to single before any further arithmetic.

`_rng.NextDouble()` returns a double in `[0.0, 1.0)`: 0 is attainable, 1 is not. So `coldPart = NextDouble() * (-VariabilityMagnitude)` (`TemperatureCalculator.cs:163`) lies in `(-VariabilityMagnitude, 0]`, with 0 attainable when `NextDouble()` returns 0 and `-VariabilityMagnitude` never reached because `NextDouble()` never equals 1. The `warmPart` interval `[0, VariabilityMagnitude*WarmingBias)` is the mirror case: 0 is attainable, the upper bound is not.

`VariabilityMagnitude` is in degrees C. `WarmingBias` is dimensionless. `biasMean` is in degrees C.

Why `biasMean` is subtracted (`TemperatureCalculator.cs:140-167`): `coldPart` is uniform on `(-VariabilityMagnitude, 0]` with expected value `-VariabilityMagnitude/2`. `warmPart` is uniform on `[0, VariabilityMagnitude*WarmingBias)` with expected value `VariabilityMagnitude*WarmingBias/2`. The average `(coldPart + warmPart)/2` therefore has expected value

```
( -VariabilityMagnitude/2 + VariabilityMagnitude*WarmingBias/2 ) / 2
  = VariabilityMagnitude * (WarmingBias - 1) / 4
```

which is exactly `biasMean`. Subtracting it makes the interannual term zero mean for any `WarmingBias`. The result is that `WarmingBias` controls the asymmetry (the shape: a wider warm tail when `WarmingBias > 1`) without leaking a hidden warming or cooling trend. The long term trend is owned solely by `ClimateTrendPerYear`. The comment notes the uncorrected leak would be about 0.25 degrees C per year at `VariabilityMagnitude = 2` and `WarmingBias = 1.5`.

`biasMean` is a fixed analytic constant computed only from `VariabilityMagnitude` and `WarmingBias` (`TemperatureCalculator.cs:165`), which is the theoretical expected value of the draw. It does not use the actual `coldPart`/`warmPart` samples. The formula `yearValue = (coldPart + warmPart)/2 - biasMean` (`TemperatureCalculator.cs:166`) consumes the two samples, but `biasMean` itself references neither. Subtracting `biasMean` removes the expected bias, not the per year realized bias, so a single year's offset is still random; only its expected value is forced to zero.

`WarmingBias = 1` makes the warm and cold tails symmetric and `biasMean = 0`. `WarmingBias < 1` narrows the warm tail.

RNG consumption: this method consumes exactly two `NextDouble()` calls per distinct year, in the order `coldPart` then `warmPart`. The order matters for reproducibility because the same RNG also feeds the daily term (section 6). Across a multi year run the per year draws are taken lazily, on the first day of each year, interleaved with the daily draws.

## 6. Daily variation and autocorrelation

`GetDailyVariation(int day)` returns fresh noise each day, optionally smoothed against the previous day (`TemperatureCalculator.cs:175-196`).

The half width grows with the year index (`TemperatureCalculator.cs:177-178`):

```
year = day / 365                                         // integer division
currentRandomness = BaseRandomness + RandomnessGrowthRate * year
```

`BaseRandomness` and `currentRandomness` are in degrees C; `RandomnessGrowthRate` is degrees C per year. The growth is stepwise per year, not continuous, because `year` is an integer.

A new noise sample is drawn (`TemperatureCalculator.cs:181`):

```
newRandom = (_rng.NextDouble() * 2 - 1) * currentRandomness   // uniform in [-currentRandomness, +currentRandomness)
```

Then the smoothing (`TemperatureCalculator.cs:185-196`):

```
if UseAutocorrelation:
    variation = _previousDayVariation * AutocorrelationCoefficient + newRandom * (1 - AutocorrelationCoefficient)
else:
    variation = newRandom
_previousDayVariation = variation                               // persisted for the next day
return variation
```

The AR(1) coefficient is a parameter, `AutocorrelationCoefficient`, default 0.7 (`TemperatureCalculator.cs:27,188`). It replaces the previously hard coded `0.7 * yesterday + 0.3 * new`; with the default 0.7 the blend is `0.7 * previous + 0.3 * newRandom`, bit identical to that old form. The weight on fresh noise is the complement `1 - AutocorrelationCoefficient`. At coefficient 0 the term is pure white noise (`variation = newRandom`, independent days). As the coefficient approaches 1 the marginal daily amplitude shrinks toward zero (this is AR(1) with innovation weight `1 - coeff`, whose stationary variance is proportional to `(1 - coeff) / (1 + coeff)`), a red noise dampening. The valid range is 0..1; the bulk parser rejects values outside it (`CsvBatchParser.cs:308-309`) and `SimulationConfig` carries a `[Range(0,1)]` attribute. `_previousDayVariation` is instance state initialized to 0 (`TemperatureCalculator.cs:35`), so on day 0 with autocorrelation on the value is `coeff * 0 + (1 - coeff) * newRandom = (1 - coeff) * newRandom`. Each subsequent day folds in `1 - coeff` of fresh noise and keeps `coeff` of the prior smoothed value, producing an AR(1) random walk that stays bounded because both inputs are bounded.

`_previousDayVariation` is assigned in both branches: the `_previousDayVariation = variation` line (`TemperatureCalculator.cs:195`) sits after the if/else, so it runs whether autocorrelation is on or off. When autocorrelation is off, `variation = newRandom` and the stored previous value is therefore the raw `newRandom`. A reimplementer must place this assignment after the if/else, not inside the autocorrelation branch; guarding it inside the branch would match all-on and all-off runs but diverge the stored state on any mid run toggle. The flag is read fresh every call, so a toggle between days would take effect immediately, but the standard run holds `UseAutocorrelation` fixed for the lifetime of a scenario (it is copied once from config before `Run()`), so no toggle occurs in practice.

RNG consumption: this method consumes exactly one `NextDouble()` per call to `GetTemperature` in parametric mode, regardless of whether autocorrelation is on. Combined with section 5, a day that starts a new year consumes one daily draw plus two interannual draws; every other day consumes one daily draw. Because `GetDailyVariation` always draws (`TemperatureCalculator.cs:181`) before the autocorrelation branch, toggling `UseAutocorrelation` does not change how much of the RNG stream is consumed, only how the sample is combined.

## 7. Timeseries override

A caller can replace the entire parametric model with an explicit per day series.

Loading (`TemperatureCalculator.cs:91-97`): `LoadTimeseries(List<float> dailyTempsC)` stores the list as `_timeseries` when it is non null and non empty, otherwise clears it to null. `HasTimeseries` reports whether a series is loaded. Each entry is a temperature in degrees C, indexed by day.

Lookup (`TemperatureCalculator.cs:66-75`): when `_timeseries` is set, `GetTemperature(day)` returns `clamp(_timeseries[day % _timeseries.Count], MinTemp, MaxTemp)`. Modulo and indexing happen first, then the clamp. The modulo is always safe: a loaded series is guaranteed non empty (`Count >= 1`) by the loader normalization above, so `day % _timeseries.Count` never divides by zero, and `day` is always `>= 0` in this code path because the runner only calls `GetTemperature` with `dayIndex` in `[0, TotalDays-1]` (section 9). The modulo means the series loops if the run is longer than the series. The first time `day` exceeds the series length a single `Debug.LogWarning` is emitted, guarded by `_timeseriesLoopWarned` so it warns at most once per calculator instance (`TemperatureCalculator.cs:41,68-73`). The same `MinTemp`/`MaxTemp` clamp from section 1 applies, so bounds still cap the series.

Important consequences of the override:

- The five parametric components are skipped entirely. `BaseTemperature`, `SeasonalAmplitude`, `ClimateTrendPerYear`, `VariabilityMagnitude`, `WarmingBias`, `BaseRandomness`, `RandomnessGrowthRate`, `UseInterannualVariation`, and `UseAutocorrelation` have no effect while a series is loaded. Only `MinTemp` and `MaxTemp` still act.
- No RNG is consumed in timeseries mode. The seed has no effect on the temperature path; scenarios with different seeds see identical temperatures.
- Per species `temp_offset` is still applied downstream in the thermal calculation, so each species' experienced temperature still shifts relative to this ambient series (`TemperatureCalculator.cs:64-65`). See `biology-and-formulas.md`.

Parsing the series file (`TemperatureCalculator.cs:104-121`): the static `ParseTimeseriesCsv(string content)` reads a `Day,Temperature_C` CSV. It normalizes line endings, then for each line: trims it, skips blank lines and lines whose first character is `#`, splits on commas, and parses the last comma separated cell as the temperature using invariant culture float parsing. Taking the last cell handles both a two column `Day,Temperature_C` row and a bare single column `Temperature_C` row. The `Day` column is informational and is not used to index; rows are consumed in file order. Unparseable rows, including a header row, are silently skipped. The method returns null if no numeric value was found, otherwise the list of temperatures.

How a series reaches a run (`SimulationController.cs:314-334`): only the bulk path wires this up. `BulkBatchConfig.TemperatureTimeseriesFile` holds an optional file path, defaulting to the empty string (`BulkBatchConfig.cs:61-62`), populated from the optional `temperature_timeseries_file` CSV column (`CsvBatchParser.cs:38,193`). In `RunSingleScenarioFromBatch`, when that path is non blank the code reads the file with `System.IO.File.ReadAllText`, parses it with `ParseTimeseriesCsv`, and calls `LoadTimeseries` on success. Failures are non fatal: a missing file, a parse that yields no rows, or any exception logs a `Debug.LogWarning` and the run falls back to the parametric model (`SimulationController.cs:317-334`). Because the load uses `System.IO.File`, the timeseries override is an Editor and standalone feature; under WebGL there is no local file read, so the standard `SimulationController.RunSingleScenario` path never loads a series and always uses the parametric model.

## 8. Random number generator and seeding

The generator is a single `System.Random` instance, `_rng`, held by the calculator (`TemperatureCalculator.cs:33`). It is the only source of randomness in this class and feeds both the interannual draws (section 5) and the daily noise (section 6).

Construction (`TemperatureCalculator.cs:43-46`): the constructor takes `int seed = -1`. A seed below zero produces `new Random()` (system time seeded, non reproducible). Any seed of zero or above produces `new Random(seed)`, which is fully reproducible. `Reset(int seed = -1)` reconstructs `_rng` the same way and also clears `_yearVariations` and resets `_previousDayVariation` to 0 (`TemperatureCalculator.cs:51-56`), returning the calculator to a fresh deterministic state.

Seed source per scenario: `SimulationRunner`'s constructor forwards its `seed` argument to both `new TemperatureCalculator(seed)` and `new EcosystemSimulator(seed)`, so the temperature stream and the biology stream are seeded from the same per scenario seed (`SimulationRunner.cs:382-387`). The two classes hold separate `Random` instances, so they do not share or interleave a single stream.

The per scenario seed is computed by the controllers, not by the calculator. For a base seed of zero or above, scenario `i` (zero based) uses `RandomSeed + i`; for a base seed below zero it uses `-1` (system time). This rule is identical in the standard path (`SimulationController.cs:180,209`) and the bulk path (`BulkSimulationController.cs:209,259`). The base `RandomSeed` default is 12345 (`SimulationConfig.cs:139`), and the shipped asset keeps 12345 (`SimulationConfig.asset:34`). Because the offset is added per scenario, the N scenarios of one run get N consecutive seeds and therefore N distinct but reproducible temperature traces.

The negativity test is on the base seed. When the base seed is negative, every scenario constructs `new Random()` with no seed (the per scenario value is `-1`, which the calculator constructor maps to `new Random()` per `TemperatureCalculator.cs:45`). Those instances are seeded from system time, so a negative base seed is not reproducible across runs and the scenarios within a single run generally differ from each other as well, because each `new Random()` call reads the clock at a slightly different moment. A base seed of zero is reproducible (zero maps to `new Random(0)`). Reproducibility requires a base seed `>= 0`.

Byte identical reproduction requires the exact .NET Framework `System.Random` implementation, not merely any uniform `[0, 1)` generator seeded with the same integer. `System.Random` uses a specific subtractive pseudo random generator with its own seed scrambling, and `NextDouble()` returns a double in `[0.0, 1.0)` produced by that algorithm. A different language's PRNG (for example a Mersenne Twister, a PCG, or a generator whose `NextDouble()` can return 1.0) seeded with the same integer will produce a different sequence and therefore different temperatures, even though every formula in this document is followed exactly. To reproduce TinySea's numbers outside .NET, port the `System.Random` algorithm itself, do not substitute a local uniform generator.

The TinySea code targets the Mono runtime used by Unity, whose `System.Random` matches the reference .NET Framework `System.Random` (the Knuth subtractive method described in Donald Knuth, The Art of Computer Programming, Volume 2, section 3.2.2). The full algorithm a port must reproduce bit for bit is given below. All arithmetic is on 32 bit signed integers (`int`) unless noted, and overflow wraps in two's complement.

Constants:

| Name | Value | Meaning |
|---|---|---|
| `MBIG` | 2147483647 | `int.MaxValue`, equal to 2^31 - 1. Modulus of the subtractive generator. |
| `MSEED` | 161803398 | Initial seed constant. The leading digits of the golden ratio scaled to 9 digits. |
| `MZ` | 0 | Lower bound sentinel used in the wrap correction. |
| seed array length | 56 | The state vector `SeedArray` has 56 entries; index 0 is unused, indices 1 through 55 hold state. |

Seeding, executed once in the constructor `new Random(seed)` (this is what `TemperatureCalculator.cs:45` and `TemperatureCalculator.cs:53` invoke when `seed >= 0`):

```
int[] SeedArray = new int[56]                 // all zero initially; index 0 stays unused

// Subtract the seed from MSEED, taking absolute value with a guard for int.MinValue.
int subtraction = (seed == int.MinValue) ? int.MaxValue : Math.Abs(seed)
int mj = MSEED - subtraction
SeedArray[55] = mj
int mk = 1

// First pass: fill indices in a fixed non-sequential order (i*21 mod 55).
for (int i = 1; i < 55; i++)
{
    int ii = (21 * i) % 55
    SeedArray[ii] = mk
    mk = mj - mk
    if (mk < 0) mk += MBIG                     // keep non-negative modulo MBIG
    mj = SeedArray[ii]
}

// Second pass: four sweeps that mix every state entry against the one 30 places ahead.
for (int k = 1; k < 5; k++)
{
    for (int i = 1; i < 56; i++)
    {
        SeedArray[i] -= SeedArray[1 + (i + 30) % 55]
        if (SeedArray[i] < 0) SeedArray[i] += MBIG
    }
}

int inext = 0
int inextp = 21
```

`inext`, `inextp`, and the 56 entry `SeedArray` are the entire persistent state of one `System.Random` instance. `inext` starts at 0 and `inextp` starts at 21 in the reference implementation.

Core sample, `InternalSample()`, called once per random number:

```
int InternalSample()
{
    int locINext  = inext
    int locINextp = inextp

    if (++locINext  >= 56) locINext  = 1
    if (++locINextp >= 56) locINextp = 1

    int retVal = SeedArray[locINext] - SeedArray[locINextp]

    if (retVal == MBIG) retVal -= 1            // exclude the value MBIG so the range stays [0, MBIG)
    if (retVal < 0)     retVal += MBIG         // wrap negatives back into [0, MBIG)

    SeedArray[locINext] = retVal

    inext  = locINext
    inextp = locINextp

    return retVal                              // an int in [0, MBIG), i.e. [0, 2147483647)
}
```

Mapping to `[0.0, 1.0)`. The protected `Sample()` method multiplies the integer sample by `1.0 / MBIG`, and the public `NextDouble()` returns `Sample()` directly:

```
double Sample()
{
    return InternalSample() * (1.0 / MBIG)     // 1.0 / 2147483647, computed in double
}

double NextDouble()
{
    return Sample()
}
```

Because `InternalSample()` returns an int in `[0, MBIG)` and the scale factor is `1.0 / MBIG`, `NextDouble()` returns a double in `[0.0, 1.0)`: exactly 0.0 is reachable (when the integer sample is 0) and 1.0 is never reached (the largest integer sample is `MBIG - 1`, giving `(MBIG - 1) / MBIG < 1`). This is the property the interannual draws in section 5 and the daily draws in section 6 rely on. The reciprocal `1.0 / MBIG` is formed once as a double and multiplied, so a port must use that exact factor (not, for example, divide the integer by `MBIG` with a different rounding path or by `MBIG + 1`) to match the low order bits.

A port that implements the three blocks above (seeding, `InternalSample`, `Sample`/`NextDouble`) with 32 bit two's complement integer arithmetic, seeded with the same scenario seed (`RandomSeed + scenarioIndex`), reproduces TinySea's `_rng.NextDouble()` stream exactly. Every `NextDouble()` consumed by the temperature model (one per day for the daily term, plus two on the first day of each new year for the interannual term, in the draw order documented in sections 5 and 6) then yields byte identical temperatures. Only the `seed < 0` path is non reproducible, because `new Random()` seeds from `Environment.TickCount` (system time) rather than a fixed integer, as covered above.

Determinism guarantees that follow from the above:

- Two runs with the same parameters and the same non negative seed produce byte identical temperature sequences, because `System.Random(seed)` is deterministic and the per day RNG consumption is fixed (section 6).
- Pausing a run does not perturb the sequence. The day loop spins while paused without calling `GetTemperature` or advancing any state, so resume is byte identical (`SimulationRunner.cs:417-423`).
- In timeseries mode no RNG is consumed at all (section 7), so the seed is irrelevant to temperature there.

## 9. Day to temperature flow and year numbering

The runner drives the calculator one day at a time inside its main loop (`SimulationRunner.cs:415-448`). For each `dayIndex` from 0 to `TotalDays - 1`:

1. It computes a one based display day `displayDay = dayIndex + 1` and a one based `year = (dayIndex / 365) + 1` for reporting (`SimulationRunner.cs:425-426`).
2. It calls `temp = TempCalc.GetTemperature(dayIndex)` using the zero based index (`SimulationRunner.cs:428`).
3. If biology runs this day it passes `temp` to `Ecosystem.ProcessBiologyStep(temp)` (`SimulationRunner.cs:435`).
4. It records `temp` on the day's `StepRecord.Temperature` (`SimulationRunner.cs:484`).

Note the two day conventions. `GetTemperature`, `GetSeasonalComponent`, `GetClimateTrend`, the year key in `_yearVariations`, and the `currentRandomness` growth all use the zero based `dayIndex`. The reporting `year` and `displayDay` are one based. The static helper `TemperatureCalculator.GetYear(int day)` returns `(day / 365) + 1` and matches the runner's one based reporting convention (`TemperatureCalculator.cs:201-204`). Inside the calculator the year used for memoization and noise growth is the zero based `day / 365` (`TemperatureCalculator.cs:152,177`), so year 0 in the calculator equals display Year 1 in output.

`BiologyStep` does not change the temperature schedule. `GetTemperature` is called for every integer `dayIndex` in `[0, TotalDays-1]` with no gaps, regardless of whether biology runs that day, so the RNG advances every day and the temperature trace is independent of the biology cadence (`SimulationRunner.cs:428-436`). The biology cadence (which days call `ProcessBiologyStep`) is a separate concern and does not affect the temperature or RNG schedule: even on a day where biology is skipped, that day's temperature is still computed and its RNG draws are still consumed. The day loop and `BiologyStep` semantics are covered in `run-scenario-batch.md`.

## 10. Where the temperature parameters appear in output

The calculator's parameters are echoed verbatim into each scenario CSV as `#config:` comment lines, read straight off the live `TempCalc` instance (`SimulationRunner.cs:755-773`): `base_temperature`, `seasonal_amplitude`, `climate_trend_per_year`, `variability_magnitude`, `warming_bias`, `daily_variation_range` (from `BaseRandomness`), `randomness_growth_rate`, `autocorrelated`, `autocorrelation_coefficient` (`SimulationRunner.cs:771`), `temperature_bounds_min`, and `temperature_bounds_max`. The realized daily temperature appears per row, and per scenario `AvgTemperature`, `MinTemperature`, and `MaxTemperature` are summarized into `ScenarioResult` (`SimulationRunner.cs:972-974`). The exact CSV layout is documented in `csv-output-formats.md`; the configuration echo is documented in `configuration-reference.md`.

## 11. Reimplementation checklist

For the concrete parameter values to hard code, use the shipped `SimulationConfig.asset` set listed in Section 2 (canonical for the standard run); the C# field defaults and inspector defaults are fallbacks only. The steps below name the parameters; substitute the canonical values for them.

To reproduce a single day's parametric temperature from scratch:

1. Hold one `System.Random` seeded with the scenario seed (`RandomSeed + scenarioIndex`, or system time if the base seed is negative). For byte identical output this must be the .NET Framework / Unity Mono `System.Random` algorithm, ported exactly per section 8 (MBIG = 2147483647, MSEED = 161803398, the `(21*i) % 55` seed fill, the four mixing sweeps, `InternalSample`, and `NextDouble() = InternalSample() * (1.0 / MBIG)`). A generic uniform generator will be statistically similar but not bit identical.
2. Keep two pieces of state across days: a map from year index to that year's interannual offset, and the previous day's smoothed daily variation (start at 0).
3. For day index `d` (zero based):
   - `seasonal = sin(2*pi*d/365) * SeasonalAmplitude`.
   - `trend = ClimateTrendPerYear * (d / 365.0)`. The `365.0` makes this FLOAT division (continuous), so the trend rises smoothly every day.
   - `interannual`: if disabled, 0; else with `year = floor(d / 365)` (INTEGER division, stepwise), if `year` is unseen draw `cold = (float)(rng.NextDouble() * (-VariabilityMagnitude))`, then `warm = (float)(rng.NextDouble() * VariabilityMagnitude * WarmingBias)`, compute `biasMean = VariabilityMagnitude*(WarmingBias-1f)/4f` in single precision, set the year's offset to `(cold + warm)/2f - biasMean`, cache it; return the cached offset. Draw `cold` before `warm`. Note `* (-VariabilityMagnitude)` is a negative scaling of `NextDouble()`, not a subtraction. Precision matters: `cold`, `warm`, and `biasMean` are all `float`, and the two `NextDouble()` products are cast to `float` BEFORE they are added (section 5). Keeping them as doubles and rounding only at the end changes the low order bits and breaks byte identical reproduction.
   - `daily`: `r = BaseRandomness + RandomnessGrowthRate * floor(d / 365)` (INTEGER year, stepwise growth); `newRandom = (rng.NextDouble()*2 - 1) * r`; if autocorrelation on `variation = prev*AutocorrelationCoefficient + newRandom*(1 - AutocorrelationCoefficient)` (default coefficient 0.7 reproduces the legacy `prev*0.7 + newRandom*0.3`, coefficient 0 = white noise) else `variation = newRandom`; store `variation` as the new `prev`; use `variation`.
   - `T = BaseTemperature + seasonal + trend + interannual + daily`.
   - `T = clamp(T, MinTemp, MaxTemp)`.
4. Preserve draw order across the day: for a day that begins a new year, the interannual draws (cold then warm) happen before the single daily draw within that same `GetTemperature` call, because `GetInterannualVariation` is summed before `GetDailyVariation` (`TemperatureCalculator.cs:77-81`).

For timeseries mode, skip steps 1 through 3 entirely and return `clamp(series[d % series.Count], MinTemp, MaxTemp)`, consuming no RNG.
