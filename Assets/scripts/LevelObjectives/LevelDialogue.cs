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
    private Animator textAnim;
    private Button nextButton;
    private Text characterName;
    private Text textBox;
    private int textIndex = 0;
    private bool isDialogueFinished = false;
    private bool hasInitialized = false;
    private bool isOpen = true;
    private bool hasOpenedOnce = false;
    private string currentShownText = "";
    private int currentShownTextIndex = 0;
    private float textDisplayTimer = 0.0f;
    private float timeBetweenCharDisplay = 0.03f;
    private bool displayingText = false;

    private void Awake()
    {
        InitializeEvent();
    }

    private void Update()
    {
        if (displayingText && !isDialogueFinished) { DisplayText(); }
    }

    public int GetID()
    {
        return ID;
    }

    public void InitializeEvent()
    {
        textPanel = GameObject.FindGameObjectWithTag("DialoguePanel");
        textAnim = textPanel.GetComponent<Animator>();
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
        SetTextBoxActive(!isDialogueFinished);
        return isDialogueFinished;
    }

    public bool IsEventFailure()
    {
        return false;
    }

    public string GetLevelDescription()
    {
        return "";
    }

    public bool IsLevel()
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
            currentShownText = "";
            textBox.text = currentShownText;
            currentShownTextIndex = 0;
            displayingText = true;
            textIndex++;
        }
    }

    private void DisplayText()
    {
        if (Input.GetMouseButtonDown(0))
        {
            currentShownText = dialogue[textIndex - 1];
            textBox.text = currentShownText;
            currentShownTextIndex = 0;
            displayingText = false;
            return;
        }

        if (textDisplayTimer >= timeBetweenCharDisplay)
        {
            currentShownText += dialogue[textIndex - 1][currentShownTextIndex];
            textBox.text = currentShownText;
            currentShownTextIndex++;
            if (currentShownTextIndex >= dialogue[textIndex - 1].Length)
            {
                currentShownTextIndex = 0;
                displayingText = false;
            }
            textDisplayTimer = 0f;
        }
        else { textDisplayTimer += Time.deltaTime; }
    }

    private void SetTextBoxActive(bool isActive)
    {
        textPanel.SetActive(true);
        
        if ((!isOpen && isActive && !isDialogueFinished) || 
            (!hasOpenedOnce && textAnim.GetCurrentAnimatorStateInfo(0).IsName("InactiveState")))
        {
            isOpen = true;
            textAnim.SetTrigger("Open");
        }
        else if (isOpen && !isActive)
        {
            isOpen = false;
            textAnim.SetTrigger("Close");
        }

        hasOpenedOnce = true;
    }
}
