using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BeginningTutorial : Tutorial {

    public override void CheckIfHappening()
    {
        if (Input.GetKeyDown("space"))
        {
            TutorialManager.pubInstance.CompletedTutorial();
        }
    }
}
