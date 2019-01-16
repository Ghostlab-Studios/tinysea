﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class ShopWindowControl : MonoBehaviour {

	//initialize the window
	public GameObject window;
	//public Renderer button;


	//initialize Button
	public GameObject controller;
    public GameObject shopIcon;
    public GameObject exitButton;
	RectTransform rt;

	
	//define if it is open;
	public bool open = false;

    private float homeX = 0;
    public float slideAmount = 480;
    public float slideTime = .6f;
    public float slideTimer = 0;

	void Start() 
    {
		rt = (RectTransform)window.transform;
		controller.GetComponent<Button>().
			onClick.AddListener (() => Controler());
        exitButton.GetComponent<Button>().
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

	void OnGUI(){
		/*controler.GetComponent<Button>().
			onClick.AddListener (() => Controler());*/
		if (open == true) {
            shopIcon.SetActive(false);
			controller.transform.rotation = Quaternion.Euler(0,0,180);
			rt.anchoredPosition = new Vector2
				(Mathf.Lerp (homeX - slideAmount, homeX, 1 - (slideTimer / slideTime)),
                 rt.anchoredPosition.y);
			//GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = false;

		}

		if (open == false) {
            shopIcon.SetActive(true);
			controller.transform.rotation = Quaternion.Euler(0,0,0);
            rt.anchoredPosition = new Vector2
                (Mathf.Lerp(homeX, homeX - slideAmount, 1 - (slideTimer / slideTime)),
                 rt.anchoredPosition.y);
			//GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = true;

		}
	}


	void Controler(){
		open = !open;
        slideTimer = slideTime;
	}

}
