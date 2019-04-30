using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class level_select : MonoBehaviour
{
    private Vector3 normal_scale;
    private GameObject env_image;
    public Queue<Sprite> env;

    public void Start()
    {
        env_image = new GameObject();
        foreach (Transform child in transform)
            if (child.gameObject.name == "level")
            {
                env_image = child.gameObject;
                Sprite back = env.Dequeue();
                env.Enqueue(back);
                env_image.GetComponent<Image>().sprite = back;
                break;
            }

        normal_scale = gameObject.transform.localScale;
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

    public void press_arrow(bool up)
    {
        if (up)
        {

        }
        else
        {

        }
    }

    private void shuffle_env()
    {
        env.
        env_image.GetComponent<Image>().sprite = 
    }

    public void hover(bool on)
    {

        if(on && gameObject.GetComponent<Image>().IsActive())
        {
            gameObject.transform.localScale = normal_scale * 1.2f;
            gameObject.GetComponent<Image>().color = Color.gray;
        }
        else
        {
            gameObject.transform.localScale = normal_scale;
            gameObject.GetComponent<Image>().color = Color.white;
        }
    }
}
