using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMenuController : MonoBehaviour {

    public Animator UIAnimator;
    public GameObject sfxSlider;
    public GameObject musicSlider;
    public GameObject homeMenu;
    public GameObject saveMenu;

    private bool isOpen = false;
    
	public void MenuPressed()
    {
        if (isOpen)
        {
            UIAnimator.SetTrigger("CloseMenu");
            isOpen = false;
        }
        else
        {
            UIAnimator.SetTrigger("OpenMenu");
            isOpen = true;
        }
    }

    public void MusicPressed()
    {
        musicSlider.SetActive(!musicSlider.activeSelf);
    }

    public void SFXPressed()
    {
        sfxSlider.SetActive(!sfxSlider.activeSelf);
    }

    public void HomePressed()
    {
        homeMenu.SetActive(!homeMenu.activeSelf);
    }

    public void SavePressed()
    {
        saveMenu.SetActive(!saveMenu.activeSelf);
    }
}
