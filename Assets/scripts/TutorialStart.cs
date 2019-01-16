using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialStart : MonoBehaviour {

	public TutorialTextTrigger trigger;

	// Use this for initialization
	void Start () {
        starting();
    }

    //if players wanna restart the tutorial
    public void starting()
    {
        trigger.TriggerTutorial();
    }


}
