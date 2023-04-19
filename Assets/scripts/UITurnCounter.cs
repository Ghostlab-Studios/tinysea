using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITurnCounter : MonoBehaviour 
{
	[SerializeField]
	private PlayerManager pm;
	[SerializeField]
	private Text turnText;

	private int turnCount = 0;

	private void Start() 
	{
		pm.onNextTurn.AddListener(IncrementTurnCounter);
	}
	
	private void IncrementTurnCounter()
    {
		turnCount++;
		turnText.text = turnCount.ToString();
    }
}
