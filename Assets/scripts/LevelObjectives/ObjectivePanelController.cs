using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for level objective panels. Used to set objective text, deactivate unused panels, and
/// check the checkbox when an objective is complete.
/// </summary>
public class ObjectivePanelController : MonoBehaviour
{
	[SerializeField] private Text textBox;
	[SerializeField] private GameObject checkMark;

	public void SetObjectiveText(string text)
    {
		textBox.text = text;
    }

	public void ObjectiveIsComplete()
    {
		checkMark.SetActive(true);
		textBox.tag = "Untagged";
    }
}
