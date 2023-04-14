﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class EcoWindowControl : MonoBehaviour
{

    //initialize the window
    public GameObject window;
    //public Renderer button;


    //initialize Button
    public GameObject controler;
    public GameObject ecoIcon;
    RectTransform rt;


    //define if it is open;
    public bool open = false;

    private float homeX = 0;
    public float slideAmount = 555;
    public float slideTime = .6f;
    public float slideTimer = 0;

    void Start()
    {
        rt = (RectTransform)window.transform;
        homeX = rt.anchoredPosition.x;
        controler.GetComponent<Button>().
            onClick.AddListener(() => Controler());
        //float startX = rt.anchoredPosition.x;
    }

    void Update()
    {
        if (slideTimer < 0)
        {
            slideTimer = 0;
        }
        if (slideTimer > 0)
        {
            slideTimer -= Time.deltaTime;
        }
    }

    void OnGUI()
    {
        /*controler.GetComponent<Button>().
			onClick.AddListener (() => Controler());*/
        if (open == false)
        {
            //ecoIcon.GetComponent<Image>().enabled = true;
            controler.transform.rotation = Quaternion.Euler(0, 0, 0);
            rt.anchoredPosition = new Vector2
                (Mathf.Lerp(homeX - slideAmount, homeX, 1 - (slideTimer / slideTime)),
                 rt.anchoredPosition.y);
            //GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = false;

        }

        if (open == true)
        {
            //ecoIcon.GetComponent<Image>().enabled = false;
            controler.transform.rotation = Quaternion.Euler(0, 0, 180);
            rt.anchoredPosition = new Vector2
                (Mathf.Lerp(homeX, homeX - slideAmount, 1 - (slideTimer / slideTime)),
                 rt.anchoredPosition.y);
            //GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = true;

        }
    }


    void Controler()
    {
        open = !open;
        slideTimer = slideTime;
    }

}
