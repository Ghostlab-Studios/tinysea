using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Events;

public class PlayerManager : MonoBehaviour {

	/*
	 *  Player's species
     *  Make sure that these are in the same order as the SwimmingHolder's prefabs!
	 */
	public List<CharacterManager> species;
    public SwimmingHolder holder;

    //Highest level in the species list
    public int lowestLevel = 1;
    public int highestLevel = 3;
    public Temperature temperature;
	/*
	 *  Player's data
	 */

	public float days = 0;
	public float moneys = 0;
    public float dayStep = 1;
    public float dailyIncome = 100;
    public float netWorthIncomeRate = .2f;
    public float sellRate = .5f;
    public float maxFishes = 200;
    public float stepWaitTime = 1;
    //float stepTimer = 0;
    bool waitingForTemperature = false;
    bool waitingForSteps = false;
    enum FishSteps { predation, reproduce, capMax, finished};
    FishSteps currentStep = FishSteps.finished;
    int predationPhase = 2;
    int highestPredator = 3;
    [HideInInspector]
    public bool busy = false;

    //gameobjects
    public Text moneyText;

    public RectTransform tier1EcoBar;
    public RectTransform tier2EcoBar;
    public RectTransform tier3EcoBar;

    public RectTransform tier1Glow;
    public RectTransform tier2Glow;
    public RectTransform tier3Glow;
    public RectTransform fullGlow;

    public ParticleSystem tooCoolPart;
    public ParticleSystem tooHotPart;
    public ParticleSystem eatenPart;
    public ParticleSystem starvedPart;
    public ParticleSystem reproducePart;

    /*
	 * Initialize with three species at each level
	 */
    void Awake(){
        species = new List<CharacterManager>(GetComponentsInChildren<CharacterManager>());
        foreach (CharacterManager c in species)
        {
            c.player = this;
            //Debug.Log(c.uniqueName);
        }
        holder = FindObjectOfType<SwimmingHolder>();
	}

    public void nextTurn()
    {
        if(!busy && !holder.anyCreaturesBusy())
        {
            waitingForTemperature = true;
            busy = true;
            temperature.updateTemperature();
            temperature.updateDay();
        }
    }

    public void Update()
    {
        if (Input.GetButtonDown("Submit"))
        {
            nextTurn();
        }
        if (!temperature.animating && waitingForTemperature)
        {
            waitingForTemperature = false;
            currentStep = FishSteps.predation;
            predationPhase = 2;
            //stepTimer = 0;
            waitingForSteps = true;

            moneys += dailyIncome;
            foreach (CharacterManager c in species)
            {
                moneys += c.cost * c.speciesAmount * netWorthIncomeRate;
            }

        }
        if (waitingForSteps)
        {
            //Debug.Log(currentStep);
            if (!holder.anyCreaturesBusy())
            {
                switch (currentStep)
                {
                    case FishSteps.predation:
                        //calculate performance rates (based on temperature and food supply)
                        foreach (CharacterManager c in species)
                        {
                            c.updatePerformance(temperature.temperature);
                        }
                        //bigger creatures eat the smaller ones
                        Predation(dayStep, predationPhase);
                        predationPhase++;
                        if (predationPhase > highestPredator)
                        {
                            currentStep = FishSteps.reproduce;
                        }
                        break;
                    case FishSteps.reproduce:
                        //creatures reproduce or die depending on performance rate
                        foreach (CharacterManager c in species)
                        {
                            c.ReproduceOrDie(dayStep);
                        }
                        currentStep = FishSteps.capMax;
                        //stepTimer = stepWaitTime;
                        break;
                    case FishSteps.capMax:
                        capFishMax();
                        currentStep = FishSteps.finished;
                        break;
                    case FishSteps.finished:
                        busy = false;
                        waitingForSteps = false;
                        break;
                }
            }
        }
        
        if (/*Input.GetButtonDown("Cancel") && Input.GetButton("Submit")*/ Input.GetKeyDown("q"))
        {
            moneys += 999999;
        }

        //set ecosystem pyramid.
        float totalFishes = getTotalFishCount();
        if (totalFishes <= 0)
        {
            tier1EcoBar.localScale = new Vector3(0, 1, 1);
            tier2EcoBar.localScale = new Vector3(0, 1, 1);
            tier3EcoBar.localScale = new Vector3(0, 1, 1);
        }
        if (totalFishes > 0)
        {
            float t1Proportion = getTotalAmountAtLevel(1) / totalFishes;
            float t2Proportion = (getTotalAmountAtLevel(2) * 3) / totalFishes;
            float t3Proportion = (getTotalAmountAtLevel(3) * 9) / totalFishes;

            Vector3 tiersProp = new Vector3(t1Proportion, t2Proportion, t3Proportion) / 
                                Mathf.Max(t1Proportion, t2Proportion, t3Proportion);

            tier1EcoBar.localScale = new Vector3(tiersProp.x, 1, 1);
            tier2EcoBar.localScale = new Vector3(tiersProp.y, 1, 1);
            tier3EcoBar.localScale = new Vector3(tiersProp.z, 1, 1);

            if (tiersProp.x == 1 && tiersProp.y == 1 && tiersProp.z == 1)
            {
                tier1Glow.gameObject.SetActive(false);
                tier2Glow.gameObject.SetActive(false);
                tier3Glow.gameObject.SetActive(false);
                fullGlow.gameObject.SetActive(true);
            }

            if (tiersProp.x == 1)
            {
                tier1Glow.gameObject.SetActive(true);
            }
            else
            {
                tier1Glow.gameObject.SetActive(false);
            }

            if (tiersProp.y == 1)
            {
                tier2Glow.gameObject.SetActive(true);
            }
            else
            {
                tier2Glow.gameObject.SetActive(false);
            }

            if (tiersProp.z == 1)
            {
                tier3Glow.gameObject.SetActive(true);
            }
            else
            {
                tier3Glow.gameObject.SetActive(false);
            }
        }

        moneyText.text = "$" + Mathf.Floor(moneys).ToString();
    }

    public void BuyCreatures(int index, float amount, bool money_access = true)
    {
        if(money_access)
            moneys = moneys - species[index].cost * amount;
        for (int i = 0; i < amount; i++)
        {
            species[index].birthList.Enqueue(CharacterManager.BirthCause.Bought);
        }
        species[index].speciesAmount += amount;
    }

    public void SellCreatures(int index, float amount)
    {
        for (int i = 0; i < amount; i++)
        {
            species[index].deathList.Enqueue(CharacterManager.DeathCause.Sold);
        }
        species[index].speciesAmount -= amount;
        moneys = moneys + species[index].cost * sellRate * amount;
    }

    private void capFishMax()
    {
        //fish out any excess fish
        float totalFish = getTotalFishCount();
        if (totalFish > maxFishes)
        {
            int fishToSell = Mathf.CeilToInt(totalFish - maxFishes);
            moneys += fishToSell * sellRate;
            for (int i = 0; i < fishToSell; i++)
            {
                //randomly pick a fish
                float fish = Random.Range(0, Mathf.FloorToInt(getTotalFishCount()));
                //step through character managers until we find the fish that we want to remove.
                foreach (CharacterManager c in species)
                {
                    if (fish <= c.speciesAmount)
                    {
                        c.speciesAmount--;
                        c.deathList.Enqueue(CharacterManager.DeathCause.Sold);
                        break;
                    }
                    fish -= c.speciesAmount;
                }
            }
        }
    }

    //returns the total amount of all fish
    public float getTotalFishCount()
    {
        float totalFish = 0;
        foreach (CharacterManager c in species)
        {
            totalFish += c.speciesAmount;
        }
        return totalFish;
    }

    //gets the total amount of all species for a given level
    public float getTotalAmountAtLevel(int level)
    {
        float amount = 0;
        foreach (CharacterManager c in species)
        {
            if (c.foodChainLevel == level)
            {
                amount += c.speciesAmount;
            }
        }
        return amount;
    }

    //returns a list of all the creatures for a given level
    private List<CharacterManager> getCharactersAtLevel(int level)
    {
        List<CharacterManager> list = new List<CharacterManager>();
        foreach (CharacterManager c in species)
        {
            if (c.foodChainLevel == level)
            {
                list.Add(c);
            }
        }
        return list;
    }

    //eats a certain amount of creatures sampled evenly within that level
    //negative things will happen if eatAmount is > the amount of creatures
    //^^^^^^^^ pun intended.
    private void eatAtLevel(int level, float eatAmount)
    {
        //make a list of all creatures in this level
        float totalAmount = getTotalAmountAtLevel(level);
        List<CharacterManager> eatenCreatures = getCharactersAtLevel(level);
        //eat them... EAT THEM!!! 
        if (totalAmount == 0)
            return;
        foreach (CharacterManager c in eatenCreatures)
        {
            float ratio = c.speciesAmount / totalAmount;
            float eatenFish = ratio * eatAmount;
            c.speciesAmount = c.speciesAmount - eatenFish;
            for (int i = 0; i < eatenFish - .9f; i++)
            {
                c.deathList.Enqueue(CharacterManager.DeathCause.Eaten);
            }
        }
    }

    // Food obtain
    private void Predation(float dayStep, int tier)
    {
        //step through each species level, skipping the first one (they don't eat anything)
        List<CharacterManager> leveliCharacters = new List<CharacterManager>();
        float leveliAmount = 0;
        float foodRequestAmount = 0;
        foreach (CharacterManager c in species)
        {
            if (c.foodChainLevel == tier)
            {
                leveliCharacters.Add(c);
                leveliAmount += c.speciesAmount;
                foodRequestAmount += c.speciesAmount * c.GetEatingRate() * dayStep;
            }
        }
            
        //if we don't have enough food to go around
        if (foodRequestAmount > getTotalAmountAtLevel(tier - 1))
        {
            //set fed performance to the reduced amount. 
            float subOptimalFoodRate = getTotalAmountAtLevel(tier - 1) / foodRequestAmount;
            foreach (CharacterManager c in leveliCharacters)
            {
                c.fedRate = subOptimalFoodRate;
            }
            eatAtLevel(tier - 1, getTotalAmountAtLevel(tier - 1));
        }
        else
        {
            foreach (CharacterManager c in leveliCharacters)
            {
                c.fedRate = 1;
            }
            eatAtLevel(tier - 1, foodRequestAmount);
        }
    }

}
