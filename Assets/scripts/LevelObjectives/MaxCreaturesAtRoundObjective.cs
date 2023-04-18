using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ILevelEvent for getting as many organisms as possible in a given number of rounds.
/// </summary>
public class MaxCreaturesAtRoundObjective : MonoBehaviour, ILevelEvent
{
    public int ID;
    public int numRounds;
    public LevelManager.Tier tier;

    private PlayerManager pm;
    private int roundsSinceStart;
    private bool eventRunning = false;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        pm = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
        GetComponent<LevelManager>().levelGoals.Add(this);
        Button nextTurnButton = GameObject.FindGameObjectWithTag("NextTurnButton").GetComponent<Button>();
        nextTurnButton.onClick.AddListener(OnNextTurnPressed);
    }

    public bool IsEventComplete()
    {
        if (!eventRunning) { eventRunning = true; }
        bool isLevelOver = roundsSinceStart >= numRounds;
        if (isLevelOver) { eventRunning = false; }
        return isLevelOver;
    }

    /// <summary>
    /// Assigned to the Next Turn button to increment internal round counter. Does 
    /// nothing until the corresponding event is activated.
    /// </summary>
    public void OnNextTurnPressed()
    {
        if (eventRunning)
        {
            roundsSinceStart++;
        }
    }

    public bool IsEventFailure()
    {
        return false;
    }

    public string GetLevelDescription()
    {
        return "Get as many " + GetTier() + "organisms as possible in " + numRounds.ToString() + " rounds.\n" +
               "Current round: " + roundsSinceStart.ToString() + "/" + numRounds.ToString() + "\n" +
               "Current organism total: " + GetTierTotal().ToString();
    }

    private string GetTier()
    {
        string tierText = "";
        switch (tier)
        {
            case LevelManager.Tier.Tier1:
                tierText = "Tier 1 ";
                break;
            case LevelManager.Tier.Tier2:
                tierText = "Tier 2 ";
                break;
            case LevelManager.Tier.Tier3:
                tierText = "Tier 3 ";
                break;
        }
        return tierText;
    }

    private int GetTierTotal()
    {
        int total;
        switch (tier)
        {
            case LevelManager.Tier.Tier1:
                total = pm.GetTotalFishAtLevelInt(1);
                break;
            case LevelManager.Tier.Tier2:
                total = pm.GetTotalFishAtLevelInt(2);
                break;
            case LevelManager.Tier.Tier3:
                total = pm.GetTotalFishAtLevelInt(3);
                break;
            default:
                total = pm.GetTotalFishInt();
                break;
        }
        return total;
    }

    public int GetID()
    {
        return ID;
    }

    public bool IsLevel()
    {
        return true;
    }
}
