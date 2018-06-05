using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PyramidTutorialThree : Tutorial {

    public RectTransform pyramidHighlight;
    public GameObject nextTutorial;
    bool nextClicked = false;

    public override void CheckIfHappening()
    {
        pyramidHighlight.gameObject.SetActive(false);
        nextTutorial.GetComponent<Button>().onClick.AddListener(() => nextClicked = true);
        if (Input.GetKeyDown("space") || nextClicked)
        {
            nextClicked = false;
            TutorialManager.pubInstance.CompletedTutorial();
        }
        
    }
}
