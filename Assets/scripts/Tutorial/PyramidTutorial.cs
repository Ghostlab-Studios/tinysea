using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PyramidTutorial : Tutorial {

    public RectTransform pyramidHighlight;
    public GameObject nextTutorial;
    public GameObject prevTutorial;
    bool nextClicked = false;
    bool prevClicked = false;

    public override void CheckIfHappening()
    {
        pyramidHighlight.gameObject.SetActive(true);
        nextTutorial.GetComponent<Button>().onClick.AddListener(() => nextClicked = true);
        prevTutorial.GetComponent<Button>().onClick.AddListener(() => prevClicked = true);
        
        if (Input.GetKeyDown("space") || nextClicked)
        {
            nextClicked = false;
            prevClicked = false;
            TutorialManager.pubInstance.CompletedTutorial();
        }
        else if (Input.GetKeyDown("backspace") || prevClicked)
        {
            nextClicked = false;
            prevClicked = false;
            pyramidHighlight.gameObject.SetActive(false);
            TutorialManager.pubInstance.ReversedTutorial();
        }
    }
}
