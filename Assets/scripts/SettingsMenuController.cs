using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMenuController : MonoBehaviour {

    public Animator UIAnimator;

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
}
