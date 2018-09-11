using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialTextManager : MonoBehaviour {

	public Text nameSpace;
	public Text textSpace;
	private Queue<string> sentences;

	public Animator textAnimator;
	public Animator tutoAnimator;
	public Button contButton;
	public bool isTutorialScene;

	private TutorialTextTrigger nextDialogue;
	private GameObject visualAid;
	private bool hasVisualAid;


	// Use this for initialization
	void Awake () {
		sentences = new Queue<string>();
	}

	public void StartTutorial(TutorialText tutorialText) {
		textAnimator.SetInteger ("TextPosition", tutorialText.textPosition);
		tutoAnimator.SetInteger ("PositionState", tutorialText.textPosition); 
		sentences.Clear (); 
		nameSpace.text = tutorialText.name;
		nextDialogue = tutorialText.nextDialogue;
		visualAid = tutorialText.visualAid;

		foreach (string sentence in tutorialText.sentences) {
			sentences.Enqueue (sentence);
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

	public void DisplayNextSentence() {

		if (sentences.Count == 0) {
			EndTutorial ();
			return;
		}
		tutoAnimator.SetInteger ("stateVal", Random.Range (0, 3));
		string sentence = sentences.Dequeue (); 
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
				yield break;
			}
			textSpace.text += letter;
			yield return new WaitForSeconds(0.02f);
		}
		contButton.interactable = true;
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
