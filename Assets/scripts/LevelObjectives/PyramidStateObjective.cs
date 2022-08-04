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
    public float epsilon = 0.15f; // [0, 1], easier to complete the higher epsilon is

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

        bool tier1ReachesThreshold = tier1Threshold >= 1 - epsilon;
        bool tier2ReachesThreshold = tier2Threshold >= 1 - epsilon;
        bool tier3ReachesThreshold = tier3Threshold >= 1 - epsilon;

        return tier1ReachesThreshold && tier2ReachesThreshold && tier3ReachesThreshold;
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
}
