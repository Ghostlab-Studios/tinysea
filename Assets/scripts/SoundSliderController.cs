using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sets slider values to current volume upon loading a scene.
/// </summary>
public class SoundSliderController : MonoBehaviour
{
	public SoundManager.SoundType soundType;

	void Start() 
	{
		Slider slider = GetComponent<Slider>();

		if (soundType == SoundManager.SoundType.Music)
        {
			slider.value = SoundManager.musicVolume;
        }
		else
        {
			slider.value = SoundManager.sfxVolume;
		}
	}
}
