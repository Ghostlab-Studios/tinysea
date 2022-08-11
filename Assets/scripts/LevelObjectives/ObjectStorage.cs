using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A probably bad solution to needing to find many objects at once under the same GameTag.
/// Only intended purpose is to get multiple game objects at once without needing to use
/// "FindGameObjectWithTag."
/// </summary>
public class ObjectStorage : MonoBehaviour
{
    public GameObject[] objects;
}
