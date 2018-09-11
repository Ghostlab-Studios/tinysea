using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TutorialText {

	public string name;
	public int textPosition;
	[TextArea(3, 10)]
	public string[] sentences;
	public TutorialTextTrigger nextDialogue = null;
	public GameObject visualAid = null;
}
