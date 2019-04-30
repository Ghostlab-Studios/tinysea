using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class level_select : MonoBehaviour
{
    private void Start()
    {
        
    }

    public void act_select()
    {
        hover(false);
        Image select = gameObject.GetComponent<Image>();
        Button butt = gameObject.GetComponent<Button>();

        select.enabled = false;
        butt.enabled = false;

        foreach(Transform child in transform)
        {
            child.gameObject.SetActive(true);
        }
    }

    public void deact_select()
    {
        Image select = gameObject.GetComponent<Image>();
        Button butt = gameObject.GetComponent<Button>();

        select.enabled = true;
        butt.enabled = true;

        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }

        hover(true);
    }

    public void hover(bool on)
    {

        if(on && gameObject.GetComponent<Image>().IsActive())
        {
            gameObject.transform.localScale = gameObject.transform.localScale * 1.2f;
            gameObject.GetComponent<Image>().color = Color.gray;
        }
        else
        {
            gameObject.transform.localScale = gameObject.transform.localScale / 1.2f;
            gameObject.GetComponent<Image>().color = Color.white;
        }
    }
}
