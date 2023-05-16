using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls animations for the Settings Menu found in the main menu screen and gameplay scene.
/// </summary>
public class SettingsMenuController : MonoBehaviour 
{
    public Animator UIAnimator;
    public GameObject sfxSlider;
    public GameObject musicSlider;
    public GameObject homeMenu;
    public GameObject saveMenu;
    [SerializeField] private SceneLoad sceneLoader;

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
                if (homeMenu) { homeMenu.SetActive(false); }
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
        if (!IsBusy() && homeMenu) { homeMenu.SetActive(!homeMenu.activeSelf); }
    }

    public void SavePressed()
    {
        if (!IsBusy()) { saveMenu.SetActive(!saveMenu.activeSelf); }
    }

    public void ReturnHome()
    {
        SessionRecorder.instance.WriteToSessionDataWithRound("Return Home Selected,,");
        sceneLoader.LoadByIndex(0);
    }

    private bool IsBusy()
    {
        return UIAnimator.GetBool("OpenMenu") || UIAnimator.GetBool("CloseMenu");
    }
}
