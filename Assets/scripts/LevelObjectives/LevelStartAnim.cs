using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

public class LevelStartAnim : MonoBehaviour, ILevelEvent
{
    public int ID;

    private Animator anim;
    private bool hasStarted = false;
    private bool hasStopped = false;

    private void Awake()
    {
        InitializeEvent();
    }

    public int GetID()
    {
        return ID;
    }

    public string GetLevelDescription()
    {
        return "";
    }

    public void InitializeEvent()
    {
        anim = GameObject.FindGameObjectWithTag("LevelStartAnimation").GetComponent<Animator>();
        GetComponent<LevelManager>().levelGoals.Add(this);
    }

    public bool IsEventComplete()
    {
        if (!hasStarted)
        {
            anim.SetTrigger("OpenLevelStart");
            hasStarted = true;
        }
        else if (!hasStopped && !IsPlaying(anim, "New State") && !IsPlaying(anim, "OpenLevelStart"))
        {
            //anim.SetTrigger("OpenLevelStop");
            hasStopped = true;
        }
        else if (IsPlaying(anim, "DoneState"))
        {
            anim.gameObject.SetActive(false);
            return true;
        }
        return false;
    }

    private bool IsPlaying(Animator anim, string stateName)
    {
        if (anim.GetCurrentAnimatorStateInfo(0).IsName(stateName) &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
        { return true; }
        else { return false; }
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
