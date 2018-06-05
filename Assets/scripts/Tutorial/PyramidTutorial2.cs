using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PyramidTutorial2 : Tutorial {

    public RectTransform tier1EcoBar;
    public RectTransform tier2EcoBar;
    public RectTransform tier3EcoBar;
    public GameObject nextTutorial;
    bool nextClicked = false;
    public GameObject prevTutorial;
    bool prevClicked = false;
    Vector3 full = new Vector3(1, 1, 1);
    Vector3 empty = new Vector3(0, 1, 1);

    public override void CheckIfHappening()
    {
        tier1EcoBar.localScale = full;
        tier2EcoBar.localScale = full;
        tier3EcoBar.localScale = full;
        nextTutorial.GetComponent<Button>().onClick.AddListener(() => nextClicked = true);
        prevTutorial.GetComponent<Button>().onClick.AddListener(() => prevClicked = true);
        if (Input.GetKeyDown("space") || nextClicked)
        {
            prevClicked = false;
            nextClicked = false;
            tier1EcoBar.localScale = empty;
            tier2EcoBar.localScale = empty;
            tier3EcoBar.localScale = empty;
            TutorialManager.pubInstance.CompletedTutorial();
        }
        else if (Input.GetKeyDown("backspace") || prevClicked)
        {
            prevClicked = false;
            nextClicked = false;
            tier1EcoBar.localScale = empty;
            tier2EcoBar.localScale = empty;
            tier3EcoBar.localScale = empty;
            TutorialManager.pubInstance.ReversedTutorial();
        }
    }
}
