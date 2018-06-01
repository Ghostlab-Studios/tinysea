using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour {

    public List<Tutorial> tutorials = new List<Tutorial>();
    public Text expText;

    private static TutorialManager instance;
    public static TutorialManager pubInstance
    {
        get
        {
            if (instance == null)
            {
                instance = GameObject.FindObjectOfType<TutorialManager>();
            }
            return instance;
        }
    }

    private Tutorial currentTutorial;

    private void Start()
    {
        SetNextTutorial(0);
    }

    private void Update()
    {
        if (currentTutorial)
        {
            currentTutorial.CheckIfHappening();
        }
    }

    public Tutorial GetTutorial(int order)
    {
        for (int i = 0; i < tutorials.Count; i++)
        {
            if (tutorials[i].order == order)
            {
                return tutorials[i];
            }
        }
        return null;
    }

    public void SetNextTutorial(int currentOrder)
    {
        currentTutorial = GetTutorial(currentOrder);

        if (!currentTutorial)
        {
            CompletedAllTutorials();
            return;
        }

        expText.text = currentTutorial.explanation;

    }

    public void CompletedTutorial()
    {
        SetNextTutorial(currentTutorial.order + 1);
    }

    public void CompletedAllTutorials()
    {
        //expText.text = "Completed all tutorials";
    }


}
