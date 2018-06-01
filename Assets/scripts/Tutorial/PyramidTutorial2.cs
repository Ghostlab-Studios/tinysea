using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyramidTutorial2 : Tutorial {

    public RectTransform tier1EcoBar;
    public RectTransform tier2EcoBar;
    public RectTransform tier3EcoBar;
    Vector3 tiersProp = new Vector3(1, 1, 1);

    float flashSpeed = 3.0f;
    int highlightPeriod = 3;

    public override void CheckIfHappening()
    {
        tier1EcoBar.localScale = new Vector3(tiersProp.x, 1, 1);
        tier2EcoBar.localScale = new Vector3(tiersProp.y, 1, 1);
        tier3EcoBar.localScale = new Vector3(tiersProp.z, 1, 1); 
        if (Input.GetKeyDown("space"))
        {
            TutorialManager.pubInstance.CompletedTutorial();
        }
    }
}
