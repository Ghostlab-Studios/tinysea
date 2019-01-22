using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BeginningTutorial : Tutorial {

    public GameObject nextTutorial;
    public GameObject prevTutorial;
    public bool nextClicked = false;
    public bool prevClicked = false;

    public override void CheckIfHappening()
    {
        nextTutorial.GetComponent<Button>().onClick.AddListener(() => nextClicked = true);

        if (order != 0)
        {
            if (!prevTutorial.activeInHierarchy)
            {
                prevTutorial.SetActive(true);
            }
            prevTutorial.GetComponent<Button>().onClick.AddListener(() => prevClicked = true);
        }
        else
        {
            prevTutorial.SetActive(false);
        }

        if (Input.GetKeyDown("space") || nextClicked)
        {
            prevClicked = false;
            nextClicked = false;
            TutorialManager.pubInstance.CompletedTutorial();
        }
        if (Input.GetKeyDown("backspace") || prevClicked)
        {
            prevClicked = false;
            nextClicked = false;
            TutorialManager.pubInstance.ReversedTutorial();
        }
    }
}
