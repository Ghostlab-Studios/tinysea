using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundPlayer : MonoBehaviour {

    public AudioSource source;

    private void Start()
    {
        //source = GetComponent<AudioSource>();
    }

    public void PlayAudio()
    {
        source.Play();
    }

    public void adjust_vol(float vol)
    {
        source.volume = vol;
    }
}
