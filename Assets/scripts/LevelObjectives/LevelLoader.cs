﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads levels and gameplay scene. Only loads levels in the list of LevelManagers if
/// the current scene is the gameplay scene.
/// </summary>
public class LevelLoader : MonoBehaviour
{
    public static int levelToLoad = -1;
    public LevelManager[] levelManagers;

    private void Awake()
    {
        if (levelToLoad < 0)
        {
            gameObject.SetActive(false);
        }
        // If the active scene is the gameplay scene, create the LevelManager for that scene
        else if (SceneManager.GetActiveScene().buildIndex == 1)
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

    public void SetSectionToLoad(int section)
    {
        LevelManager.currentGoal = section;
    }

    /// <summary>
    /// Changes the temperature mean, yearly climate increase, and range of random
    /// temperatures set by the Temperature and TemperatureTrend classes.
    /// </summary>
    public void SetDefaultTemperatureParameters()
    {
        Temperature.tMean = 15f;
        TemperatureTrend.clim = 1;
        TemperatureTrend.yrRange = 10;
        TemperatureTrend.rand = 10;
    }

    public void SetLevel1TemperatureParameters()
    {
        Temperature.tMean = 15f;
        TemperatureTrend.clim = 0;
        TemperatureTrend.yrRange = 0;
        TemperatureTrend.rand = 0;
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
