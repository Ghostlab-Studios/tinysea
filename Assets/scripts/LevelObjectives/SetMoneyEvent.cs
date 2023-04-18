using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets the current player's money to given amount of money.
/// </summary>
public class SetMoneyEvent : MonoBehaviour, ILevelEvent
{
    public int ID;
    public int money;

    private PlayerManager pm;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        GetComponent<LevelManager>().levelGoals.Add(this);
        pm = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
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
        pm.moneys = money;
        return true;
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
