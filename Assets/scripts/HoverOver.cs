using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoverOver : MonoBehaviour {

    public Vector3 offset;

    private void Update()
    {
        transform.position = Input.mousePosition + offset;
    }
}
