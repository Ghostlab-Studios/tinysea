using UnityEngine;
using System.Collections.Generic;

public class CharacterManager : MonoBehaviour {

    //species stats (these change during gameplay)
	public float speciesAmount = 0;
	public float performanceRate = 1;
    public float fedRate = 1;
    public float cost = 100;

    //object references
	public ThermalCurve thermalcurve;
	public PlayerManager player;
	/*
	 *  Species Properties
	 */
	public string uniqueName = "Nameless"; //a unique name for each species
    public float eatingAmount = 3; //the amount of fish I need to eat every day
    public int foodChainLevel = 1; //how high I am in the food chain (1 == bottom)
    public float reproductionMultiplier = .5f; //how many babies I have every day

    public float deathThreashold = .3f; //if performance gets too low, you start dying
    public float deathRate = .5f; //if I'm dying, population drops by this ratio every day
    public float minimumDeaths = 1; //if I'm dying, I will always lose at least this many fish!

    public int eatingStars = 5;
    public int reproductionStars = 5;
    public int deathThreasholdStars = 5;
    public int deathRateStars = 5;
    public int thermalBreadthStars = 5;
    public string description = "This is a fish";

    public enum DeathCause { Cold, Hot, Starve, Eaten, Sold};
    
    public Queue<DeathCause> deathList = new Queue<DeathCause>();

    public enum BirthCause { Reproduction, Bought};
    
    public Queue<BirthCause> birthList = new Queue<BirthCause>();

    private float lastTemp = 0;

    public void updatePerformance(float temperature)
    {
        lastTemp = temperature;
        performanceRate = thermalcurve.getCurve(temperature + 273);
    }
    
	public float GetEatingRate(){
		return eatingAmount * performanceRate;
	}

    public float getFinalPerformance()
    {
        return fedRate * performanceRate;
    }

    //fish reproduce or die based on performance.
    public void ReproduceOrDie(float days)
    {
        if (speciesAmount == 0)
        {
            return;
        }

        Debug.Log("reproducing or dying: " + speciesAmount + " of " + uniqueName);
        //if performance is too low, fish die
        if (getFinalPerformance() < deathThreashold)
        {
            float deaths = speciesAmount * deathRate * days;
            deaths = Mathf.Max(minimumDeaths, deaths);
            speciesAmount -= deaths;
            speciesAmount = Mathf.Max(0, speciesAmount);
            Debug.Log("Deaths: " + deaths);
            //figure out why we're dying (starve, too hot, or too cool)
            if (fedRate < deathThreashold)
            {
                Debug.Log("died from starvation");
                for (int i = 0; i < deaths; i++)
                {
                    deathList.Enqueue(DeathCause.Starve);
                }
            }
            else
            {
                if (lastTemp + 273 < thermalcurve.optimalTemp)
                {
                    Debug.Log("died from cold");
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(DeathCause.Cold);
                    }
                }
                else
                {
                    Debug.Log("died from heat");
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(DeathCause.Hot);
                    }
                }
            }
        }
        else if (player.getTotalFishCount() >= player.maxFishes - 1)
        {
            Debug.Log("Max fishes, not reproducing");
            return;
        }
        else //otherwise, fish reproduce
        {
            float reproduced = speciesAmount * getFinalPerformance() * 
                (reproductionMultiplier) * days;
            speciesAmount = speciesAmount + reproduced;
            Debug.Log("reproduced: " + reproduced);
            for(int i = 0; i < reproduced - .9f; i++) {
                birthList.Enqueue(BirthCause.Reproduction);
            }
        }

        if (speciesAmount < 1)
        {
            speciesAmount = 0;
            //clear out any straggler fish
            deathList.Enqueue(DeathCause.Starve);
            deathList.Enqueue(DeathCause.Starve);
            deathList.Enqueue(DeathCause.Starve);
            deathList.Enqueue(DeathCause.Starve);
        }
    }



	
}
