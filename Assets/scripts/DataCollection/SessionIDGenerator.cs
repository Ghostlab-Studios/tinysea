using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SessionIDGenerator : MonoBehaviour
{
	public static string sessionID = "";
	public UnityEvent onSessionIDGenerated;

	private string[] chars = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l",
			"m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "1", "2", "3", "4",
			"5", "6", "7", "8", "9", "0" };

	private void Start()
	{
		if (sessionID == "")
		{
			sessionID = GenerateSessionID(15);
			onSessionIDGenerated.Invoke();
		}
	}

	private string GenerateSessionID(int length)
    {
		string sessionID = "";
		for (int i = 0; i < length; i++)
        {
			sessionID += chars[Random.Range(0, chars.Length)];
        }
		Debug.Log(sessionID);
		return sessionID;
    }
}
