using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UImenu : MonoBehaviour
{
    [SerializeField]
    Animator thisAnim;

    private void Awake()
    {
        //thisAnim.SetInteger("menuState", 2);
    }

    public void menu_on()
    {
        if (thisAnim.GetInteger("menuState") == 1)
        {
            thisAnim.SetInteger("menuState", 2);
        }
        else
        {
            thisAnim.SetInteger("menuState", 1);
        }
    }
}
