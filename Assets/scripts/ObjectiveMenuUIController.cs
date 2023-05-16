using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectiveMenuUIController : MonoBehaviour 
{
	[SerializeField] private GameObject content;
	[SerializeField] private RectTransform arrow;

	private bool isOpen = true;
	
	public void ToggleWindow()
    {
		isOpen = !isOpen;
		string value = isOpen ? "Open" : "Close";
		SessionRecorder.instance.WriteToSessionDataWithRound("Select Objective Menu,," + value);
		content.SetActive(isOpen);
		arrow.Rotate(new Vector3(0, 0, 180));
    }
}
