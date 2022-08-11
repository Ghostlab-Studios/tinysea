using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// For telling whether or not the food pyramid is balanced at any given point
/// in the level. Balance between tiers is determined by the tier size difference and room for
/// error can be adjusted with epsilon (pyramid not exactly balanced).
/// </summary>
public class PyramidStateObjective : MonoBehaviour, ILevelEvent
{
    public int ID;
    public float tierSizeDifference = 3f; // Multiplicative size difference between each tier
    public float delta = 0.15f; // [0, 1], easier to complete the higher delta is
    public bool tier1Balance;
    public bool tier2Balance;
    public bool tier3Balance;

    private PlayerManager playerManager;

    private void Awake () {
        InitializeEvent();
	}

    public void InitializeEvent()
    {
        playerManager = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
        GetComponent<LevelManager>().levelGoals.Add(this);
    }

    /// <summary>
    /// Returns true if the food pyramid is balanced, false if not.
    /// </summary>
    public bool IsEventComplete()
    {
        int creaturesAtTier1 = Mathf.FloorToInt(playerManager.getTotalAmountAtLevel(1));
        int creaturesAtTier2 = Mathf.FloorToInt(playerManager.getTotalAmountAtLevel(2));
        int creaturesAtTier3 = Mathf.FloorToInt(playerManager.getTotalAmountAtLevel(3));

        // Calculated the same way as in the pyramid heirarchy.
        float tier1Threshold = creaturesAtTier1;
        float tier2Threshold = creaturesAtTier2 * tierSizeDifference;
        float tier3Threshold = creaturesAtTier3 * (tierSizeDifference * tierSizeDifference);
        float maxThreshold = Mathf.Max(tier1Threshold, tier2Threshold, tier3Threshold);
        tier1Threshold /= maxThreshold;
        tier2Threshold /= maxThreshold;
        tier3Threshold /= maxThreshold;
        // Debug.Log(tier1Threshold.ToString() + ", " + tier2Threshold.ToString() + ", " + tier3Threshold.ToString());

        bool tier1ReachesThreshold = tier1Threshold >= 1 - delta;
        bool tier2ReachesThreshold = tier2Threshold >= 1 - delta;
        bool tier3ReachesThreshold = tier3Threshold >= 1 - delta;

        bool toReturn = true;
        if (tier1Balance) { toReturn = toReturn && tier1ReachesThreshold; }
        if (tier2Balance) { toReturn = toReturn && tier2ReachesThreshold; }
        if (tier3Balance) { toReturn = toReturn && tier3ReachesThreshold; }

        return toReturn;
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
        int total = 0;
        if (tier1Balance) { total++; }
        if (tier2Balance) { total++; }
        if (tier3Balance) { total++; }
        string pyramidText = "";
        switch (total)
        {
            case 0:
                pyramidText = "ERROR: 0";
                break;
            case 1:
                pyramidText = "ERROR: 1";
                break;
            case 2:
                string tier1Text = "";
                string tier2Text = "";
                if (tier1Balance) { tier1Text = "Tier 1"; }
                if (tier2Balance)
                {
                    if (tier1Text == "")
                    {
                        tier1Text = "Tier 2";
                    }
                    else
                    {
                        tier2Text = "Tier 2";
                    }
                }
                if (tier3Balance) { tier2Text = "Tier 3"; }
                pyramidText = tier1Text + " and " + tier2Text;
                break;
            case 3:
                pyramidText = "Tier 1, Tier 2, and Tier 3";
                break;
        }
        return "Balance " + pyramidText + " organisms.";
    }

    public bool IsLevel()
    {
        return true;
    }
}
