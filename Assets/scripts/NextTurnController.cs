using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Disables the Next Turn button while processes are busy doing other things.
/// </summary>
public class NextTurnController : MonoBehaviour 
{
	private Button nextTurnButton;
	private PlayerManager pm;

	private void Start()
	{
		nextTurnButton = GetComponent<Button>();
		pm = GameObject.FindGameObjectWithTag("PlayerManager").GetComponent<PlayerManager>();
	}
	
	private void Update()
	{
		nextTurnButton.interactable = !pm.busy && !pm.holder.anyCreaturesBusy();
	}
}
