using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyramidTutorialThree : Tutorial {

    public RectTransform pyramidHighlight;

    public override void CheckIfHappening()
    {
        pyramidHighlight.gameObject.SetActive(false);
        if (Input.GetKeyDown("space"))
        {
            TutorialManager.pubInstance.CompletedTutorial();
        }
        
    }
}
