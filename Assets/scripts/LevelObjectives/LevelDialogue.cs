using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LevelEvent that creates a dialogue menu. Depends on a DialoguePanel, DialogueContinueButton,
/// DialogueCharacterName, and DialogueTextBox to be active in the scene. Pressing "Continue" after
/// the final textbox will complete the event.
/// </summary>
public class LevelDialogue : MonoBehaviour, ILevelEvent
{
    public int ID;
    public List<string> dialogue;

    private GameObject textPanel;
    private Button nextButton;
    private Text characterName;
    private Text textBox;
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
        textPanel = GameObject.FindGameObjectWithTag("DialoguePanel");
        nextButton = GameObject.FindGameObjectWithTag("DialogueContinueButton").GetComponent<Button>();
        characterName = GameObject.FindGameObjectWithTag("DialogueCharacterName").GetComponent<Text>();
        textBox = GameObject.FindGameObjectWithTag("DialogueTextBox").GetComponent<Text>();
        nextButton.onClick.AddListener(ProcessText);
        GetComponent<LevelManager>().levelGoals.Add(this);
    }

    /// <summary>
    /// Event is considered complete when all dialogue is exhausted.
    /// </summary>
    /// <returns>Returns true if all dialogue is exhausted, false if otherwise.</returns>
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

    /// <summary>
    /// Called upon event start and whenever the player presses the "Continue" button. Moves
    /// to the next dialogue box in the Dialogue field.
    /// </summary>
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
