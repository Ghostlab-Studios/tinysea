using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Adapter for logging through HTTP POST using Unity's WWW API.
public class SimpleWWWAdapter : MonoBehaviour
{
    //stuff to send info to an online repository.
    private string urlbase = "http://107.21.26.163/secphp/tinysea_to_server.php?user=tinysea&pass=083bfenouhb13bsd23rtepzxzlqw02349asdflk";
    private UnityWebRequest data;

    public void Handle(string node, string fileName)
    {
        string node_output = "test_output";
        // string test = "testfile";
        node_output = UnityWebRequest.EscapeURL(node);
        string url = urlbase + "&csv=" + node_output + "&file=" + fileName + ".csv";
        StartCoroutine(DoWWW(url, node_output));
    }

    IEnumerator DoWWW(string url, string form)
    {
        using (var data = UnityWebRequest.Post(url, form))
        {
            yield return data.SendWebRequest();
            Debug.Log(url + " : Web response code: " + data.downloadHandler.text);
            if (data.error != null)
            {
                Debug.Log("Web error: " + data.error);
            }
        }
    }
}
