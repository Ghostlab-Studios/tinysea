using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UImenu : MonoBehaviour
{
    [SerializeField]
    Animator menuAnim,musicAnim, homeAnim, resetAnim, saveAnim;

    private void Awake()
    {
//can't save this way in webgl mode so turn it off
#if UNITY_WEBGL
        if(saveAnim != null)
            saveAnim.gameObject.SetActive(false);
#else
        if(saveAnim != null)
            saveAnim.gameObject.SetActive(true);
#endif

        if (menuAnim == null && homeAnim == null && resetAnim == null)
            menuAnim = new Animator();
        if (homeAnim == null)
            homeAnim = new Animator();
        if (resetAnim == null)
            resetAnim = new Animator();
        if (musicAnim == null)
            musicAnim = new Animator();
        if (saveAnim == null)
            saveAnim = new Animator();
        menu_on();
    }

    public void menu_on()
    {
        menuAnim.speed = 2f;

        if (menuAnim.GetInteger("menuState") == 1)
        {
            menuAnim.SetInteger("menuState", 2);

            if (homeAnim != null)
                homeAnim.SetInteger("homeState", 2);
            if (resetAnim != null)
                resetAnim.SetInteger("resetState", 2);
            if (musicAnim != null)
                musicAnim.SetInteger("musicState", 2);
            if (saveAnim != null)
                saveAnim.SetInteger("saveState", 1);
        }
        else
        {
            menuAnim.SetInteger("menuState", 1);
        }
    }

    public void music_on(bool tutorial)
    {
        musicAnim.speed = 3;

        if (musicAnim.GetInteger("musicState") == 1)
        {
            musicAnim.SetInteger("musicState", 2);
        }
        else
        {
            musicAnim.SetInteger("musicState", 1);

            if(tutorial)
            {
                if (homeAnim != null)
                    homeAnim.SetInteger("homeState", 2);
                if (resetAnim != null)
                    resetAnim.SetInteger("resetState", 2);
            }
        }
    }

    public void reset_on()
    {
        resetAnim.speed = 3;

        if (resetAnim.GetInteger("resetState") == 1)
        {
            resetAnim.SetInteger("resetState", 2);
        }
        else
        {
            resetAnim.SetInteger("resetState", 1);

            if (homeAnim != null)
                homeAnim.SetInteger("homeState", 2);
            if (musicAnim != null)
                musicAnim.SetInteger("musicState", 2);
        }
    }

    public void save_on(GameObject save_tab)
    {
        saveAnim.speed = 3;

        if(save_tab.activeSelf)
        {
            saveAnim.SetInteger("saveState", 1);
            save_tab.SetActive(false);
        }
        else
        {
            saveAnim.SetInteger("saveState", 2);
            save_tab.SetActive(true);

            if (homeAnim != null)
                homeAnim.SetInteger("homeState", 2);
            if (musicAnim != null)
                musicAnim.SetInteger("musicState", 2);
        }
    }

    public void home_on()
    {
        homeAnim.speed = 3;

        if (homeAnim.GetInteger("homeState") == 1)
        {
            homeAnim.SetInteger("homeState", 2);
        }
        else
        {
            homeAnim.SetInteger("homeState", 1);

            if (resetAnim != null)
                resetAnim.SetInteger("resetState", 2);
            if (musicAnim != null)
                musicAnim.SetInteger("musicState", 2);
        }
    }
}
