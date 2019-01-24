using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Pages : MonoBehaviour {

	//Buttons
	public GameObject rightButton;
	public GameObject leftButton;
    public GameObject middleButton;

	//Panels
	public GameObject Panel1;
	public GameObject Panel2;
    public GameObject Panel3;

    public GameObject tier1Button;
    public GameObject tier2Button;
    public GameObject tier3Button;

    public int tier;


	// Use this for initialization
	void Start () {
        /*rightButton.GetComponent<Button> ().
			onClick.AddListener (() => Windows (rightButton, leftButton, middleButton, Panel1, Panel2, Panel3));
		leftButton.GetComponent<Button> ().
			onClick.AddListener (() => Windows (leftButton, rightButton, middleButton, Panel2, Panel3, Panel1));
        middleButton.GetComponent<Button>().
            onClick.AddListener(() => Windows(middleButton, rightButton, leftButton, Panel3, Panel1, Panel2));*/
        SetListeners();
	}
	
	// Update is called once per frame
	void Update () {

	}
	
	public void Windows(GameObject selectedButton, GameObject nonselectedButton1, GameObject nonselectedButton2, GameObject selectedPanel,GameObject nonselectedPanel1,
        GameObject nonSelectedPanel2){
		selectedPanel.SetActive (false);
		nonselectedPanel1.SetActive (true);
        nonSelectedPanel2.SetActive(false);
		selectedButton.SetActive (true);
		nonselectedButton1.SetActive (true);
        nonselectedButton2.SetActive(true);
        this.gameObject.SetActive(false);
		GameObject.Find ("ShopCanvas").GetComponent<Shop> ().selectionWindow.SetActive (false);
	}

    private void SetListeners()
    {
        if (this.gameObject.CompareTag("Common"))
        {
            rightButton.GetComponent<Button>().
                onClick.AddListener(() => Windows(rightButton, leftButton, middleButton, Panel1, Panel2, Panel3));
            
            leftButton.GetComponent<Button>().
                onClick.AddListener(() => Windows(leftButton, rightButton, middleButton, Panel1, Panel3, Panel2));
        }

        else if (this.gameObject.CompareTag("Arctic"))
        {
            rightButton.GetComponent<Button>().
                onClick.AddListener(() => Windows(rightButton, leftButton, middleButton, Panel1, Panel2, Panel3));
            middleButton.GetComponent<Button>().
                onClick.AddListener(() => Windows(middleButton, rightButton, leftButton, Panel1, Panel3, Panel2));
        }
        else if (this.gameObject.CompareTag("Tropical"))
        {
            leftButton.GetComponent<Button>().
                onClick.AddListener(() => Windows(leftButton, rightButton, middleButton, Panel1, Panel2, Panel3));
            middleButton.GetComponent<Button>().
                onClick.AddListener(() => Windows(middleButton, rightButton, leftButton, Panel1, Panel3, Panel2));
        }
    }

    private void TierListeners()
    {
        if (tier == 1)
        {
            tier2Button.GetComponent<Button>().
                onClick.AddListener(() => Tiers(Panel1));
            tier3Button.GetComponent<Button>().
                onClick.AddListener(() => Tiers(Panel1));
        }
        else if (tier == 2)
        {
            tier1Button.GetComponent<Button>().onClick.AddListener(() => Tiers(Panel1));
            tier3Button.GetComponent<Button>().onClick.AddListener(() => Tiers(Panel1));
        }
        else
        {
            tier1Button.GetComponent<Button>().onClick.AddListener(() => Tiers(Panel1));
            tier2Button.GetComponent<Button>().onClick.AddListener(() => Tiers(Panel1));
        }
    }

    private void Tiers(GameObject selectedPanel)
    {
        selectedPanel.SetActive(false);
        this.gameObject.SetActive(false);
        GameObject.Find("ShopCanvas").GetComponent<Shop>().selectionWindow.SetActive(false);
    }

}
