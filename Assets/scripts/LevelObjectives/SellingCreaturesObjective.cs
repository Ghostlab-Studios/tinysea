using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generalized class for selling a certain amount of creatures of a specified tier to complete
/// the objective.
/// </summary>
public class SellingCreaturesObjective : MonoBehaviour, ILevelGoal {

    public int ID;
    public Shop shop;
    public int amountToSell;
    public LevelManager.Tier activeTier;
    
    private int sellCount = 0;

    void Awake()
    {
        InitializeLevelGoal();
    }

    public void InitializeLevelGoal()
    {
        GetComponent<LevelManager>().levelGoals.Add(this);
        shop.OnSell.AddListener(ProcessSaleData);
    }

    /// <summary>
    /// Returns true if the current sold amount is above the given amount to sell, false if otherwise.
    /// </summary>
    public bool IsLevelWon()
    {
        Debug.Log(sellCount);
        return sellCount >= amountToSell;
    }

    /// <summary>
    /// Always returns false. This objective cannot be lost.
    /// </summary>
    public bool IsLevelLost()
    {
        return false;
    }
    
    public int GetID() {
        return ID;
    }

    /// <summary>
    /// Called whenever a sale is performed in the shop. Adds to current selling objective goal.
    /// </summary>
    private void ProcessSaleData()
    {
        if (GetComponent<LevelManager>().GetCurrentObjectiveID() == ID)
        {
            switch (activeTier)
            {
                case LevelManager.Tier.Tier1:
                    if (shop.selectedFish >= 0 && shop.selectedFish < 9) { sellCount += shop.currentSellingFishes; }
                    break;
                case LevelManager.Tier.Tier2:
                    if (shop.selectedFish >= 9 && shop.selectedFish < 18) { sellCount += shop.currentSellingFishes; }
                    break;
                case LevelManager.Tier.Tier3:
                    if (shop.selectedFish >= 18 && shop.selectedFish < 27) { sellCount += shop.currentSellingFishes; }
                    break;
            }
        }
    }
}
