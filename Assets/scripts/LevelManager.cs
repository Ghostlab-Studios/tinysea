using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level manager script that oversees the completion status of a level's goal.
/// Uses ALevelGoal abstract class to easily swap between goals.
/// </summary>
public class LevelManager : MonoBehaviour {
    public ILevelGoal levelGoal;

    private int currentTurn = 0;
    private static bool isGameOver = false;

    private void Start()
    {
        isGameOver = false;
    }

    private void Update()
    {
        if (levelGoal.IsLevelWon()) { LevelWon(); }
        else if (levelGoal.IsLevelLost()) { LevelLost(); }
    }

    /// <summary>
    /// Called when starting a new turn to keep track of current number of turns in a given level
    /// </summary>
    public void NextTurn()
    {
        currentTurn++;
    }

    /// <summary>
    /// Called when the level is won
    /// </summary>
    private void LevelWon()
    {
        // To be filled in later
        isGameOver = true;
    }

    /// <summary>
    /// Called when the level is lost
    /// </summary>
    private void LevelLost()
    {
        // To be filled in later
        isGameOver = true;
    }
}
