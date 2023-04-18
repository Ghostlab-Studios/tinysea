using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sells all creatures currently owned by the player.
/// </summary>
public class SellAllOrganismsEvent : MonoBehaviour, ILevelEvent
{
    public int ID;

    private PlayerManager pm;
    private Animator textAnim;

    void Awake()
    {
        InitializeEvent();
    }

    public void InitializeEvent()
    {
        GetComponent<LevelManager>().levelGoals.Add(this);
        pm = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
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
        for (int i = 0; i < pm.species.Count; i++)
        {
            if (pm.species[i].speciesAmount >= 1)
            {
                pm.SellCreatures(i, (int)pm.species[i].speciesAmount);
            }
        }
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
