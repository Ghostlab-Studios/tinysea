using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine.Events;

public class Shop : MonoBehaviour {

    public PlayerManager playerObj;

	//initialize the prefab
	public GameObject selectionWindow;
	public List<Text> priceTexts;
    public List<GameObject> Pages;
    public GameObject costPrefab;

	//text
	public Text totalfishes;
    public Text totalPrice;
    public Text currentName;

    public Text sellingfishes;
    public Text sellingPrice;
    public Text sellingName;

    public string eatingName = "Hunger";
    public string reproductionName = "Spawn Rate";
    public string deathThreasholdName = "Resilience";
    public string deathRateName = "Health";
    public string thermalBreadthName = "Thermal Tolerance";
    public string starChar = "★";

    public Text eatingText;
    public Text reproductionText;
    public Text deathThreasholdText;
    public Text deathRateText;
    public Text thermalBreadthText;
    public Text descriptionText;

    int currentTier = 1;

    //curve
    public CurveRenderer curveRender;

	//sell slider
    public Scrollbar slider;
	
	// Tires' button
	public GameObject tire1Button;
	public GameObject tire2Button;
	public GameObject tire3Button;

    //tires' selection
    public GameObject tire1Selected;
	public GameObject tire2Selected;
	public GameObject tire3Selected;
    public GameObject tier1ASelected;
    public GameObject tier1TSelected;
    public GameObject tier2ASelected;
    public GameObject tier2TSelected;
    public GameObject tier3ASelected;
    public GameObject tier3TSelected;

    public Button buyIncrease;
    public Button buyDecrease;
    public Button sellIncrease;
    public Button sellDecrease;

    public int selectedFish = 0;
    private int currentFishes = 0;
    public int currentSellingFishes = 0;

    private WaitForSeconds buffer = new WaitForSeconds(0.2f);
    public bool buyIncreasing;
    public bool buyDecreasing;
    public bool sellIncreasing;
    public bool sellDecreasing;
    private bool allowPress = true;

    //to check how many times the respective buttons are clicked
    public int buy_press { get; set; }
    public int sell_press { get; set; }

    public UnityEvent OnSell;

    // Use this for initialization
    void Start()
    {
        buy_press = 0;
        sell_press = 0;
        /*tire1Button.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tire1Selected, tire2Selected, tire3Selected, 1));
        tire2Button.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tire2Selected, tire1Selected, tire3Selected, 2));
        tire3Button.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tire3Selected, tire1Selected, tire2Selected, 3));
        selectionWindow.SetActive(false);*/

        tire1Button.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tire1Selected, tire2Selected, tier2ASelected, tier2TSelected, tire3Selected, tier3ASelected, tier3TSelected));
        tire2Button.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tire2Selected, tire1Selected, tier1ASelected, tier1TSelected, tire3Selected, tier3ASelected, tier3TSelected));
        tire3Button.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tire3Selected, tire1Selected, tier1ASelected, tier1TSelected, tire2Selected, tier2ASelected, tier2TSelected));
        selectionWindow.SetActive(false);

        /*tier1AButton.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tier1ASelected, tier2ASelected, tier3ASelected, 1));
        tier2AButton.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tier2ASelected, tier1ASelected, tier3ASelected, 2));
        tier3AButton.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tier3ASelected, tier1ASelected, tier2ASelected, 3));

        tier1TButton.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tier1TSelected, tier2TSelected, tier3TSelected, 1));
        tier2TButton.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tier2TSelected, tier1TSelected, tier3TSelected, 2));
        tier3TButton.GetComponent<Button>().
            onClick.AddListener(() => EnableWindow(tier3TSelected, tier1TSelected, tier1TSelected, 1));*/

        buttonPress(0);

        writePricebox();
    }

	void Awake(){
        //add cost boxes to all the buttons
        priceTexts = new List<Text>();
        foreach (GameObject p in Pages)
        {
            foreach (Transform child in p.transform)
            {
                GameObject nText = GameObject.Instantiate(costPrefab);
                RectTransform rT = nText.GetComponent<RectTransform>();
                RectTransform prefabRT = costPrefab.GetComponent<RectTransform>();
                rT.SetParent(child.transform);
                rT.localScale = prefabRT.localScale;
                rT.localPosition = prefabRT.localPosition;
                rT.offsetMax = prefabRT.offsetMax;
                rT.offsetMin = prefabRT.offsetMin;
                priceTexts.Add(nText.GetComponent<Text>());
            }
        }
	}

    public void ToggleBuyIncrease()
    {
        if (buyIncreasing)
        {
            buyIncreasing = false;
        }
        else
        {
            buyIncreasing = true;
        }
    }

    public void ToggleBuyDecrease()
    {
        if (buyDecreasing)
        {
            buyDecreasing = false;
        }
        else
        {
            buyDecreasing = true;
        }
    }

    public void ToggleSellIncrease()
    {
        if (sellIncreasing)
        {
            sellIncreasing = false;
        }
        else
        {
            sellIncreasing = true;
        }
    }

    public void ToggleSellDecrease()
    {
        if (sellDecreasing)
        {
            sellDecreasing = false;
        }
        else
        {
            sellDecreasing = true;
        }
    }

    public void writePricebox (){
        //collect all the fish costs
        List<float> costs = new List<float>();
        foreach (CharacterManager c in playerObj.species)
        {
            costs.Add(c.cost);
        }

        //update the text boxes
        
        for (int i = 0; i < costs.Count; i++)
        {
            priceTexts[i].text = "$" + costs[i];
        }
	}

    private void Update()
    {
        if (buyIncreasing)
        {
            if (allowPress)
            {
                StartCoroutine(BuySellAction(0));
            }
            else
            {
                return;
            }
        }
        if (buyDecreasing)
        {
            if (allowPress)
            {
                StartCoroutine(BuySellAction(1));
            }
            else return;
        }
        if (sellIncreasing)
        {
            if (allowPress)
            {
                StartCoroutine(BuySellAction(2));
            }
            else return;
        }
        if (sellDecreasing)
        {
            if (allowPress)
            {
                StartCoroutine(BuySellAction(3));
            }
            else return;
        }
    }

    private IEnumerator BuySellAction(int index)
    {
        switch (index)
        {
            case 0:
                allowPress = false;
                yield return buffer;
                addfishes(1);
                allowPress = true;
                break;
            case 1:
                allowPress = false;
                addfishes(-1);
                yield return buffer;
                allowPress = true;
                break;
            case 2:
                allowPress = false;
                addSellingFishes(1);
                yield return buffer;
                allowPress = true;
                break;
            default:
                allowPress = false;
                addSellingFishes(-1);
                yield return buffer;
                allowPress = true;
                break;
        }
    }

    /*public void EnableWindow(GameObject SlectedTire, GameObject NonSelectedTire1, GameObject NonSelectedTire2, int tab){
		SlectedTire.SetActive (true);
		NonSelectedTire1.SetActive(false);
		NonSelectedTire2.SetActive(false);
        //currentTab = tab;
		selectionWindow.SetActive (false);


	}*/

    public void EnableWindow(GameObject selectedTier, GameObject tab1, GameObject tab2, GameObject tab3, GameObject tab4, GameObject tab5, GameObject tab6)
    {
        selectedTier.SetActive(true);
        tab1.SetActive(false);
        tab2.SetActive(false);
        tab3.SetActive(false);
        tab4.SetActive(false);
        tab5.SetActive(false);
        tab6.SetActive(false);

        selectionWindow.SetActive(false);
    }

    public void buttonPress(int fish)
    {
        //Debug.Log("click" + fish);
        //add 1 fish or switch to new fish type.
        if (selectedFish == fish)
        {
            addfishes(1);
        }
        else
        {
            selectedFish = fish;
            currentFishes = 0;
            addfishes(1);
        }

        slider.value = 0;

        CharacterManager theFishWeWant = playerObj.species[selectedFish];
        //Debug.Log(theFishWeWant.uniqueName);

        //cap purchasing based on money
        while (theFishWeWant.cost * currentFishes > playerObj.moneys)
        {
            currentFishes--;
        }
        //update text
        totalfishes.text = currentFishes.ToString();
        totalPrice.text = "- $" + (theFishWeWant.cost * currentFishes).ToString();
        currentName.text = theFishWeWant.uniqueName;
        sellingName.text = currentName.text;

        eatingText.text = eatingName + "\n" + getStars(theFishWeWant.eatingStars);
        reproductionText.text = reproductionName + "\n" + getStars(theFishWeWant.reproductionStars);
        deathThreasholdText.text = deathThreasholdName + "\n" + getStars(theFishWeWant.deathThreasholdStars);
        deathRateText.text = deathRateName + "\n" + getStars(theFishWeWant.deathRateStars);
        thermalBreadthText.text = thermalBreadthName + "\n" + getStars(theFishWeWant.thermalBreadthStars);
        descriptionText.text = theFishWeWant.description;

        //update thermal curve
        curveRender.curve = playerObj.species[selectedFish].thermalcurve;

    }

    private string getStars(int starNum)
    {
        string s = "";
        for(int i = 0; i < starNum + 1; i++)
        {
            s += starChar;
        }
        return s;
    }

	public void SelectedWindow(GameObject selected){
		selectionWindow.SetActive (true);
		selectionWindow.transform.position = new Vector3 (selected.transform.position.x,
			 selected.transform.position.y, selected.transform.position.z);
	}


	public void addfishes(int number){
		currentFishes += number;
        if (currentFishes < 0)
        {
            currentFishes = 0;
        }

        //cap purchasing based on money
        while (playerObj.species[selectedFish].cost * currentFishes > playerObj.moneys)
        {
            currentFishes--;
        }

        //cap based on total fish count
        float totalFish = playerObj.getTotalFishCount();
        while (totalFish + currentFishes > playerObj.maxFishes)
        {
            currentFishes--;
        }

        totalfishes.text = currentFishes.ToString();
        totalPrice.text = (playerObj.species[selectedFish].cost * currentFishes).ToString();
	}

    public void addSellingFishes(int number)
    {
        float amount = playerObj.species[selectedFish].speciesAmount;

        if (amount <= 0)
            return;

        currentSellingFishes += number;
        if (currentSellingFishes < 0)
        {
            currentSellingFishes = 0;
        }

        //cap selling based on number of fish
        
        if (amount < currentSellingFishes)
        {
            currentSellingFishes = Mathf.FloorToInt(amount);
        }

        slider.value = currentSellingFishes / amount;

        sellingfishes.text = currentSellingFishes.ToString();
        sellingPrice.text = (playerObj.species[selectedFish].cost * currentSellingFishes * playerObj.sellRate).ToString();
    }

    public void buyFishes()
    {
        if (playerObj.busy)
            return;
        playerObj.BuyCreatures(selectedFish, currentFishes);
        currentFishes = 0;
        totalfishes.text = "0";
        totalPrice.text = "0";

//this is for data collection which only possible on non webgl version
#if !UNITY_WEBGL
        buy_press += 1;
#endif
    }

    public void sellFishes()
    {
        OnSell.Invoke();
        if (playerObj.busy)
            return;
        playerObj.SellCreatures(selectedFish, currentSellingFishes);
        currentSellingFishes = 0;
        sellingfishes.text = "0";
        sellingPrice.text = "0";

        //this is for data collection which only possible on non webgl version
#if !UNITY_WEBGL
        sell_press += 1;
#endif
    }

    public void sliderSlide()
    {
        currentSellingFishes = Mathf.FloorToInt(playerObj.species[selectedFish].speciesAmount * slider.value);

        sellingfishes.text = currentSellingFishes.ToString();
        sellingPrice.text = (playerObj.species[selectedFish].cost * currentSellingFishes * playerObj.sellRate).ToString();
    }
}
