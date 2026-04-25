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
    public float MinTemp = -5f;                   // Hard floor
    public float MaxTemp = 40f;                   // Hard ceiling

    public const int DAYS_PER_YEAR = 365;

    // State
    private Random _rng;
    private Dictionary<int, float> _yearVariations = new Dictionary<int, float>();
    private float _previousDayVariation = 0f;

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
        float temp = BaseTemperature
                   + GetSeasonalComponent(day)
                   + GetClimateTrend(day)
                   + GetInterannualVariation(day)
                   + GetDailyVariation(day);

        // Clamp to bounds
        return Math.Max(MinTemp, Math.Min(MaxTemp, temp));
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
            // 70% yesterday + 30% new = smooth transitions
            variation = _previousDayVariation * 0.7f + newRandom * 0.3f;
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
