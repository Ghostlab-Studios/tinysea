using UnityEngine;
using System.Collections.Generic;

public class SwimmingCreature : MonoBehaviour {

    public int tier;
    public int id;
    public string creatureName;
    public int cost;
    public int eatingStars;
    public int reproductionStars;
    public string description;

    public enum SpawnCause { Reproduction, Bought };
    public enum DeathCause { Cold, Heat, Hunger, Eaten, Sold };
    public SpawnCause spawnCause;
    public DeathCause deathCause;

    private Animator animator;

    public float performanceRate;
    public float eatingAmount;
    public float fedRate;
    public float reproductionMultiplier;
    public float deathThreshold;
    public float deathRate;
    public float minimumDeaths;
    public float lastTemperature;

    public ThermalCurve thermalCurve;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void UpdatePerformance(float temperature)
    {
        lastTemperature = temperature;
        performanceRate = thermalCurve.getCurve(temperature + 273);
    }

    public float GetEatingRate()
    {
        return eatingAmount * performanceRate;
    }

    public float GetFinalPerformance()
    {
        return fedRate * performanceRate;
    }


}
