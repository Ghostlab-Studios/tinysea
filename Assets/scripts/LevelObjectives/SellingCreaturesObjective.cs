﻿using System.Collections;
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
    public int amountToSell;
    public LevelManager.Tier activeTier;

    private ShopManager shop;
    private int sellCount = 0;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        shop = GameObject.FindGameObjectWithTag("ShopManager").GetComponent<ShopManager>();
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

    public string GetLevelDescription()
    {
        string targetTier = "TIER ERROR";
        switch (activeTier)
        {
            case LevelManager.Tier.Tier1:
                targetTier = "Tier 1";
                break;
            case LevelManager.Tier.Tier2:
                targetTier = "Tier 2";
                break;
            case LevelManager.Tier.Tier3:
                targetTier = "Tier 3";
                break;
        }
        return "Sell " + amountToSell.ToString() + " " + targetTier + " organisms.\n" +
               "Current organisms: " + sellCount.ToString() + "/" + amountToSell.ToString();
    }

    public bool IsLevel()
    {
        return true;
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
