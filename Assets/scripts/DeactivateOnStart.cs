using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deactivates object on call of Start. Created as a work-around to find inactive GameObjects
/// which normally isn't possible. Thanks, Unity!
/// </summary>
public class DeactivateOnStart : MonoBehaviour
{
	void Start () {
        gameObject.SetActive(false);
	}
}
