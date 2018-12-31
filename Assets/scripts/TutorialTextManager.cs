using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialTextManager : MonoBehaviour {

	public Text nameSpace;
	public Text textSpace;
	private List<string> sentences;
    private int count_sent;

	public Animator textAnimator;
	public Animator tutoAnimator;
    public Button contButton;
    public Button backButton;
	public bool isTutorialScene;

    private TutorialTextTrigger lastDialogue;
    private TutorialTextTrigger nextDialogue;
	private GameObject visualAid;
	private bool hasVisualAid;
    private bool goingBack;


	// Use this for initialization
	void Awake () {
		sentences = new List<string>();
        count_sent = 0;
        goingBack = false;
	}

	public void StartTutorial(TutorialText tutorialText) {
		textAnimator.SetInteger ("TextPosition", tutorialText.textPosition);
		tutoAnimator.SetInteger ("PositionState", tutorialText.textPosition); 
		sentences.Clear (); 
		nameSpace.text = tutorialText.name;
        lastDialogue = tutorialText.lastDialogue;
		nextDialogue = tutorialText.nextDialogue;
		visualAid = tutorialText.visualAid;

		foreach (string sentence in tutorialText.sentences) {
			sentences.Add (sentence);
		}
		
        if(goingBack)
        {
            count_sent = sentences.Count - 1;
            goingBack = false;
        }

		DisplayNextSentence ();
		tutoAnimator.SetInteger ("stateVal", 0);
		if (visualAid != null){
			hasVisualAid = true;
		} 

		if (hasVisualAid) {
			visualAid.SetActive (true);
		}
	}

    public void DisplayLastSentence(){

        count_sent--;
        if (count_sent < 1)
        {
            LastTutorial();
            return;
        }
        tutoAnimator.SetInteger("stateVal", Random.Range(0, 3));
        string sentence = sentences[count_sent-1];
        StopAllCoroutines();
        backButton.interactable = false;
        StartCoroutine(TypeSentence(sentence));
    }

	public void DisplayNextSentence() {

		if (sentences.Count <= count_sent) {
            count_sent = 0;
			EndTutorial ();
			return;
		}
		tutoAnimator.SetInteger ("stateVal", Random.Range (0, 3));
		string sentence = sentences[count_sent];
        count_sent++;
		StopAllCoroutines ();
		contButton.interactable = false;
		StartCoroutine (TypeSentence (sentence));
	}

	IEnumerator TypeSentence (string sentence){
		textSpace.text = "";
		foreach (char letter in sentence.ToCharArray()) {
			if (Input.GetMouseButtonDown (0)) {
				textSpace.text = sentence;
				contButton.interactable = true;
                backButton.interactable = true;
				yield break;
			}
			textSpace.text += letter;
			yield return new WaitForSeconds(0.02f);
		}
		contButton.interactable = true;
        backButton.interactable = true;
	}

    void LastTutorial(){

        if (hasVisualAid)
            visualAid.SetActive(false);
        hasVisualAid = false;

        if (lastDialogue == null)
        {
            count_sent = 1;
            return;
        }
        else
        {
            goingBack = true;
            lastDialogue.TriggerTutorial();
        }
    }

	void EndTutorial() {

		if (hasVisualAid)
			visualAid.SetActive(false);
		hasVisualAid = false;

		if (nextDialogue == null) {
			textAnimator.SetInteger ("TextPosition", 0);
			tutoAnimator.SetInteger ("PositionState", 0);
			if (isTutorialScene) {
				SceneManager.LoadScene (sceneBuildIndex: 1);
			}
		} else {
			nextDialogue.TriggerTutorial (); 
		}
	}
}
