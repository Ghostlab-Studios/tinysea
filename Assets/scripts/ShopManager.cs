using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Manages the Shop UI by buying/selling organisms and setting appropriate visual parameters
/// based on organisms selected.
/// </summary>
public class ShopManager : MonoBehaviour {

    public PlayerManager playerManager;
    public CurveRenderer curve;
    public RectTransform organismHighlight;
    public RectTransform variantHighlight;
    public Image[] organismIconSprites;
    public Image[] defaultVariantIconSprites;
    public Text icon1MoneyText;
    public Text icon2MoneyText;
    public Text icon3MoneyText;
    public RectTransform descriptionBox;
    public Text temperatureText;
    public Text reproductionText;
    public Text descriptionText;
    public Text totalMoneyText;
    public InputField amountToBuyText;
    public InputField amountToSellText;
    public Text totalMoneyLostText;
    public Text totalMoneyGainedText;
    public UnityEvent onBuy;
    public UnityEvent onSell;

    private int currentOrganismID = 0;
    private int activeTier = 0; // 0 = Tier 3, 1 = Tier 2, 2 = Tier 1
    private int activeIcon = 0;
    private int activeOrganism = 0; // 0 = Icon1, 1 = Icon2, 2 = Icon3
    private int activeVariant = 0; // 0 = common, 1 = tropical, 2 = arctic
    private int amountToBuy = 0;
    private int amountToSell = 0;
    private int[] recordedVariants = new int[3];

    private void Start()
    {
        StartCoroutine(LateStart(0.1f));
    }

    private IEnumerator LateStart(float time)
    {
        yield return new WaitForSeconds(time);
        ChangeActiveTier(0);
    }

    /// <summary>
    /// Changes the current viewed Tier in the shop page (of Tiers 1, 2, and 3)
    /// </summary>
    /// <param name="tier">The tier to display [0 - 2].</param>
    public void ChangeActiveTier(int tier)
    {
        activeTier = tier;

        int tierBase = activeTier * 9;

        organismIconSprites[0].sprite = playerManager.species[tierBase].icon;
        organismIconSprites[1].sprite = playerManager.species[tierBase + 1].icon;
        organismIconSprites[2].sprite = playerManager.species[tierBase + 2].icon;

        icon1MoneyText.text = "$" + playerManager.species[tierBase].cost.ToString();
        icon2MoneyText.text = "$" + playerManager.species[tierBase + 1].cost.ToString();
        icon3MoneyText.text = "$" + playerManager.species[tierBase + 2].cost.ToString();

        recordedVariants = new int[3];

        if (organismIconSprites[0].gameObject.activeInHierarchy)
        {
            ChangeActiveOrganism(0);
            UpdateOrganismHighlight(organismIconSprites[0].gameObject.GetComponent<RectTransform>()); 
        }
        else if (organismIconSprites[1].gameObject.activeInHierarchy)
        {
            ChangeActiveOrganism(1);
            UpdateOrganismHighlight(organismIconSprites[1].gameObject.GetComponent<RectTransform>()); 
        }
        else if (organismIconSprites[2].gameObject.activeInHierarchy)
        {
            ChangeActiveOrganism(2);
            UpdateOrganismHighlight(organismIconSprites[2].gameObject.GetComponent<RectTransform>()); 
        }
    }

    /// <summary>
    /// Changes the current selected organism within the current tier.
    /// </summary>
    /// <param name="icon">The icon of the organism to select [0 - 2].</param>
    public void ChangeActiveOrganism(int icon)
    {
        activeIcon = icon;
        int tierBase = activeTier * 9;
        activeOrganism = tierBase + icon;
        activeVariant = recordedVariants[icon];
        currentOrganismID = activeOrganism + (activeVariant * 3);
        UpdateCurve(currentOrganismID);
        UpdateDescriptionBox(activeIcon);
        ResetBuySell();
    }

    /// <summary>
    /// Changes the current selected variant of the current selected organism.
    /// </summary>
    /// <param name="variant">The variant type of the current selected organism [0 - 2].</param>
    public void ChangeActiveVariant(int variant)
    {
        activeVariant = variant;
        recordedVariants[activeIcon] = variant;
        currentOrganismID = activeOrganism + (activeVariant * 3);
        organismIconSprites[activeIcon].sprite = playerManager.species[currentOrganismID].icon;
        UpdateDescriptionBox(activeIcon);
        UpdateCurve(currentOrganismID);
        ResetBuySell();
    }

    /// <summary>
    /// Updates the description box with organism information depending on the current selected organism.
    /// </summary>
    /// <param name="icon">The icon of the current selected organism [0 - 2].</param>
    public void UpdateDescriptionBox(int icon)
    {
        descriptionBox.SetPositionAndRotation(new Vector3(organismIconSprites[icon].gameObject.GetComponent<RectTransform>().position.x, descriptionBox.position.y, 0),
                                              descriptionBox.rotation);
        int tierBase = activeTier * 9;
        int organism = tierBase + icon;
        int variant = recordedVariants[icon];
        int id = organism + (variant * 3);
        temperatureText.text = playerManager.species[id].temperatureThresholdText;
        reproductionText.text = playerManager.species[id].reproductionRateText;
        descriptionText.text = playerManager.species[id].description;
    }

    /// <summary>
    /// Updates the thermal fitness curve based on the current selected organism.
    /// </summary>
    /// <param name="id">The icon of the current selected organism [0 - 2]</param>
    public void UpdateCurve(int id)
    {
        curve.UpdateCurve(playerManager.species[id].thermalcurve, activeVariant);
    }

    /// <summary>
    /// Changes the icon highlight to be over the current selected organism.
    /// </summary>
    /// <param name="transform">The transform component of the icon to highlight.</param>
    public void UpdateOrganismHighlight(RectTransform transform)
    {
        organismHighlight.SetPositionAndRotation(transform.position + new Vector3(0, -4f, 0), organismHighlight.rotation);

        RectTransform variantTransform = defaultVariantIconSprites[activeVariant].gameObject.GetComponent<RectTransform>();
        RectTransform originTransform = organismIconSprites[0].gameObject.GetComponent<RectTransform>();
        variantHighlight.SetPositionAndRotation(organismHighlight.position + (variantTransform.position - originTransform.position) + new Vector3(-0.5f, 3f, 0), variantHighlight.rotation);
    }

    /// <summary>
    /// Changes the current amount of organisms to buy. Changes UI to reflect this.
    /// </summary>
    /// <param name="amount">The amount to change the current amount to buy by.</param>
    public void ChangeAmountToBuy(int amount)
    {
        amountToBuy = Mathf.Clamp(amountToBuy + amount, 0, Mathf.FloorToInt(playerManager.moneys / playerManager.species[currentOrganismID].cost));
        amountToBuyText.text = amountToBuy.ToString();
        totalMoneyLostText.text = "- $" + (amountToBuy * playerManager.species[currentOrganismID].cost).ToString();
    }

    /// <summary>
    /// Changes the current amount of organisms to sell. Changes the UI to reflect this.
    /// </summary>
    /// <param name="amount">The amount to change the current amount to sell by.</param>
    public void ChangeAmountToSell(int amount)
    {
        amountToSell = Mathf.Clamp(amountToSell + amount, 0, Mathf.RoundToInt(playerManager.species[currentOrganismID].speciesAmount));
        amountToSellText.text = amountToSell.ToString();
        totalMoneyGainedText.text = "+ $" + (amountToSell * Mathf.RoundToInt(playerManager.species[currentOrganismID].cost * 0.75f)).ToString();
    }

    public void VerifyAmountToBuy()
    {
        int attemptedTotal = 0;
        if (!int.TryParse(amountToBuyText.text, out attemptedTotal)) { amountToBuy = 0; }
        else { amountToBuy = Mathf.Clamp(attemptedTotal, 0, Mathf.FloorToInt(playerManager.moneys / playerManager.species[currentOrganismID].cost)); }
        amountToBuyText.text = amountToBuy.ToString();
        totalMoneyLostText.text = "- $" + (amountToBuy * playerManager.species[currentOrganismID].cost).ToString();
    }

    public void VerifyAmountToSell()
    {
        int attemptedTotal = 0;
        if (!int.TryParse(amountToSellText.text, out attemptedTotal)) { amountToSell = 0; }
        else { amountToSell = Mathf.Clamp(attemptedTotal, 0, Mathf.RoundToInt(playerManager.species[currentOrganismID].speciesAmount)); }
        amountToSellText.text = amountToSell.ToString();
        totalMoneyGainedText.text = "+ $" + (amountToSell * Mathf.RoundToInt(playerManager.species[currentOrganismID].cost * 0.75f)).ToString();
    }

    /// <summary>
    /// Resets the UI and buy/sell amounts.
    /// </summary>
    public void ResetBuySell()
    {
        amountToBuy = 0;
        amountToBuyText.text = "0";
        totalMoneyLostText.text = "- $0";
        amountToSell = 0;
        amountToSellText.text = "0";
        totalMoneyGainedText.text = "+ $0";
    }

    /// <summary>
    /// Buys the current selected organism in the quantity of <param name="amountToBuy"></param>.
    /// </summary>
    public void BuyOrganism()
    {
        if (!playerManager.busy && amountToBuy > 0)
        {
            playerManager.BuyCreatures(currentOrganismID, amountToBuy);
            onBuy.Invoke();
            ResetBuySell();
        }
    }

    /// <summary>
    /// Sells the current selected organism in the quantity of <param name="amountToSell"></param>.
    /// </summary>
    public void SellOrganism()
    {
        if (!playerManager.busy && amountToSell > 0)
        {
            playerManager.SellCreatures(currentOrganismID, amountToSell);
            onSell.Invoke();
            ResetBuySell();
        }
    }

    public int GetAmountToSell()
    {
        return amountToSell;
    }

    public int GetActiveOrganism()
    {
        return currentOrganismID;
    }
}
