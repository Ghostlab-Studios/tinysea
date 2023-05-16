using UnityEngine;
using System.Collections.Generic;

public class CharacterManager : MonoBehaviour {

    //species stats (these change during gameplay)
	public float speciesAmount = 0;
	public float performanceRate = 1;
    public float fedRate = 1;
    public float cost = 100;
    public LevelManager.Variant variant;

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
    public float reproThreshold = .25f; // Only reproduce if above this threshold

    public int eatingStars = 5;
    public int reproductionStars = 5;
    public int deathThreasholdStars = 5;
    public int deathRateStars = 5;
    public int thermalBreadthStars = 5;
    public string description = "This is a fish";
    public string temperatureThresholdText;
    public string reproductionRateText;
    public Sprite icon;

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
                SessionRecorder.instance.WriteToSessionDataWithRound(",Death - " + GetSessionRecorderText() + " Starvation," + deaths.ToString());
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
                    SessionRecorder.instance.WriteToSessionDataWithRound(",Death - " + GetSessionRecorderText() + " Freeze," + deaths.ToString());
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(DeathCause.Cold);
                    }
                }
                else
                {
                    Debug.Log("died from heat");
                    SessionRecorder.instance.WriteToSessionDataWithRound(",Death - " + GetSessionRecorderText() + " Heat," + deaths.ToString());
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(DeathCause.Hot);
                    }
                }
            }
        }
        else if (player.getTotalFishCount() >= player.maxFishes)
        {
            Debug.Log("Max fishes, not reproducing");
            return;
        }
        else if (getFinalPerformance() < reproThreshold)
        {
            Debug.Log("Not higher than the reproduction threshold");
            return;
        }
        else if (speciesAmount < 2)
        {
            Debug.Log("Need two fish to reproduce");
            return;
        }
        else 
        {
            float fishToMax = (player.maxFishes) - player.getTotalFishCount();
            float reproReduction = foodChainLevel == 1 && !HasCreaturesOfTier(2) ? 0.85f : 1f;
            float reproduced = speciesAmount * getFinalPerformance() * reproductionMultiplier * reproReduction * days;
            if (reproduced > fishToMax)
            {
                reproduced = fishToMax;
            }
            speciesAmount = speciesAmount + reproduced;
            Debug.Log("reproduced : " + reproduced);
            SessionRecorder.instance.WriteToSessionDataWithRound(",Reproduction - " + GetSessionRecorderText() + "," + reproduced.ToString());
            for (int i = 0; i < reproduced -.9f; i++)
            {
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

    public string GetSessionRecorderText()
    {
        return "Tier " + foodChainLevel + " " + variant.ToString() + " " + GetGeneralistOrSpecialistType();
    }

    private string GetGeneralistOrSpecialistType()
    {
        string textToReturn = "ERR";
        switch (reproductionRateText)
        {
            case "Low":
                textToReturn = "Generalist";
                break;
            case "Average":
                textToReturn = "Average Species";
                break;
            case "High":
                textToReturn = "Specialist";
                break;
        }
        return textToReturn;
    }

    private bool HasCreaturesOfTier(int tier)
    {
        foreach (CharacterManager c in player.species)
        {
            return c.foodChainLevel == tier && c.speciesAmount > 0;
        }
        return false;
    }
}
