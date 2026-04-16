using System;
using System.Collections.Generic;

/// <summary>
/// Calculates daily temperature with realistic climate components.
/// 
/// Formula: T(day) = Base + Seasonal + Trend + InterannualVar + DailyVar
/// Then clamped to bounds.
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
    /// Interannual: each year gets a random offset (with warm bias)
    /// Same value for entire year
    /// </summary>
    private float GetInterannualVariation(int day)
    {
        if (!UseInterannualVariation) return 0f;

        int year = day / DAYS_PER_YEAR;

        if (!_yearVariations.ContainsKey(year))
        {
            // Generate this year's variation
            float coldPart = (float)(_rng.NextDouble() * -VariabilityMagnitude);
            float warmPart = (float)(_rng.NextDouble() * VariabilityMagnitude * WarmingBias);
            _yearVariations[year] = (coldPart + warmPart) / 2f;
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
