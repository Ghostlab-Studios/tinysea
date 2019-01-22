using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopTutorial : Tutorial {

    public GameObject button;
    bool clicked = false;
    public RectTransform buttonHighlight;

    public override void CheckIfHappening()
    {
        buttonHighlight.gameObject.SetActive(true);
        button.GetComponent<Button>().onClick.AddListener(() => clicked = true);

        if (clicked)
        {
            buttonHighlight.gameObject.SetActive(false);
            TutorialManager.pubInstance.CompletedTutorial();
        }
    }
}
