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
        if (!IsBusy())
        {
            if (isOpen)
            {
                UIAnimator.SetTrigger("CloseMenu");
                sfxSlider.SetActive(false);
                musicSlider.SetActive(false);
                homeMenu.SetActive(false);
                isOpen = false;
            }
            else
            {
                UIAnimator.SetTrigger("OpenMenu");
                isOpen = true;
            }
        }
    }

    public void MusicPressed()
    {
        if (!IsBusy()) { musicSlider.SetActive(!musicSlider.activeSelf); }
    }

    public void SFXPressed()
    {
        if (!IsBusy()) { sfxSlider.SetActive(!sfxSlider.activeSelf); }
    }

    public void HomePressed()
    {
        if (!IsBusy()) { homeMenu.SetActive(!homeMenu.activeSelf); }
    }

    public void SavePressed()
    {
        if (!IsBusy()) { saveMenu.SetActive(!saveMenu.activeSelf); }
    }

    private bool IsBusy()
    {
        return UIAnimator.GetBool("OpenMenu") || UIAnimator.GetBool("CloseMenu");
    }
}
