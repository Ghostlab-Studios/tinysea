using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opens the shop window before moving onto the next segment of the level.
/// </summary>
public class ToggleShopWindow : MonoBehaviour, ILevelEvent
{
    public int ID;

    private ShopWindowControl swc;
    private GameObject textPanel;
    private Animator textAnim;
    private bool isToggled = false;

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
        swc = GameObject.FindGameObjectWithTag("ShopManager").GetComponent<ShopWindowControl>();
        textPanel = GameObject.FindGameObjectWithTag("DialoguePanel");
        textAnim = textPanel.GetComponent<Animator>();
    }

    public bool IsEventComplete()
    {
        if (!isToggled)
        {
            swc.AutoToggleWindow();
            isToggled = true;
        }
        return swc.IsNotMoving() && textAnim.GetCurrentAnimatorStateInfo(0).IsName("InactiveState");
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
