using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeactivateInFreeplay : MonoBehaviour {
    
	void Start () {
		if (LevelLoader.levelToLoad == -1)
        {
            gameObject.SetActive(false);
        }
	}
}
