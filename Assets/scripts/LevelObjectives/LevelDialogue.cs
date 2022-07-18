using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelDialogue : MonoBehaviour, ILevelEvent {

    public int ID;
    public GameObject textPanel;
    public Button nextButton;
    public Text characterName;
    public Text textBox;
    public List<string> dialogue;

    private int textIndex = 0;
    private bool isDialogueFinished = false;
    private bool hasInitialized = false;

    private void Awake()
    {
        InitializeEvent();
    }

    public int GetID()
    {
        return ID;
    }

    public void InitializeEvent()
    {
        nextButton.onClick.AddListener(ProcessText);
        GetComponent<LevelManager>().levelGoals.Add(this);
    }

    public bool IsEventComplete()
    {
        if (!hasInitialized)
        {
            ProcessText();
            hasInitialized = true;
        }
        textPanel.SetActive(!isDialogueFinished);
        return isDialogueFinished;
    }

    public bool IsEventFailure()
    {
        return false;
    }

    private void ProcessText()
    {
        if (GetComponent<LevelManager>().GetCurrentObjectiveID() == ID)
        {
            if (textIndex >= dialogue.Count)
            {
                isDialogueFinished = true;
                return;
            }

            textBox.text = dialogue[textIndex];
            textIndex++;
        }
    }
}
