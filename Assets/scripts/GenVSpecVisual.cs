using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GenVSpecVisual : MonoBehaviour {

	public Text textBox;
	public Animator anim;
	[TextArea(3, 10)]
	public string[] targetStrings;


	private int anim_count = 0, text_count = 1;
	private string targetString;
    private string backString;

	// Use this for initialization
	void Start () {
		targetString = targetStrings [text_count];
        backString = "empty";
	}

	//This is an expensive way to calculate this
	void Update ()
    {
        if (textBox.text.Contains(targetString) && text_count < targetStrings.Length - 1)
        {
            backString = targetStrings[text_count - 1];
            anim_count++;
            text_count++;
			targetString = targetStrings [text_count];
            anim.SetInteger ("backState", 0);
			anim.SetInteger ("animState", anim_count);
		}
        else if (textBox.text.Contains(backString))
        {
            if (anim_count > 0)
            {
                anim_count--;
                anim.SetInteger ("animState", 0);
                anim.SetInteger ("backState", (anim_count + 1));

                text_count--;
                targetString = targetStrings[text_count];
                if (text_count > 1)
                    backString = targetStrings[text_count - 2];
                else
                    backString = "empty";
            }
        }

    }


	public void SimulateTank1() {
		anim.SetBool ("tank1Simulate", true);

	}

	public void SimulateTank2() {
		anim.SetBool ("tank2Simulate", true);
	}
		

}
