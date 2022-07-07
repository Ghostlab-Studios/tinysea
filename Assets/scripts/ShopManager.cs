﻿using System.Collections;
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
    public Text temperatureText;
    public Text reproductionText;
    public Text descriptionText;
    public Text totalMoneyText;
    public Text amountToBuyText;
    public Text amountToSellText;
    public Text totalMoneyLost;
    public Text totalMoneyGained;
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

    public void ChangeActiveTier(int tier)
    {
        activeTier = tier;

        int tierBase = activeTier * 9;

        organismIconSprites[0].sprite = playerManager.species[tierBase].icon;
        organismIconSprites[1].sprite = playerManager.species[tierBase + 1].icon;
        organismIconSprites[2].sprite = playerManager.species[tierBase + 2].icon;

        icon1MoneyText.text = playerManager.species[tierBase].cost.ToString();
        icon2MoneyText.text = playerManager.species[tierBase + 1].cost.ToString();
        icon3MoneyText.text = playerManager.species[tierBase + 2].cost.ToString();

        recordedVariants = new int[3];
        ChangeActiveOrganism(0);
        UpdateOrganismHighlight(organismIconSprites[0].gameObject.GetComponent<RectTransform>());
    }

    public void ChangeActiveOrganism(int icon)
    {
        activeIcon = icon;
        int tierBase = activeTier * 9;
        activeOrganism = tierBase + icon;
        activeVariant = recordedVariants[icon];
        currentOrganismID = activeOrganism + (activeVariant * 3);
        UpdateCurve(currentOrganismID);
        ResetBuySell();
    }

    public void ChangeActiveVariant(int variant)
    {
        activeVariant = variant;
        recordedVariants[activeIcon] = variant;
        currentOrganismID = activeOrganism + (activeVariant * 3);
        organismIconSprites[activeIcon].sprite = playerManager.species[currentOrganismID].icon;
        UpdateCurve(currentOrganismID);
        ResetBuySell();
    }

    public void UpdateCurve(int id)
    {
        curve.curve = playerManager.species[id].thermalcurve;
    }

    public void UpdateOrganismHighlight(RectTransform transform)
    {
        organismHighlight.SetPositionAndRotation(transform.position + new Vector3(0, -4f, 0), organismHighlight.rotation);

        RectTransform variantTransform = defaultVariantIconSprites[activeVariant].gameObject.GetComponent<RectTransform>();
        RectTransform originTransform = organismIconSprites[0].gameObject.GetComponent<RectTransform>();
        variantHighlight.SetPositionAndRotation(organismHighlight.position + (variantTransform.position - originTransform.position) + new Vector3(-0.5f, 3f, 0), variantHighlight.rotation);
    }

    public void ChangeAmountToBuy(int amount)
    {
        amountToBuy = Mathf.Clamp(amountToBuy + amount, 0, Mathf.FloorToInt(playerManager.moneys / playerManager.species[currentOrganismID].cost));
        Debug.Log(amountToBuy.ToString());
        amountToBuyText.text = amountToBuy.ToString();
        totalMoneyLost.text = "-" + (amountToBuy * playerManager.species[currentOrganismID].cost).ToString();
    }

    public void ChangeAmountToSell(int amount)
    {
        amountToSell = Mathf.Clamp(amountToSell + amount, 0, Mathf.RoundToInt(playerManager.species[currentOrganismID].speciesAmount));
        amountToSellText.text = amountToSell.ToString();
        totalMoneyGained.text = "+" + (amountToSell * playerManager.species[currentOrganismID].cost).ToString();
    }

    public void ResetBuySell()
    {
        amountToBuy = 0;
        amountToBuyText.text = "0";
        totalMoneyLost.text = "-0";
        amountToSell = 0;
        amountToSellText.text = "0";
        totalMoneyGained.text = "+0";
    }

    public void BuyOrganism()
    {

    }

    public void SellOrganism()
    {

    }
}
