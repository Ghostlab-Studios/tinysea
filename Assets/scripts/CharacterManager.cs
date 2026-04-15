using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// LEGACY GAME CODE — Not used by the simulation.
//
// This is the interactive game's per-species manager (MonoBehaviour, one per
// species GameObject). It holds population, thermal curve reference, biology
// stats, and handles reproduction/death with visual feedback (death/birth
// queues drive SwimmingHolder animations).
//
// The simulation splits this into two separate concerns:
//   - Species data:    SimSpecies.cs (plain C# class, no MonoBehaviour)
//   - Biology logic:   EcosystemSimulator.cs (10-step sequence with
//                      accumulators, condition system, natural death —
//                      more detailed than the game's ReproduceOrDie)
//
// Key differences from simulation:
//   - Game uses integer-ish populations with floor/ceil rounding inline;
//     simulation uses float populations with explicit rounding at step 10.
//   - Game has no condition system or natural death rate.
//   - Game tracks death/birth causes via queues for animations;
//     simulation tracks them via numeric accumulators for CSV output.
//
// Simulation equivalents:
//   Assets/scripts/Simulation/SimSpecies.cs
//   Assets/scripts/Simulation/EcosystemSimulator.cs
// ============================================================================
public class CharacterManager : MonoBehaviour {

    #region Species Data — shared biology concept, see SimSpecies for simulation equivalent
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
    public float reproThreshold = .25f; // Only reproduce if above this threshold
    #endregion

    #region Game UI & Animation — game only, not used in simulation
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
    #endregion

    #region Biology Logic — shared concept, see EcosystemSimulator for simulation equivalent
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
            speciesAmount -= deaths;
            speciesAmount = Mathf.Max(0, speciesAmount);
            Debug.Log("Deaths: " + deaths);
            //figure out why we're dying (starve, too hot, or too cool)
            if (fedRate < deathThreashold)
            {
                Debug.Log("died from starvation");
                SessionRecorder.instance.WriteToSessionDataWithRound(",Death by Starve - " + GetSessionRecorderText() + "," + deaths.ToString());
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
                    SessionRecorder.instance.WriteToSessionDataWithRound(",Death by Freeze - " + GetSessionRecorderText() + "," + deaths.ToString());
                    for (int i = 0; i < deaths; i++)
                    {
                        deathList.Enqueue(DeathCause.Cold);
                    }
                }
                else
                {
                    Debug.Log("died from heat");
                    SessionRecorder.instance.WriteToSessionDataWithRound(",Death by Heat - " + GetSessionRecorderText() + "," + deaths.ToString());
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
    #endregion

    #region Session Recording — game only, not used in simulation
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
    #endregion
}
