using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GenVSpecVisual : MonoBehaviour {

	public Text textBox;
	public Animator anim;
	[TextArea(3, 10)]
	public string[] targetStrings;
    public string lastString;


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
			anim.SetInteger ("animState", anim_count);
		}
        else if (textBox.text.Contains(backString))
        {
            if (anim_count > 0)
            {
                anim_count--;
                anim.SetInteger ("animState", anim_count);

                text_count--;
                targetString = targetStrings[text_count];
                if (text_count > 1)
                    backString = targetStrings[text_count - 2];
                else
                    backString = "empty";
            }
        }
        else if(textBox.text.Contains(lastString))
        {
            text_count = targetStrings.Length - 1;
            anim_count = targetStrings.Length - 2;
            backString = targetStrings[text_count - 1];
            targetString = targetStrings[text_count];
            anim.SetInteger("animState", anim_count);
        }
    }

    public void reset_state()
    {
        anim_count = 0; text_count = 1;
        backString = "empty";
        targetString = targetStrings[text_count];
        anim.SetInteger("animState", 0);
    }

	public void SimulateTank1(GameObject button) {
		anim.SetBool ("tank1Simulate", true);
        button.GetComponent<Button>().interactable = false;
	}

	public void SimulateTank2(GameObject button) {
		anim.SetBool ("tank2Simulate", true);
        button.GetComponent<Button>().interactable = false;
	}
		

}
