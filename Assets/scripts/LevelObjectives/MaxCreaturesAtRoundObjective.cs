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
        return "Get as many organisms as possible in " + numRounds.ToString() + " rounds.\n" +
               "Current round: " + roundsSinceStart.ToString() + "/" + numRounds.ToString() + "\n" +
               "Current organism total: " + pm.GetTotalFishInt().ToString();
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
