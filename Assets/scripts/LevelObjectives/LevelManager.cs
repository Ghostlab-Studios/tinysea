﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Level manager script that oversees the completion status of a level's goal.
/// Uses ALevelGoal abstract class to easily swap between goals.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public int levelID;
    public List<ILevelEvent> levelGoals = new List<ILevelEvent>(); // Guaranteed to be in order of
                                                                   // ILevelEvent ID (0 to max ID)
                                                                   // as long as there are no post-
                                                                   // launch edits to the list
    public static bool isGameOver = false;
    public static int currentGoal = 0;

    private ObjectStorage levelOneObjects;
    private Text objectiveText;
    // private ObjectStorage levelTwoObjects;
    private int currentTurn = 0;
    private Text levelText;
    private Text goalText;
    private int levelSection = 0;

    public enum Tier
    {
        Tier1,
        Tier2,
        Tier3
    }

    private void Awake()
    {
        objectiveText = GameObject.FindGameObjectWithTag("ObjectiveText").GetComponent<Text>();
        levelOneObjects = GameObject.FindGameObjectWithTag("Level1Objects").GetComponent<ObjectStorage>();
    }

    private void Start()
    {
        isGameOver = false;
        SortLevelGoalsByID();
        SetLevelOneUI();

        LevelDialogue[] dialogue = GetComponents<LevelDialogue>();
        foreach (LevelDialogue text in dialogue)
        {
            if (text.GetID() <= currentGoal)
            {
                foreach (LevelDialogue.DialogueTuple tuple in text.dialogue)
                {
                    foreach (LevelDialogue.ActivatableObject aobj in tuple.activeObjects)
                    {
                        if (aobj.remainsActive)
                        {
                            aobj.GetObject().SetActive(true);
                        }
                    }
                }
            }
        }
    }

    private void Update()
    {
        Debug.Log(GetCurrentObjectiveID().ToString());
        if (!isGameOver)
        {
            if (levelGoals[currentGoal].IsLevel()) { objectiveText.text = levelGoals[currentGoal].GetLevelDescription(); }
            if (levelGoals[currentGoal].IsEventComplete()) { ObjectiveComplete(); }
            else if (levelGoals[currentGoal].IsEventFailure()) { LevelLost(); }
        }
    }

    /// <summary>
    /// Called when starting a new turn to keep track of current number of turns in a given level
    /// </summary>
    public void NextTurn()
    {
        currentTurn++;
    }

    /// <summary>
    /// Proceeds to the next level objective, or goes to level win screen if all objectives are completed.
    /// </summary>
    private void ObjectiveComplete()
    {
        if (levelGoals[currentGoal].IsLevel()) { levelSection++; }
        objectiveText.text = levelGoals[currentGoal].GetLevelDescription();
        currentGoal++;
        if (currentGoal >= levelGoals.Count) { LevelWon(); }
    }

    /// <summary>
    /// Called when the level is won
    /// </summary>
    private void LevelWon()
    {
        isGameOver = true;
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Called when the level is lost
    /// </summary>
    private void LevelLost()
    {
        // To be filled in later
    }

    /// <summary>
    /// Sets the text at the beginning of the level to the correct level description.
    /// </summary>
    private void SetLevelText()
    {
        levelText.text = "Level " + (levelID + 1).ToString() + "-" + (levelSection + 1).ToString();
    }

    public void SetLevelOneUI()
    {
        foreach (GameObject obj in levelOneObjects.objects)
        {
            obj.SetActive(false);
        }
    }

    public void SetLevelTwoUI()
    {

    }

    public void SetLevelThreeUI()
    {
        // Do nothing probably? Why is this even declared? Maybe I'll need it someday
    }

    /// <summary>
    /// Uses BubbleSort to sort level goals by ID.
    /// </summary>
    private void SortLevelGoalsByID()
    {
        for (int i = 0; i < levelGoals.Count - 1; i++)
        {
            for (int j = 0; j < levelGoals.Count - i - 1; j++)
            {
                if (levelGoals[j].GetID() > levelGoals[j + 1].GetID())
                {
                    ILevelEvent temp = levelGoals[j + 1];
                    levelGoals[j + 1] = levelGoals[j];
                    levelGoals[j] = temp;
                }
            }
        }
    }

    public int GetCurrentObjectiveID()
    {
        return currentGoal;
    }
}