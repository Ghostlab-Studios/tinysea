using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialStart : MonoBehaviour {

	public TutorialTextTrigger trigger;

	// Use this for initialization
	void Start () {
		trigger.TriggerTutorial ();
	}


}
