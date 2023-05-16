using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SoundManager class used to manage the volume of all sounds and music in-game.
/// Singleton class to be placed at the very least in the main menu scene for initialization.
/// </summary>
public class SoundManager : MonoBehaviour {
    // Enum for differentiating sounds by type - Music or SFX
    public enum SoundType
    {
        SFX,
        Music
    }

    public static float sfxVolume = 1f;    // Volume scale for SFX from 0.0 to 1.0, works on a percentage of starting volume of corresponding audio sources
    public static float musicVolume = 1f;  // Volume scale for music from 0.0 to 1.0, works on a percentage of starting volume of corresponding audio sources
    public static bool init = false;
    public Slider musicSlider;
    public Slider soundSlider;

    private void Start()
    {
        if (!init)
        {
            sfxVolume = 0.5f;
            musicVolume = 0.5f;
            init = true;
        }
        else
        {
            sfxVolume = Mathf.Clamp(sfxVolume, 0f, 1f);
            musicVolume = Mathf.Clamp(musicVolume, 0f, 1f);
        }
        musicSlider.value = musicVolume;
        soundSlider.value = sfxVolume;
    }

    public void ChangeMusicVolume()
    {
        if (musicSlider != null) { musicVolume = musicSlider.value; }
    }

    public void ChangeSFXVolume()
    {
        if (soundSlider != null) { sfxVolume = soundSlider.value; }
    }

    public void RecordMusicChange()
    {
        SessionRecorder.instance.WriteToSessionData("Volume Change,Music," + musicVolume.ToString());
    }

    public void RecordSFXChange()
    {
        SessionRecorder.instance.WriteToSessionData("Volume Change,SFX," + musicVolume.ToString());
    }
}
