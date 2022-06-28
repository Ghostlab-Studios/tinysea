using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generalized class for having a certain number of creatures to complete the objective.
/// </summary>
public class HaveCreaturesObjective : MonoBehaviour, ILevelGoal {

    public int ID;
    public PlayerManager playerManager;
    public int targetCreatureCount;
    public Tier activeTier;

    public enum Tier
    {
        Tier1,
        Tier2,
        Tier3
    }
    
    void Awake()
    {
        InitializeLevelGoal();
    }
    
    public void InitializeLevelGoal()
    {
        GetComponent<LevelManager>().levelGoals.Add(this);
    }
    
    /// <summary>
    /// Returns true if the amount of creatures within a given tier is greater than or equal to the
    /// current amount of creatures of that tier.
    /// </summary>
    public bool IsLevelWon()
    {
        int totalCreatures = 0;
        foreach (CharacterManager cm in playerManager.species)
        {
            switch (activeTier)
            {
                case Tier.Tier1:
                    if (cm.foodChainLevel == 1)
                    {
                        totalCreatures += (int)cm.speciesAmount;
                    }
                    break;
                case Tier.Tier2:
                    if (cm.foodChainLevel == 2)
                    {
                        totalCreatures += (int)cm.speciesAmount;
                    }
                    break;
                case Tier.Tier3:
                    if (cm.foodChainLevel == 3)
                    {
                        totalCreatures += (int)cm.speciesAmount;
                    }
                    break;
            }
        }
        Debug.Log("Total Creatures: " + totalCreatures.ToString());
        return totalCreatures >= targetCreatureCount;
    }
    
    /// <summary>
    /// Always returns false. This objective cannot be lost.
    /// </summary>
    public bool IsLevelLost()
    {
        return false;
    }

    public int GetID()
    {
        return ID;
    }
}
