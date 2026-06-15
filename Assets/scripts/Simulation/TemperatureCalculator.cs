using System;
using System.Collections.Generic;

/// <summary>
/// Calculates daily temperature with realistic climate components.
///
/// Formula: T(day) = Base + Seasonal + Trend + InterannualVar + DailyVar
/// Then clamped to bounds.
///
/// Trend ownership: ClimateTrendPerYear is the only component that contributes
/// a non-zero long-term mean. Seasonal averages to 0 over a year. Daily and
/// interannual variations are zero-mean by construction (see GetInterannualVariation
/// for the WarmingBias-vs-mean separation).
/// </summary>
public class TemperatureCalculator
{
    // Configuration (with defaults)
    public float BaseTemperature = 20f;           // Starting temperature in °C
    public float SeasonalAmplitude = 5f;          // ±5°C seasonal swing
    public float ClimateTrendPerYear = 1f;        // +1°C warming per year
    public float VariabilityMagnitude = 2f;       // Year-to-year variation range
    public float WarmingBias = 1.5f;              // Warm years more likely
    public float BaseRandomness = 5f;             // Daily random range
    public float RandomnessGrowthRate = 0.5f;     // Daily randomness increases per year
    public bool UseInterannualVariation = true;    // Year-to-year variation on/off
    public bool UseAutocorrelation = true;        // Smooth weather transitions
    public float AutocorrelationCoefficient = 0.7f; // AR(1) phi when UseAutocorrelation is on (0 = white noise)
    public float MinTemp = -5f;                   // Hard floor
    public float MaxTemp = 40f;                   // Hard ceiling

    public const int DAYS_PER_YEAR = 365;

    // State
    private Random _rng;
    private Dictionary<int, float> _yearVariations = new Dictionary<int, float>();
    private float _previousDayVariation = 0f;

    // Batch 3: optional environmental temperature timeseries (one value per day, °C).
    // When loaded, GetTemperature returns the series value for the day (looping if the
    // series is shorter than the run) instead of the parametric 5-component model.
    private List<float> _timeseries = null;
    private bool _timeseriesLoopWarned = false;

    public TemperatureCalculator(int seed = -1)
    {
        _rng = (seed < 0) ? new Random() : new Random(seed);
    }

    /// <summary>
    /// Reset for a new simulation run
    /// </summary>
    public void Reset(int seed = -1)
    {
        _rng = (seed < 0) ? new Random() : new Random(seed);
        _yearVariations.Clear();
        _previousDayVariation = 0f;
    }

    /// <summary>
    /// Get temperature for a specific day
    /// </summary>
    public float GetTemperature(int day)
    {
        // Batch 3: if a temperature timeseries is loaded, use it (looping if shorter than
        // the run). Per-species temp_offset is still applied downstream in the thermal
        // calc, so each species' experienced temperature shifts as before.
        if (_timeseries != null)
        {
            if (day >= _timeseries.Count && !_timeseriesLoopWarned)
            {
                UnityEngine.Debug.LogWarning(
                    $"TemperatureCalculator: timeseries ({_timeseries.Count} days) is shorter than the run; looping.");
                _timeseriesLoopWarned = true;
            }
            return Math.Max(MinTemp, Math.Min(MaxTemp, _timeseries[day % _timeseries.Count]));
        }

        float temp = BaseTemperature
                   + GetSeasonalComponent(day)
                   + GetClimateTrend(day)
                   + GetInterannualVariation(day)
                   + GetDailyVariation(day);

        // Clamp to bounds
        return Math.Max(MinTemp, Math.Min(MaxTemp, temp));
    }

    /// <summary>
    /// Batch 3: load a daily temperature timeseries (°C, index = day). When non-empty it
    /// overrides the parametric model in GetTemperature. Pass null/empty to clear.
    /// </summary>
    public void LoadTimeseries(List<float> dailyTempsC)
    {
        _timeseries = (dailyTempsC != null && dailyTempsC.Count > 0) ? dailyTempsC : null;
        _timeseriesLoopWarned = false;
    }

    public bool HasTimeseries => _timeseries != null;

    /// <summary>
    /// Batch 3: parse a "Day,Temperature_C" CSV (header optional) into a per-day list.
    /// Rows are read in order; the Day column is informational. Blank/comment (#) lines
    /// and unparseable rows (e.g. the header) are skipped. Returns null if no numeric data.
    /// </summary>
    public static List<float> ParseTimeseriesCsv(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var temps = new List<float>();
        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(',');
            // Temperature is the last column (handles "Day,Temperature_C" and bare "Temperature_C").
            string cell = parts[parts.Length - 1].Trim();
            if (float.TryParse(cell, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float t))
                temps.Add(t);
        }
        return temps.Count > 0 ? temps : null;
    }

    /// <summary>
    /// Seasonal: sin wave, coldest at day 0, warmest at day 182
    /// </summary>
    private float GetSeasonalComponent(int day)
    {
        return (float)(Math.Sin(2.0 * Math.PI * day / DAYS_PER_YEAR) * SeasonalAmplitude);
    }

    /// <summary>
    /// Climate trend: linear warming over time
    /// </summary>
    private float GetClimateTrend(int day)
    {
        float years = day / (float)DAYS_PER_YEAR;
        return ClimateTrendPerYear * years;
    }

    /// <summary>
    /// Interannual: each year gets a zero-mean random offset that may be
    /// asymmetric (warm tail wider than cold tail when WarmingBias > 1).
    /// Same value for the entire year.
    ///
    /// WarmingBias controls the SHAPE of the distribution only, not its mean.
    /// Long-term warming/cooling trends are expressed via ClimateTrendPerYear.
    /// </summary>
    private float GetInterannualVariation(int day)
    {
        if (!UseInterannualVariation) return 0f;

        int year = day / DAYS_PER_YEAR;

        if (!_yearVariations.ContainsKey(year))
        {
            // Draw cold ~ uniform(-mag, 0) and warm ~ uniform(0, mag * bias),
            // then average. The naive (cold + warm) / 2 has expected value
            //     mag * (bias - 1) / 4
            // which would leak a hidden warming trend (~0.25 °C/yr at
            // mag = 2, bias = 1.5) on top of ClimateTrendPerYear. Subtract
            // that mean so WarmingBias only skews the *shape* of the
            // distribution; the trend is owned solely by ClimateTrendPerYear.
            float coldPart = (float)(_rng.NextDouble() * -VariabilityMagnitude);
            float warmPart = (float)(_rng.NextDouble() * VariabilityMagnitude * WarmingBias);
            float biasMean = VariabilityMagnitude * (WarmingBias - 1f) / 4f;
            _yearVariations[year] = (coldPart + warmPart) / 2f - biasMean;
        }

        return _yearVariations[year];
    }

    /// <summary>
    /// Daily variation: random noise, optionally smoothed with autocorrelation
    /// </summary>
    private float GetDailyVariation(int day)
    {
        int year = day / DAYS_PER_YEAR;
        float currentRandomness = BaseRandomness + (RandomnessGrowthRate * year);

        // Generate new random value
        float newRandom = (float)((_rng.NextDouble() * 2 - 1) * currentRandomness);

        float variation;
        if (UseAutocorrelation)
        {
            // AR(1): coeff*yesterday + (1-coeff)*new. Default coeff 0.7 reproduces the legacy 0.7/0.3 blend.
            variation = _previousDayVariation * AutocorrelationCoefficient + newRandom * (1f - AutocorrelationCoefficient);
        }
        else
        {
            variation = newRandom;
        }

        _previousDayVariation = variation;
        return variation;
    }

    /// <summary>
    /// Get year number from day (Year 1 = days 0-364)
    /// </summary>
    public static int GetYear(int day)
    {
        return (day / DAYS_PER_YEAR) + 1;
    }
}
