using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelStartAnim : MonoBehaviour, ILevelEvent
{
    public int ID;
    public string label;

    private Animator anim;
    private Text text;
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
        text = GameObject.FindGameObjectWithTag("LevelOpenText").GetComponent<Text>();
        anim = GameObject.FindGameObjectWithTag("LevelStartAnimation").GetComponent<Animator>();
        GetComponent<LevelManager>().levelGoals.Add(this);
    }

    public bool IsEventComplete()
    {
        if (!hasStarted)
        {
            text.text = label;
            anim.gameObject.SetActive(true);
            anim.SetTrigger("OpenLevelStart");
            anim.SetTrigger("LevelComplete");
            hasStarted = true;
        }
        else if (!anim.GetBool("LevelComplete"))
        {
            anim.gameObject.SetActive(false);
            return true;
        }
        return false;
    }

    private bool IsPlaying(Animator anim, string stateName)
    {
        return anim.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash(stateName) &&
               anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f;
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
