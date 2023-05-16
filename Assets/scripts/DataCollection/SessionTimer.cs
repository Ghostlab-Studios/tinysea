using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SessionTimer : MonoBehaviour 
{
	public static float sessionTime = 0f;
	public static float sessionRoundTime = 0f;

	private void Start() 
	{
		DontDestroyOnLoad(this);
	}
	
	private void Update() 
	{
		sessionTime += Time.deltaTime;
		sessionRoundTime += Time.deltaTime;
	}

	public static string FormatSessionTime()
    {
		int hours = Mathf.FloorToInt(sessionTime / 60f / 60f);
		int minutes = Mathf.FloorToInt(sessionTime / 60f % 60);
		int seconds = Mathf.FloorToInt(sessionTime % 60);
		return hours.ToString() + ":" + minutes.ToString() + ":" + seconds.ToString();
    }

	public static string FormatSessionRoundTime()
    {
		int hours = Mathf.FloorToInt(sessionRoundTime / 60f / 60f);
		int minutes = Mathf.FloorToInt(sessionRoundTime / 60f % 60);
		int seconds = Mathf.FloorToInt(sessionRoundTime % 60);
		return hours.ToString() + ":" + minutes.ToString() + ":" + seconds.ToString();
	}

	public static void ResetRoundTime()
    {
		sessionRoundTime = 0;
    }
}
