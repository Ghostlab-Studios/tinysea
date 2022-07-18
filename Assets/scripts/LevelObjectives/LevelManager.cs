using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level manager script that oversees the completion status of a level's goal.
/// Uses ALevelGoal abstract class to easily swap between goals.
/// </summary>
public class LevelManager : MonoBehaviour {
    public List<ILevelEvent> levelGoals = new List<ILevelEvent>(); // Guaranteed to be in order of
                                                                   // ILevelEvent ID (0 to max ID)
                                                                   // as long as there are no post-
                                                                   // launch edits to the list
    public GameObject winScreenPanel;
    public GameObject loseScreenPanel;
    public static bool isGameOver = false;

    private int currentTurn = 0;
    private int currentGoal = 0;

    public enum Tier
    {
        Tier1,
        Tier2,
        Tier3
    }

    private void Start()
    {
        isGameOver = false;
        SortLevelGoalsByID();
    }

    private void Update()
    {
        if (!isGameOver)
        {
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
        currentGoal++;
        if (currentGoal >= levelGoals.Count) { LevelWon(); }
    }

    /// <summary>
    /// Called when the level is won
    /// </summary>
    private void LevelWon()
    {
        // To be filled in later
        Debug.Log("Game Won");
        loseScreenPanel.SetActive(false);
        winScreenPanel.SetActive(true);
        isGameOver = true;
    }

    /// <summary>
    /// Called when the level is lost
    /// </summary>
    private void LevelLost()
    {
        // To be filled in later
        winScreenPanel.SetActive(false);
        loseScreenPanel.SetActive(true);
        isGameOver = true;
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
