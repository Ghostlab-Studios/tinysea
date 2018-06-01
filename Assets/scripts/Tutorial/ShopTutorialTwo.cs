using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopTutorialTwo : Tutorial {

    public PlayerManager player;

    public override void CheckIfHappening()
    {
        if (player.getTotalFishCount() > 0)
        {
            TutorialManager.pubInstance.CompletedTutorial();
        }
    }
}
