using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SessionIDPanelController : MonoBehaviour 
{
	[SerializeField] private InputField sessionIDText;

	private void Start()
    {
		if (SessionIDGenerator.sessionID != "") { UpdateSessionIDText(); }
    }

	public void UpdateSessionIDText()
    {
		sessionIDText.text = SessionIDGenerator.sessionID;
    }

	public void CopyIDToClipboard()
    {
		GUIUtility.systemCopyBuffer = SessionIDGenerator.sessionID;
	}
}
