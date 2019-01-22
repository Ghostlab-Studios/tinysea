using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopTutorialThree : Tutorial {

    public PlayerManager player;
    public GameObject nextButton;
    public Text tutorialText;
    bool clicked = false;

    public override void CheckIfHappening()
    {
        nextButton.GetComponent<Button>().onClick.AddListener(() => clicked = true);

        if (clicked)
        {
            if (!player.busy)
            { 
                if (player.getTotalFishCount() > 0)
                {
                    TutorialManager.pubInstance.CompletedTutorial();
                }
                else
                {
                    tutorialText.text = "Hmm... Looks like you may not have selected the right fish. Try again!";
                    clicked = false;
                }
            }
        }
    }
}
