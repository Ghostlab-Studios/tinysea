using System.Runtime.ConstrainedExecution;
using UnityEngine;

[CreateAssetMenu(fileName = "SimulationConfig", menuName = "TinySea/Simulation Config")]
public class SimulationConfig : ScriptableObject
{
    [Header("Base Parameters")]
    public float BaseTemperature = 20f;

    [Header("Seasonal")]
    public float SeasonalAmplitude = 10f;

    [Header("Climate")]
    public float ClimateTrend = 1f;
    public bool InterannualVariation = true;
    public float WarmBias = 1f;

    [Header("Interannual Variation")]
    public float variabilityMagnitude = 1f;
    public float warmingBias = 1f;

    [Header("Daily Variation")]
    public bool Autocorrelated = true;
    public float RandomRangeMin = -5f;
    public float RandomRangeMax = 5f;
    public float randomnessGrowthRate = 0.5f;

    [Header("Temperature Bounds")]
    public float TempratureBoundsMin = -5f;
    public float TempratureBoundsMax = 50f;


    [Header("Species Data")]
    public SpeciesDatabase Database;

    [Header("Simulation Parameters")]
    public int RunsPerScenario = 10;




}
