using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads levels and gameplay scene. Only loads levels in the list of LevelManagers if
/// the current scene is the gameplay scene.
/// </summary>
public class LevelLoader : MonoBehaviour
{
    public static int levelToLoad = 0;
    public LevelManager[] levelManagers;

    private void Awake()
    {
        // If the active scene is the gameplay scene, create the LevelManager for that scene
        if (SceneManager.GetActiveScene().buildIndex == 1)
        {
            Instantiate(levelManagers[levelToLoad]);
        }
    }

    /// <summary>
    /// Levels are loaded based on the static int <param name="levelToLoad"></param>.
    /// To change the level to load, call this function. Currently called on level select
    /// button press.
    /// </summary>
    /// <param name="level"></param>
    public void SetLevelToLoad(int level)
    {
        levelToLoad = level;
    }

    /// <summary>
    /// Changes active game scene to the gameplay scene. Breaks if the build order doesn't have
    /// it at 1.
    /// </summary>
    public void ChangeToGameScene()
    {
        SceneManager.LoadScene(1);
    }
}
