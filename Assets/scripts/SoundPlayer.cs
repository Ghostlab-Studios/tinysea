using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SoundPlayer class to store and play sounds to be adjusted with SoundManager volume settings.
/// </summary>
public class SoundPlayer : MonoBehaviour {

    public AudioSource source;                  // Audio source to play sound
    public SoundManager.SoundType soundType;    // Type of sound

    private float maxVolume;

    private void Start()
    {
        maxVolume = source.volume;
    }

    public void PlayAudio()
    {
        source.Play();
    }

    private void Update()
    {
        // Adjusts volume depending on type of sound being played
        source.volume = (soundType == SoundManager.SoundType.SFX) ? maxVolume * SoundManager.Instance.sfxVolume : maxVolume * SoundManager.Instance.musicVolume;
    }
}
