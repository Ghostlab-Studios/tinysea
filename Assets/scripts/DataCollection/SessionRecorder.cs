using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SessionRecorder : MonoBehaviour
{
	public static SessionRecorder instance;
	public bool isRecording = true;

	private string urlbase = "http://107.21.26.163/secphp/tinysea_to_server.php?user=tinysea&pass=083bfenouhb13bsd23rtepzxzlqw02349asdflk";
	private UnityWebRequest data;
	private UITurnCounter turnCounter;
	private Queue<string> sessionQueue;

	private void Awake()
	{
		if (instance == null) { instance = this; }
		else { Destroy(gameObject); }
	}

	private void Start()
	{
		if (sessionQueue == null)
		{
			sessionQueue = new Queue<string>();
			if (isRecording) WebRequestEnqueue("Timestamp,Round,Action,Type,Value\n");
		}
	}
	
	public void WriteToSessionData(string data)
    {
		string currentSessionTime = SessionTimer.FormatSessionTime();
		Debug.Log(currentSessionTime + ",," + data);
		if (isRecording) WebRequestEnqueue(currentSessionTime + ",," + data + "\n");
	}

	public void WriteToSessionDataWithRound(string data)
    {
		if (turnCounter == null) { turnCounter = GameObject.FindGameObjectWithTag("TurnCounter").GetComponent<UITurnCounter>(); }
		string currentSessionTime = SessionTimer.FormatSessionTime();
		string dataWithTurn = turnCounter.GetTurn() + "," + data;
		Debug.Log(currentSessionTime + "," + dataWithTurn);
		if (isRecording) WebRequestEnqueue(currentSessionTime + "," + dataWithTurn + "\n");
	}

	public void WriteToSessionDataWithNoTimestampAndRound(string data)
    {
		string currentSessionTime = SessionTimer.FormatSessionTime();
		Debug.Log(",," + data);
		if (isRecording) WebRequestEnqueue(",," + data + "\n");
	}

	private void WebRequestEnqueue(string data)
    {
		sessionQueue.Enqueue(data);
    }

	private string CompileWebRequestQueue()
    {
		string compilation = "";

		while (sessionQueue.Count > 0)
        {
			string node = sessionQueue.Dequeue();
			compilation += node;
        }

		return compilation;
    }

	public void RunWebRequestQueue()
    {
		string node = CompileWebRequestQueue();
		if (node == "")
        {
			Debug.Log("Write failed: empty queue");
			return;
        }

		string node_output = "test_output";
		node_output = UnityWebRequest.EscapeURL(node);
		string url = urlbase + "&csv=" + node_output + "&file=" + SessionIDGenerator.sessionID.ToString() + ".csv";
		StartCoroutine(DoWWW(url, node_output));
	}

	private IEnumerator DoWWW(string url, string form)
	{
		using (var data = UnityWebRequest.Post(url, form))
		{
			yield return data.SendWebRequest();
			// Debug.Log(url + " : Web response code: " + data.downloadHandler.text);
			if (data.error != null)
			{
				Debug.Log("Web error: " + data.error);
			}
		}
	}
}