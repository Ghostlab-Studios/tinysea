using UnityEngine;
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

    private float homeY = 0;
    public float slideAmount = 482;
    public float slideTime = .6f;
    public float slideTimer = 0;

	void Start()
    {
		rt = (RectTransform)window.transform;
        homeY = rt.anchoredPosition.y;
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

	void OnGUI()
    {
		/*controler.GetComponent<Button>().
			onClick.AddListener (() => Controler());*/
		if (open == false)
        {
            //shopIcon.SetActive(true);
			controller.transform.rotation = Quaternion.Euler(0,0,-90);
            rt.anchoredPosition = new Vector2
				(rt.anchoredPosition.x,
                 Mathf.Lerp(homeY + slideAmount, homeY, 1 - (slideTimer / slideTime)));
			//GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = false;

		}

		if (open == true) {
            //shopIcon.SetActive(false);
			controller.transform.rotation = Quaternion.Euler(0,0,90);
            rt.anchoredPosition = new Vector2
                (rt.anchoredPosition.x,
                 Mathf.Lerp(homeY, homeY + slideAmount, 1 - (slideTimer / slideTime)));
			//GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = true;

		}
	}


	public void Controler(){
		open = !open;
        string value = open ? "Open" : "Close";
        SessionRecorder.instance.WriteToSessionDataWithRound("Select Shop Menu,," + value);
        slideTimer = slideTime;
	}

    public bool IsNotMoving()
    {
        return slideTimer == 0;
    }
}
