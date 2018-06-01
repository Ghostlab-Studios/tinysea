using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyramidTutorial : Tutorial {

    public RectTransform pyramidHighlight;

    public override void CheckIfHappening()
    {
        pyramidHighlight.gameObject.SetActive(true);
        TutorialManager.pubInstance.CompletedTutorial();
    }
}
