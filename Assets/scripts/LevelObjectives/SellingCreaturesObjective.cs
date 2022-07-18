using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generalized class for selling a certain amount of creatures of a specified tier to complete
/// the objective.
/// </summary>
public class SellingCreaturesObjective : MonoBehaviour, ILevelEvent
{
    public int ID;
    public ShopManager shop;
    public int amountToSell;
    public LevelManager.Tier activeTier;
    
    private int sellCount = 0;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        GetComponent<LevelManager>().levelGoals.Add(this);
        shop.onSell.AddListener(ProcessSaleData);
    }

    /// <summary>
    /// Returns true if the current sold amount is above the given amount to sell, false if otherwise.
    /// </summary>
    public bool IsEventComplete()
    {
        return sellCount >= amountToSell;
    }

    /// <summary>
    /// Always returns false. This objective cannot be lost.
    /// </summary>
    public bool IsEventFailure()
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
                    if (shop.GetActiveOrganism() >= 0 && shop.GetActiveOrganism() < 9) { sellCount += shop.GetAmountToSell(); }
                    break;
                case LevelManager.Tier.Tier2:
                    if (shop.GetActiveOrganism() >= 9 && shop.GetActiveOrganism() < 18) { sellCount += shop.GetAmountToSell(); }
                    break;
                case LevelManager.Tier.Tier3:
                    if (shop.GetActiveOrganism() >= 18 && shop.GetActiveOrganism() < 27) { sellCount += shop.GetAmountToSell(); }
                    break;
            }
        }
    }
}
