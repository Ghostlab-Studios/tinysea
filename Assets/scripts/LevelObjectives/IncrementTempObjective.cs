using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A level segment that increases the temperature by a given amount over a given
/// amount of rounds.
/// </summary>
public class IncrementTempObjective : MonoBehaviour, ILevelEvent
{
    public int ID;
    public int tempIncrease;
    public int roundsBetweenIncrement;
    public int numIncrements;
    public int organismGoal;

    private PlayerManager pm;
    private Button nextTurnButton;
    private Temperature temp;
    private bool eventHasStarted = false;
    private bool eventHasEnded = false;
    private int roundsSinceStart = 0;
    private bool roundHasPassed = true;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        pm = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
        GetComponent<LevelManager>().levelGoals.Add(this);
        //nextTurnButton = GameObject.FindGameObjectWithTag("NextTurnButton").GetComponent<Button>();
        //nextTurnButton.onClick.AddListener(OnNextTurnPressed);
        pm.onNextTurn.AddListener(OnNextTurnPressed);
        temp = GameObject.FindGameObjectWithTag("Temperature").GetComponent<Temperature>();
        temp.setRandomForecast = false;
        temp.forecastHigh = Temperature.tMean;
        temp.forecastLow = Temperature.tMean;
        temp.forecastHighText.text = Mathf.RoundToInt(temp.forecastHigh).ToString();
        temp.forecastLowText.text = Mathf.RoundToInt(temp.forecastLow).ToString();
    }

    public bool IsEventComplete()
    {
        if (temp.setRandomForecast) { temp.setRandomForecast = false; }
        if (!pm.busy && !pm.holder.anyCreaturesBusy() && !roundHasPassed) { roundHasPassed = true; }
        if (!eventHasStarted) { eventHasStarted = true; }
        if (roundsSinceStart >= roundsBetweenIncrement * numIncrements)
        {
            eventHasEnded = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Assigned to the Next Turn button to increment temperature on new rounds. Does 
    /// nothing until the corresponding event is activated.
    /// </summary>
    public void OnNextTurnPressed()
    {
        if (roundHasPassed && eventHasStarted && !eventHasEnded)
        {
            roundsSinceStart++;
            if (roundsSinceStart % roundsBetweenIncrement == 0) { Temperature.tMean += tempIncrease; }
            if (roundsSinceStart % roundsBetweenIncrement == roundsBetweenIncrement - 1)
            {
                temp.forecastHigh = Temperature.tMean + tempIncrease;
                temp.forecastLow = Temperature.tMean + tempIncrease;
                // temp.forecastHighText.text = temp.forecastHigh.ToString();
                // temp.forecastLowText.text = temp.forecastLow.ToString();
            }
            else
            {
                temp.forecastHigh = Temperature.tMean;
                temp.forecastLow = Temperature.tMean;
                // temp.forecastHighText.text = temp.forecastHigh.ToString();
                // temp.forecastLowText.text = temp.forecastLow.ToString();
            }
            roundHasPassed = false;
        }
    }

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
        return "Try to have " + organismGoal.ToString() + " organisms at the end of " + (roundsBetweenIncrement * numIncrements).ToString() + " rounds.\n" +
               "Current Rounds: " + roundsSinceStart.ToString() + "/" + (roundsBetweenIncrement * numIncrements).ToString();
    }

    public bool IsLevel()
    {
        return true;
    }
}
