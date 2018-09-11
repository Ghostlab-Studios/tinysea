using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GenVSpecVisual : MonoBehaviour {

	public Text textBox;
	public Animator anim;
	[TextArea(3, 10)]
	public string[] targetStrings;


	private int count = 0;
	private string targetString;

	// Use this for initialization
	void Start () {
		targetString = targetStrings [count];

	}

	//This is an expensive way to calculate this
	void Update () {

		if (textBox.text.Contains(targetString) && count < targetStrings.Length-1) {
				count++;
				targetString = targetStrings [count];
				anim.SetInteger ("animState", count);
		}

	}


	public void SimulateTank1() {
		anim.SetBool ("tank1Simulate", true);

	}

	public void SimulateTank2() {
		anim.SetBool ("tank2Simulate", true);
	}
		

}
