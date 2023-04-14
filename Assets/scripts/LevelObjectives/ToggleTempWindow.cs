using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opens the temperature window before moving onto the next segment of the level.
/// </summary>
public class ToggleTempWindow : MonoBehaviour, ILevelEvent
{
    public int ID;
    public float waitTime = 1f;

    private TempWindowControl twc;
    private GameObject textPanel;
    private Animator textAnim;
    private bool timeReached = false;
    private bool isToggled = false;
    private bool timerStarted = false;

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
        GetComponent<LevelManager>().levelGoals.Add(this);
        twc = GameObject.FindGameObjectWithTag("TempGraph").GetComponent<TempWindowControl>();
        textPanel = GameObject.FindGameObjectWithTag("DialoguePanel");
        textAnim = textPanel.GetComponent<Animator>();
    }

    public bool IsEventComplete()
    {
        if (!isToggled)
        {
            twc.Controler();
            isToggled = true;
        }
        if (!timerStarted && twc.IsNotMoving())
        {
            Invoke("TimerDone", waitTime);
            timerStarted = true;
        }
        return timeReached && textAnim.GetCurrentAnimatorStateInfo(0).IsName("InactiveState");
    }

    private void TimerDone()
    {
        timeReached = true;
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
