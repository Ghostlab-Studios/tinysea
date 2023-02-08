using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public Animator anim;

    private int scene = -1;

    private void Update()
    {
        if (scene >= 0 && !anim.GetBool("FinishedState"))
        {
            SceneManager.LoadScene(scene);
        }
    }

    public void TutorialPressed()
    {
        scene = 2;
        anim.SetTrigger("FinishedState");
        anim.SetTrigger("TutorialClicked");
    }

    public void FreeplayPressed()
    {
        scene = 1;
        LevelLoader.levelToLoad = -1;
        anim.SetTrigger("FinishedState");
        anim.SetTrigger("FreeplayClicked");
    }

    public void LevelSelectPressed()
    {
        anim.SetTrigger("FinishedState");
        anim.SetTrigger("LevelSelectClicked");
    }
}
