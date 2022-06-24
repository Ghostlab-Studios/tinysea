using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level Goal interface for level goal classes. Has some goal that is checked by the LevelManager
/// class to tell when a level is won or lost.
/// </summary>
public interface ILevelGoal {
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
}
