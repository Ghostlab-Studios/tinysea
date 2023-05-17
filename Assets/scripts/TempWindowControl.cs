using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class TempWindowControl : MonoBehaviour
{

    //initialize the window
    public GameObject window;
    //public Renderer button;


    //initialize Button
    public GameObject controler;
    public GameObject tempIcon;
    RectTransform rt;


    //define if it is open;
    public bool open = false;

    private float homeX = 30;
    public float slideAmount = 555;
    public float slideTime = .6f;
    public float slideTimer = 0;

    private bool guiPressed = false;

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
        if (open == false && guiPressed)
        {
            //tempIcon.GetComponent<Image>().enabled = false;
            controler.transform.rotation = Quaternion.Euler(0, 0, 180);
            rt.anchoredPosition = new Vector2
                (Mathf.Lerp(homeX + slideAmount, homeX, 1 - (slideTimer / slideTime)),
                 rt.anchoredPosition.y);
            //GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = false;

        }

        if (open == true)
        {
            controler.transform.rotation = Quaternion.Euler(0, 0, 0);
            rt.anchoredPosition = new Vector2
                (Mathf.Lerp(homeX, homeX + slideAmount, 1 - (slideTimer / slideTime)),
                 rt.anchoredPosition.y);
            
            //tempIcon.GetComponent<Image>().enabled = true;
            //GameObject.Find("Main Camera").GetComponent<MovingCamera>().enabled = true;

        }
    }


    public void Controler()
    {
        open = !open;
        string value = open ? "Open" : "Close";
        SessionRecorder.instance.WriteToSessionDataWithRound("Select Temperature Menu,," + value);
        slideTimer = slideTime;
        guiPressed = true;
    }

    public void AutoToggleWindow()
    {
        open = !open;
        string value = open ? "Open" : "Close";
        SessionRecorder.instance.WriteToSessionDataWithRound(",Auto-Toggle Temperature Window," + value);
        slideTimer = slideTime;
        guiPressed = true;
    }

    public bool IsNotMoving()
    {
        return slideTimer == 0;
    }
}
