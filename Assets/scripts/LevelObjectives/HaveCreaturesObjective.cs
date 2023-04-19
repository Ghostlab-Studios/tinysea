using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generalized class for having a certain number of creatures to complete the objective.
/// </summary>
public class HaveCreaturesObjective : MonoBehaviour, ILevelEvent
{
    public int ID;
    public int targetCreatureCount;
    public LevelManager.Tier[] activeTiers;
    public LevelManager.Variant[] activeVariants;
    public bool achieveInNumRounds = false;
    public int numRounds;

    private PlayerManager playerManager;
    private bool eventRunning = false;
    private int organismTotal;
    private int currRound;

    void Awake()
    {
        InitializeEvent();
    }
    
    public void InitializeEvent()
    {
        playerManager = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
        GetComponent<LevelManager>().levelGoals.Add(this);
        //Button nextTurnButton = GameObject.FindGameObjectWithTag("NextTurnButton").GetComponent<Button>();
        //nextTurnButton.onClick.AddListener(OnNextTurnPressed);
        playerManager.onNextTurn.AddListener(OnNextTurnPressed);
    }
    
    /// <summary>
    /// Returns true if the amount of creatures within a given tier is greater than or equal to the
    /// current amount of creatures of that tier.
    /// </summary>
    public bool IsEventComplete()
    {
        if (!eventRunning) { eventRunning = true; }
        int totalCreatures = 0;
        foreach (CharacterManager cm in playerManager.species)
        {
            if (activeTiers.Length > 0)
            {
                foreach (LevelManager.Tier tier in activeTiers)
                {
                    totalCreatures += IncrementByTier(tier, cm);
                }
            }
            else if (IsCorrectVariant(cm))
            {
                totalCreatures += (int)cm.speciesAmount;
            }
        }
        organismTotal = totalCreatures;
        bool eventComplete = totalCreatures >= targetCreatureCount;
        if (achieveInNumRounds) { eventComplete = eventComplete && currRound >= numRounds; }
        if (eventComplete) { eventRunning = false; }
        return eventComplete;
    }

    private int IncrementByTier(LevelManager.Tier tier, CharacterManager cm)
    {
        if (IsCorrectVariant(cm))
        {
            switch (tier)
            {
                case LevelManager.Tier.Tier1:
                    if (cm.foodChainLevel == 1)
                    {
                        return (int)cm.speciesAmount;
                    }
                    break;
                case LevelManager.Tier.Tier2:
                    if (cm.foodChainLevel == 2)
                    {
                        return (int)cm.speciesAmount;
                    }
                    break;
                case LevelManager.Tier.Tier3:
                    if (cm.foodChainLevel == 3)
                    {
                        return (int)cm.speciesAmount;
                    }
                    break;
            }
        }
        return 0;
    }

    private bool IsCorrectVariant(CharacterManager cm)
    {
        if (activeVariants.Length > 0)
        {
            foreach (LevelManager.Variant variant in activeVariants)
            {
                if (cm.variant == variant)
                {
                    return true;
                }
            }
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// Assigned to the Next Turn button to increment internal round counter. Does 
    /// nothing until the corresponding event is activated.
    /// </summary>
    public void OnNextTurnPressed()
    {
        if (achieveInNumRounds && eventRunning)
        {
            currRound++;
        }
    }

    /// <summary>
    /// Always returns false. This objective cannot be lost.
    /// </summary>
    public bool IsEventFailure()
    {
        return false;
    }

    public int GetID()
    {
        return ID;
    }

    public string GetLevelDescription()
    {
        string targetTier = "";
        if (activeTiers.Length > 0)
        {
            int numTiers = activeTiers.Length;

            switch (activeTiers[0])
            {
                case LevelManager.Tier.Tier1:
                    targetTier += " in Tier 1";
                    break;
                case LevelManager.Tier.Tier2:
                    targetTier += " in Tier 2";
                    break;
                case LevelManager.Tier.Tier3:
                    targetTier += " in Tier 3";
                    break;
            }

            if (numTiers > 2)
            {
                switch (activeTiers[1])
                {
                    case LevelManager.Tier.Tier1:
                        targetTier += ", Tier 1, ";
                        break;
                    case LevelManager.Tier.Tier2:
                        targetTier += ", Tier 2, ";
                        break;
                    case LevelManager.Tier.Tier3:
                        targetTier += ", Tier 3, ";
                        break;
                }

                switch (activeTiers[2])
                {
                    case LevelManager.Tier.Tier1:
                        targetTier += "and Tier 1";
                        break;
                    case LevelManager.Tier.Tier2:
                        targetTier += "and Tier 2";
                        break;
                    case LevelManager.Tier.Tier3:
                        targetTier += "and Tier 3";
                        break;
                }
            }
            else if (numTiers > 1)
            {
                switch (activeTiers[1])
                {
                    case LevelManager.Tier.Tier1:
                        targetTier += "and Tier 1";
                        break;
                    case LevelManager.Tier.Tier2:
                        targetTier += "and Tier 2";
                        break;
                    case LevelManager.Tier.Tier3:
                        targetTier += "and Tier 3";
                        break;
                }
            }
        }

        string targetVariants = "";
        if (activeVariants.Length > 0)
        {
            int numVariants = activeVariants.Length;

            switch (activeVariants[0])
            {
                case LevelManager.Variant.Arctic:
                    targetVariants += " Arctic";
                    break;
                case LevelManager.Variant.Common:
                    targetVariants += " Common";
                    break;
                case LevelManager.Variant.Tropical:
                    targetVariants += " Tropical";
                    break;
            }

            if (numVariants > 2)
            {
                switch (activeVariants[1])
                {
                    case LevelManager.Variant.Arctic:
                        targetVariants += ", Arctic, ";
                        break;
                    case LevelManager.Variant.Common:
                        targetVariants += ", Common, ";
                        break;
                    case LevelManager.Variant.Tropical:
                        targetVariants += ", Tropical, ";
                        break;
                }

                switch (activeVariants[2])
                {
                    case LevelManager.Variant.Arctic:
                        targetVariants += "and Arctic";
                        break;
                    case LevelManager.Variant.Common:
                        targetVariants += "and Common";
                        break;
                    case LevelManager.Variant.Tropical:
                        targetVariants += "and Tropical";
                        break;
                }
            }
            else if (numVariants > 1)
            {
                switch (activeVariants[1])
                {
                    case LevelManager.Variant.Arctic:
                        targetVariants += " and Arctic";
                        break;
                    case LevelManager.Variant.Common:
                        targetVariants += " and Common";
                        break;
                    case LevelManager.Variant.Tropical:
                        targetVariants += " and Tropical";
                        break;
                }
            }
        }

        string inNumRounds = "";
        string currRounds = "";
        if (achieveInNumRounds) 
        { 
            inNumRounds = " in " + numRounds.ToString() + " rounds";
            currRounds = "\nCurrent round: " + currRound.ToString() + "/" + numRounds;
        }

        return "Have " + targetCreatureCount.ToString() + targetVariants + " organisms total" + targetTier + inNumRounds + ".\n" + 
               "Current organisms: " + organismTotal.ToString() + "/" + targetCreatureCount.ToString() +
               currRounds;
    }

    public bool IsLevel()
    {
        return true;
    }
}
