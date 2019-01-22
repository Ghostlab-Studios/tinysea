using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour {

    // Use this for initialization
    void Start()
    {

    }

    public void ChangeScene(string scene)
    {
        //Application.LoadLevel(scene);
        SceneManager.LoadScene(scene);
    }
}
