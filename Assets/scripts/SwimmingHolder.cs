using UnityEngine;
using System.Collections.Generic;

public class SwimmingHolder : MonoBehaviour {

    public PlayerManager player;
    public List<List<SwimmingCreature>> creatures;
    public List<Lure> lures;

    public Queue<SwimmingCreature.DeathCause> deathList;
    public Queue<SwimmingCreature.SpawnCause> spawnList;

    void Start()
    {
        creatures = new List<List<SwimmingCreature>>();
    }

    public void ReproduceOrDie(float days, List<SwimmingCreature> species)
    {
        float speciesAmount = species.Count;
        
        if (speciesAmount == 0)
        {
            return;
        }
        SwimmingCreature c = species[0];

        if (c.GetFinalPerformance() < c.deathThreshold)
        {
            float deaths = speciesAmount * c.deathRate * days;
            deaths = Mathf.Max(c.minimumDeaths, deaths);
            speciesAmount -= deaths;
            speciesAmount = Mathf.Max(0, speciesAmount);

            if (c.fedRate < c.deathThreshold)
            {
                for (int i = 0; i < deaths; i++)
                {
                    deathList.Enqueue(SwimmingCreature.DeathCause.Hunger);
                }
            }
            else
            {
                if (c.lastTemperature + 273 < c.thermalCurve.optimalTemp)
                {
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(SwimmingCreature.DeathCause.Cold);
                    }
                }
                else
                {
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(SwimmingCreature.DeathCause.Heat);
                    }
                }
            }
        }
        else
        {
            float reproduced = speciesAmount * c.GetFinalPerformance() * c.reproductionMultiplier * days;
            speciesAmount += reproduced;
            for (int i = 0; i < reproduced - .9f; i++)
            {
                spawnList.Enqueue(SwimmingCreature.SpawnCause.Reproduction);
            }
        }

        if (speciesAmount < 1)
        {
            speciesAmount = 0;
        }
    }
}
