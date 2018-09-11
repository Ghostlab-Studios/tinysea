using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialTextTrigger : MonoBehaviour {

	public TutorialText tutorialText;

	public void TriggerTutorial () {
		
		FindObjectOfType<TutorialTextManager> ().StartTutorial (tutorialText);
	}
}
