using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets new temperature parameters in the middle of a level.
/// </summary>
public class SetTemperatureEvent : MonoBehaviour, ILevelEvent
{
    public int ID;
    public float temperatureMean;
    public float climateChange;
    public float yearlyRange;
    public float fluctuation;

    private Animator textAnim;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        GetComponent<LevelManager>().levelGoals.Add(this);
        textAnim = GameObject.FindGameObjectWithTag("DialoguePanel").GetComponent<Animator>();
    }

    public int GetID()
    {
        return ID;
    }

    public string GetLevelDescription()
    {
        return "";
    }

    public bool IsEventComplete()
    {
        Temperature.tMean = temperatureMean;
        TemperatureTrend.clim = climateChange;
        TemperatureTrend.yrRange = yearlyRange;
        TemperatureTrend.rand = fluctuation;
        return textAnim.GetCurrentAnimatorStateInfo(0).IsName("InactiveState");
    }

    public bool IsEventFailure()
    {
        return false;
    }

    public bool IsLevel()
    {
        return false;
    }
}
