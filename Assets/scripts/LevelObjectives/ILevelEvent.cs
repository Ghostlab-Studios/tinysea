using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level Goal interface for level goal classes. Has some goal that is checked by the LevelManager
/// class to tell when a level is won or lost.
/// </summary>
public interface ILevelEvent
{
    /// <summary>
    /// Sets level goal of event script (should be attached to same object).
    /// </summary>
    void InitializeEvent();

    /// <summary>
    /// Checks if the current event is complete.
    /// </summary>
    /// <returns>Returns true if yes, false otherwise.</returns>
    bool IsEventComplete();

    /// <summary>
    /// Checks if the current event is failed (mostly for levels).
    /// </summary>
    /// <returns>Returns true if yes, false otherwise.</returns>
    bool IsEventFailure();

    /// <summary>
    /// Gets the level ID of this level goal. Intended to keep track of order of levels in the 
    /// LevelManager.
    /// </summary>
    /// <returns>Returns integer ID of LevelGoal.</returns>
    int GetID();
}
