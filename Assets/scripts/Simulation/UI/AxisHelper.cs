using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility for computing nice axis tick values and formatting labels.
/// </summary>
public static class AxisHelper
{
    /// <summary>
    /// Compute clean tick positions for a numeric axis range.
    /// Returns values at multiples of 1, 2, 5, 10, 20, 50, etc.
    /// </summary>
    public static float[] ComputeNiceTicks(float rangeMin, float rangeMax, int targetCount = 5)
    {
        if (rangeMax <= rangeMin || targetCount <= 0)
            return new float[0];

        float range = rangeMax - rangeMin;
        float rawInterval = range / targetCount;

        // Find order of magnitude
        float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rawInterval)));
        float normalized = rawInterval / magnitude;

        // Snap to nice value
        float niceInterval;
        if (normalized <= 1.5f) niceInterval = 1f * magnitude;
        else if (normalized <= 3.5f) niceInterval = 2f * magnitude;
        else if (normalized <= 7.5f) niceInterval = 5f * magnitude;
        else niceInterval = 10f * magnitude;

        // Generate ticks
        var ticks = new List<float>();
        float start = Mathf.Ceil(rangeMin / niceInterval) * niceInterval;

        for (float v = start; v <= rangeMax + niceInterval * 0.01f; v += niceInterval)
        {
            if (v >= rangeMin - niceInterval * 0.01f && v <= rangeMax + niceInterval * 0.01f)
            {
                // Round to avoid floating point artifacts
                float rounded = Mathf.Round(v / (niceInterval * 0.1f)) * (niceInterval * 0.1f);
                ticks.Add(rounded);
            }

            // Safety: don't produce too many ticks
            if (ticks.Count > 10) break;
        }

        return ticks.ToArray();
    }

    /// <summary>
    /// Format a temperature value for display.
    /// Integers shown as "10", fractional as "2.5".
    /// </summary>
    public static string FormatTemp(float tempC)
    {
        if (Mathf.Approximately(tempC, Mathf.Round(tempC)))
            return Mathf.RoundToInt(tempC).ToString();
        return tempC.ToString("F1");
    }

    /// <summary>
    /// Format a performance value (0-1) for axis display.
    /// </summary>
    public static string FormatPerformance(float perf)
    {
        if (perf <= 0.001f) return "0.0";
        return perf.ToString("F1");
    }
}
