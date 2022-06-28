using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level Goal interface for level goal classes. Has some goal that is checked by the LevelManager
/// class to tell when a level is won or lost.
/// </summary>
public interface ILevelGoal {

    /// <summary>
    /// Sets level goal of LevelManager script (should be attached to same object).
    /// </summary>
    void InitializeLevelGoal();

    /// <summary>
    /// Checks if the current level is won.
    /// </summary>
    /// <returns>Returns true if yes, false otherwise.</returns>
    bool IsLevelWon();

    /// <summary>
    /// Checks if the current level is lost.
    /// </summary>
    /// <returns>Returns true if yes, false otherwise.</returns>
    bool IsLevelLost();

    /// <summary>
    /// Gets the level ID of this level goal. Intended to keep track of order of levels.
    /// </summary>
    /// <returns>Returns integer ID of LevelGoal.</returns>
    int GetID();
}
